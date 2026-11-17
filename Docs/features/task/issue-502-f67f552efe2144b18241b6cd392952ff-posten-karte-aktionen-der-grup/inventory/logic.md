# Logik – Bestand

## `PostingsCardViewModel`

Datei: `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs` (231 Zeilen). `BaseCardViewModel<(string Key, string Value)>`, `[CardRoute("postings")]`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|--------------|-------------------|
| `PostingsCardViewModel(IServiceProvider)` | `public` | Konstruktor, reicht `sp` an Basisklasse (Zeile 18). |
| `Configure(Guid id)` | `public` | Setzt `Id` vor der Initialisierung (Zeile 39). |
| `LoadAsync(Guid id)` | `public override` | Lädt Posten via `IApiClient.Postings_GetByIdAsync`; bei `Guid.Empty` oder `null` leerer `CardRecord` + ggf. `SetError` (Zeilen 48–81). |
| `BuildCardRecordsAsync(PostingServiceDto)` | `private` | Baut `CardRecord` mit 7+ Feldern; `Informal`-Flag und `SavingsPlanSubType` (Zeilen 88–115). |
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | **Kernstelle** (Zeilen 122–155), Details unten. |
| `IsSymbolUploadAllowed()` | `protected override` | `false` (Zeile 162). |
| `GetSymbolParent()` | `protected override` | `(AttachmentEntityKind.Posting, Guid.Empty)` (Zeilen 168–169). |
| `AssignNewSymbolAsync(Guid?)` | `protected override` | No-op (Zeile 176). |
| `ValidateSymbolAsync(...)` | `public override` | `null` (Zeilen 185–186). |
| `ReloadAsync()` | `public override` | `LoadAsync(Id)` (Zeilen 192–193). |
| `ReverseAsync()` | `private` | `Postings_ReverseAsync`; bei Erfolg `RaiseUiActionRequested("NavigateToPosting", reversalId.ToString())`, sonst `SetError` (Zeilen 198–230). |

### `GetRibbonRegisterDefinition` (Zeilen 122–155)

- `kindLower` (Zeilen 124–131): Mapping `Posting.Kind` → Back-Payload `account/{AccountId}`, `contact/{ContactId}`, `savings-plan/{SavingsPlanId}`, `security/{SecurityId}`.
- Tab `nav` (`Ribbon_Group_Navigation`, Zeilen 132–135): `Back` (Large, `MobileShortcut = true`) → `RaiseUiActionRequested("Back", kindLower)`.
- Tab `linked` (`Ribbon_Group_Linked`, Zeilen 137–144) — **der Problem-Tab**:

| `Id` | `Disabled` | Callback-Effekt |
|------|-----------|------------------|
| `OpenAccount` | `Posting?.AccountId == null` | `RaiseUiActionRequested("OpenAccount")` — **ohne Payload**, kein `case` in `CardPage.razor` → läuft ins Leere (Zeile 139). |
| `OpenContact` | `Posting?.ContactId == null` | `RaiseUiActionRequested("OpenContact")` — ebenso (Zeile 140). |
| `OpenSavingsPlan` | `Posting?.SavingsPlanId == null` | `RaiseUiActionRequested("OpenSavingsPlan")` — ebenso (Zeile 141). |
| `OpenSecurity` | `Posting?.SecurityId == null` | `RaiseUiActionRequested("OpenSecurity")` — ebenso (Zeile 142). |
| `OpenAttachments` | **hartkodiert `false`** (Zeile 143) | `RaiseUiActionRequested("OpenAttachments")` — **ohne `UiOverlaySpec`-Payload**; `CardPage`-`case "OpenAttachments"` ist bewusst No-Op und `OverlayHost` reagiert nur auf `UiOverlaySpec` → kein Overlay. |

- Tab `actions` (`Ribbon_Group_Actions`, Zeilen 146–153): `Reverse` (Large), `Disabled = Posting?.IsReversed == true || Posting?.IsReversal == true` → `ReverseAsync()`.
- Rückgabe: ein `UiRibbonRegister(UiRibbonRegisterKind.Actions, { nav, actions, linked })` — `Linked` steht seit #498 an letzter Position (Zeile 154).

**Events:** publiziert `UiActionRequested` (über `RaiseUiActionRequested`/`RequestOpenAttachments`-Infrastruktur der Basisklasse) und `StateChanged`. Abonniert keine Events.

## `CardPage.razor` — `OnUiActionRequested`-Dispatch

Datei: `FinanceManager.Web/Components/Pages/CardPage.razor`. Hostet alle Karten (`/card/{Kind}[/{SubKind}]/{Id}`, `?back`, `?prefill`); `_vm` ist `BaseCardViewModel<(string,string)>`; subskribiert `UiActionRequested` in `SetViewModel` (Zeilen 105–116).

Switch in `OnUiActionRequested` (Zeilen 128–365):

| `case` | Zeilen | Wirkung |
|--------|--------|---------|
| `Back` | 130–191 | Payload-URL direkt, sonst `Back`-Query-Param, Draft-Back, Listen-/Karten-Payload-Auswertung; Fallback `BuildListPath()`. |
| `OpenPostings` | 192–194 | `NavigationManager.NavigateTo(payload)` — **generischer URL-Handler** (Muster für Variante 1 der Anforderung). |
| `OpenStatementDraft` | 195–197 | `NavigateTo(payload)`. |
| `Prev` / `Next` | 198–213 | StatementDraft-Navigation via `BuildCardPath(..., Back)`. |
| `NavigateToPosting` | 214–219 | `NavigateTo($"/card/postings/{payload}")`. |
| `OpenPrices` / `OpenPerformance` | 220–225 | `NavigateTo(payload)`. |
| `OpenBankContact` | 226–236 | Payload `"{kind},{guid}"` → `NavigateTo($"/card/{kind}/{cid}?back={EscapeDataString(BuildCardPath(Kind, SubKind, Id, Back))}")` — **Muster für Variante 2**. |
| `OpenAccountDetails` | 237–249 | Payload-Guid (Fallback `Draft.DetectedAccountId`) → `/card/accounts/{id}?back=…` — **Muster für Variante 2**. |
| `OpenCategory` | 250–252 | `NavigateTo(payload)`. |
| `OpenAttachments` | 253–255 | **Bewusste No-Op**: Kommentar „Attachments are handled via UiOverlaySpec payloads emitted by viewmodels. No fallback." (plus Notiz Zeile 369). |
| `ClearEmbeddedPanel` | 256–258 | No-Op (EmbeddedPanelHost). |
| `DownloadOriginal` | 259–268 | Download Statement-Datei (`TriggerDownloadAsync`). |
| `ExportCsv` / `ExportXlsx` | 269–285 | Export-URL-Download. |
| `ViewOriginal` | 286–302 | `JS.InvokeVoidAsync("open", url, "_blank")` + `forceLoad`-Fallback. |
| `OpenMerge` | 303–305 | No-Op (UiOverlaySpec). |
| `Delete` | 306–308 | `HandleDeleteAsync()` (`IDeletableViewModel`, Zeilen 408–433). |
| `Deleted` | 309–313 | Navigation `Back` oder Liste. |
| `Saved` | 314–338 | Navigation mit `Back`-Auflösung bzw. `BuildCardPath`. |
| `NewRule` | 340–364 | Budget-Rule-Create mit `prefill`/`back`. |

**Befund:** Es gibt **keine** `case`s für `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity` — die vier Posten-Actions landen im leeren Default des Switches (kein `default`-Zweig → nichts passiert).

Hilfsmethoden: `BuildListPath()` (Zeilen 90–95), `BuildCardPath(kind, subKind, id, back)` (Zeilen 97–103), `TriggerDownloadAsync(url)` (Zeilen 435–451). `OnParametersSetAsync` (Zeilen 383–405) löst über `CardViewModelResolver.Resolve(kind, sub)` + `ICardInitializable` das ViewModel.

## `BaseViewModel` — relevante Infrastruktur

Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`.

| Member | Sichtbarkeit | Zweck |
|--------|--------------|-------|
| `UiActionRequested` | `public event EventHandler<UiActionEventArgs?>` | Einziger UI-Action-Kanal (Zeile 251). |
| `StateChanged` | `public event EventHandler` | UI-Refresh (Zeile 179). |
| `RaiseUiActionRequested(string?)` / `(string?, string?)` / `(string?, object?)` | `protected` | Feuert `UiActionRequested` mit leerer/String-/Objekt-Payload (Zeilen 271–288). |
| `RequestOpenAttachments(AttachmentEntityKind, Guid)` | `protected` | Baut `UiOverlaySpec(typeof(AttachmentsPanel), { ParentKind, ParentId })` und feuert `RaiseUiActionRequested("OpenAttachments", spec)` (Zeilen 325–334). **Die von der Anforderung geforderte Wiederverwendung.** |
| `RaiseUiEmbeddedPanelRequested(EmbeddedPanelSpec)` | `protected` | `UiActionRequested("EmbeddedPanel", spec)` (Zeilen 295–296). |
| `GetRibbonRegisters(IStringLocalizer)` | `public virtual` | Aggregiert `GetRibbonRegisterDefinition` + aktive Child-VMs (`IsChildViewModelActive`) (Zeilen 628–644). |
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected virtual` | `null` (Zeile 651). |
| `Navigation` | `protected` | Lazy `NavigationManager` aus `ServiceProvider` (Zeilen 130–136). |
| `ApiClient` | `protected` | Lazy `IApiClient` (Zeilen 118–124). |
| `EmbeddedPanelFinished(object?)` | `public virtual` | Feuert `UiActionRequested("EmbeddedFinished", payload)` (Zeilen 733–737). |

## `OverlayHost.razor`

Datei: `FinanceManager.Web/Components/Shared/OverlayHost.razor` (129 Zeilen). In `CardPage.razor` Zeile 73 eingebunden (`TKeyValue = (string,string)`, `Provider = _vm`).

- `OnParametersSet` (Zeilen 70–84): hängt/entfernt `UiActionRequested`-Subscription am `Provider`.
- `OnUiActionRequested` (Zeilen 86–94): **reagiert ausschließlich auf `e.PayloadObject is UiOverlaySpec`** — Action-Id ist irrelevant; string-Payloads werden ignoriert.
- Rendert `DynamicComponent` mit den `Parameters` (gefiltert um `OverlayTitle`/`Visible`, Zeilen 37–44) in Modal-Dialog; `OverlayOnFinished` navigiert via `NavigationManager.NavigateToModelCard(obj)` (Zeilen 46–51); `GetOverlayTitle` mappt u. a. `AttachmentsPanel` → `Attachments_Title` (Zeile 110).

## `Ribbon.razor`

Datei: `FinanceManager.Web/Components/Shared/Ribbon.razor`. Rendert `UiRibbonAction`s aus `Provider.GetRibbonRegisters(Localizer)`; gruppiert Tabs nach `Title` in First-Seen-Reihenfolge, sortiert per `Sort` (Zeilen 166–253). `OnItemClick` bricht bei `item.Disabled` ab und ruft `Callback` über `LoadingBar.RunAsync` (Zeilen 317–321). Button-`id` = `item.Id` (Zeile 70), `disabled`-Attribut aus `item.Disabled`.

## Referenzimplementierungen (unverändert, Musterquellen)

| ViewModel | Datei / Stelle | Muster |
|-----------|----------------|--------|
| `StatementDraftEntryCardViewModel` | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` Zeilen 995–1054 | Linked-Actions `OpenContact`/`OpenSavingsPlan`/`OpenSecurity` mit `Disabled = Entry == null \|\| Entry.X == null`; Callback baut `/card/{kind}/{id}` + `?back={EscapeDataString(nav.Uri)}` aus `ServiceProvider.GetService<NavigationManager>()` und feuert `RaiseUiActionRequested("OpenPostings", url)`; `MobileShortcut = true`; try/catch-Fallback ohne `back`. `[CardRoute("statement-drafts","entries")]` (Zeile 25). |
| `BankAccountCardViewModel` | `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` Zeilen 261–282 | `OpenPostings` mit URL-Payload `/list/postings/account/{Id}`; `OpenBankContact` mit Payload `"contacts,{BankContactId}"` (typspezifischer `case` in `CardPage`); `OpenAttachments` via `RequestOpenAttachments(AttachmentEntityKind.Account, Id)`, `Disabled = Account == null`. `[CardRoute("accounts")]` (Zeile 15). |
| `SavingsPlanCardViewModel` | `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` Zeile 862 | `OpenAttachments` via `RequestOpenAttachments(AttachmentEntityKind.SavingsPlan, Id)`, `Disabled = !IsEdit`. `[CardRoute("savings-plans")]` (Zeile 12). |
| `SecurityCardViewModel` | `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs` Zeile 383 | `Attachments` via `RequestOpenAttachments(AttachmentEntityKind.Security, Id)`, `Disabled = !(Id != Guid.Empty)`. `[CardRoute("securities")]` (Zeile 13). |
| `ContactCardViewModel` | `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` Zeile 300–302 | `Attachments` via `RequestOpenAttachments(AttachmentEntityKind.Contact, Id)`, `Disabled = Contact == null`. `[CardRoute("contacts")]` (Zeile 15). |

## Serverseitige Vorbedingung für Posten-Anhänge

`FinanceManager.Infrastructure/Statements/StatementDraftService.cs`, `PropagateEntryAttachmentsAsync` (Zeilen 50–66): `ReassignAsync(StatementDraftEntry → AttachmentEntityKind.Posting, bankPostingId)` (Zeile 58) und `CreateReferenceAsync(..., AttachmentEntityKind.Posting, pid, att.Id)` (Zeilen 59–63) — Posten-Anhänge werden beim Buchen bereits serverseitig erzeugt.

## `NavigationManagerExtensions.NavigateToModelCard`

`FinanceManager.Web/Extensions/NavigationManagerExtensions.cs` (Zeilen 23–96): Overlay-`OnFinished`-Navigation; `Guid`-Payload → `/card/contacts/{id}` (Zeile 42); String → direkt.

## Ziel-`CardRoute`s für die Navigationsaktionen

| Aktion | Ziel-VM | Route |
|--------|---------|-------|
| `OpenAccount` | `BankAccountCardViewModel` | `accounts` → `/card/accounts/{Posting.AccountId}` |
| `OpenContact` | `ContactCardViewModel` | `contacts` → `/card/contacts/{Posting.ContactId}` |
| `OpenSavingsPlan` | `SavingsPlanCardViewModel` | `savings-plans` → `/card/savings-plans/{Posting.SavingsPlanId}` |
| `OpenSecurity` | `SecurityCardViewModel` | `securities` → `/card/securities/{Posting.SecurityId}` |

`CardViewModelResolver` (`FinanceManager.Web/ViewModels/Common/CardViewModelResolver.cs`) scannt geladene Assemblies nach `ICardInitializable`-Typen mit `CardRouteAttribute` (gecacht, Zeilen 30–69).
