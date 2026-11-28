# Inventarisierung – Issue #593: „Zurücksetzen"-Aktion auf Detailansichten

**Quelle:** `requirement.md` (Übersetzte Anforderung Issue #593)
**Datum der Bestandsaufnahme:** aktueller Arbeitsstand des Repositories
**Art:** Faktenbasierte Code-Inventarisierung (keine Implementierung)

## 1. Aufgabe in einem Satz

In acht Card-ViewModels fehlt die Ribbon-Aktion `Reset` in der Gruppe `Ribbon_Group_Manage`
(direkt hinter `Save`), die per `ClearPendingChanges()` ungespeicherte Feldänderungen verwirft
und das `CardRecord` aus dem im ViewModel gehaltenen Modell/DTO ohne Server-Neuladen neu aufbaut —
Referenz ist `BankAccountCardViewModel` (Aktion Z. 291, `ResetPendingAsync` Z. 373–389).

## 2. Detaildokumente

| Dokument | Inhalt |
|---|---|
| [inventory/in-scope-viewmodels.md](inventory/in-scope-viewmodels.md) | Pro-ViewModel-Fakten der acht In-Scope-ViewModels: Manage-Ribbon-Zeilen, Baseline-Träger, `CardRecord`-Neuaufbau, Save-Pfad-Besonderheiten, Neuanlage-Modus, Insert-Position |
| [inventory/reference-and-basetypes.md](inventory/reference-and-basetypes.md) | Referenzimplementierung `BankAccountCardViewModel`, Zweitverwendung `SetupCardViewModel`, `BaseCardViewModel`/`BaseViewModel`-API, `RibbonModels.cs`, `MobileShortcut`-Semantik in `Ribbon.razor` |
| [inventory/tests-resources-excluded.md](inventory/tests-resources-excluded.md) | Test-Infrastruktur und Vorbilder, vorhandene resx-Ressourcen/Icons, ausgeschlossene ViewModels mit Code-Befund, Projektregeln, Einordnung der offenen Fragen |

## 3. Verbindlicher Scope — Befund

### 3.1 Referenzimplementierung

`FinanceManager.Web/ViewModels/Accounts/BankAccountCardViewModel.cs`:
- Aktion `"Reset"` Z. 291: `UiRibbonItemSize.Small`, Icon `icons/sprite.svg#undo`, Label `localizer["Ribbon_Reset"].Value`, `Disabled = !HasPendingChanges`, Callback `ResetPendingAsync()`, `MobileShortcut = true` (Referenz setzt `true` — siehe §7.1).
- `ResetPendingAsync` Z. 373–389: try/catch-tolerant, `ClearPendingChanges()`, `if (Account != null) CardRecord = await BuildCardRecordsAsync(Account)`, `RaiseStateChanged()` am Ende.
- Eine zweite bestehende Verwendung desselben Labels/Icons existiert in `SetupCardViewModel` Z. 481–490 (`ResetAll()`, Setup-Aggregat — nicht kartengebunden).

### 3.2 Acht In-Scope-ViewModels (keines hat heute eine `Reset`-Aktion)

| # | ViewModel / Datei | Manage-Tab | Baseline | CardRecord-Neuaufbau | Besonderheiten |
|---|---|---|---|---|---|
| 1 | `FinanceManager.Web/ViewModels/Contacts/ContactCardViewModel.cs` | Z. 260–295 (Save 262, Delete 290, OpenMerge 291) | `Contact` (`ContactDto`, Z. 34) | `BuildCardRecordAsync(Contact)` (Z. 140–166) — Lookup-Nebenabruf `ContactCategories_ListAsync` (Z. 147) | Neuanlage-DTO inkl. `InitPrefill`-Name (Z. 89–91) |
| 2 | `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` | Z. 826–855 (Save 828, Delete 837, Archive 846) | `_loadedDto` (`SavingsPlanDto`, Z. 110) | `BuildCardRecordAsync(_loadedDto)` (Z. 567–683); `RebuildCardRecordAsync` (Z. 756–766) als Ansatzpunkt | `Model` wird bei Eingabe mutiert (Z. 727–784) → zusätzlich aus `_loadedDto` neu ableiten bzw. Neuanlage-`SavingsPlanCreateRequest` (InitPrefill, Z. 144–145) |
| 3 | `FinanceManager.Web/ViewModels/SavingsPlans/Categories/SavingsPlanCategoryCardViewModel.cs` | Z. 192–196 (Save 194, Delete 195) | `Name`/`SymbolId` (Z. 32/37) | `BuildCardRecord(Name, SymbolId)` (Z. 92–100) | Save mutiert Baseline vor API-Aufruf (Z. 110–116) → persistierter Stand separat halten |
| 4 | `FinanceManager.Web/ViewModels/Securities/SecurityCardViewModel.cs` | Z. 371–376 (Save 373, Delete 374, Archive 375) | `Security` (`SecurityDto`, Z. 32) | `BuildCardRecordAsync(Security)` (Z. 330–347) | `Model`-Fallback-Felder; `ApplyPendingValuesToModel` mutiert `Model` vor Save (Z. 525–559); kein `ClearPendingChanges` im Erfolgs-Save |
| 5 | `FinanceManager.Web/ViewModels/Securities/Categories/SecurityCategoryCardViewModel.cs` | Z. 207–211 (Save 209, Delete 210) | `Model` (`EditModel`, Z. 33/256–263) | `BuildCardRecord(Model.Name, Model.SymbolAttachmentId)` (Z. 95–103) | Gleiche Vorab-Mutation wie #3 (Z. 111–117) → persistierter Stand separat halten |
| 6 | `FinanceManager.Web/ViewModels/Budget/BudgetPurposeCardViewModel.cs` | Z. 458–462 (Save 460, Delete 461) | `Purpose` (`BudgetPurposeDto`, Z. 34) | `BuildCardRecord(Purpose)` (Z. 235–272) | SourceType/SourceId-Abhängigkeit (`ValidateFieldValue` Z. 493–522); Fire-and-forget-Lookups im Build (Z. 268–269) |
| 7 | `FinanceManager.Web/ViewModels/Budget/BudgetRuleCardViewModel.cs` | Z. 392–396 (Save 394, Delete 395) | `Rule` (`BudgetRuleDto`, Z. 40) | `BuildCardRecord(Rule)` (Z. 128–148) | Interval-abhängiges Feld (`ApplyIntervalDependentState` Z. 513–529); Neuanlage-Prefill via `InitPrefill` `"purpose:"/`"category:"` (Z. 55–79) |
| 8 | `FinanceManager.Web/ViewModels/Mcp/McpAccessCardViewModel.cs` | `manage`-Liste Z. 744–749 (Save 746 `Disabled = Access is null`), konditional RotateToken/Activate/Deactivate/Revoke (Z. 751–777) | `Access` (`McpAccessDetailDto`, Z. 39) | `BuildCardRecord(Access)` (Z. 672–715) | Kein Neuanlage-Modus (Z. 91–96); Konstruktor `(IServiceProvider, IApiClient)` (Z. 30–33); Scope-Matrix sofort persistiert (`SaveScopesAsync` Z. 276–309, `SaveScopeFiltersAsync` Z. 320–339, `SaveApprovalGrantsAsync` Z. 351–394) → Reset nur Kartenfelder |

### 3.3 Ausgeschlossene ViewModels (verifiziert, keine Änderung)

- `UserCardViewModel` (`FinanceManager.Web/ViewModels/Setup/UserCardViewModel.cs`): Save bei bestehenden Benutzern deaktiviert (Z. 371), Felder nur in Neuanlage editierbar (Z. 342–344) — Ausschluss begründet.
- `ContactGroupCardViewModel` (`.../Contacts/Groups/`): Manage = Save + Delete (Z. 213–217).
- `BudgetCategoryCardViewModel` (`.../Budget/`): Manage = Save + Delete (Z. 125–128).
- `StatementDraftCardViewModel` / `StatementDraftEntryCardViewModel` (`.../StatementDrafts/`): eigener Entwurfsworkflow (QuickEdit-Tab Z. 372–397 bzw. `ResetDuplicate` Z. 1099–1101) — keine Pending-Reset-Aktion.
- Keine Änderung an `Ribbon_Reset`-Ressourcen, kein neues Icon, keine Ribbon-Infrastruktur-Änderung, kein Server-Reload.

### 3.4 Basisklassen und Ribbon-Infrastruktur (Wiederverwendung, keine Änderung)

- `BaseCardViewModel` (`FinanceManager.Web/ViewModels/Common/BaseCardViewModel.cs`): `_pendingFieldValues` (Z. 75), `HasPendingChanges` (Z. 93), `ClearPendingChanges()` (Z. 134, protected — je VM direkt nutzbar), `ApplyPendingValues` (Z. 143–188), `InitPrefill` (Z. 41).
- `BaseViewModel` (`.../Common/BaseViewModel.cs`): `GetRibbonRegisterDefinition`-Override-Punkt (Z. 711), `GetRibbonRegisters` (Z. 688), `RaiseStateChanged` (Z. 277), `ApplyEnumTranslations` (Z. 617).
- `RibbonModels.cs`: `UiRibbonAction` (Z. 42–76, inkl. `MobileShortcut` Z. 68, `Hidden` Z. 75), `UiRibbonTab` mit `Sort` (Z. 85), `UiRibbonRegister` (Z. 93).
- `Ribbon.razor`: rendert **alle** `MobileShortcut && !Disabled`-Items als Gruppen-Shortcuts (Z. 95); Auto-Vergabe nur bei Ein-Item-Gruppen (Z. 224–227).

### 3.5 Ressourcen/Icons — bereits vorhanden

- `Ribbon_Reset` in `FinanceManager.Web/Resources/Pages.resx` (~Z. 306, „Reset"), `Pages.de.resx` (~Z. 306, „Zurücksetzen"), `Pages.en.resx` (~Z. 306, „Reset").
- `icons/sprite.svg#undo` in `FinanceManager.Web/wwwroot/icons/sprite.svg` (Z. 41); relative Referenz, regelkonform.
- Keine neuen Strings/Icons/API-Endpunkte/Entities/Migrationen; keine Backup-Auswirkungen.

### 3.6 Tests

- Vorbild: `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs` (DI-Setup mit `Mock<IApiClient>` + `ICurrentUserService`-Double + `DummyLocalizer`, `FindRibbonAction`-Helper Z. 324–331, Reflection-Aufruf privater Methoden Z. 121–123, Callback-Invokation Z. 310–311).
- Bestehende Ribbon-Shape-Tests, die zu prüfen/erweitern sind: `RibbonConfigTests.cs` (u. a. `SecurityCardViewModel` Z. 269–289 — asserted nur Linked-Items, nicht Manage), `SecurityCategoryDetailViewModelTests.cs` (Save-Disabled-Test Z. 197–227), `SavingsPlanCategoryDetailViewModelTests.cs` (Smoke Z. 75–80), `SavingsPlanCardLinkTests.cs` (Tab-Sequenz Z. 333–352), `SecurityCardViewModelTests.cs`, `ContactCardViewModelTests.cs`, `McpAccessCardViewModelTests.cs` (spezielles CreateVm mit Konstruktor-`IApiClient`, Z. 30–45). Kein bestehender Test enumeriert die Manage-Item-Liste exakt → kein erwarteter Bruch durch das neue Item.
- `SetupCardViewModelTests.cs` assertiert genau eine `Reset`-Aktion im Setup-Aggregat (Z. 208–218) — scope-fremd.

## 4. Projektregeln, die die Umsetzung berühren

- Ribbon-Gruppenreihenfolge: Navigation (Sort 0) → Manage → größere Aktionen → Linked zuletzt; `Reset` gehört in `Ribbon_Group_Manage` direkt hinter `Save`.
- `MobileShortcut` gehört der primären Aktion (`Save`); requirement-Präferenz: `Reset` ohne `MobileShortcut` (Referenz weicht ab — Entscheidung in Planung).
- Aktionen pro Eintrag nur auf Detailseiten — erfüllt.
- resx de+en — bereits vorhanden; keine hartkodierten Strings neu.
- Keine root-absoluten Pfade — Icon-String relativ.
- Keine neuen Enum-Mitglieder, keine EF-Migration, kein Backup-Schema-Eingriff.
- Verbindliche Nebenpflicht: Folge-Issue in `martin-stromberg/FinanceManager` zum Aufräumen der KI-Zugangsansicht (inkonsistente Persistenz: Kartenfelder pending vs. Scope-Matrix/Grants sofort), Verweis auf #593.

## 5. Auffälligkeiten/Risiken für die Umsetzung (aus dem Code)

1. **Separater persistierter Stand** bei `SavingsPlanCategoryCardViewModel` und `SecurityCategoryCardViewModel`: `SaveAsync` mutiert `Name`/`SymbolId` bzw. `Model` vor dem API-Aufruf ohne Rollback → Baseline-Speicher nötig, damit Reset nach fehlgeschlagenem Speichern den persistierten Stand wiederherstellt.
2. **`SavingsPlanCardViewModel`**: `Model` ist kein Baseline-Träger (laufende Mutation); Reset muss `Model` zusätzlich aus `_loadedDto` (Edit) bzw. aus dem initialen `SavingsPlanCreateRequest` (Neuanlage, `InitPrefill` noch verfügbar) ableiten.
3. **`SecurityCardViewModel`**: `Model`-Fallback-Felder können nach fehlgeschlagenem Save verworfene Werte liefern (`ApplyPendingValuesToModel` ohne Rollback); `Model` beim Reset ggf. mit zurücksetzen.
4. **Lookup-Nebenabrufe beim Neuaufbau**: `ContactCardViewModel` (Z. 147), `BudgetPurposeCardViewModel` (Z. 268–269), Referenz `BankAccountCardViewModel` (Z. 184) — kein Datensatz-Reload, aber API-Calls; „kein Server-Neuladen" betrifft den Datensatz selbst.
5. **`McpAccessCardViewModel`**: `Save`-Disabled ist `Access is null` (nicht pending-basiert); Reset-Disabled nach Referenzmuster `!HasPendingChanges` (ggf. mit `Access is null ||` kombinieren). `BuildCardRecord` benötigt `IStringLocalizer<Pages>` im ServiceProvider (Test-Setup beachten).
6. **Disabled-Auswertung ist zeitpunktbezogen**: `Disabled` wird beim Ribbon-Aufbau berechnet; `RaiseStateChanged()` nach dem Reset stellt Aktualisierung sicher (Tabs werden pro Render neu gebaut, `Ribbon.razor` Z. 164–170).
