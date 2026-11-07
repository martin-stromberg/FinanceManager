using FinanceManager.Application.Accounts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.Infrastructure.MassImport;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_statement_draft_import</c> (T16): imports
/// a statement file as new draft(s) or creates an empty/preliminary draft — exclusively
/// through the existing import pipeline (<see cref="IStatementDraftService"/> upload path
/// or <see cref="IMassImportOrchestrator"/>); no bank connectivity, no side channel.
/// Scope: (Kontoauszugsentwürfe, Erstellung). Security class: schreibvorbereitend.
/// </summary>
public sealed class FmStatementDraftImportDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_import";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftImportDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"mode\":{\"type\":\"string\",\"enum\":[\"upload\",\"empty\",\"preliminary\",\"massImport\"],\"description\":\"Import mode (default: inferred — massImport when 'files' is set, preliminary when only 'accountId' is set, upload when 'contentBase64' is set, otherwise empty).\"},\"originalFileName\":{\"type\":\"string\",\"description\":\"Original file name (required for upload/empty).\"},\"contentBase64\":{\"type\":\"string\",\"description\":\"Base64-encoded statement file content (required for upload).\"},\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target account (required for preliminary).\"},\"description\":{\"type\":\"string\",\"description\":\"Optional draft description (preliminary only).\"},\"files\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"fileId\":{\"type\":\"string\",\"format\":\"uuid\"},\"fileName\":{\"type\":\"string\"},\"contentType\":{\"type\":\"string\"},\"contentBase64\":{\"type\":\"string\"}},\"required\":[\"fileName\",\"contentBase64\"],\"additionalProperties\":false},\"description\":\"Multi-file import via the mass-import orchestrator (max 50 files, 10 MB each, 100 MB total).\"},\"decisions\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"fileId\":{\"type\":\"string\",\"format\":\"uuid\"},\"excluded\":{\"type\":\"boolean\"},\"selectedSecurityId\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"fileId\"],\"additionalProperties\":false},\"description\":\"Optional per-file decisions (exclude / security selection for price files).\"}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Imports a statement file as new draft(s) via the existing import pipeline (upload ≤ 20 MB, empty draft, preliminary draft for an account, or multi-file mass import with per-file decisions). No bank connectivity. Scope: (Kontoauszugsentwürfe, Erstellung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Create)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_import</c>: adapts the existing import entry points —
/// file upload via <see cref="IStatementDraftService.CreateDraftAsync"/> (async stream,
/// may yield several drafts of one upload group), empty draft via
/// <c>CreateEmptyDraftAsync</c>, preliminary draft via <c>CreatePreliminaryDraftAsync</c>
/// and multi-file batches via <see cref="IMassImportOrchestrator.ProcessAsync"/>. Upload
/// contents are untrusted input: they are only passed to the parser pipeline, never
/// echoed; the audit payload carries file metadata and a SHA-256 fingerprint instead of
/// the content (audit-konzept.md §4.2). The <c>DataChange</c> record is staged into the
/// shared unit of work before the import so it commits atomically with the created
/// drafts (F-0301). File contents, names and parser output fields are emitted inside
/// <c>untrusted_data</c> wrappers (sicherheitskonzept.md §5).
/// </summary>
public sealed class FmStatementDraftImportHandler : IMcpToolHandler
{
    private const int MaxFileCount = 50;

    private readonly IStatementDraftService _drafts;
    private readonly IMassImportOrchestrator _massImport;
    private readonly IAccountService _accounts;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly MassImportOptions _massImportOptions;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftImportHandler(
        IStatementDraftService drafts,
        IMassImportOrchestrator massImport,
        IAccountService accounts,
        IMcpAuditWriter auditWriter,
        IOptions<MassImportOptions> massImportOptions)
    {
        _drafts = drafts;
        _massImport = massImport;
        _accounts = accounts;
        _auditWriter = auditWriter;
        _massImportOptions = massImportOptions?.Value ?? new MassImportOptions();
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftImportDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var mode = ResolveMode(arguments);
        var originalFileName = McpToolArguments.OptionalString(arguments, "originalFileName", McpStatementDraftGuards.MaxFileNameLength);
        var contentBase64 = McpToolArguments.OptionalString(arguments, "contentBase64", (int)(McpStatementDraftGuards.MaxUploadBytes * 2));
        var accountId = McpToolArguments.OptionalGuid(arguments, "accountId");
        var description = McpToolArguments.OptionalString(arguments, "description", McpStatementDraftGuards.MaxTextLength);

        return mode switch
        {
            "upload" => await UploadAsync(owner, originalFileName, contentBase64, context, ct),
            "empty" => await EmptyAsync(owner, originalFileName, contentBase64, context, ct),
            "preliminary" => await PreliminaryAsync(owner, accountId, description, originalFileName, contentBase64, context, ct),
            "massImport" => await MassImportAsync(owner, arguments, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments", $"Unknown mode '{mode}'.")
        };
    }

    private static string ResolveMode(JsonElement arguments)
    {
        var explicitMode = McpToolArguments.OptionalString(arguments, "mode", 20);
        if (explicitMode is not null)
        {
            return explicitMode switch
            {
                "upload" or "empty" or "preliminary" or "massImport" => explicitMode,
                _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                    "Argument 'mode' must be one of upload|empty|preliminary|massImport.")
            };
        }
        if (McpToolArguments.HasNonNull(arguments, "files"))
        {
            return "massImport";
        }
        if (McpToolArguments.HasNonNull(arguments, "accountId") && !McpToolArguments.HasNonNull(arguments, "contentBase64"))
        {
            return "preliminary";
        }
        return McpToolArguments.HasNonNull(arguments, "contentBase64") ? "upload" : "empty";
    }

    private async Task<object> UploadAsync(
        Guid owner, string? originalFileName, string? contentBase64,
        McpToolCallContext context, CancellationToken ct)
    {
        var fileName = RequireFileName(originalFileName);
        var bytes = DecodeContent(contentBase64, McpStatementDraftGuards.MaxUploadBytes);

        // Import first, then stage the DataChange with the real draft ids — the draft
        // records only exist after the pipeline ran and the audit writer rejects the
        // former Guid.Empty placeholder entity id.
        var created = new List<StatementDraftDto>();
        await foreach (var draft in _drafts.CreateDraftAsync(owner, fileName, bytes, ct))
        {
            created.Add(draft);
        }
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftImported", ToolName,
            changes: created.Select(d => new McpAuditFieldChange(
                    "StatementDraft", d.DraftId, "Import", null, fileName)).ToList(),
            affectedCount: created.Count,
            payloadJson: JsonSerializer.Serialize(new
            {
                mode = "upload",
                fileName,
                sizeBytes = bytes.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
            }), ct);

        if (created.Count == 0)
        {
            // No parser recognized the file: the pipeline only persisted the raw file as
            // statement detail (no draft record exists to anchor a field delta on — the
            // entry keeps the file metadata + sha256 as payload). Report the recognition
            // status honestly.
            return new
            {
                mode = "upload",
                recognized = false,
                draftCount = 0,
                drafts = Array.Empty<object>(),
                untrusted_data = new { originalFileName = fileName }
            };
        }
        return new
        {
            mode = "upload",
            recognized = true,
            draftCount = created.Count,
            drafts = created.Select(ToDraftResult).ToList(),
            untrusted_data = new { originalFileName = fileName }
        };
    }

    private async Task<object> EmptyAsync(
        Guid owner, string? originalFileName, string? contentBase64,
        McpToolCallContext context, CancellationToken ct)
    {
        var fileName = RequireFileName(originalFileName);
        if (contentBase64 is not null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Mode 'empty' does not accept 'contentBase64' — use mode 'upload'.");
        }

        // Create first, then stage the DataChange with the real draft id — the audit
        // writer rejects placeholder entity ids (Guid.Empty).
        var draft = await _drafts.CreateEmptyDraftAsync(owner, fileName, ct);
        if (draft is null)
        {
            throw new McpToolException("Err_Mcp_InvalidState", "The empty draft could not be created.");
        }
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftImported", ToolName,
            changes: new[] { new McpAuditFieldChange("StatementDraft", draft.DraftId, "Created", null, "empty") },
            affectedCount: 1,
            payloadJson: JsonSerializer.Serialize(new { mode = "empty", fileName }), ct);
        return new
        {
            mode = "empty",
            recognized = (bool?)null,
            draftCount = 1,
            drafts = new[] { ToDraftResult(draft) },
            untrusted_data = new { originalFileName = fileName }
        };
    }

    private async Task<object> PreliminaryAsync(
        Guid owner, Guid? accountId, string? description,
        string? originalFileName, string? contentBase64,
        McpToolCallContext context, CancellationToken ct)
    {
        if (!accountId.HasValue)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Mode 'preliminary' requires 'accountId'.");
        }
        if (originalFileName is not null || contentBase64 is not null)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                "Mode 'preliminary' accepts neither 'originalFileName' nor 'contentBase64'.");
        }
        // F-0704: reject foreign/missing account references before any effect.
        var account = await McpStatementDraftGuards.RequireAccountAsync(_accounts, accountId.Value, owner, ct);

        // Create first, then stage the DataChange with the real draft id — the audit
        // writer rejects placeholder entity ids (Guid.Empty).
        var draft = await _drafts.CreatePreliminaryDraftAsync(owner, account.Id, ct, description);
        if (draft is null)
        {
            throw new McpToolException("Err_Mcp_InvalidState", "The preliminary draft could not be created.");
        }
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftImported", ToolName,
            changes: new[] { new McpAuditFieldChange("StatementDraft", draft.DraftId, "Created", null, "preliminary") },
            affectedCount: 1,
            payloadJson: JsonSerializer.Serialize(new { mode = "preliminary", accountId = accountId.Value }), ct);
        return new
        {
            mode = "preliminary",
            recognized = (bool?)null,
            draftCount = 1,
            drafts = new[] { ToDraftResult(draft) },
            untrusted_data = new { originalFileName = draft.OriginalFileName }
        };
    }

    private async Task<object> MassImportAsync(Guid owner, JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        if (!McpToolArguments.HasNonNull(arguments, "files"))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Mode 'massImport' requires 'files'.");
        }
        var filesEl = arguments.GetProperty("files");
        if (filesEl.ValueKind != JsonValueKind.Array || filesEl.GetArrayLength() == 0
            || filesEl.GetArrayLength() > Math.Min(MaxFileCount, _massImportOptions.MaxFiles))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument 'files' must be a non-empty array of at most {Math.Min(MaxFileCount, _massImportOptions.MaxFiles)} files.");
        }

        var uploads = new List<MassImportFileUploadDto>(filesEl.GetArrayLength());
        long totalBytes = 0;
        foreach (var f in filesEl.EnumerateArray())
        {
            var fileName = f.TryGetProperty("fileName", out var fn) && fn.ValueKind == JsonValueKind.String
                ? fn.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(fileName) || fileName!.Length > McpStatementDraftGuards.MaxFileNameLength)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    $"Each file requires a 'fileName' of at most {McpStatementDraftGuards.MaxFileNameLength} characters.");
            }
            string? contentBase64 = f.TryGetProperty("contentBase64", out var cb) && cb.ValueKind == JsonValueKind.String
                ? cb.GetString()
                : null;
            var bytes = DecodeContent(contentBase64, _massImportOptions.MaxBytesPerFile);
            totalBytes += bytes.Length;
            if (totalBytes > _massImportOptions.MaxTotalContentBytes)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    $"The batch exceeds the maximum total content size of {_massImportOptions.MaxTotalContentBytes} bytes.");
            }
            Guid fileId = Guid.NewGuid();
            if (f.TryGetProperty("fileId", out var fid))
            {
                if (fid.ValueKind != JsonValueKind.String || !Guid.TryParse(fid.GetString(), out fileId))
                {
                    throw new McpToolException("Err_Mcp_InvalidArguments", "File 'fileId' must be a UUID string.");
                }
            }
            string? contentType = f.TryGetProperty("contentType", out var ctv) && ctv.ValueKind == JsonValueKind.String
                ? ctv.GetString()
                : null;
            uploads.Add(new MassImportFileUploadDto
            {
                FileId = fileId,
                FileName = fileName,
                ContentType = contentType,
                Content = bytes
            });
        }

        var decisions = new List<MassImportFileDecisionDto>();
        if (McpToolArguments.HasNonNull(arguments, "decisions"))
        {
            var decEl = arguments.GetProperty("decisions");
            if (decEl.ValueKind != JsonValueKind.Array)
            {
                throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'decisions' must be an array.");
            }
            var knownFileIds = uploads.Select(u => u.FileId).ToHashSet();
            foreach (var d in decEl.EnumerateArray())
            {
                if (!d.TryGetProperty("fileId", out var fid) || fid.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(fid.GetString(), out var decisionFileId))
                {
                    throw new McpToolException("Err_Mcp_InvalidArguments", "Each decision requires a 'fileId' UUID string.");
                }
                if (!knownFileIds.Contains(decisionFileId))
                {
                    throw new McpToolException("Err_Mcp_InvalidArguments",
                        "A decision references a fileId that is not part of this batch.");
                }
                var excluded = d.TryGetProperty("excluded", out var ex) && ex.ValueKind == JsonValueKind.True;
                Guid? selectedSecurityId = null;
                if (d.TryGetProperty("selectedSecurityId", out var sid) && sid.ValueKind != JsonValueKind.Null)
                {
                    if (sid.ValueKind != JsonValueKind.String || !Guid.TryParse(sid.GetString(), out var parsedSid))
                    {
                        throw new McpToolException("Err_Mcp_InvalidArguments", "Decision 'selectedSecurityId' must be a UUID string.");
                    }
                    selectedSecurityId = parsedSid;
                }
                decisions.Add(new MassImportFileDecisionDto
                {
                    FileId = decisionFileId,
                    Excluded = excluded,
                    SelectedSecurityId = selectedSecurityId
                });
            }
        }

        // Run the batch first — the delta rows anchor on the created draft ids which only
        // exist afterwards.
        var result = await _massImport.ProcessAsync(owner, new MassImportBatchRequestDto
        {
            // MCP calls are fire-and-forget tool calls: the client confirms implicitly;
            // files lacking required information are reported as skipped, never silently
            // auto-assigned.
            DialogPolicy = MassImportDialogPolicy.OnMissingInformation,
            ConfirmExecution = true,
            Files = uploads,
            Decisions = decisions
        }, context.CorrelationId.ToString(), ct);

        // Stage the DataChange after the batch ran: one delta row per created draft; when
        // the batch produced no draft at all (all files skipped/excluded) the übergeordnete
        // batch record anchors the event — the audit writer rejects placeholder entity ids.
        var importedDraftIds = result.Files
            .SelectMany(f => f.StatementDraftIds)
            .Concat(result.Files.Where(f => f.StatementDraftId.HasValue)
                .Select(f => f.StatementDraftId!.Value))
            .Distinct()
            .ToList();
        var changes = importedDraftIds.Count > 0
            ? importedDraftIds.Select(id => new McpAuditFieldChange(
                "StatementDraft", id, "Import", null, "massImport")).ToList()
            : new List<McpAuditFieldChange>
            {
                new("MassImportBatch", result.BatchId, "Import", null, $"massImport:{uploads.Count} files")
            };
        await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "DraftImported", ToolName,
            changes: changes,
            affectedCount: uploads.Count,
            payloadJson: JsonSerializer.Serialize(new
            {
                mode = "massImport",
                fileCount = uploads.Count,
                batchId = result.BatchId,
                files = uploads.Select(u => new
                {
                    fileName = u.FileName,
                    sizeBytes = u.Content.Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(u.Content)).ToLowerInvariant()
                })
            }), ct);

        return new
        {
            mode = "massImport",
            batchId = result.BatchId,
            dialogRequired = result.DialogRequired,
            files = result.Files.Select(f => new
            {
                fileId = f.FileId,
                fileType = f.FileType.ToString(),
                canImport = f.CanImport,
                excluded = f.Excluded,
                executionStatus = f.ExecutionStatus.ToString(),
                selectedSecurityId = f.SelectedSecurityId,
                securityAutoGuessed = f.SecurityAutoGuessed,
                statementDraftIds = f.StatementDraftIds,
                untrusted_data = new
                {
                    fileName = f.FileName,
                    serviceDisplayName = f.ServiceDisplayName,
                    validationMessage = f.ValidationMessage
                }
            }).ToList()
        };
    }

    private static string RequireFileName(string? originalFileName)
        => string.IsNullOrWhiteSpace(originalFileName)
            ? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'originalFileName' is required.")
            : originalFileName!;

    private static byte[] DecodeContent(string? contentBase64, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contentBase64' is required.");
        }
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(contentBase64!);
        }
        catch (FormatException)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'contentBase64' is not valid base64.");
        }
        if (bytes.Length == 0)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments", "The uploaded content is empty.");
        }
        if (bytes.LongLength > maxBytes)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"The uploaded content exceeds the maximum size of {maxBytes} bytes.");
        }
        return bytes;
    }

    private static object ToDraftResult(StatementDraftDto draft)
        => new
        {
            draftId = draft.DraftId,
            entryCount = draft.Entries.Count,
            status = draft.Status.ToString(),
            detectedAccountId = draft.DetectedAccountId,
            accountBankContactId = draft.AccountBankContactId,
            isPreliminary = draft.IsPreliminary,
            isSplitDraft = draft.IsSplitDraft,
            parentDraftId = draft.ParentDraftId,
            uploadGroupId = draft.UploadGroupId,
            totalAmount = draft.TotalAmount,
            untrusted_data = new
            {
                originalFileName = draft.OriginalFileName,
                description = draft.Description
            }
        };
}
