# Logik – Ribbon-Definition und -Rendering

Die Anforderung betrifft ausschließlich die Reihenfolge der `UiRibbonTab`-Einträge in drei Card-ViewModels. Diese Datei dokumentiert die beteiligten Logikklassen.

## `SavingsPlanCardViewModel`
Datei: `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs`

`public sealed class SavingsPlanCardViewModel : BaseCardViewModel<(string Key, string Value)>` (Zeile 13).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | Baut fünf `UiRibbonTab`s (`nav`, `manage`, `linked`, `analysis`, `bookings`) und gibt sie als Register `UiRibbonRegisterKind.Actions` zurück. **Aktuelle Reihenfolge in Zeile 895:** `{ nav, manage, linked, analysis, bookings }` — `linked` an Position 2 statt am Ende. Hängt anschließend `base.GetRibbonRegisterDefinition(localizer)` an (Zeilen 897–898); die Basiskette liefert `null` (siehe `BaseViewModel`), es kommen also keine weiteren Register hinzu. |
| `ArchiveAsync` | `private` | Archiviert den Sparplan (Ribbon-Aktion `Archive` der `manage`-Gruppe). |
| `IsSymbolUploadAllowed` / `GetSymbolParent` / `AssignNewSymbolAsync` | `protected override` | Symbol-Hooks, nicht ribbon-relevant. |

Gruppeninhalte (unverändert zu belassen):
- `nav` (Zeile 812): Aktion `Back` → `RaiseUiActionRequested("Back")`.
- `manage` (Zeile 826): `Save` (→ `SaveAsync`), `Archive` (→ `ArchiveAsync`), `Delete` (→ `DeleteAsync`).
- `linked` (Zeile 858): `Category` (→ `OpenCategory` mit URL `/card/savings-plans/categories/{CategoryId}`), `OpenPostings` (→ `/list/postings/savings-plan/{Id}`), `OpenAttachments` (→ `RequestOpenAttachments(AttachmentEntityKind.SavingsPlan, Id)`).
- `analysis` (Zeile 866): `Recalculate` (→ `LoadAnalysisAsync`).
- `bookings` (Zeile 882): `ActualReport` (→ `RequestActualReport`), deaktiviert sobald `HasLinkedAccounts`.

## `BankAccountCardViewModel`
Datei: `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`

`public sealed class BankAccountCardViewModel : BaseCardViewModel<(string Key, string Value)>, IDeletableViewModel` (Zeile 16).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | Baut vier Aktionslisten (`navActions`, `manageActions`, `linkedActions`, `bookingActions`) und erzeugt die Tab-Liste inline in Zeilen 302–308. **Aktuelle Reihenfolge:** Navigation → Manage → **Linked** → Bookings — `Linked` vor `Bookings` statt am Ende. |
| `ResetPendingAsync` | `private` | Verwirft Pending-Changes (Ribbon-Aktion `Reset`). |
| `SavePendingAsync` | `private` | Speichert Pending-Changes (Ribbon-Aktion `Save`). |

Gruppeninhalte (unverändert zu belassen):
- `Ribbon_Group_Navigation` (Zeile 304): `Back` mit `MobileShortcut = true`.
- `Ribbon_Group_Manage` (Zeile 305): `Save`, `Reset`, `Delete`.
- `Ribbon_Group_Linked` (Zeile 306): `OpenPostings` (URL-Payload `/list/postings/account/{Id}`), `OpenBankContact` (Payload `contacts,{BankContactId}`), `OpenAttachments`.
- `Ribbon_Group_Bookings` (Zeile 307): `CreatePreliminaryDraft` (legt vorläufigen Statement-Draft an, → `OpenStatementDraft`), `MobileShortcut = true`.

## `PostingsCardViewModel`
Datei: `FinanceManager.Web/ViewModels/Postings/Common/PostingsCardViewModel.cs`

`public sealed class PostingsCardViewModel : BaseCardViewModel<(string Key, string Value)>` (Zeile 12), Route-Attribut `CardRoute("postings")`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected override` | Baut drei `UiRibbonTab`s und gibt sie inline zurück. **Aktuelle Reihenfolge in Zeile 154:** `{ nav, linked, actions }` — `linked` vor `actions` statt am Ende. |
| `LoadAsync(Guid)` | `public override` | Lädt `PostingServiceDto` und baut `CardRecord`. |
| `ReverseAsync` | `private` | Storniert die Buchung, navigiert via `RaiseUiActionRequested("NavigateToPosting", id)` (Ribbon-Aktion `Reverse` der `actions`-Gruppe). |

Gruppeninhalte (unverändert zu belassen):
- `nav` (Zeile 132): `Back` mit Payload `kindLower` (z. B. `account/{AccountId}`), `MobileShortcut = true`.
- `linked` (Zeile 137): `OpenAccount`, `OpenContact`, `OpenSavingsPlan`, `OpenSecurity`, `OpenAttachments`.
- `actions` (Zeile 147): `Reverse`, deaktiviert wenn `Posting.IsReversed || Posting.IsReversal`.

## `BaseViewModel`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRibbonRegisters(IStringLocalizer)` | `public virtual` | Ruft `GetRibbonRegisterDefinition` auf und aggregiert Register aktiver Kind-ViewModels (`_childViewModels`, gefiltert über `IsChildViewModelActive`). Liefert `null` bei leerem Ergebnis (Zeilen 628–644). Wird von `IRibbonProvider` gefordert. |
| `GetRibbonRegisterDefinition(IStringLocalizer)` | `protected virtual` | Standard: `null` (Zeile 651). Von den drei Card-ViewModels überschrieben. |
| `GetRibbon(IStringLocalizer)` | `public` | Kompatibilitäts-Shim auf `GetRibbonRegisterDefinition` (Zeile 658). |
| `SetActiveTab<T>` / `GetActiveTab<T>` | `public` | No-op-Standardimplementierung (Zeilen 666–678). |

`BaseCardViewModel` (`FinanceManager.Web/ViewModels/Common/BaseCardViewModel.cs`, Zeile 13) definiert **kein** `GetRibbonRegisterDefinition`; der `baseRegs`-Aufruf in `SavingsPlanCardViewModel` (Zeile 897) landet daher in `BaseViewModel` und liefert `null`.

## `Ribbon` (Blazor-Komponente)
Datei: `FinanceManager.Web/Components/Shared/Ribbon.razor`

| Methode / Element | Kurzbeschreibung |
|-------------------|------------------|
| `BuildTabsToRender()` (Zeilen 166–254) | Ruft `Provider.GetRibbonRegisters(Localizer)` auf. Merged Gruppen registerübergreifend nach Gruppentitel in `groupMap`; die Reihenfolge wird in `groupOrder` als **First-Seen-Reihenfolge** festgehalten (Zeilen 174–191). `Sort` wird aus dem jeweils ersten `UiRibbonTab` einer Gruppe übernommen (Zeile 188). |
| `Groups.OrderBy(g => g.Sort)` (Zeile 251) | Sortiert die Gruppen nach `Sort`. Da LINQ-`OrderBy` stabil ist und alle betroffenen Tabs `Sort = 0` (Default) haben, entscheidet bei gleichem `Sort` die First-Seen-Reihenfolge — die Listenposition im ViewModel bestimmt die sichtbare Gruppenreihenfolge. |

Wichtig für die Auswertung: Eine reine Umordnung der `UiRibbonTab`-Listen genügt, um die sichtbare Reihenfolge zu ändern; `Sort`-Werte sind ein alternatives, explizites Mittel.

## `RibbonExtensions`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonExtensions.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| (Erweiterung, Zeilen 15–25) | Castet ein ViewModel auf `IRibbonProvider` und ruft `GetRibbonRegisters` auf; wird von Seiten/Komponenten zum Abfragen des Ribbons genutzt. |

## Referenzimplementierungen (bereits konform, unverändert)

| ViewModel | Datei | Tab-Reihenfolge |
|-----------|-------|-----------------|
| `SecurityCardViewModel` | `ViewModels/Securities/SecurityCardViewModel.cs` (Zeilen 364–385) | Navigation → Manage → **Linked** (letzte Gruppe) |
| `ContactCardViewModel` | `ViewModels/Contacts/ContactCardViewModel.cs` (Zeile 306) | Navigation → Manage → **Linked** (letzte Gruppe) |
| `StatementDraftCardViewModel` | `ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` (Zeilen 346–421) | Navigation → Manage → QuickEdit → Statement → Export → **Linked** (letzte Gruppe, Zeile 421) |
| `StatementDraftEntryCardViewModel` | `ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` (Zeilen 975–1055) | Navigation → Actions → **Linked** (letzte Gruppe, Zeile 1055) |

## `IRibbonProvider`-Verwendung

- `ViewModelBase.cs` (Zeilen 14–21): Interface-Deklaration mit `GetRibbonRegisters`.
- `IListProvider.cs` (Zeile 118): enthält ebenfalls `GetRibbonRegisters` im Contract.
- `Ribbon.razor` (Zeilen 17–19, 235–239, 260–270): nutzt `IRibbonProvider` für aktive Tab-Verwaltung.
