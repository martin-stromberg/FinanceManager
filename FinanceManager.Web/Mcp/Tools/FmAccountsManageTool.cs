using FinanceManager.Application.Accounts;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the write-preparing tool <c>fm_accounts_manage</c> (T23): creates and
/// updates accounts (name, <c>AccountType</c>, IBAN, <c>BankContactId</c>,
/// <c>SavingsPlanExpectation</c>, <c>SecurityProcessingEnabled</c>,
/// <c>IsCollectionAccount</c>) and maintains linked sub-IBANs. The destructive
/// <c>delete</c> operation is genehmigungspflichtig and answers
/// <c>Err_Mcp_ApprovalRequired</c> without effect until the approval queue ships (EP-09).
/// Scopes: (Konten, Erstellung/Änderung/Vervollständigung); delete additionally
/// (Konten, Löschung) — evaluated inside the handler.
/// </summary>
public sealed class FmAccountsManageDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_accounts_manage";

    /// <summary>Creates the descriptor.</summary>
    public FmAccountsManageDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"operation\":{\"type\":\"string\",\"enum\":[\"create\",\"update\",\"addLinkedIban\",\"removeLinkedIban\",\"delete\"],\"description\":\"Master-data operation (required); delete requires approval and has no effect until EP-09.\"},\"accountId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Target account (required for update/addLinkedIban/removeLinkedIban/delete).\"},\"name\":{\"type\":\"string\",\"description\":\"Account name (required for create; optional overwrite for update).\"},\"accountType\":{\"type\":\"string\",\"enum\":[\"Giro\",\"Savings\"],\"description\":\"Account type (required for create; the type cannot be changed afterwards).\"},\"iban\":{\"type\":\"string\",\"description\":\"Primary IBAN; explicit null clears it on update.\"},\"bankContactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Bank contact reference (required for create; optional overwrite for update).\"},\"savingsPlanExpectation\":{\"type\":\"string\",\"enum\":[\"None\",\"Optional\",\"Required\"],\"description\":\"Savings-plan expectation flag.\"},\"securityProcessingEnabled\":{\"type\":\"boolean\",\"description\":\"Security-processing flag.\"},\"isCollectionAccount\":{\"type\":\"boolean\",\"description\":\"Collection-account flag.\"}},\"required\":[\"operation\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Creates and updates accounts (name, accountType, iban, bankContactId, savingsPlanExpectation, securityProcessingEnabled, isCollectionAccount) and maintains linked sub-IBANs (addLinkedIban/removeLinkedIban). The delete operation requires approval and answers Err_Mcp_ApprovalRequired without effect until the approval queue ships (EP-09). Scopes: (Konten, Erstellung/Änderung/Vervollständigung); delete additionally (Konten, Löschung).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Schreibvorbereitend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Accounts, McpRightKind.Create),
        new McpToolScope(McpDataType.Accounts, McpRightKind.Modify),
        new McpToolScope(McpDataType.Accounts, McpRightKind.Complete)
    };
}

/// <summary>
/// Handler of <c>fm_accounts_manage</c> (F-0802): adapts <see cref="IAccountService"/>.
/// Update merges the provided fields over the current state so unspecified fields are
/// preserved (the account type is immutable after creation — the service contract has no
/// type parameter). The bank-contact reference and (owner,name)/IBAN uniqueness are
/// validated by the service and surface as <c>Err_Mcp_InvalidArguments</c>. The
/// record-level scope filter binds the account type dimension. Each effect commits
/// atomically with its staged <c>DataChange</c> record (F-0301); delete answers the
/// structured approval-required result without any service call.
/// </summary>
public sealed class FmAccountsManageHandler : IMcpToolHandler
{
    private const string EntityType = "Account";

    private readonly IAccountService _accounts;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly IMcpAuditWriter _auditWriter;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmAccountsManageHandler(
        IAccountService accounts,
        IMcpScopeEvaluator scopeEvaluator,
        IMcpAuditWriter auditWriter,
        McpRecordScopeFilter recordFilter)
    {
        _accounts = accounts;
        _scopeEvaluator = scopeEvaluator;
        _auditWriter = auditWriter;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmAccountsManageDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var operation = McpToolArguments.OptionalString(arguments, "operation", 30)
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'operation' is required.");

        return operation switch
        {
            "create" => await CreateAsync(arguments, owner, context, ct),
            "update" => await UpdateAsync(arguments, owner, context, ct),
            "addLinkedIban" => await LinkedIbanAsync(arguments, owner, context, add: true, ct),
            "removeLinkedIban" => await LinkedIbanAsync(arguments, owner, context, add: false, ct),
            "delete" => await DeleteAsync(arguments, owner, context, ct),
            _ => throw new McpToolException("Err_Mcp_InvalidArguments",
                "Argument 'operation' must be one of create|update|addLinkedIban|removeLinkedIban|delete.")
        };
    }

    private async Task<object> CreateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var name = McpMasterDataGuards.RequiredName(arguments);
        var type = McpToolArguments.OptionalEnum<AccountType>(arguments, "accountType")
            ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'accountType' is required for create.");
        var iban = McpToolArguments.HasNonNull(arguments, "iban")
            ? McpMasterDataGuards.RequireIban(McpToolArguments.OptionalString(arguments, "iban", McpMasterDataGuards.MaxIbanLength + 10))
            : null;
        var bankContactId = McpToolArguments.RequiredGuid(arguments, "bankContactId");
        var expectation = McpToolArguments.OptionalEnum<SavingsPlanExpectation>(arguments, "savingsPlanExpectation")
            ?? SavingsPlanExpectation.Optional;
        var securities = McpToolArguments.OptionalBool(arguments, "securityProcessingEnabled") ?? true;
        var collection = McpToolArguments.OptionalBool(arguments, "isCollectionAccount") ?? false;

        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Accounts, McpRightKind.Create,
            new McpRecordContext { AccountTypeValue = (short)type }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, Guid.Empty, "Created", null, $"{type}|{name}")
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AccountCreated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "create", name, accountType = type.ToString(), bankContactId }), ct);
        AccountDto created;
        try
        {
            created = await _accounts.CreateAsync(owner, name, type, iban, bankContactId, expectation, securities, collection, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { operation = "create", applied = true, account = Result(created) };
    }

    private async Task<object> UpdateAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var accountId = McpToolArguments.RequiredGuid(arguments, "accountId");
        var existing = await _accounts.GetAsync(accountId, owner, ct);
        if (existing is null)
        {
            // 404 semantics — a foreign account behaves like a missing one.
            return new { operation = "update", account = (object?)null };
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Accounts, McpRightKind.Modify,
            new McpRecordContext { AccountTypeValue = (short)existing.Type }, ct);

        var effName = McpToolArguments.Has(arguments, "name")
            ? McpMasterDataGuards.RequiredName(arguments)
            : existing.Name;
        var effIban = McpToolArguments.Has(arguments, "iban")
            ? (arguments.GetProperty("iban").ValueKind == JsonValueKind.Null
                ? null
                : McpMasterDataGuards.RequireIban(McpToolArguments.OptionalString(arguments, "iban", McpMasterDataGuards.MaxIbanLength + 10)))
            : existing.Iban;
        var effBankContact = McpToolArguments.Has(arguments, "bankContactId")
            ? McpToolArguments.RequiredGuid(arguments, "bankContactId")
            : existing.BankContactId;
        var effExpectation = McpToolArguments.Has(arguments, "savingsPlanExpectation")
            ? McpToolArguments.OptionalEnum<SavingsPlanExpectation>(arguments, "savingsPlanExpectation")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'savingsPlanExpectation' must not be null.")
            : existing.SavingsPlanExpectation;
        var effSecurities = McpToolArguments.Has(arguments, "securityProcessingEnabled")
            ? McpToolArguments.OptionalBool(arguments, "securityProcessingEnabled")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'securityProcessingEnabled' must not be null.")
            : existing.SecurityProcessingEnabled;
        var effCollection = McpToolArguments.Has(arguments, "isCollectionAccount")
            ? McpToolArguments.OptionalBool(arguments, "isCollectionAccount")
                ?? throw new McpToolException("Err_Mcp_InvalidArguments", "Argument 'isCollectionAccount' must not be null.")
            : existing.IsCollectionAccount;

        var changes = new List<McpAuditFieldChange>();
        McpMasterDataGuards.AddChange(changes, EntityType, accountId, "Name", existing.Name, effName);
        McpMasterDataGuards.AddChange(changes, EntityType, accountId, "Iban", existing.Iban, effIban);
        McpMasterDataGuards.AddChange(changes, EntityType, accountId, "BankContactId", existing.BankContactId, effBankContact);
        McpMasterDataGuards.AddChange(changes, EntityType, accountId, "SavingsPlanExpectation", existing.SavingsPlanExpectation, effExpectation);
        McpMasterDataGuards.AddChange(changes, EntityType, accountId, "SecurityProcessingEnabled", existing.SecurityProcessingEnabled, effSecurities);
        McpMasterDataGuards.AddChange(changes, EntityType, accountId, "IsCollectionAccount", existing.IsCollectionAccount, effCollection);
        if (changes.Count == 0)
        {
            return new { operation = "update", applied = false, reason = "unchanged", account = Result(existing) };
        }

        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AccountUpdated", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation = "update", accountId }), ct);
        AccountDto? updated;
        try
        {
            updated = await _accounts.UpdateAsync(accountId, owner, effName, effIban, effBankContact, effExpectation, effSecurities, effCollection, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        if (updated is null)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidState", "The account could not be updated.");
        }
        return new
        {
            operation = "update",
            applied = true,
            changedFields = changes.Select(c => c.Field).ToList(),
            account = Result(updated)
        };
    }

    private async Task<object> LinkedIbanAsync(
        JsonElement arguments, Guid owner, McpToolCallContext context, bool add, CancellationToken ct)
    {
        var accountId = McpToolArguments.RequiredGuid(arguments, "accountId");
        var iban = McpMasterDataGuards.RequireIban(McpToolArguments.OptionalString(arguments, "iban", McpMasterDataGuards.MaxIbanLength + 10));
        var operation = add ? "addLinkedIban" : "removeLinkedIban";

        var existing = await _accounts.GetAsync(accountId, owner, ct);
        if (existing is null)
        {
            return new { operation, account = (object?)null };
        }
        await McpMasterDataGuards.RequireRecordWritableAsync(
            _recordFilter, context.Access, McpDataType.Accounts,
            add ? McpRightKind.Complete : McpRightKind.Modify,
            new McpRecordContext { AccountTypeValue = (short)existing.Type }, ct);

        var changes = new List<McpAuditFieldChange>
        {
            new(EntityType, accountId, add ? "LinkedIbanAdded" : "LinkedIbanRemoved",
                add ? null : iban, add ? iban : null)
        };
        var staged = await McpWriteAudit.StageDataChangeAsync(
            _auditWriter, context, "AccountLinkedIbanChanged", ToolName, changes, 1,
            JsonSerializer.Serialize(new { operation, accountId }), ct);
        try
        {
            if (add)
            {
                await _accounts.AddLinkedIbanAsync(accountId, owner, iban, ct);
            }
            else if (!await _accounts.RemoveLinkedIbanAsync(accountId, owner, iban, ct))
            {
                McpWriteAudit.DiscardStaged(context, staged);
                throw new McpToolException("Err_Mcp_InvalidArguments",
                    "The linked IBAN does not exist on this account.");
            }
        }
        catch (McpToolException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw new McpToolException("Err_Mcp_InvalidArguments", ex.Message);
        }
        catch
        {
            McpWriteAudit.DiscardStaged(context, staged);
            throw;
        }
        return new { operation, applied = true, accountId, untrusted_data = new { iban } };
    }

    private async Task<object> DeleteAsync(JsonElement arguments, Guid owner, McpToolCallContext context, CancellationToken ct)
    {
        var accountId = McpToolArguments.RequiredGuid(arguments, "accountId");
        var existing = await _accounts.GetAsync(accountId, owner, ct);
        if (existing is null)
        {
            return new { operation = "delete", account = (object?)null };
        }
        return await McpMasterDataGuards.ApprovalRequiredAsync(
            _scopeEvaluator, context, McpDataType.Accounts, "delete",
            "Deletes the account including its linked sub-IBANs. Postings and statement " +
            "references lose their account link; the deletion is final.",
            new { accountId }, ct);
    }

    private static object Result(AccountDto a)
        => new
        {
            id = a.Id,
            accountType = a.Type.ToString(),
            bankContactId = a.BankContactId,
            savingsPlanExpectation = a.SavingsPlanExpectation.ToString(),
            securityProcessingEnabled = a.SecurityProcessingEnabled,
            isCollectionAccount = a.IsCollectionAccount,
            symbolAttachmentId = a.SymbolAttachmentId,
            untrusted_data = new { name = a.Name, iban = a.Iban, linkedIbans = a.LinkedIbans }
        };
}
