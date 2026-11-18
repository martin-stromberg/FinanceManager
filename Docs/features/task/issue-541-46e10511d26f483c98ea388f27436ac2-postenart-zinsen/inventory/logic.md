# Logik

## `BankAccountCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` (`[CardRoute("accounts")]`, `BaseCardViewModel<(string Key, string Value)>`, `IDeletableViewModel`)

### Posting-Rules-relevante Member

| Member | Sichtbarkeit | Kurzbeschreibung |
|--------|-------------|------------------|
| `PostingRules` | `public IReadOnlyList<AccountPostingRuleDto> { get; private set; }` (Z. 52) | Aktuelle Regelliste; wird bei `LoadAsync` bestehender Konten via `LoadPostingRulesAsync` befüllt (Z. 122) |
| `NewPostingRuleField` | `public` Property (Z. 57) | Eingabefeld für das Match-Feld, Default `Subject` |
| `NewPostingRulePattern` | `public` Property (Z. 62) | Eingabefeld Muster |
| `NewPostingRuleType` | `public` Property (Z. 67) | Eingabefeld Postenart, Default `Interest` |
| `NewPostingRuleOrderIndex` | `public int?` Property (Z. 73) | Eingabefeld Reihenfolge (`null` = anhängen/behalten) |
| `LoadPostingRulesAsync()` | `public async Task` (Z. 588) | Lädt Regeln via `api.GetPostingRulesAsync(Id)`; Fehler → `PostingRules = []` + `SetError` |
| `UpsertPostingRuleAsync(Guid? ruleId = null)` | `public async Task<bool>` (Z. 607) | Clientseitige Leer-Pattern-Vorprüfung (SetError + `false`), dann `api.UpsertPostingRuleAsync`, danach Pattern-Reset + Reload |
| `RemovePostingRuleAsync(Guid ruleId)` | `public async Task` (Z. 644) | `api.RemovePostingRuleAsync` + Reload |
| `LoadAsync(Guid id)` | `public override` (Z. 100) | Lädt Konto; bei `Guid.Empty` leeres `AccountDto` (Neuanlage); bei bestehendem Konto `LoadPostingRulesAsync()` |
| `Account` | `public AccountDto?` (Z. 35) | Geladenes Konto; `Account.Id == Guid.Empty` markiert den ungespeicherten Zustand |

### Ribbon (`GetRibbonRegisterDefinition`, Z. 277–356)

Tabs in Reihenfolge:

1. `Ribbon_Group_Navigation`: `Back` (Large, `MobileShortcut`).
2. `Ribbon_Group_Manage`: `Save` (Large, disabled `!HasPendingChanges`), `Reset`, `Delete` (disabled wenn `Account == null || Account.Id == Guid.Empty`).
3. `Ribbon_Group_Bookings` (Z. 318–345): 
   - `CreatePreliminaryDraft` — Label `Ribbon_CreatePreliminaryDraft`, Icon `#edit`, `UiRibbonItemSize.Large`, disabled `Account == null || Account.Id == Guid.Empty`, `MobileShortcut = true`; Callback erzeugt Statement-Draft via API und raist `OpenStatementDraft` mit URL.
   - `ReevaluatePostingTypes` — Label `Ribbon_ReevaluatePostingTypes`, Icon `#refresh`, `Small`, disabled `Account == null || Account.Id == Guid.Empty`; Callback ruft `ApiClient.EnqueuePostingTypeReevaluationAsync(Id)`.
4. `Ribbon_Group_Linked`: `OpenPostings`, `OpenBankContact`, `OpenAttachments` (letzterer ruft `RequestOpenAttachments(AttachmentEntityKind.Account, Id)` — Referenzmuster für Overlay-Auslösung aus Ribbon-Aktion heraus).

Ein neuer Ribbon-Button in der Buchungen-Gruppe würde dem bestehenden Muster folgen: `new UiRibbonAction("<Id>", localizer["<Key>"].Value, "<svg><use href='/icons/sprite.svg#<icon>'/></svg>", UiRibbonItemSize.Small, Account == null || Account.Id == Guid.Empty, null, () => { RaiseUiActionRequested("<Action>", new UiOverlaySpec(typeof(PostingRulesPanel), parms)); return Task.CompletedTask; })`.

### Events

- Publiziert: `UiActionRequested` (via `RaiseUiActionRequested`), `StateChanged` (via `RaiseStateChanged`).
- Ruft `EmbeddedPanelFinished` **nicht** auf; `GetEmbeddedPanelSpecs` wird nicht überschrieben.

## `PostingRulesPanel` (Blazor-Komponente)

Datei: `FinanceManager.Web/Components/Shared/PostingRulesPanel.razor`

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Vm` | `[Parameter, EditorRequired] BankAccountCardViewModel?` | Datenquelle und Aktionsziel |
| `Localizer` | `[Parameter] IStringLocalizer?` | Übergabe aus `CardPage` (`IStringLocalizer<Pages>`) |
| `FieldLabel(PostingRuleField)` | `private` | Lokalisierte Feldnamen via `EnumDisplayTexts.GetText` |
| `PostingTypeLabel(BankPostingType)` | `private` | Dito mit `emptyWhenUnspecified: true` |
| `OnFieldChanged` / `OnTypeChanged` / `OnOrderIndexChanged` / `OnPatternInput` | `private` | Zweiweg-Bindung der Eingabezeile in die `Vm.NewPostingRule*` Properties |
| `OnKeyDownAsync(KeyboardEventArgs)` | `private` | `Enter` → `SaveRuleAsync()` |
| `BeginEdit(AccountPostingRuleDto)` | `private` | Edit-Modus: `_editRuleId` setzen, Werte in `Vm.NewPostingRule*` übernehmen |
| `CancelEdit()` | `private` | Edit-Modus verlassen, Pattern/OrderIndex zurücksetzen |
| `SaveRuleAsync()` | `private async Task` | Leer-Pattern-Vorprüfung → `_panelError` (`PostingRules_Error_PatternRequired`); sonst `Vm.UpsertPostingRuleAsync(_editRuleId)` mit `_busy`-Sperre |
| `RemoveRuleAsync(Guid)` | `private async Task` | `Vm.RemovePostingRuleAsync` mit `_busy`-Sperre; entfernt ggf. laufende Bearbeitung derselben Regel |
| `_busy`, `_editRuleId`, `_panelError` | `private` Felder | UI-Zustand: Sperre, Edit-Modus, Panel-Fehlerzeile |

**Markup (Ist-Zustand):** `<div class="posting-rules-panel">` > `<table class="fm-table card card-table">` mit Titelzeile (`PostingRules_Section_Title`), optionalen Spaltenköpfen (Feld/Muster/Postenart/Reihenfolge/leer), Leermeldung (`PostingRules_Empty`), Regelzeilen und fixer Eingabezeile.

**Aktuelle Buttons (alle Textbuttons):**
- Zeile 45–50: Bearbeiten — `class="btn btn-sm"`, `disabled="@_busy"`, Text `PostingRules_Edit_Button`
- Zeile 51–56: Entfernen — `class="btn btn-sm btn-danger"`, Text `PostingRules_Remove_Button`
- Zeile 106–111: Abbrechen — `class="btn btn-sm"`, nur wenn `_editRuleId.HasValue`, Text `PostingRules_Cancel_Button`
- Zeile 113–120: Hinzufügen/Speichern — `class="btn btn-sm btn-primary"`, Text `PostingRules_Add_Button` bzw. `PostingRules_Save_Button`

Kein `title`-Attribut, keine SVG-Icons. Hinweis: `btn`, `btn-sm`, `btn-primary` sind **nicht** im CSS definiert; wirksam sind nur die generischen `button`-/`button.danger`-Stile (`theme.Dark.css` Z. 48–63) und Inline-Styles.

**Eingabezeile:** `select.card-input` ×2 (Feld via `_ruleFields` = {Subject, BookingDescription}; Postenart via `_postingTypes` = {Inflow, Outflow, Interest, Tax} — `Unspecified` bewusst nicht auswählbar), `input.card-input` Text (Placeholder `PostingRules_Add_Placeholder`) + Number (`PostingRules_OrderIndex_Hint` als Placeholder **und** `title` — einziges vorhandenes Tooltip im Panel).

**Verwendung:** ausschließlich `CardPage.razor` Z. 48. Kein Verweis auf `OverlayClose`/`EmbeddedClose` Cascading Values (Panel hat aktuell kein eigenes Schließen).

## `OverlayHost` (generischer Overlay-Dialog)

Datei: `FinanceManager.Web/Components/Shared/OverlayHost.razor` (`@typeparam TKeyValue`)

| Element | Beschreibung |
|---------|--------------|
| `Provider` | `[Parameter] BaseCardViewModel<TKeyValue>?` — abonniert `UiActionRequested` in `OnParametersSet` |
| `Localizer` | `[Parameter] IStringLocalizer<Pages>?` |
| `OnUiActionRequested` | Bei `e.PayloadObject is UiOverlaySpec` → `_spec` setzen, `_visible = spec.Modal`, Re-Render (Action-Name irrelevant) |
| Rendering | `.split-center` (Backdrop-Klick → `CloseOverlay`) > `.split-dialog` (`stopPropagation`, `max-width:90vH`) > Titelzeile + `icon-btn`-Schließen-Button (`#clear`, `Btn_Close`) > `DynamicComponent` mit gefilterten Parametern |
| Parameterfilter | `OverlayTitle` und `Visible` werden aus `_spec.Parameters` entfernt; **`Title` bleibt drin** → Komponenten, die `Title` nicht als `[Parameter]` deklarieren, würden bei `DynamicComponent` eine Ausnahme werfen. `AssignStatementOverlay` deklariert `Title`; `SavingsPlanActualReportPanel` deklariert `OverlayTitle`, erhält aber `["Title"]` im Spec (potenzielle Latenz-Falle) — sicherer Weg für neue Panels: `OverlayTitle`-Key oder `[Parameter] string? Title`. |
| Titelauflösung | `Parameters["Title"]` (String) > `Parameters["OverlayTitle"]` > Known-Types-Liste (`AttachmentsPanel`, `ContactMergePanel`, `SecurityPriceImportPanel`, `SecurityPricesBackfillPanel`, `SetPasswordOverlay`, `AssignStatementOverlay`, `MassBookingOptionsPanel`, `McpAccessCreateWizard`, `McpTokenDisplayPanel`) > `ComponentType.Name`. `PostingRulesPanel` ist nicht in der Liste. |
| Cascading Values | `OverlayClose` (`Action`), `OverlayOnFinished` (`Action<object>` → schließt + `NavigateToModelCard`) |
| `CloseOverlay()` | `_visible = false; _spec = null` |

Bereits auf `CardPage.razor` Z. 77 verdrahtet: `<OverlayHost TKeyValue="(string,string)" Provider="_vm" Localizer="Localizer" />`.

## `EmbeddedPanelHost`

Datei: `FinanceManager.Web/Components/Shared/EmbeddedPanelHost.razor`

Generischer Host für **eingebettete** Panels (`EmbeddedPanelSpec`, Positionen `AfterRibbon`/`AfterCard`/`AfterList`, Action `ClearEmbeddedPanel`, Cascading `EmbeddedClose`/`EmbeddedOnFinished`). Für diese Anforderung nur Kontext: `PostingRulesPanel` ist **kein** deklaratives EmbeddedPanel — es wird hart in `CardPage` verdrahtet.

## `CardPage.razor`

Datei: `FinanceManager.Web/Components/Pages/CardPage.razor` (`@rendermode InteractiveServer`)

| Zeilen | Inhalt |
|--------|--------|
| 36 | `<Ribbon TTabEnum="TabId" Provider="_vm" Localizer="Localizer" />` |
| 43, 60 | `<EmbeddedPanelHost>` an `AfterRibbon` bzw. `AfterCard` |
| 44 | `<GenericCardPage>` (Karteninhalt) |
| **46–53** | **`@if (_vm is BankAccountCardViewModel bankVm && bankVm.Account?.Id != Guid.Empty)` → `<PostingRulesPanel Vm="bankVm" Localizer="Localizer" />` (Z. 48) und, bei `bankVm.EffectiveIsCollectionAccount`, `<LinkedIbansPanel>` (Z. 51)** — der zu entfernende Teil ist nur Z. 48; die `if`-Bedingung bleibt für `LinkedIbansPanel` erhalten |
| 55–58 | `SavingsPlanLinkedAccountsPanel` für `SavingsPlanCardViewModel` (unverändert) |
| 77 | `<OverlayHost TKeyValue="(string,string)" Provider="_vm" Localizer="Localizer" />` |
| 124–370 | `OnUiActionRequested`-Switch: behandelt u. a. `Back`, `OpenPostings`, `OpenStatementDraft`, `OpenBankContact`, `Saved`, `Deleted`; `OpenAttachments` (Z. 257) und `OpenMerge` (Z. 307) sind explizite No-ops — der `UiOverlaySpec`-Payload wird allein vom `OverlayHost` verarbeitet. Ein neuer Action-Name für das Regel-Overlay braucht keinen Switch-Eintrag. |

## `Ribbon.razor`

Datei: `FinanceManager.Web/Components/Shared/Ribbon.razor`

- Rendert jede `UiRibbonAction` als `<button id="{item.Id}" disabled title="{Tooltip ?? Label}" aria-label="{Label}" class="fm-ribbon-btn {small|large}">` (Z. 70–79); bei `Disabled` wird der `Callback` nicht ausgeführt (`OnItemClick`, Z. 317–321).
- `Hidden == true` Items werden übersprungen (Z. 196–199) — Alternative zum Deaktivieren (Offene Frage 1 der Anforderung).
- Desktop-Gruppe + Mobile-Gruppe je `UiRibbonTab`; Gruppen werden nach Tab-Listenreihenfolge bzw. `Sort` angeordnet.
- Mobile: `{Id}-mobile`-Buttons und `{Id}-mobile-shortcut` für `MobileShortcut`-Items.

## `AccountService` (Posting-Rule-Implementierung)

Datei: `FinanceManager.Infrastructure/Accounts/AccountService.cs` (Z. ~471–539)

Implementiert `IAccountService.GetPostingRulesAsync`/`UpsertPostingRuleAsync`/`RemovePostingRuleAsync` mit Owner-Filterung (`ownerUserId`), OrderIndex-Logik (Append = max+1, explizit inkl. 0, `null` beim Update = behalten) und Ablehnung von `BankPostingType.Unspecified`. Unverändert von der Anforderung.

## `AccountsController` (Endpunkte)

Datei: `FinanceManager.Web/Controllers/AccountsController.cs`

| Endpunkt | Zeile | Beschreibung |
|----------|-------|--------------|
| `GET /api/accounts/{id}/posting-rules` | 450–466 | Regelliste (404 bei unbekanntem/fremdem Konto) |
| `POST /api/accounts/{id}/posting-rules` | 479–504 | Upsert (400 bei ungültigem Request, 404 bei fremdem Konto) |
| `DELETE /api/accounts/{id}/posting-rules/{ruleId}` | 515–530 | Löschen (204/404) |
| `POST /api/accounts/{id}/posting-types/reclassify` | 532 ff. | Re-Evaluation-Hintergrundtask (Ribbon `ReevaluatePostingTypes`) |

## `ApiClient` (Client-Methoden)

Datei: `FinanceManager.Shared/ApiClient.Accounts.cs`

| Methode | Zeile | Route |
|---------|-------|-------|
| `GetPostingRulesAsync(Guid, CancellationToken)` | 185–190 | GET posting-rules |
| `UpsertPostingRuleAsync(Guid, AccountPostingRuleUpsertRequest, CancellationToken)` | 198–202 | POST posting-rules |
| `RemovePostingRuleAsync(Guid, Guid, CancellationToken)` | 210–214 | DELETE posting-rules/{ruleId} |
| `EnqueuePostingTypeReevaluationAsync(Guid, CancellationToken)` | 222 ff. | POST posting-types/reclassify |

## Referenzmuster für Overlay-Auslösung aus Ribbon

- `BaseViewModel.RequestOpenAttachments(parentKind, parentId)` (Z. 325–334): baut `UiOverlaySpec(typeof(AttachmentsPanel), {ParentKind, ParentId})` und raist `RaiseUiActionRequested("OpenAttachments", spec)`.
- `UserCardViewModel` Z. 405: Ribbon-Aktion `SetPassword` raist `RaiseUiActionRequested("SetPassword", new UiOverlaySpec(typeof(SetPasswordOverlay), specParams))` — direktes Ribbon→Overlay-Muster.
- `SavingsPlanCardViewModel.RequestActualReport()` (Z. 334–348): `UiOverlaySpec(typeof(SavingsPlanActualReportPanel), { ["Title"], ["Vm"] = this })` — Übergabe des eigenen ViewModels als Parameter (analog zu `Vm` in `PostingRulesPanel`).
