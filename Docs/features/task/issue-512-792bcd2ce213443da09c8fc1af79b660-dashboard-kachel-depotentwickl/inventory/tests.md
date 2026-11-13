# Tests – Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** Läufe am 2026-11-13 zwischen 21:43 UTC und 22:10 UTC (22:43–23:10 +0100, lokale Systemzeit des Rechners `DESKTOP-CM8OBSG`; Systemuhr steht auf Jahr 2026). Dokumentation erstellt 2026-11-13, 23:10 +0100.
- **Branch und Commit-ID:** `task/issue-512-792bcd2ce213443da09c8fc1af79b660-dashboard-kachel-depotentwickl` @ `1a4417cfe30487b1f25aeb5434b5eebddd0ea78e` — identisch mit `staging`-Tip (keine Branch-eigenen Commits).
- **Uncommittete Änderungen im getesteten Stand:** Keine Änderungen an Produktiv- oder Testcode. Vorhandene SCM-/Lifecycle-Arbeitsdateien: `Docs/features/task/issue-512-…-dashboard-kachel-depotentwickl/` (untracked, u. a. `requirement.md` und diese Bestandsaufnahme inkl. `test-results/`), `issue.md` (git-ignoriert, Issue-Text #512), `task.json` (git-ignoriert; veralteter Inhalt aus früherer Aufgabe #141), `.task-init` (git-ignoriert).
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (`windows`-lokal, `C:\Program Files\dotnet`), .NET SDK `10.0.403` (`dotnet --version`; `global.json` aktiviert Microsoft.Testing.Platform; CI nutzt `10.0.x`), xunit.v3 4.0.2 über MTP (`dotnet test … -- <mtp-args>`), Playwright-Browser lokal installiert (`~/AppData/Local/ms-playwright`, u. a. `chromium-1248`; Projekt nutzt `Microsoft.Playwright` 1.64.1).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** `.github/workflows/pr-staging-ci.yml` (Job `build-and-test`, identisch in `staging-ci.yml`):
  - `FinanceManager.Tests` mit Filter `--filter-not-trait "Category=OsInterface"` + Coverage (Coverage-Flags lokal weggelassen, beeinflussen nicht das Testergebnis),
  - `FinanceManager.Tests.Integration` (ohne Filter),
  - `FinanceManager.Tests.E2E` (CI: `continue-on-error: true`, best effort).
  - `FinanceManager.Tests.Integration.ApiClient` existiert nur als Verzeichnis mit einer `.cs`-Datei, **ohne csproj** und nicht in `FinanceManager.sln`/CI referenziert → nicht ausführbar (siehe Testlücken).

### Vorhergehende Builds

| Befehl | Exit-Code | Anmerkung |
|--------|-----------|-----------|
| `dotnet restore FinanceManager.sln` | 0 | NU1510-Warnungen (bekannt, in CI `WarningsNotAsErrors`-Liste) |
| `dotnet build FinanceManager.Tests/FinanceManager.Tests.csproj --no-restore -c Debug` | 0 | 62 Warnungen, 0 Fehler |
| `dotnet build FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-restore -c Debug` | 0 | 6 Warnungen, 0 Fehler |
| `dotnet build FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-restore -c Debug` (1. Versuch) | 1 | **Infrastrukturfehler, kein Codefehler:** MSB3021/MSB3027 — DLLs des noch laufenden Integrations-Testprozesses (`FinanceManager.Tests.Integration`, PID 30784) gesperrt |
| `dotnet build FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-restore -c Debug` (2. Versuch, nach Ende des Integrationslaufs) | 0 | 5 Warnungen, 0 Fehler |

### Testläufe

Alle Läufe im Arbeitsverzeichnis `D:\Repositories\softwareschmiede\792bcd2c-e213-443d-a09c-8fc1af79b660` (Repo-Root). `--results-directory` zeigt auf `Docs/features/task/issue-512-…/inventory/test-results`.

| Lauf | Befehl inkl. Filter | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|-----------|-------------|----------------|--------------|----------|
| Regular (Unit + bUnit) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <test-results> -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-regular-tests.trx` | 0 | 2244 | 0 | 0 | [TRX](test-results/test-results-regular-tests.trx) / [Konsole](test-results/regular-tests-console.log) — 21:43:05–21:43:49 UTC, 46 s |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <test-results> -- --report-xunit-trx --report-xunit-trx-filename test-results-regular-integration.trx` | 0 | 172 | 0 | 0 | [TRX](test-results/test-results-regular-integration.trx) / [Konsole](test-results/regular-integration-console.log) — 21:43:56–21:49:47 UTC, 5 min 53 s |
| E2E (gefiltert: Portfolio) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <test-results> -- --filter "FullyQualifiedName~PortfolioAnalysisReportE2ETests" --report-xunit-trx --report-xunit-trx-filename test-results-e2e-portfolio.trx` | 0 | 4 | 0 | 0 | [TRX](test-results/test-results-e2e-portfolio.trx) / [Konsole](test-results/e2e-portfolio-console.log) — 21:50:13–21:50:31 UTC, 19 s |
| E2E (vollständig) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <test-results> -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-tests.trx` | 2 | 146 | 1 | 0 | [TRX](test-results/test-results-e2e-tests.trx) / [Konsole](test-results/e2e-tests-console.log) — 21:50:38–22:09:43 UTC, 19 min 06 s |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `FinanceManager.Tests.E2E.ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi` | `FinanceManager.Tests.E2E/Tests/Auth/ChangePasswordWellKnownPlaywrightTests.cs:108` (Fehlerstelle `Helpers/AuthGateway.cs:138` in `LogoutAsync`) | `Microsoft.Playwright.PlaywrightException: net::ERR_ABORTED at https://127.0.0.1:58511/login` — Navigation zur Login-Seite nach Logout abgebrochen (browserseitiger Navigationsabbruch; fachlich nicht portfolio-bezogen) | E2E-Volllauf, [TRX](test-results/test-results-e2e-tests.trx), [Konsole](test-results/e2e-tests-console.log) |

Hinweis: Alle vier `PortfolioAnalysisReportE2ETests` sind im E2E-Volllauf (und im separaten gefilterten Lauf) bestanden. Für `FinanceManager.Tests` und `FinanceManager.Tests.Integration` wurden **keine** Testfehler nachgewiesen (vollständige Läufe, Exit 0).

### Testlücken und Ausführungsprobleme

- **`Category=OsInterface`:** Im Regular-Lauf durch den CI-Filter `--filter-not-trait "Category=OsInterface"` ausgeschlossen; laut CI-Kommentar (pr-staging-ci.yml, Zeile ~145) ist aktuell kein Test mit diesem Trait markiert — effektiv kein ausgeschlossener Umfang.
- **`FinanceManager.Tests.Integration.ApiClient`:** Verzeichnis enthält nur `ApiClientBudgetReportUnbudgetedMirrorTests.cs`, keine `.csproj`, nicht in `FinanceManager.sln` und in keinem Workflow referenziert → Suite nicht ausführbar (Waisen-Datei).
- **E2E-Build (1. Versuch):** MSB3021/MSB3027-Sperrfehler durch parallel laufenden Integrations-Testprozess — Infrastrukturproblem, kein Code-/Testfehler; Wiederholung erfolgreich (siehe Build-Tabelle).
- **E2E-Volllauf Exit-Code 2:** einzige Ursache ist der oben dokumentierte Einzelfehlschlag (Navigation `ERR_ABORTED`); in der CI ist die E2E-Suite bewusst `continue-on-error` (best effort).
- Keine `nul`-Umleitungen o. ä. verwendet; Konsolenlogs der Läufe liegen als `*-console.log` bei.

## Testklassen (relevant für die Anforderung)

### `PortfolioAnalysisReportServiceTests`
Datei: `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportServiceTests.cs` — EF Core InMemory + echte `FifoCostBasisCalculator`/`ReturnCalculationService`-Instanzen.

- `GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure` — Marktwert/Investiertes Kapital/unrealisiertes Ergebnis, Top-Position
- `GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly` — Allokation inkl. „Ohne Kategorie"-Bucket
- `GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly` — `DividendsCurrentYear`
- `GetPortfolioReport_WithDepotCashPostingGroup_CalculatesLiquidityRatio` — Verrechnungskonto via `GroupId`
- `GetPortfolioReport_MultipleSecurityGroupsForSameAccount_DeduplicatesCashBalance` — Deduplizierung
- `GetPortfolioReport_ForeignAccountsAndGroups_DoNotAffectLiquidityRatio` — Owner-Scoping
- `GetPortfolioReport_NoCashAccount_ReturnsZeroLiquidityRatio`
- `GetPortfolioReport_NegativeDepotCash_ReturnsUnavailableLiquidityRatio` — `null`-Konvention
- `GetPortfolioReport_ClosedPositionWithPositiveDepotCash_ReturnsUnavailableLiquidityRatio`
- `GetPortfolioReport_NoPostings_ReturnsEmptyStructure` — Leerzustand ohne Buchungen
- `GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData` — Benutzerisolation
- `GetPortfolioReport_AllPositions_ContainsAllNonZeroPositionsSortedByMarketValueDescending`
- `GetPortfolioReport_TopPositions_EqualsAllPositionsTakeTen`
- `GetPortfolioReport_InvestedCapitalBreakdown_LotsSumMatchesInvestedCapitalAndSortedByPurchaseDateDescending`
- `GetPortfolioReport_InvestedCapitalBreakdown_FullySoldSecurity_IsExcluded`

Befund: Keine Tests für Jahresserien/Depotentwicklung (Methode existiert noch nicht).

### `PortfolioAnalysisReportCacheServiceTests`
Datei: `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportCacheServiceTests.cs`

- `CacheHit_WithinMonth_ReturnsCachedData` — einmalige Berechnung
- `CacheMiss_EndOfMonth_RecalculatesReport` — Monatsgrenze
- `CacheHit_EntryFromOlderDtoSchema_TreatedAsMissAndRecalculated` — `Parameter != CacheSchemaVersion` → Miss (**direkt relevant für den erforderlichen Versions-Bump**)
- `InvalidateCache_AfterPostingUpdate_DeletesCacheEntry`

### `PortfolioAnalysisReportControllerTests`
Datei: `FinanceManager.Tests/Controllers/PortfolioAnalysisReportControllerTests.cs` — Moq-basiert.

- `GetAnalysisReport_Controller_Returns200AndData`
- `PostKpiConfiguration_Controller_SavesAndReturns200` (+ Cache-Invalidierung)
- `PostKpiConfiguration_Controller_RejectsEmptyActiveTiles`
- `ResetCache_Controller_InvalidatesCache`

Befund: `CreateReport()`-Hilfsmethode konstruiert `PortfolioAnalysisReportDto` — bei neuer Record-Komponente müssen alle solchen Test-Hilfskonstruktoren angepasst werden (Compile-Break ist erwünschter Guard).

### `PortfolioAnalysisReportPageViewModelTests`
Datei: `FinanceManager.Tests/Web/ViewModels/Portfolio/PortfolioAnalysisReportPageViewModelTests.cs`

- `LoadReport_ViewModel_CallsServiceAndSetsData`, `EditMode_SaveConfiguration_PersistsAndInvalidatesCache`, `EnterEditMode_ViewModel_PopulatesEditOrderAndActiveFromConfiguration`, `SaveEditConfiguration_ViewModel_PersistsEditStateAndExitsEditMode`, `GetRibbonRegisters_ViewModel_ExposesSaveActionOnlyInEditMode`, `Refresh_ViewModel_ClearsAndReloadsReport`

### `PortfolioKpiConfigurationRepositoryTests`
Datei: `FinanceManager.Tests/Portfolio/PortfolioKpiConfigurationRepositoryTests.cs` — Upsert/Get/Delete der JSON-Konfiguration.

### `PortfolioStructureCardTests` (bUnit)
Datei: `FinanceManager.Tests/Components/Portfolio/PortfolioStructureCardTests.cs` — 12 Tests zu Erklärungs-Panels (`KpiInfoButton`), `MaxListEntries`-Kappung (200), Accordion, Top-Positions-Tabelle. Muster: `BunitContext`, `WithInvariantCulture`, `PagesStringLocalizer`.

### `PortfolioPerformanceCardTests_YtdFootnote` (bUnit)
Datei: `FinanceManager.Tests/Components/Portfolio/PortfolioPerformanceCardTests_YtdFootnote.cs`

- `AnnualReturns_ContainsYtdPoint_RendersFootnoteExplainingAsterisk` — `*`-Fußnoten-Muster für `IsYtd`
- `AnnualReturns_NoYtdPoint_DoesNotRenderFootnote`
- `AnnualReturns_Empty_DoesNotRenderChartOrFootnote` — Leerzustands-Konvention für die neue Kachel

### `PortfolioAnalysisReportE2ETests` (Playwright)
Datei: `FinanceManager.Tests.E2E/Tests/Portfolio/PortfolioAnalysisReportE2ETests.cs` — alle 4 im Baseline-Lauf bestanden.

- `LoadReportScenario_ShouldRenderTileGrid` — `.portfolio-tile-grid` + `.portfolio-tile`
- `RibbonNavigationScenario_ShouldNavigateFromSecuritiesList` — `button#PortfolioAnalysisReport`
- `EditConfigurationScenario_ShouldHideDeactivatedTileAfterSave` — deaktivierte Kachel verschwindet
- `MultiUserIsolationScenario_ShouldNotLeakConfigurationBetweenUsers`

### `EnumCoverageTests`
Datei: `FinanceManager.Tests/Common/EnumCoverageTests.cs` — `AssertValues<TEnum>`-Pattern; **`PortfolioTileId` derzeit nicht abgedeckt** (kein Test-Eintrag; `SecurityPostingSubType` ebenfalls nicht gelistet).

## Hilfsmethoden

### `PortfolioAnalysisReportServiceTests` (lokale Helfer)
- `CreateUser()` / `CreateSecurity(ownerUserId, name, categoryId?, region?, sector?)` / `CreateAccountAsync(ownerUserId, balance, name)`
- `AddBuyPosting` / `AddSellPosting` / `AddDividendPosting` / `AddBankPosting(accountId, groupId, date, amount)` / `AddPrice(securityId, date, close)` — direkt wiederverwendbar für Jahresserien-Szenarien; `Posting`-Konstruktor-Signatur ersichtlich.

### `FinanceManager.Tests/TestHelpers`
- `CultureTestHelper.WithInvariantCulture(...)` — kulturunabhängige Formatierungs-Asserts
- `TestEntityHelper`, `CapturingLogger`, `TestLoggerHelper`, `TestReturnAnalysisLocalizer`, `StubAccountService`, `SecurityTxtSettingsTestData`

### `FinanceManager.Web.Services.PagesStringLocalizer`
- `IStringLocalizer<Pages>`-Implementierung; in bUnit-Tests als Singleton registriert (`Components/Portfolio/*Tests.cs`).

### E2E-Infrastruktur (`FinanceManager.Tests.E2E`)
- `PlaywrightWebAppFixture` — WebApp + SQLite-Datenbank + Browser; `CreateSessionAsync()`, `DatabasePath`
- `TestUserSeeder.EnsureUserAsync(username, password)` — Testbenutzer
- `AuthGateway.LoginAsync/LogoutAsync` — Login-Flow
