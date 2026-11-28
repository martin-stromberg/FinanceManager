# Detailinventar: Tests, Ressourcen/Icons, ausgeschlossene ViewModels, Projektregeln

## 1. Test-Infrastruktur und Vorbilder

### Vorbild `AccountDetailViewModelTests`

**Datei:** `FinanceManager.Tests/ViewModels/AccountDetailViewModelTests.cs` (332 Zeilen)

- DI-Setup `CreateVm()` (Z. 41–53): `ServiceCollection` mit `ICurrentUserService`-Testdouble (Z. 17–23), `Mock<IApiClient>` (Moq), `IStringLocalizer<Pages>` als `DummyLocalizerGeneric<Pages>` (Z. 33–39). VMs werden direkt mit dem `ServiceProvider` instanziiert (kein DI-Container-Resolve nötig, da VM-Konstruktoren `IServiceProvider` erwarten — Ausnahme `McpAccessCardViewModel`, s. u.).
- `DummyLocalizer`/`DummyLocalizerGeneric<T>` (Z. 25–39): Echo-Localizer (Key = Value).
- Ribbon-Assertions: `vm.GetRibbonRegisters(loc)` bzw. `vm.GetRibbon(loc)`; Helper `FindRibbonAction(vm, loc, actionId)` (Z. 324–331) flacht Register → Tabs → Items ab und selektiert per `Id`.
- Gruppenreihenfolge-Assertion mit exakter Tab-Titel-Liste (Z. 211–233, `GetRibbonRegisters_GroupOrder_PlacesLinkedLast`).
- Private Methoden werden per Reflection aufgerufen (z. B. `SavePendingAsync`, Z. 121–123) — Muster auch für `ResetPendingAsync` nutzbar; alternativ kann der Ribbon-`Callback` der `"Reset"`-Aktion direkt invokiert werden (`action.Callback!()`-Muster in Z. 310–311).
- `UiActionRequested`-Event-Capture (Z. 306–321) zum Assert von UI-Aktionen.

### Weitere relevante Testdateien

| Datei | Relevanz für dieses Issue |
|---|---|
| `FinanceManager.Tests/ViewModels/RibbonConfigTests.cs` | Assertiert Gruppentitel/-reihenfolge und Item-IDs für `SecurityCardViewModel` (Z. 269–289: `Ribbon_Group_Linked`-Items `Postings`/`Attachments`/`Prices` exakt; Manage-Items werden **nicht** enumeriert → Reset-Ergänzung bricht den Test nicht). `SetupCard_AdminRibbonGroups_FollowSortConvention` (Z. 300–322) betrifft nur Setup. |
| `FinanceManager.Tests/ViewModels/SecurityCategoryDetailViewModelTests.cs` | `Ribbon_Disables_Save_When_Name_Short` (Z. 197–227): assertiert `groups.Count == 2` (nav+manage) und findet `Save` per `First(i => i.Action == "Save")` — zusätzliche `Reset`-Aktion bricht den Test nicht; gute Erweiterungsstelle für Reset-Assertions (Disabled-Umschalten über Pending-Edit). |
| `FinanceManager.Tests/ViewModels/SavingsPlanCategoryDetailViewModelTests.cs` | Nur Smoke-Test `Ribbon_Has_Actions` (Z. 75–80: `GetRibbonRegisters` nicht null). Erweiterbar. |
| `FinanceManager.Tests/ViewModels/SavingsPlanCardLinkTests.cs` | `GetRibbonRegisters_GroupOrder_PlacesLinkedLast` (Z. 333–352) assertiert exakte Tab-Titel-Sequenz `Navigation, Manage, Analysis, Bookings, Linked` — **Aktionsebene unberührt**, Reset-Insert in Manage ist unproblematisch. `FindActualReportAction`-Helper (Z. 79–82) als Flatten-Muster. |
| `FinanceManager.Tests/Web/ViewModels/Securities/SecurityCardViewModelTests.cs` | `GetRibbonRegisters_ShouldNotExposeImportPricesAction`, `PricesAction_ShouldRaiseOpenPricesNavigation_WhenIdIsValid` — `GetActions`/`GetAction`-Helper (Z. 74–81); DI-Setup mit `PassthroughLocalizer<>` (Z. 63–72). |
| `FinanceManager.Tests/Web/ViewModels/Contacts/ContactCardViewModelTests.cs` | Testet nur das EmbeddedPanel-Request beim Laden (Z. 73–92); DI-Setup `CreateVm` (Z. 57–65). |
| `FinanceManager.Tests/ViewModels/McpAccessCardViewModelTests.cs` | `CreateVm` (Z. 30–45): `IApiClient` wird **zusätzlich zum ServiceProvider direkt in den Konstruktor** übergeben (`new McpAccessCardViewModel(sp, api.Object)`); `IConfirmationService`-Mock wird registriert; `IStringLocalizer<>` generisch offen registriert (`typeof(IStringLocalizer<>), typeof(DummyGenericLocalizer<>)` — nötig, weil `BuildCardRecord` `IStringLocalizer<Pages>` via `GetRequiredService` auflöst, `McpAccessCardViewModel` Z. 674). |
| `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs` | Assertiert genau eine globale `Reset`-Aktion im aggregierten Setup-Ribbon (Z. 208–218: `Count(id => id == "Reset") == 1`) — bezieht sich auf `SetupCardViewModel.ResetAll`, keine Überschneidung mit den Karten-Resets. |
| `FinanceManager.Tests/Components/RibbonTests.cs` | bUnit-Tests der `MobileShortcut`-Semantik (Z. 321–581): u. a. „alle `MobileShortcut && !Disabled`-Items rendern als Header-Shortcuts", „disabled Items werden nicht gerendert", automatische Vergabe bei Ein-Item-Gruppen. |
| `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs` | Scope-fremd (Entwurfs-Workflow); keine Anpassung erwartet. |

### Test-Double-Bedarf je ViewModel

- Alle VMs: `ICurrentUserService`, `Mock<IApiClient>`, `IStringLocalizer<Pages>` (bzw. offenes `IStringLocalizer<>`); optional `NavigationManager`-Testdouble für VMs, die `TryGetParentLinkFromQuery`/`Navigation` nutzen (Muster `TestNavigationManager` in `RibbonConfigTests` Z. 35–46 — `BudgetPurposeCardViewModel`/`SavingsPlanCardViewModel`/`SecurityCardViewModel`/`ContactCardViewModel` rufen `TryGetParentLinkFromQuery()` nur im Save-Pfad auf, nicht beim Reset).
- `McpAccessCardViewModel`: zusätzlich `IApiClient` im Konstruktor + `IConfirmationService` empfohlen.
- `BudgetPurposeCardViewModel`: `EmbeddedList` wird in `LoadAsync` angelegt (`BudgetRuleListViewModel` benötigt ebenfalls DI-Fähigkeit aus dem gleichen ServiceProvider, Z. 50/61/79).

## 2. Ressourcen und Icons (bereits vorhanden, keine Änderung nötig)

| Artefakt | Fundstelle | Wert |
|---|---|---|
| `Ribbon_Reset` (Fallback) | `FinanceManager.Web/Resources/Pages.resx` ~Z. 306 | `Reset` |
| `Ribbon_Reset` (de) | `FinanceManager.Web/Resources/Pages.de.resx` ~Z. 306 | `Zurücksetzen` |
| `Ribbon_Reset` (en) | `FinanceManager.Web/Resources/Pages.en.resx` ~Z. 306 | `Reset` |
| `Ribbon_Group_Manage`, `Ribbon_Save`, `Ribbon_Back`, `Ribbon_Group_Navigation` u. a. | resx-Triple, bereits in allen betroffenen VMs in Verwendung | vorhanden |
| Icon `undo` | `FinanceManager.Web/wwwroot/icons/sprite.svg` Z. 41 (`<symbol id="undo" ...>`) | vorhanden; Verwendung `href='icons/sprite.svg#undo'` (relativ, regelkonform — kein Root-Pfad) |
| `Ribbon_ResetDuplicate` | `Pages.*.resx` ~Z. 1272 | gehört zum ausgeschlossenen Entwurfs-Workflow |

Keine neuen resx-Keys, keine neuen Icons, keine API-Endpunkte, keine Entities/Migrationen, keine
Backup-/Restore-Auswirkungen (keine persistenten Modelländerungen → `EnumCoverageTests`,
Backup-Tests und `RazorRootAbsolutePathTests` unberührt).

## 3. Ausgeschlossene ViewModels (verbindlich, Verifikation am Code)

| ViewModel | Datei | Befund | Begründung der Anforderung |
|---|---|---|---|
| `UserCardViewModel` | `FinanceManager.Web/ViewModels/Setup/UserCardViewModel.cs` | Manage-Liste Z. 369–373: `Save` `Disabled = User != null && User.Id != Guid.Empty` (Z. 371) — Save nur in Neuanlage aktiv; Felder nur bei `UserId == Guid.Empty` editierbar (Z. 342–344). Kein Reset vorhanden. | „Zurücksetzen" nicht benötigt (ausdrücklich ausgenommen). |
| `ContactGroupCardViewModel` | `FinanceManager.Web/ViewModels/Contacts/Groups/ContactGroupCardViewModel.cs` | Manage-Tab Z. 213–217: nur `Save` + `Delete`; `ClearPendingChanges` in Load/Save (Z. 82/148). Kein Reset. | Bei Issue-Analyse „in Ordnung" bewertet. |
| `BudgetCategoryCardViewModel` | `FinanceManager.Web/ViewModels/Budget/BudgetCategoryCardViewModel.cs` | Manage-Tab Z. 125–128: nur `Save` + `Delete`. Kein Reset. | Bei Issue-Analyse „in Ordnung" bewertet. |
| `StatementDraftCardViewModel` | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` | Manage-Tab Z. 359–369: `Save`, `Add`, `Book`, `DeleteDraft`, `Classify`, `Validate`; eigener `Ribbon_Group_QuickEdit`-Tab (Z. 372–397) mit `SaveQuickEdit`/`CancelQuickEdit`. Kein `Reset`. | Eigener Entwurfsworkflow (QuickEdit-Abbrechen deckt Verwerfen ab). |
| `StatementDraftEntryCardViewModel` | `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftEntryCardViewModel.cs` | `ResetDuplicate`-Aktion (Z. 1099–1101, `Ribbon_ResetDuplicate`, `reset`-Icon) → `ResetDuplicateAsync` (Z. 1250 ff., Server-Call `StatementDrafts_ResetDuplicateEntryAsync`). Semantisch anderer Reset (Statusrücksetzung, nicht Pending-Verwerfen). | Eigener Entwurfsworkflow; `Ribbon_ResetDuplicate` unverändert. |

## 4. Relevante Projektregeln (AGENT.md / globale Regeln)

- **Ribbon-Gruppierung:** `Navigation`-Gruppe stets Sort 0, danach (1) `Ribbon_Group_Manage` (Aktionen an der Entität: Neu/Speichern/Löschen/Archivieren — `Reset` gehört fachlich hierhin, direkt hinter `Save`), (2) größere Aktionen (`Ribbon_Group_Actions`/`_Bookings`/`_Analysis`/`_Fields`), (3) `Ribbon_Group_Linked` zuletzt. Alle acht VMs folgen dem Muster bereits (Details: in-scope-viewmodels.md).
- **`MobileShortcut`:** Projektregel „die primäre Aktion erhält `MobileShortcut = true`" → `Save` ist primär; Präferenz der requirement.md: `Reset` **ohne** `MobileShortcut` (die Referenz in `BankAccountCardViewModel` Z. 291 weicht davon ab und setzt `true` — Entscheidung konsistent in der Planung zu treffen; Rendering-Effekt siehe reference-and-basetypes.md §5).
- **Aktionen pro Eintrag nur auf Detailseiten** — betroffen sind ausschließlich Karten-Detailansichten: konform.
- **Ressourcen:** de + en in `Resources/*.resx` — `Ribbon_Reset` ist in `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` vorhanden; keine neuen Strings nötig.
- **Keine root-absoluten Pfade:** Icon-Referenz `icons/sprite.svg#undo` ist relativ — konform; Guard-Test `RazorRootAbsolutePathTests` betrifft `*.razor`, die VM-Strings folgen demselben Muster wie Bestandscode.
- **Keine neuen Enum-Mitglieder** → `EnumCoverageTests` unberührt.
- **Keine EF-Migration / kein Backup-Schema** → keine Migrations-/Backup-Arbeit nötig.
- **Sofort-Persistierung MCP:** akzeptiertes Inkonsistenz-Verhalten; **verbindliche Nebenpflicht:** Folge-Issue in `martin-stromberg/FinanceManager` zum Aufräumen der KI-Zugangsansicht (Kartenfelder pending vs. Scope-Matrix/Grants sofort persistiert, `Mcp_SetScopesAsync`/`Mcp_SetFiltersAsync`/`Mcp_SetApprovalGrantsAsync`) mit Verweis auf #593 — z. B. via `gh issue create`.

## 5. Offene Punkte aus der requirement.md — faktische Einordnung

1. **`MobileShortcut` auf Reset:** Referenz setzt `true`; Projektregel + requirement-Präferenz sprechen für ungesetzt. Rendering-Unterschied dokumentiert (reference-and-basetypes.md §5).
2. **Separater persistierter Stand für `SavingsPlanCategoryCardViewModel`/`SecurityCategoryCardViewModel`:** durch Code belegt erforderlich — `SaveAsync` mutiert `Name`/`SymbolId` bzw. `Model` vor dem API-Aufruf, Fehlschlag hinterlässt ungespeicherte Modellwerte (in-scope-viewmodels.md §3/§5). `SecurityCardViewModel` und `SavingsPlanCardViewModel` mutieren `Model` ebenfalls vor/während Save (§2/§4); dort existieren mit `Security`/`_loadedDto` bereits DTO-Baselines — für `Model`-Fallback-Felder kann eine Rücksetzung von `Model` nötig sein.
3. **Lookup-Nebenabrufe beim Reset:** `ContactCardViewModel.BuildCardRecordAsync` (`ContactCategories_ListAsync`, Z. 147) und `BudgetPurposeCardViewModel.BuildCardRecord` (Fire-and-forget `TryResolveAndSet*`, Z. 268–269) lösen Lookup-Calls aus — kein Datensatz-Reload; die Referenz macht dasselbe (`Contacts_GetAsync`, `BankAccountCardViewModel` Z. 184). Vermeidbar z. B. durch Kategorien-Cache; Entscheidung in der Planung.
4. **Folge-Issue-Titel/-Beschreibung:** bei Umsetzung zu formulieren (Inhalt: inkonsistente Persistenz der KI-Zugangsansicht, Reset-Umfang, Konsistenz).
