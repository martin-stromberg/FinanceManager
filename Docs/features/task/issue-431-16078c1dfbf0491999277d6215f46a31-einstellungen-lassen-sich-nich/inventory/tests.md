# Tests – Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-10-18T07:25:59Z (UTC)
- Branch und Commit-ID: `task/issue-431-16078c1dfbf0491999277d6215f46a31-einstellungen-lassen-sich-nich`, Commit `e1439588d71a5cf702f15915f757ece582ed9aca`
- Uncommittete Änderungen im getesteten Stand: nur untracked Datei `nul` (Windows-Artefakt) sowie die neu angelegten Lifecycle-Artefakte unter `Docs/features/task/issue-431-…/` — keine Produktiv-/Testcode-Änderungen
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.402, Ziel-Framework `net10.0`, xUnit v3, bUnit 2.11.3, Moq 4.21.0, FluentAssertions 8.11.0, Microsoft.Playwright 1.63.0
- Ermittelte Testsuiten und Quellen der Testbefehle: `CI-CD.md` (Schritte 7–9) nennt `FinanceManager.Tests`, `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E` (E2E mit `continue-on-error` in CI). Das Verzeichnis `FinanceManager.Tests.Integration.ApiClient` enthält keine `.csproj` (nur eine lose `.cs`-Datei) und ist keine eigenständige Suite.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --nologo` | Repo-Root | 0 | 1389 | 0 | 0 | [Log](test-results/unittests-baseline.log) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --nologo` | Repo-Root | 0 | 134 | 0 | 0 | [Log](test-results/integration-baseline.log) |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --nologo` | Repo-Root | siehe Log | siehe Log | siehe Log | siehe Log | [Log](test-results/e2e-baseline.log) |

### Nachgewiesene bestehende Testfehler

Keine — der Unit- und der Integrationstest-Lauf waren vollständig erfolgreich. E2E-Ergebnis siehe `e2e-baseline.log`.

### Testlücken und Ausführungsprobleme

- `FinanceManager.Tests.Integration.ApiClient` ist nicht ausführbar: Das Verzeichnis enthält keine `.csproj`; `dotnet test` auf den Ordner schlägt mit Handshake-Fehlern fehl (Exit-Code 5). Nachweis: `test-results/apiclient-baseline.log`.
- Es existieren keine Tests, die die Dirty-Erkennung der Setup-Sektionen in Kombination mit der Ribbon-Aktivierung („Speichern") pro Eingabefeld abdecken.

## Testklassen

### `SetupCardViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupCardViewModelTests.cs`
- `GetEmbeddedPanelSpecs_Returns_SetupPanel_AfterRibbon` — Embedded-Panel-Spezifikation
- `TryGetSectionComponentType_Profile_ReturnsExpectedComponent` — Sektions-Registry
- `CreateSectionViewModel_Profile_CreatesExpectedViewModel` — VM-Erzeugung
- `LoadAsync_HidesUpdateSectionForNonAdmin` / `LoadAsync_ShowsUpdateSectionForAdmin` — Admin-Gating
- `GetRibbonRegisters_AfterLoad_IncludesBackupSectionActions` / `..._IncludesUpdateActions` — Ribbon-Aggregation
- `GetRibbonRegisters_AfterLoad_ExposesSingleGlobalSaveAndReset` — genau ein globaler Save/Reset
- `CreateSectionViewModel_AfterLoad_ReturnsCachedInstance` — Caching
- `GlobalSave_InvokesDirtySetupSectionSaves` — Save-Fanout auf alle dirty Sektionen (setzt `Dirty` direkt per Methoden-Aufruf, prüft nicht die UI-Erkennung)

### `SetupImportSplitViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/SetupImportSplitViewModelTests.cs`
- Tests für `SetupStatementsViewModel`: Load/Save/Validate/Dirty-Logik der Import-Split-Einstellungen

### `SetupSectionsTests` (bUnit)
Datei: `FinanceManager.Tests/Components/SetupSectionsTests.cs`
- `UploadBackup_RaisesUploadRequested_AfterSectionExpand` — rendert `SetupSections` + `Ribbon<string>`, klickt Ribbon-Button

## Hilfsmethoden

### `SetupCardViewModelTests`
- `BuildServices(...)` — ServiceProvider mit `TestCurrentUserService`, `IKpiLocalStorageCache`-Mock, `IApiClient`-Mock
- `GetAllActionIds(vm)` / `GetAllActionIdsAfterLoad()` — extrahiert Ribbon-Action-Ids über `GetRibbonRegisters(localizerMock)`

### `SetupSectionsTests`
- `TestCurrentUserService` — `ICurrentUserService`-Stub
- `PagesStringLocalizer` — Real-Localizer für `Pages`-Ressourcen
