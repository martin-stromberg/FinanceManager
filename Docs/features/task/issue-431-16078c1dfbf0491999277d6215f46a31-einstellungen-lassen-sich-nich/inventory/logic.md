# Logik / ViewModels

## `SetupCardViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs`

| Methode / Member | Sichtbarkeit | Kurzbeschreibung |
|------------------|-------------|------------------|
| `SectionDefinitions` | private static | Registry aller Setup-Sektionen (Key, Lokaliserungskey, VM-Typ, Komponententyp): profile, notifications, statements, attachments, backup, update, security, securitytxt, wellknown, returnanalysis |
| `HasPendingChanges` (new) | public | Aggregiert `Dirty` der Sektions-VMs: profile, notifications, statements, update, returnanalysis, securitytxt, wellknown. Steuert Disabled-Zustand der Ribbon-Aktionen „Save"/„Reset" |
| `CreateSectionViewModel(key, services)` | public | Liefert gecachtes Sektions-VM aus `_sectionViewModels` oder erzeugt es per `ActivatorUtilities.CreateInstance`. **Verdrahtet `StateChanged` nicht an die Karte** (im Gegensatz zu `CreateSubViewModel`) |
| `LoadAsync(id)` | public override | Erstellt Kern-Sektions-VMs via `CreateSubViewModel<T>`: profile, notifications, backup, statements, update (nur Admin), securitytxt, wellknown. **`returnanalysis` wird nicht vorerzeugt** |
| `SaveAllAsync(ct)` | public | Ruft `SaveAsync` aller Sektions-VMs mit `Dirty == true` auf (inkl. returnanalysis) |
| `ResetAll()` | public | Ruft `Reset()` der Sektions-VMs auf (inkl. returnanalysis) |
| `GetRibbonRegisterDefinition` | protected override | Ribbon „Save" (`Saving \|\| !HasPendingChanges` deaktiviert), „Reset", „RebuildAggregates", „ResetReportCache" |

Abonnierte Events: `StateChanged` der via `CreateSubViewModel` erzeugten Kinder (durch `BaseViewModel.CreateSubViewModel` verdrahtet).
Publizierte Events: `StateChanged`, `ExpandSectionRequested`.

## `SetupStatementsViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadAsync` | public | Lädt `ImportSplitSettingsDto`, setzt `_original = Clone(Model)`, `RecomputeDirty()`, `Validate()` |
| `SaveAsync` | public | `Validate()` → API-Update; bei Erfolg `_original = Clone(Model)`, `RecomputeDirty()` |
| `Reset` | public | Stellt alle sechs Felder aus `_original` wieder her, `Validate()`, `RecomputeDirty()`, `RaiseStateChanged()` |
| `OnModeChanged` | public | Passt `MonthlySplitThreshold` an, `Validate()`, `RecomputeDirty()`, `RaiseStateChanged()` |
| `Validate` | public | Validierung + `RecomputeDirty()` — **kein `RaiseStateChanged()`** |
| `RecomputeDirty` | private | Vergleicht alle sechs Felder (`Mode`, `MaxEntriesPerDraft`, `MonthlySplitThreshold` (mit `?? 0`), `MinEntriesPerDraft`, `MassImportDialogPolicy`, `KnownContactAutoCreateEnabled`) gegen `_original` |
| `Dirty` | public | `private set` |

## `SetupReturnAnalysisViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupReturnAnalysisViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadAsync` | public | Lädt `AvailableSecurities` + `ReturnAnalysisSettingsResponse` (`_currentSettings`), `RecomputeDirty()` |
| `SaveAsync` | public | Validierung (`RiskFreeRate >= 0`), API-Update, `_currentSettings` neu setzen, `RecomputeDirty()` |
| `Reset` | public | Stellt Felder aus `_currentSettings` wieder her, `RecomputeDirty()`, `RaiseStateChanged()` |
| `OnChanged` | public | `RecomputeDirty()` + `RaiseStateChanged()` |
| `RecomputeDirty` | private | Vergleicht `SelectedBenchmarkSecurityId`, `ShowSharpeRatio`, `RiskFreeRate` gegen `_currentSettings` |
| `Dirty` | public | `private set` |

## `BaseViewModel.CreateSubViewModel<T>`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeilen 346–365)

Erzeugt Child-VM per `ActivatorUtilities.CreateInstance<T>` und verdrahtet `vm.StateChanged += (_, __) => RaiseStateChanged();` — so propagiert jede Zustandsänderung des Kindes zum Eltern-VM (Setup-Card → Ribbon). Registriert das Kind in `_children`, `_childViewModels` und `_singletonChildViewModels`.

## UI-Komponenten

### `SetupSections.razor`
Datei: `FinanceManager.Web/Components/Pages/SetupSections.razor`

Rendert Accordion-Sektionen; beim Expandieren wird per `Provider.CreateSectionViewModel(key, Services)` das Sektions-VM erzeugt/aufgelöst und als `ViewModel`-Parameter an die `DynamicComponent` (Tab) übergeben.

### `SetupStatementTab.razor`
Datei: `FinanceManager.Web/Components/Pages/Setup/SetupStatementTab.razor`

| Feld | Binding | Nach-Bind-Aktion |
|------|---------|------------------|
| `Mode` (select) | `@bind` | `@bind:after` → `_vm.OnModeChanged()` |
| `MaxEntriesPerDraft` (number) | `@bind` | `@bind:after` → `_vm.Validate()` |
| `MonthlySplitThreshold` (number) | `@bind` | `@bind:after` → `_vm.Validate()` |
| `MinEntriesPerDraft` (number) | `@bind` | `@bind:after` → `_vm.Validate()` |
| `MassImportDialogPolicy` (select) | `@bind` | **keine** |
| `KnownContactAutoCreateEnabled` (checkbox) | `@bind` | **keine** |

### `SetupReturnAnalysisTab.razor`
Datei: `FinanceManager.Web/Components/Pages/Setup/SetupReturnAnalysisTab.razor`

| Feld | Binding | Nach-Bind-Aktion |
|------|---------|------------------|
| `benchmark-select` (select) | `value` + `@onchange="OnBenchmarkChanged"` | setzt `SelectedBenchmarkSecurityId`, `_vm.OnChanged()` |
| `show-sharpe` (checkbox) | `@bind` | `@bind:after` → `_vm.OnChanged()` |
| `risk-free` (number) | `@bind` | `@bind:after` → `_vm.OnChanged()` |

### Vergleichbare Sektionen (funktionieren)
`SetupProfileTab.razor`, `SetupNotificationsTab.razor`, `SecurityTxtSettingsTab.razor`, `WellKnownSettingsTab.razor`, `SetupUpdateTab.razor`: Jedes Eingabefeld ruft nach Änderung `OnChanged()`/Äquivalent mit `RecomputeDirty()` **und** `RaiseStateChanged()` auf; die VMs werden via `CreateSubViewModel` vorerzeugt bzw. (Update) als Child registriert.

## Festgestellte Defekte (Bestandsaufnahme)

1. `SetupStatementTab.razor`: `MassImportDialogPolicy` und `KnownContactAutoCreateEnabled` haben kein `@bind:after` → `RecomputeDirty()` wird bei Änderung nie aufgerufen → `Dirty` bleibt `false`.
2. `SetupStatementsViewModel.Validate()` ruft `RecomputeDirty()` ohne `RaiseStateChanged()` auf → bei Änderung von `MaxEntriesPerDraft`, `MonthlySplitThreshold`, `MinEntriesPerDraft` wird `Dirty` zwar gesetzt, aber die Setup-Card erfährt nichts davon → Ribbon „Save" bleibt deaktiviert.
3. `SetupCardViewModel.CreateSectionViewModel`: Lazy erzeugte Sektions-VMs (`returnanalysis`, `attachments`, `security`) bekommen keine `StateChanged`-Weiterleitung → `Dirty`-Änderungen der Renditeanalyse-Sektion lösen kein `RaiseStateChanged` der Karte aus → Ribbon „Save" bleibt deaktiviert.
