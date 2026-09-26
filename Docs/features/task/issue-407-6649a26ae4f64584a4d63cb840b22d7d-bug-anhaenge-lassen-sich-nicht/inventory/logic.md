# Logik

## `ConfirmDialog` (`FinanceManager.Web/Components/Shared/ConfirmDialog.razor`)

Rendert bei `Request != null` den äußeren Container `<div class="split-center" @onclick="OnBackdropClick">` (Zeile 7) mit innerem `<div class="split-dialog confirm-dialog confirm-severity-{severity}">` (Zeile 8, `@onclick:stopPropagation="true"`, `role="dialog"`, `aria-modal="true"`). Keine eigene Schichtklasse — der Dialog teilt sich `.split-center` mit allen Overlays.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnAfterRenderAsync(bool)` | `protected override` | Fokussiert beim ersten Rendern den Abbrechen-Button (`_cancelButtonRef.FocusAsync()`, Fehler werden ignoriert). |
| `OnConfirm()` | `private` | `ConfirmationService.SetResult(true)`. |
| `OnCancel()` | `private` | `ConfirmationService.Cancel()` (Schließen-Button und Abbrechen-Button). |
| `OnBackdropClick()` | `private` | `ConfirmationService.Cancel()` — Klick auf den `.split-center`-Hintergrund bricht die Bestätigung ab. |
| `GetSeverityClass()` | `private` | Liefert `Severity.ToString().ToLowerInvariant()` für die CSS-Klasse `confirm-severity-{wert}`. |

Parameter: `Request` (`ConfirmationRequest?`, `[Parameter]`). Injiziert: `IConfirmationService`, `IStringLocalizer<Pages>`.

## `ConfirmationDialogHost` (`FinanceManager.Web/Components/Shared/ConfirmationDialogHost.razor`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitialized()` | `protected override` | Abonniert `ConfirmationService.OnChanged`. |
| `OnConfirmationChanged(...)` | `private` | `InvokeAsync(StateHasChanged)`. |
| `Dispose()` | `public` | Deabonniert `OnChanged`. |

Rendert `<ConfirmDialog Request="ConfirmationService.CurrentRequest" />`, sobald `CurrentRequest != null` (Zeilen 5–8).
Abonnierte Events: `IConfirmationService.OnChanged`.

## `OverlayHost<TKeyValue>` (`FinanceManager.Web/Components/Shared/OverlayHost.razor`)

Generischer Overlay-Container der CardPage. Rendert bei `_spec != null && _visible` `<div class="split-center" @onclick="CloseOverlay">` (Zeile 10) > `<div class="split-dialog" style="max-width:90vH;" @onclick:stopPropagation="true">` (Zeile 11) mit `DynamicComponent` für `_spec.ComponentType` (Zeile 55). Stellt `CascadingValue`s `OverlayClose` und `OverlayOnFinished` bereit (Zeilen 53–57).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnParametersSet()` | `protected override` | Verwaltet das `UiActionRequested`-Abo des `Provider` (`BaseCardViewModel<TKeyValue>`). |
| `OnUiActionRequested(...)` | `private` | Bei `PayloadObject is UiOverlaySpec`: `_spec`/`_visible` setzen, `StateHasChanged`. |
| `CloseOverlay()` | `private` | Setzt `_visible = false`, `_spec = null`. |
| `GetOverlayTitle()` | `private` | Titel aus `OverlayTitle`-Parameter oder per `ComponentType`-Mapping (`AttachmentsPanel` → `Attachments_Title` usw.). |
| `Dispose()` | `public` | Deabonniert `UiActionRequested`. |

Abonnierte Events: `BaseCardViewModel<TKeyValue>.UiActionRequested` (auf `Provider`).

## `AttachmentsPanel` (`FinanceManager.Web/Components/Shared/AttachmentsPanel.razor`)

Wird innerhalb des `.split-center`-Overlays von `OverlayHost` bzw. `ListPage` als `DynamicComponent` gerendert. Lösch-Button: Zeile 118 (`@onclick="(()=> DeleteAsync(a.Id))"`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DeleteAsync(Guid id)` | `private` | Zeilen 399–422. Ruft `ConfirmationService.ConfirmAsync(new(... Severity: ConfirmationSeverity.Critical))` auf; bei `false` Abbruch, bei `true` `Api.Attachments_DeleteAsync(id)`, anschließend `LoadAsync(reset: true)`. |
| `LoadAsync(bool reset)` | `private` | Lädt Kategorien und seitenweise Anhänge (`Attachments_ListAsync`, `_pageSize = 50`). |
| `OnParametersSetAsync()` | `protected override` | `LoadAsync(reset: true)`. |
| `OnAfterRenderAsync(bool)` | `protected override` | Registriert JS-Modul `/js/attachments.js` (Drop-Area, Infinite Scroll). |
| `OnFilesSelected`/`UploadFileAsync` | `private` | Upload-Pfad (unverändert relevant). |
| `BeginEdit`/`ApplyEditAsync`/`CancelEdit` | `private` | Inline-Bearbeitung von Dateiname/Kategorie. |
| `OnDropUploadProgress`/`OnDropUploadCompleted`/`OnNeedMoreAsync` | `public` (`[JSInvokable]`) | JS-Callbacks für Upload-Fortschritt und Lazy Loading. |

Injiziert: `IApiClient`, `IConfirmationService`, `IStringLocalizer<AttachmentsPanel>`, `IJSRuntime`.

## `ConfirmationService` (`FinanceManager.Web/Services/ConfirmationService.cs`)

`sealed`, DI-registriert als `Scoped` (`ProgramExtensions.cs` Zeile 194). Thread-sicher via `_lock`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ConfirmAsync(ConfirmationRequest, CancellationToken)` | `public` | Liest `ShowConfirmations` (gecacht). Bei `false` → sofort `true`, kein Dialog. Sonst `_pendingRequest` setzen, `OnShow` + `OnChanged` auslösen, auf `TaskCompletionSource<bool>` warten, danach `CurrentRequest` zurücksetzen. |
| `SetResult(bool)` | `public` | Bestätigt/bricht ab; leert `_pendingRequest`, löst `OnChanged` aus. |
| `Cancel()` | `public` | `SetResult(false)`. |
| `InvalidateCacheAsync()` | `public` | Leert den `ShowConfirmations`-Cache. |
| `GetShowConfirmationsAsync(CancellationToken)` | `private` | `IApiClient.UserSettings_GetProfileAsync`; bei Fehler Default `true` (mit `LogWarning`). |
| `RaiseOnShow(ConfirmationRequest)` | `private` | `OnShow?.Invoke` (Fehler → `LogError`), danach `OnChanged`. |

Publizierte Events: `OnShow` (`EventHandler<ConfirmationRequest>`), `OnChanged` (`EventHandler`).
Eigenschaft: `CurrentRequest` (`ConfirmationRequest?`).

## `NullConfirmationService` (`FinanceManager.Web/Services/NullConfirmationService.cs`)

`internal sealed`, Singleton `Instance`. No-op-Implementierung von `IConfirmationService`: `ConfirmAsync` → immer `Task.FromResult(true)`, Events sind leere Accessors. Fallback für Headless-Szenarien.

## `BaseViewModel` (`FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RequestOpenAttachments(AttachmentEntityKind, Guid)` | `protected` | Zeilen 325–333. Baut `UiOverlaySpec(typeof(AttachmentsPanel), {ParentKind, ParentId})` und ruft `RaiseUiActionRequested("OpenAttachments", spec)`. |
| `RaiseUiActionRequested(string?, object?)` | `protected` | Zeilen 287–288. Löst `UiActionRequested` mit `UiActionEventArgs` aus. |
| `RaiseUiEmbeddedPanelRequested(EmbeddedPanelSpec)` | `protected` | Zeilen 295–296. Löst `UiActionRequested` mit `"EmbeddedPanel"`-Aktion aus. |

Publizierte Events: `UiActionRequested` (`EventHandler<UiActionEventArgs?>`), `StateChanged`, `AuthenticationRequired`.

## `ViewModelBase` (`FinanceManager.Web/ViewModels/ViewModelBase.cs`)

Ältere parallele Basisklasse mit eigenem `RequestOpenAttachments` (Zeilen 200–208), das `UiActionRequestedEx` auslöst (Legacy + Rich-Event-Paar `UiActionRequested`/`UiActionRequestedEx`).

## Seiten mit `ConfirmationDialogHost` und Overlay-Containern

| Datei | Position `ConfirmationDialogHost` | Overlay-Container danach (gleiche Seite) |
|-------|-----------------------------------|------------------------------------------|
| `FinanceManager.Web/Components/Pages/CardPage.razor` | Zeile 41 | `OverlayHost` Zeile 66 (`.split-center`, Zeile 10 in `OverlayHost.razor`) |
| `FinanceManager.Web/Components/Pages/ListPage.razor` | Zeile 30 | Listen-Overlay `.split-center list-overlay` ab Zeile 56 (hostet u. a. `AttachmentsPanel` via `DynamicComponent`) |
| `FinanceManager.Web/Components/Pages/Home.razor` | Zeile 22 | Mass-Import-Dialog `.split-center` Zeile 70 |
| `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | Zeile 29 | `.modal-overlay`-Dialoge Zeilen 102, 379, 400 (alle `z-index: 1000` in `app.ReportDashboard.css`) |

`BudgetReport.razor` enthält `.split-center`-Overlays (Zeilen 313, 373), aber **keinen** `ConfirmationDialogHost`. Kein `ConfirmationDialogHost` in Layout/`App`-Ebene vorhanden — jede Seite bettet den Host selbst ein.

## Weitere `ConfirmAsync`-Aufrufer (gleiches Schichtungsproblem möglich)

- `ContactMergePanel.razor` Zeile 108 (`IConfirmationService` injiziert Zeile 6) — läuft selbst im `OverlayHost`.
- `HomeKpiGrid.razor` Zeile 232 — die Seite (`Home.razor`) hat zusätzlich ein eigenes `.modal-overlay` (Zeile 52, `z-index: 1000`).
- `ContactDetail.razor` Zeile 140; `Setup/SetupUpdateTab.razor` Zeile 149; div. Card-ViewModels (`BankAccountCardViewModel`, `ContactCardViewModel`, `StatementDraftCardViewModel`, `HomeViewModel`, Setup-ViewModels u. a. — siehe `logic.md` ergänzend `grep "ConfirmAsync"`).

## Backend-Kontext (unverändert funktionsfähig)

- `AttachmentsController.DeleteAsync` (`FinanceManager.Web/Controllers/AttachmentsController.cs` Zeile 320) → `IAttachmentService.DeleteAsync` → `AttachmentService.DeleteAsync` (`FinanceManager.Infrastructure/Attachments/AttachmentService.cs` Zeile 234).
- Client: `ApiClient.Attachments_DeleteAsync` (`FinanceManager.Shared/ApiClient.Attachments.cs` Zeile 83, `DELETE /api/attachments/{id}`).
