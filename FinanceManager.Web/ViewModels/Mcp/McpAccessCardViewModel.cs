using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Mcp;
using FinanceManager.Web.Components.Shared;
using FinanceManager.Web.Services;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Mcp;

/// <summary>
/// Card view model of a single MCP access (<c>/card/mcp-accesses/{id}</c>). Renders the master
/// data as a generic card record and exposes token rotation, status switches and revocation as
/// ribbon/section actions. Plaintext tokens are never kept on the card — after creation or
/// rotation they are shown exactly once via <see cref="McpTokenDisplayPanel"/>.
/// </summary>
[CardRoute("mcp-accesses")]
public sealed class McpAccessCardViewModel : BaseCardViewModel<(string Key, string Value)>
{
    private readonly IApiClient _api;

    /// <summary>
    /// Initializes a new instance of <see cref="McpAccessCardViewModel"/>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve additional services such as localizer.</param>
    /// <param name="apiClient">API client used to call the MCP management endpoints.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="apiClient"/> is <c>null</c>.</exception>
    public McpAccessCardViewModel(IServiceProvider sp, IApiClient apiClient) : base(sp)
    {
        _api = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>Currently loaded access identifier.</summary>
    public Guid AccessId { get; private set; }

    /// <summary>Currently loaded access detail, or <c>null</c> when not loaded.</summary>
    public McpAccessDetailDto? Access { get; private set; }

    /// <summary>Server-side level definitions used to render level names/explanations.</summary>
    public IReadOnlyList<McpAccessLevelDto> Levels { get; private set; } = Array.Empty<McpAccessLevelDto>();

    /// <summary>UI options of the access management (TLS flag, scope matrix, categories).</summary>
    public McpAccessOptionsDto? Options { get; private set; }

    /// <summary>
    /// Usage overview of this access aggregated from the audit trail (F-0603), or
    /// <see langword="null"/> when not loaded.
    /// </summary>
    public McpAccessUsageDto? Usage { get; private set; }

    /// <summary>
    /// Agent-monitoring view of this access (F-1103): queue depth, pending age and the
    /// 24-hour outcome counters of its requests, or <see langword="null"/> when not loaded.
    /// </summary>
    public McpAccessMonitoringDto? Monitoring { get; private set; }

    /// <summary>
    /// Indicates whether the management API reported <c>Err_Mcp_Disabled</c>; the card then
    /// shows a friendly hint instead of the access data.
    /// </summary>
    public bool IsMcpDisabled { get; private set; }

    /// <summary>
    /// Title shown on the card header; falls back to a localized resource when no access is loaded.
    /// </summary>
    public override string Title => Access?.Name ?? Localizer?["McpAccess_Title"] ?? "MCP access";

    /// <summary>
    /// Initializes the view model for the given id by delegating to <see cref="LoadAsync(Guid)"/>.
    /// </summary>
    /// <param name="id">Identifier of the access to load.</param>
    public override async Task InitializeAsync(Guid id)
    {
        await LoadAsync(id);
    }

    /// <summary>
    /// Loads the access, the level definitions and the UI options. Creation of accesses happens
    /// exclusively through the token wizard (<see cref="McpAccessCreateWizard"/>), so
    /// <see cref="Guid.Empty"/> is not a supported card state.
    /// </summary>
    /// <param name="id">Identifier of the access to load.</param>
    public override async Task LoadAsync(Guid id)
    {
        Loading = true; SetError(null, null); IsMcpDisabled = false; RaiseStateChanged();
        try
        {
            AccessId = id;
            if (id == Guid.Empty)
            {
                Access = null;
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return;
            }

            var detail = await _api.Mcp_GetAccessAsync(id);
            if (detail is null)
            {
                Access = null;
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
            }
            else
            {
                Access = detail;
            }

            try { Levels = await _api.Mcp_GetLevelsAsync(); } catch { Levels = Array.Empty<McpAccessLevelDto>(); }
            try { Options = await _api.Mcp_GetOptionsAsync(); } catch { Options = null; }
            try { Usage = Access is null ? null : await _api.Mcp_GetAccessUsageAsync(id); } catch { Usage = null; }
            try { Monitoring = Access is null ? null : await _api.Mcp_GetAccessMonitoringAsync(id); } catch { Monitoring = null; }

            CardRecord = BuildCardRecord(Access);
        }
        catch (Exception ex)
        {
            if (string.Equals(_api.LastErrorCode, "Err_Mcp_Disabled", StringComparison.Ordinal))
            {
                IsMcpDisabled = true;
            }
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
        }
        finally { Loading = false; RaiseStateChanged(); }
    }

    private string GetPendingText(string labelKey, string? fallback)
    {
        if (_pendingFieldValues.TryGetValue(labelKey, out var v) && v is string s)
        {
            return s;
        }
        return fallback ?? string.Empty;
    }

    /// <summary>
    /// Whether the loaded access uses the individually configured level — only then is the
    /// scope matrix editable and are filter rows persisted.
    /// </summary>
    public bool IsIndividualLevel
        => Access is not null && (McpAccessLevel)Access.Level == McpAccessLevel.Individuell;

    private short ResolvePendingApprovalMode()
    {
        if (Access is null) return (short)McpApprovalMode.PerAction;
        if (_pendingFieldValues.TryGetValue("Mcp_Card_ApprovalMode", out var am)
            && am is string amText
            && Enum.TryParse<McpApprovalMode>(amText, ignoreCase: true, out var parsed))
        {
            return (short)parsed;
        }
        return Access.ApprovalMode;
    }

    private DateTime? ResolvePendingExpiresUtc()
    {
        if (Access is null) return null;
        if (_pendingFieldValues.TryGetValue("Mcp_Card_ExpiresUtc", out var ex) && ex is string exText)
        {
            return DateTime.TryParse(exText, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : null;
        }
        return Access.ExpiresUtc;
    }

    /// <summary>
    /// Switches a standard-level access to <see cref="McpAccessLevel.Individuell"/> and seeds
    /// the persisted profile with the previously effective tuples ("Von Vorlage anpassen",
    /// ui-konzept.md §4.3.2). Pending master-data edits are carried into the same update so
    /// they are not silently dropped. The effective rights are identical right after the
    /// switch — the seeded template equals the derived profile.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the access is now individually configured.</returns>
    public async Task<bool> StartIndividualEditAsync(CancellationToken ct = default)
    {
        if (Access is null) return false;
        if (IsIndividualLevel) return true;
        try
        {
            var request = new McpAccessUpdateRequest(
                Name: GetPendingText("Mcp_Card_Name", Access.Name),
                Description: GetPendingText("Mcp_Card_Description", Access.Description),
                Level: (short)McpAccessLevel.Individuell,
                ApprovalMode: ResolvePendingApprovalMode(),
                ExpiresUtc: ResolvePendingExpiresUtc(),
                AllowedIpRanges: GetPendingText("Mcp_Card_AllowedIpRanges", Access.AllowedIpRanges));
            if (!await _api.Mcp_UpdateAccessAsync(Access.Id, request, ct))
            {
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return false;
            }

            // Seed the persisted profile with the previously effective tuples — the matrix
            // editor starts from the template's profile (berechtigungsmodell.md §3.2).
            var seed = Access.EffectiveScopes
                .Select(s => new McpAccessScopeDto(Guid.Empty, s.DataType, s.Right,
                    s.AccountTypesCsv, s.ContactCategoryIdsCsv, s.SecurityCategoryIdsCsv,
                    s.SavingsPlanCategoryIdsCsv, s.AttachmentCategoryIdsCsv))
                .ToList();
            await _api.Mcp_SetScopesAsync(Access.Id, new McpScopeSetRequest(seed), ct);

            ClearPendingChanges();
            await LoadAsync(Access.Id);
            return true;
        }
        catch (Exception ex)
        {
            // The level may already be Individuell while the seeding failed — the access then
            // grants nothing (restrictive direction). Reload so the card reflects the real
            // state, then surface the error for a retry.
            try { await LoadAsync(Access.Id); } catch { /* keep the original error */ }
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Replaces the persisted scope profile of the individually configured access
    /// (<c>PUT …/scopes</c>). Right-adding changes confirm via
    /// <see cref="IConfirmationService"/> (warning severity) — the persisted profile can
    /// never exceed the F3 matrix or the owner's own rights, the confirmation makes the
    /// expansion visible locally.
    /// </summary>
    /// <param name="scopes">The scope rows to persist (tuples with filter CSVs).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the profile was applied.</returns>
    public async Task<bool> SaveScopesAsync(IReadOnlyList<McpAccessScopeDto> scopes, CancellationToken ct = default)
    {
        if (Access is null || !IsIndividualLevel) return false;

        var before = Access.EffectiveScopes
            .Select(s => $"{s.DataType}:{s.Right}")
            .ToHashSet(StringComparer.Ordinal);
        var expands = scopes.Any(s => !before.Contains($"{s.DataType}:{s.Right}"));
        if (expands)
        {
            var confirmed = await ConfirmationService.ConfirmAsync(new(
                TitleResourceKey: "Confirmation_McpScopeExpand_Title",
                MessageResourceKey: "Confirmation_McpScopeExpand_Message",
                Severity: ConfirmationSeverity.Warning), ct);
            if (!confirmed) return false;
        }

        try
        {
            var ok = await _api.Mcp_SetScopesAsync(Access.Id, new McpScopeSetRequest(scopes), ct);
            if (!ok)
            {
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return false;
            }
            await LoadAsync(Access.Id);
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Replaces the five filter dimensions of one persisted scope row
    /// (<c>PUT …/filters/{scopeId}</c>); the server records the per-dimension delta as a
    /// <c>FiltersChanged</c> access event.
    /// </summary>
    /// <param name="scopeId">Persisted scope row id.</param>
    /// <param name="request">The filter set request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when applied.</returns>
    public async Task<bool> SaveScopeFiltersAsync(Guid scopeId, McpFilterSetRequest request, CancellationToken ct = default)
    {
        if (Access is null) return false;
        try
        {
            var ok = await _api.Mcp_SetFiltersAsync(Access.Id, scopeId, request, ct);
            if (!ok)
            {
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return false;
            }
            await LoadAsync(Access.Id);
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Saves pending master data changes (name, description, level, approval mode, expiry,
    /// IP binding). A level downgrade is confirmed via <see cref="IConfirmationService"/> first
    /// because rights are lost.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when the update succeeded.</returns>
    public async Task<bool> SaveAsync(CancellationToken ct = default)
    {
        if (Access is null) return false;
        try
        {
            var level = Access.Level;
            if (_pendingFieldValues.TryGetValue("Mcp_Card_Level", out var lv)
                && lv is string lvText
                && Enum.TryParse<McpAccessLevel>(lvText, ignoreCase: true, out var parsedLevel))
            {
                level = (short)parsedLevel;
            }

            var approvalMode = ResolvePendingApprovalMode();
            var expiresUtc = ResolvePendingExpiresUtc();

            // Level downgrade loses rights -> confirm before persisting.
            if (level < Access.Level)
            {
                var confirmed = await ConfirmationService.ConfirmAsync(new(
                    TitleResourceKey: "Confirmation_McpLevelDowngrade_Title",
                    MessageResourceKey: "Confirmation_McpLevelDowngrade_Message",
                    Severity: ConfirmationSeverity.Warning), ct);
                if (!confirmed) return false;
            }

            // Switching to durably pre-approved while the profile grants booking or deletion
            // skips the per-action approval for the highest-impact actions — critical
            // confirmation (ui-konzept.md §4.3.3, approval-konzept.md §3.4). The check runs
            // against the RESULTING profile: a level change in the same save swaps the
            // derived default profile, so the loaded effective scopes are not the final state.
            if (approvalMode == (short)McpApprovalMode.PreApproved
                && Access.ApprovalMode != (short)McpApprovalMode.PreApproved
                && ResultingProfileGrantsBookOrDelete(level))
            {
                var confirmed = await ConfirmationService.ConfirmAsync(new(
                    TitleResourceKey: "Confirmation_McpPreApproved_Title",
                    MessageResourceKey: "Confirmation_McpPreApproved_Message",
                    Severity: ConfirmationSeverity.Critical), ct);
                if (!confirmed) return false;
            }

            var request = new McpAccessUpdateRequest(
                Name: GetPendingText("Mcp_Card_Name", Access.Name),
                Description: GetPendingText("Mcp_Card_Description", Access.Description),
                Level: level,
                ApprovalMode: approvalMode,
                ExpiresUtc: expiresUtc,
                AllowedIpRanges: GetPendingText("Mcp_Card_AllowedIpRanges", Access.AllowedIpRanges));

            var ok = await _api.Mcp_UpdateAccessAsync(Access.Id, request, ct);
            if (!ok)
            {
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return false;
            }

            ClearPendingChanges();
            await LoadAsync(Access.Id);
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Determines whether the scope profile that results from persisting
    /// <paramref name="level"/> grants booking or deletion. Standard levels derive their
    /// profile from <see cref="McpLevelProfiles"/> (the server clears persisted rows on the
    /// update); the individual level keeps its persisted rows — the effective scopes of the
    /// loaded access when it already is individual, otherwise an empty profile because a
    /// non-individual access persists no scope rows.
    /// </summary>
    /// <param name="level">The access level being saved.</param>
    /// <returns><c>true</c> when the resulting profile contains Book or Delete tuples.</returns>
    private bool ResultingProfileGrantsBookOrDelete(short level)
    {
        if ((McpAccessLevel)level == McpAccessLevel.Individuell)
        {
            return Access!.Level == (short)McpAccessLevel.Individuell
                && Access.EffectiveScopes.Any(s =>
                    s.Right is (short)McpRightKind.Book or (short)McpRightKind.Delete);
        }
        return McpLevelProfiles.GetDefaultScopes((McpAccessLevel)level)
            .Any(t => t.Right is McpRightKind.Book or McpRightKind.Delete);
    }

    /// <summary>
    /// Rotates the token after a high-severity confirmation; the new plaintext token is shown
    /// exactly once via the <see cref="McpTokenDisplayPanel"/> overlay.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> when rotation succeeded.</returns>
    public async Task<bool> RotateTokenAsync(CancellationToken ct = default)
    {
        if (Access is null) return false;
        var confirmed = await ConfirmationService.ConfirmAsync(new(
            TitleResourceKey: "Confirmation_McpRotate_Title",
            MessageResourceKey: "Confirmation_McpRotate_Message",
            Severity: ConfirmationSeverity.Critical), ct);
        if (!confirmed) return false;

        try
        {
            var rotated = await _api.Mcp_RotateTokenAsync(Access.Id, ct);
            if (rotated is null)
            {
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return false;
            }

            var parms = new Dictionary<string, object?>
            {
                ["PlainTextToken"] = rotated.PlainTextToken,
                ["OverlayTitle"] = Localizer?["McpToken_Title_Rotated"].Value
            };
            RaiseUiActionRequested("ShowToken", new UiOverlaySpec(typeof(McpTokenDisplayPanel), parms));
            await LoadAsync(Access.Id);
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Activates a deactivated access (confirmed via <see cref="IConfirmationService"/>).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<bool> ActivateAsync(CancellationToken ct = default)
    {
        if (Access is null) return false;
        var confirmed = await ConfirmationService.ConfirmAsync(new(
            TitleResourceKey: "Confirmation_McpActivate_Title",
            MessageResourceKey: "Confirmation_McpActivate_Message",
            Severity: ConfirmationSeverity.Default), ct);
        if (!confirmed) return false;
        return await ChangeStatusAsync(activate: true, ct);
    }

    /// <summary>
    /// Deactivates the access (confirmed, high severity — running client sessions are rejected immediately).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<bool> DeactivateAsync(CancellationToken ct = default)
    {
        if (Access is null) return false;
        var confirmed = await ConfirmationService.ConfirmAsync(new(
            TitleResourceKey: "Confirmation_McpDeactivate_Title",
            MessageResourceKey: "Confirmation_McpDeactivate_Message",
            Severity: ConfirmationSeverity.Critical), ct);
        if (!confirmed) return false;
        return await ChangeStatusAsync(activate: false, ct);
    }

    private async Task<bool> ChangeStatusAsync(bool activate, CancellationToken ct)
    {
        try
        {
            var ok = activate
                ? await _api.Mcp_ActivateAccessAsync(Access!.Id, ct)
                : await _api.Mcp_DeactivateAccessAsync(Access!.Id, ct);
            if (!ok)
            {
                SetError("Err_NotFound", Localizer?["Err_NotFound"].Value ?? "Not found");
                return false;
            }
            await LoadAsync(Access!.Id);
            return true;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Revokes (deletes) the access after a critical confirmation. On success the card page
    /// navigates back to the list.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task<bool> RevokeAsync(CancellationToken ct = default)
    {
        if (Access is null) return false;
        var confirmed = await ConfirmationService.ConfirmAsync(new(
            TitleResourceKey: "Confirmation_McpRevoke_Title",
            MessageResourceKey: "Confirmation_McpRevoke_Message",
            Severity: ConfirmationSeverity.Critical), ct);
        if (!confirmed) return false;

        try
        {
            var ok = await _api.Mcp_RevokeAccessAsync(Access.Id, reason: null, ct);
            if (ok)
            {
                RaiseUiActionRequested("Deleted");
            }
            return ok;
        }
        catch (Exception ex)
        {
            SetError(_api.LastErrorCode, _api.LastError ?? ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Requests the extras panel (token, rights, usage sections) rendered below the card.
    /// </summary>
    /// <param name="position">Requested panel position.</param>
    /// <returns>Embedded panel specs for the requested position.</returns>
    public override IReadOnlyList<EmbeddedPanelSpec> GetEmbeddedPanelSpecs(EmbeddedPanelPosition position)
    {
        if (position != EmbeddedPanelPosition.AfterCard)
        {
            return Array.Empty<EmbeddedPanelSpec>();
        }

        var parms = new Dictionary<string, object?> { ["Provider"] = this };
        return new[]
        {
            new EmbeddedPanelSpec(typeof(McpAccessExtrasPanel), parms, EmbeddedPanelPosition.AfterCard, Visible: Access is not null)
        };
    }

    /// <summary>
    /// Builds the generic card record with editable master data fields and read-only state fields.
    /// </summary>
    /// <param name="a">Access detail or <c>null</c>.</param>
    /// <returns>The card record for the generic card renderer.</returns>
    private CardRecord BuildCardRecord(McpAccessDetailDto? a)
    {
        var L = ServiceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        var editable = a is not null && (McpAccessStatus)a.Status != McpAccessStatus.Revoked;

        var levelName = a is null
            ? string.Empty
            : Enum.IsDefined(typeof(McpAccessLevel), (int)a.Level)
                ? ((McpAccessLevel)a.Level).ToString()
                : a.Level.ToString();
        var approvalName = a is null
            ? string.Empty
            : Enum.IsDefined(typeof(McpApprovalMode), (int)a.ApprovalMode)
                ? ((McpApprovalMode)a.ApprovalMode).ToString()
                : a.ApprovalMode.ToString();

        var fields = new List<CardField>
        {
            new CardField("Mcp_Card_Name", CardFieldKind.Text, text: a?.Name ?? string.Empty, editable: editable),
            new CardField("Mcp_Card_Description", CardFieldKind.Text, text: a?.Description ?? string.Empty, editable: editable,
                hint: "Mcp_Card_Description_Hint"),
            new CardField("Mcp_Card_Level", CardFieldKind.Text, text: levelName, editable: editable,
                lookupType: "Enum:McpAccessLevel",
                hint: a is null ? null : McpAccessText.LevelDescriptionKeyPrefix + levelName),
            new CardField("Mcp_Card_ApprovalMode", CardFieldKind.Text, text: approvalName, editable: editable,
                lookupType: "Enum:McpApprovalMode",
                hint: "Mcp_Card_ApprovalMode_Hint"),
            new CardField("Mcp_Card_ExpiresUtc", CardFieldKind.Date, text: a?.ExpiresUtc?.ToString("yyyy-MM-dd"), editable: editable,
                hint: "Mcp_Card_ExpiresUtc_Hint"),
            new CardField("Mcp_Card_AllowedIpRanges", CardFieldKind.Text, text: a?.AllowedIpRanges ?? string.Empty, editable: editable,
                hint: "Mcp_Card_AllowedIpRanges_Hint"),
            new CardField("Mcp_Card_Status", CardFieldKind.Text,
                text: a is null ? string.Empty : McpAccessText.Status(L, a.Status, a.IsExpired), editable: false),
            new CardField("Mcp_Card_LastUsedUtc", CardFieldKind.Text,
                text: a?.LastUsedUtc?.ToString("u") ?? (L["Mcp_Value_NeverUsed"].Value ?? "-"), editable: false),
            new CardField("Mcp_Card_TokenIssuedUtc", CardFieldKind.Text,
                text: a?.TokenIssuedUtc.ToString("u") ?? "-", editable: false),
            new CardField("Mcp_Card_CreatedUtc", CardFieldKind.Text,
                text: a?.CreatedUtc.ToString("u") ?? "-", editable: false)
        };

        var record = new CardRecord(fields, a);
        return ApplyPendingValues(record);
    }

    /// <inheritdoc />
    protected override bool IsSymbolUploadAllowed() => false;

    /// <inheritdoc />
    protected override SymbolParentRef GetSymbolParent()
        => new(AttachmentEntityKind.None, Guid.Empty);

    /// <inheritdoc />
    protected override Task AssignNewSymbolAsync(Guid? attachmentId)
        => Task.CompletedTask;

    /// <summary>
    /// Provides ribbon registers: navigation (Back) and management actions
    /// (Save, Rotate token, Activate/Deactivate, Revoke).
    /// </summary>
    /// <param name="localizer">Localizer used to resolve labels.</param>
    /// <returns>Ribbon registers for the card.</returns>
    protected override IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer localizer)
    {
        var navigation = new List<UiRibbonAction>
        {
            new UiRibbonAction("Back", localizer["Ribbon_Back"].Value, "<svg><use href='/icons/sprite.svg#back'/></svg>",
                UiRibbonItemSize.Large, false, null, () => { RaiseUiActionRequested("Back"); return Task.CompletedTask; })
            { MobileShortcut = true }
        };

        var manage = new List<UiRibbonAction>
        {
            new UiRibbonAction("Save", localizer["Ribbon_Save"].Value, "<svg><use href='/icons/sprite.svg#save'/></svg>",
                UiRibbonItemSize.Large, Access is null, null, async () => { await SaveAsync(); })
            { MobileShortcut = true }
        };

        if (Access is not null)
        {
            var revoked = (McpAccessStatus)Access.Status == McpAccessStatus.Revoked;
            if (!revoked)
            {
                manage.Add(new UiRibbonAction("RotateToken", localizer["Mcp_Ribbon_RotateToken"].Value,
                    "<svg><use href='/icons/sprite.svg#key'/></svg>",
                    UiRibbonItemSize.Small, false, null, async () => { await RotateTokenAsync(); }));

                if ((McpAccessStatus)Access.Status == McpAccessStatus.Active)
                {
                    manage.Add(new UiRibbonAction("Deactivate", localizer["Ribbon_Deactivate"].Value,
                        "<svg><use href='/icons/sprite.svg#archive'/></svg>",
                        UiRibbonItemSize.Small, false, null, async () => { await DeactivateAsync(); }));
                }
                else
                {
                    manage.Add(new UiRibbonAction("Activate", localizer["Ribbon_Activate"].Value,
                        "<svg><use href='/icons/sprite.svg#check'/></svg>",
                        UiRibbonItemSize.Small, false, null, async () => { await ActivateAsync(); }));
                }
            }

            manage.Add(new UiRibbonAction("Revoke", localizer["Mcp_Ribbon_Revoke"].Value,
                "<svg><use href='/icons/sprite.svg#delete'/></svg>",
                UiRibbonItemSize.Small, revoked, null, async () => { await RevokeAsync(); }));
        }

        var tabs = new List<UiRibbonTab>
        {
            new UiRibbonTab(localizer["Ribbon_Group_Navigation"].Value, navigation),
            new UiRibbonTab(localizer["Ribbon_Group_Manage"].Value, manage)
        };

        return new List<UiRibbonRegister> { new UiRibbonRegister(UiRibbonRegisterKind.Actions, tabs) };
    }
}
