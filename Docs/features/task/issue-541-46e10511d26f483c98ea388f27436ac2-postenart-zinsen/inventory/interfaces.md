# Interfaces

## `IAccountService`

Datei: `FinanceManager.Application/Accounts/IAccountService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPostingRulesAsync` | `Guid accountId, Guid ownerUserId, CancellationToken ct` | `Task<IReadOnlyList<AccountPostingRuleDto>?>` | Regeln eines Kontos, sortiert nach Auswertungsindex; `null` bei unbekanntem/fremdem Konto |
| `UpsertPostingRuleAsync` | `Guid accountId, Guid ownerUserId, AccountPostingRuleUpsertRequest request, CancellationToken ct` | `Task` | Regel anlegen (`request.Id == null`) oder aktualisieren; `ArgumentException` bei fremdem Konto/ungültigem Request |
| `RemovePostingRuleAsync` | `Guid accountId, Guid ownerUserId, Guid ruleId, CancellationToken ct` | `Task<bool>` | Regel löschen; `false` bei nicht gefunden |

## `IApiClient`

Datei: `FinanceManager.Shared/IApiClient.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPostingRulesAsync` | `Guid accountId, CancellationToken ct` | `Task<IReadOnlyList<AccountPostingRuleDto>>` | Regelliste des Kontos |
| `UpsertPostingRuleAsync` | `Guid accountId, AccountPostingRuleUpsertRequest request, CancellationToken ct` | `Task` | Regel anlegen/aktualisieren |
| `RemovePostingRuleAsync` | `Guid accountId, Guid ruleId, CancellationToken ct` | `Task` | Regel löschen |
| `EnqueuePostingTypeReevaluationAsync` | `Guid accountId, CancellationToken ct` | `Task<BackgroundTaskInfo>` | Hintergrundtask „Postenarten neu bewerten" (Ribbon `ReevaluatePostingTypes`) |

## `IRibbonProvider`

Datei: `FinanceManager.Web/ViewModels/ViewModelBase.cs` (Z. 14–36)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRibbonRegisters` | `IStringLocalizer localizer` | `IReadOnlyList<UiRibbonRegister>?` | Ribbon-Definitionen; `BaseViewModel` aggregiert `GetRibbonRegisterDefinition(localizer)` der ViewModels |
| `GetActiveTab<TTabEnum>` | — | `TTabEnum?` | Aktiver Ribbon-Tab |
| `SetActiveTab<TTabEnum>` | `TTabEnum id` | `void` | Ribbon-Tab setzen |

Implementiert von `BaseViewModel` (Klasse `FinanceManager.Web.ViewModels.Common.BaseViewModel`, von der `BankAccountCardViewModel` über `BaseCardViewModel` erbt).

## `BaseViewModel` — UI-Aktions-Contract (kein Interface, aber der relevante Mechanismus)

Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

| Member | Signatur | Zweck |
|--------|----------|-------|
| `UiActionRequested` | `event EventHandler<UiActionEventArgs?>?` | UI-Aktionen (Navigation, Overlays, Embedded Panels); `OverlayHost`, `EmbeddedPanelHost` und `CardPage` abonnieren es |
| `UiActionEventArgs` | `(string? Action, string? Payload)` / `(string? Action, object? PayloadObject)` | Action-Name + String- oder Objekt-Payload (`UiOverlaySpec`, `EmbeddedPanelSpec`) |
| `RaiseUiActionRequested` | `(string? action)` / `(string? action, string? payload)` / `(string? action, object? payloadObject)` | protected Helper zum Auslösen |
| `RaiseUiEmbeddedPanelRequested` | `(EmbeddedPanelSpec spec)` | Embedded-Panel-Shortcut (Action `"EmbeddedPanel"`) |
| `RequestOpenAttachments` | `(AttachmentEntityKind, Guid)` | Referenz-Helper für Overlay-Specs |
| `RequestOpenLinkedCard` | `(string route, Guid? linkedId)` | Navigation mit `back`-URL |
| `GetEmbeddedPanelSpecs` | `(EmbeddedPanelPosition)` → `IReadOnlyList<EmbeddedPanelSpec>` | deklarative Embedded Panels (von `BankAccountCardViewModel` nicht überschrieben) |
| `EmbeddedPanelFinished` | `(object payload)` | Callback-Kanal für Embedded Panels |
