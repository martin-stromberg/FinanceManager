# Detailinventar: Referenzimplementierung, Ribbon-Infrastruktur und Basisklassen

## 1. Referenz: `BankAccountCardViewModel`

**Datei:** `FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs` (698 Zeilen)

### Reset-Ribbon-Aktion (Z. 291)

```csharp
new UiRibbonAction("Reset", localizer["Ribbon_Reset"].Value, "<svg><use href='icons/sprite.svg#undo'/></svg>", UiRibbonItemSize.Small, !HasPendingChanges, null, async () => { await ResetPendingAsync(); }) { MobileShortcut = true },
```

- Position: in der `manageActions`-Liste (Z. 288–293) **direkt hinter `Save`** (Z. 290) und vor `Delete` (Z. 292).
- `Disabled = !HasPendingChanges` — identische Bedingung wie `Save`.
- `MobileShortcut = true` — die Referenz markiert auch die Sekundäraktion. Die Projektregel
  (AGENT.md: „die primäre Aktion erhält `MobileShortcut = true`") spricht dagegen; die
  requirement.md benennt die Präferenz „kein `MobileShortcut` auf `Reset`" (offene Frage 1 —
  Entscheidung in der Planung, siehe Ribbon-Semantik unten).
- Manage-Tab `Ribbon_Group_Manage` in der Tab-Liste (Z. 365) zwischen `Ribbon_Group_Navigation` (Z. 364) und `Ribbon_Group_Bookings`/`Ribbon_Group_Linked` (Z. 366–367).

### `ResetPendingAsync` (Z. 373–389)

```csharp
private async Task ResetPendingAsync()
{
    try
    {
        ClearPendingChanges();
        if (Account != null)
        {
            CardRecord = await BuildCardRecordsAsync(Account);
        }
    }
    catch
    {
        // ignore
    }
    RaiseStateChanged();
}
```

- `ClearPendingChanges()` (leert `_pendingFieldValues`), danach Neuaufbau aus dem im ViewModel
  gehaltenen `Account`-DTO — **kein** API-/Server-Neuladen des Datensatzes.
- `Account != null`-Guard (defensiv gegen Ladefehler/Null-Baseline).
- Toleranter try/catch, `RaiseStateChanged()` immer am Ende.
- Nebenbemerkung: `BuildCardRecordsAsync` führt selbst einen Best-Effort-Lookup-Abruf
  (`Contacts_GetAsync` für den Bankkontakt, Z. 178–191) aus — auch die Referenz löst beim Reset
  einen Lookup-Nebenabruf aus, aber keinen Reload des Datensatzes.

## 2. Zweite vorhandene Verwendung: `SetupCardViewModel`

**Datei:** `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`

- `Reset`-Aktion (Z. 481–490): `UiRibbonItemSize.Small`, `undo`-Icon, `Ribbon_Reset`-Label,
  `Disabled = Saving || !HasPendingChanges`, `MobileShortcut = true`, Callback `ResetAll()` —
  setzt alle Setup-Sektionen zurück (anderes Aggregat, nicht kartengebunden). Bestätigt Label/Icon/
  Disabled-Konvention; für Karten ist das `BankAccountCardViewModel`-Muster maßgeblich.

## 3. Basisklassen (unverändert wiederverwendet)

### `BaseCardViewModel<TKeyValue>` — `FinanceManager.Web/ViewModels/Common/BaseCardViewModel.cs` (297 Zeilen)

| Member | Zeile | Bedeutung für Reset |
|---|---|---|
| `protected readonly Dictionary<string, object?> _pendingFieldValues` | 75 | Pending-Speicher (LabelKey → Wert/`LookupItem`) |
| `public bool HasPendingChanges` | 93 | `_pendingFieldValues.Count > 0` — Disabled-Bedingung |
| `public virtual void ValidateFieldValue(CardField, object?)` | 105–110 | schreibt Pending-Wert + `RaiseStateChanged()` |
| `public virtual void ValidateLookupField(CardField, LookupItem?)` | 117–129 | Pending-Lookup (null entfernt Eintrag) |
| `protected void ClearPendingChanges()` | 134–135 | `_pendingFieldValues.Clear()` — protected, in jedem VM nutzbar |
| `public virtual CardRecord ApplyPendingValues(CardRecord)` | 143–188 | legt Pending-Werte über den Record; ein Neubau nach `ClearPendingChanges()` enthält keine Overrides mehr |
| `protected string? InitPrefill` | 41 | Neuanlage-Vorbefüllung (via `SetInitValue`, Z. 21) — bleibt nach dem ersten `LoadAsync` erhalten und kann für den Neuanlage-Reset wiederverwendet werden |
| `public virtual CardRecord? CardRecord` | 69 | protected Setter — Neuaufbau-Zuweisung im VM möglich |
| `public abstract Task LoadAsync(Guid)` / `SaveAsync` / `DeleteAsync` | 48/55/63 | Save/Delete-Overrides je VM |
| `public virtual Task ReloadAsync()` | 256–259 | Default no-op; VMs überschreiben mit `InitializeAsync(Id)` — **nicht** für Reset verwenden (Server-Neuladen) |

### `BaseViewModel` — `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

| Member | Zeile | Bedeutung |
|---|---|---|
| `public virtual IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer)` | 688–704 | aggregiert eigene + Child-VM-Register |
| `protected virtual IReadOnlyList<UiRibbonRegister>? GetRibbonRegisterDefinition(IStringLocalizer)` | 711 | Override-Punkt (hier kommt `Reset` rein); Default `null` |
| `public IReadOnlyList<UiRibbonRegister>? GetRibbon(IStringLocalizer)` | 718–719 | Kompatibilitäts-Shim (delegiert auf Definition) |
| `protected void RaiseStateChanged()` | 277 | UI-/Ribbon-Refresh (Disabled-Zustände werden bei jedem `GetRibbonRegisters`-Aufruf neu ausgewertet, Ribbon.razor Z. 164–170) |
| `protected virtual CardRecord ApplyEnumTranslations(CardRecord)` | 617 | Enum-Anzeigetexte — Teil der Build-Pipeline |
| `protected IApiClient ApiClient` | 131 | Lazy aus `ServiceProvider` |
| `protected IConfirmationService ConfirmationService` | 155–164 | `NullConfirmationService`-Fallback |
| `SetBackNavigation` / `protected string? InitBack` | 115/120 | Back-Kontext (BudgetRule nutzt `InitBack`) |
| `RaiseUiActionRequested` / `RaiseUiEmbeddedPanelRequested` / `EmbeddedPanelSpec` | (u. a. Z. 258/308) | UI-Aktionskanal |
| `protected ParentLinkRequest? TryGetParentLinkFromQuery()` | BaseCardViewModel Z. 266–294 | Parent-Kontext beim Anlegen — betrifft Save, nicht Reset |

## 4. Ribbon-Modell — `FinanceManager.Web/ViewModels/Common/RibbonModels.cs`

- `UiRibbonItemSize` (Z. 6–12): `Small`/`Large`.
- `UiRibbonRegisterKind` (Z. 17–29): `Actions`, `LinkedInfo`, `Reports` u. a.
- `UiRibbonAction`-Record (Z. 42–76): Positionsparameter `Id, Label, IconSvg, Size, Disabled, Tooltip, Callback`; `MobileShortcut` (Z. 64–68, init-only) und `Hidden` (Z. 70–75) als optionale Properties; `Action`-Alias auf `Id` (Z. 55).
- `UiRibbonTab` (Z. 85): `Title`, `Items`, `Sort` (Default 0).
- `UiRibbonRegister` (Z. 93–108): `Kind`, `Tabs`; Komfort-Properties `Title`/`Items` des ersten Tabs.

## 5. Ribbon-Rendering und MobileShortcut-Semantik — `FinanceManager.Web/Components/Shared/Ribbon.razor`

- Tabs werden bei jedem Render aus `Provider.GetRibbonRegisters(Localizer)` neu aufgebaut (Z. 164–170) — ein `RaiseStateChanged()` genügt, um geänderte `Disabled`-Zustände sichtbar zu machen; `Disabled` ist zum Zeitpunkt der Definition ausgewertet.
- Gruppen mit gleichem Titel werden über Register hinweg gemergt, Reihenfolge = First-Seen, `Sort` wird am RibbonGroup übernommen (Z. 173–219).
- **MobileShortcut:** Im geschlossenen mobilen Gruppen-Header werden **alle** Items mit `MobileShortcut && !Disabled` als Icon-Shortcuts gerendert (Z. 95–113) — zwei markierte Aktionen (Save **und** Reset wie in der Referenz) erzeugen also zwei Shortcuts in derselben Gruppe.
- Einzige automatische Vergabe: eine Gruppe mit genau einem sichtbaren, nicht-disabled Item bekommt `MobileShortcut = true` gesetzt (Z. 222–228) — betrifft die neuen Reset-Aktionen nicht, da die Manage-Gruppe stets mehrere Items enthält.
