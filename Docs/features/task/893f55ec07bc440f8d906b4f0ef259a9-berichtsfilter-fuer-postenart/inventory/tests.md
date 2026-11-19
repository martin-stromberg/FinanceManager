# Tests

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt:** 2026-11-19, ~04:27–05:00 Uhr (UTC+01:00)
- **Branch:** `task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart`
- **Commit:** `b609d055522998de75849519f0afde7ebc0cc521`
- **Uncommittete Änderungen:** nur untracked `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/` (diese Bestandsaufnahme)
- **Runtime/SDK:** `dotnet --version` = 11.0.100 (installiert: 10.0.404, 11.0.100); Ziel-Framework der Projekte `net10.0`; Test-Runner: xUnit v3 via Microsoft.Testing.Platform (`global.json` → `"runner": "Microsoft.Testing.Platform"`)
- **Build vor den Tests:** `dotnet build FinanceManager.sln -c Debug` → 0 Fehler, 64 Warnungen (preexisting xUnit-Analyzer/Codes)

### Ermittelte Testsuiten und Quellen der Testbefehle

Quellen: `.github/workflows/pr-staging-ci.yml` (Job `build-and-test`, Zeilen ~131–172), Projektdateien, `global.json`.

| Suite | Projekt | Laufbarkeit in dieser Umgebung |
|---|---|---|
| `FinanceManager.Tests` | `FinanceManager.Tests/FinanceManager.Tests.csproj` | lauffähig (SQLite/InMemory) |
| `FinanceManager.Tests.Integration` | `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj` | lauffähig (`TestWebApplicationFactory`, In-Process-Server inkl. ApiClient-Tests unter `FinanceManager.Tests.Integration/ApiClient/`) |
| `FinanceManager.Tests.E2E` | `FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj` | lauffähig (Playwright, Browser unter `%USERPROFILE%\AppData\Local\ms-playwright` vorhanden; Channel `msedge` via `PLAYWRIGHT_BROWSER_CHANNEL`, Default `msedge`) |
| `FinanceManager.Tests.Integration.ApiClient` | — | **kein eigenständiges Projekt**: Verzeichnis enthält nur die einzelne Datei `ApiClientBudgetReportUnbudgetedMirrorTests.cs` ohne `.csproj`; nicht in `FinanceManager.sln` enthalten. Die gleichnamige Testklasse existiert in `FinanceManager.Tests.Integration/ApiClient/` und läuft dort mit |

### Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|---|---|---|---|---|---|---|---|
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <inv>/test-results -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename test-results-unittests.trx` | Repo-Root | 0 | 2396 | 0 | 0 | `inventory/test-results/test-results-unittests.trx`, `unittests-console.log` |
| Integration (inkl. ApiClient) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <inv>/test-results -- --report-xunit-trx --report-xunit-trx-filename test-results-integration.trx` | Repo-Root | 0 | 176 | 0 | 0 | `inventory/test-results/test-results-integration.trx`, `integration-console.log` |
| E2E | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <inv>/test-results -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e.trx` | Repo-Root | 0 | 173 | 0 | 0 | `inventory/test-results/test-results-e2e.trx`, `e2e-console.log` |

`<inv>` = `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/inventory`

Hinweise zu den Läufen:
- Unit-Lauf mit dem CI-Filter `--filter-not-trait "Category=OsInterface"` (wie Gate 4 in `pr-staging-ci.yml`); Dauer ~48 s.
- Integrationslauf ohne Trait-Filter (wie CI); Dauer ~8 min 19 s. Die Konsolenausgabe enthält erwartete `SchemaPatcher`-/EF-Logzeilen (keine Fehler).
- E2E-Lauf wie CI ohne Filter; die Suite startet pro Fixture einen In-Process-Webserver + Playwright-Browser (`msedge`-Channel) und lief ~22 min 15 s; alle 173 Tests bestanden.

### Nachgewiesene bestehende Testfehler

Keine — im dokumentierten Ausgangslauf sind alle ausgeführten Tests bestanden.

### Testlücken und Ausführungsprobleme

- **Lücke Fehler 1 (Projektion ignoriert `BankPostingTypes`):** Kein Test deckt den Kombination `CompareProjection = true` + `PostingKind.Bank` + `BankPostingTypes`-Filter ab. `ReportAggregationCrossKindProjectionTests` testet Bank-Projektion nur mit `AccountIds`- und Textfiltern (`QueryAsync_BankProjection_RespectsAccountIdsFilter`, `QueryAsync_BankProjection_RespectsBankTextFilters`).
- **Lücke Fehler 2 (Filterverlust beim Speichern):** Kein Test assertiert `BankPostingTypes` im `ReportFavoriteFiltersDto`-Payload von `SaveFavoriteAsync`/`UpdateFavoriteAsync`. Muster dafür existiert: `ReportDashboardViewModelTextFilterTests.SaveFavorite_ShouldMapTextFilters`. Service-seitig ist der Roundtrip dagegen bereits abgedeckt (`ReportFavoriteServiceFilterTests.CreateListGet_ShouldRoundtripBankPostingTypes`).
- **Lücke Home-KPI-Pfad:** `HomeKpiGrid.razor` (`LoadFavoriteKpiDataAsync`, Zeilen 647–660) übergibt `BankPostingTypes` nicht an `ReportAggregatesFiltersRequest` — keine Testabdeckung dieser Komponente gefunden.
- **E2E:** Keine E2E-Tests zum Postenart-Filter in Berichten (Playwright-Report-Tests decken Textfilter, Projektion und Suppressions ab, aber nicht `BankPostingTypes`).
- **ApiClient/Integration:** `ApiClientReportsTests` enthält keine `BankPostingTypes`-Szenarien.

## Testklassen (feature-relevant)

### `ReportAggregationServiceTextFilterTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationServiceTextFilterTests.cs` (SQLite In-Memory + `EnsureCreated`)

Methoden: `QueryAsync_BankTextFilter_MatchesSubject_CaseInsensitive`, `QueryAsync_BankTextFilter_MatchesDescription_AndOrSemantics`, `QueryAsync_BankTextFilter_SkipsZeroAmountPostings`, `QueryAsync_BankTextFilter_UseValutaDate_BucketsByValutaDate`, `QueryAsync_TextFilter_UseValutaDate_UsesValutaWithFallback`, `QueryAsync_TextFilter_WithIncludeCategory_AppliesCumulatively`, `QueryAsync_MixedKinds_RawPathAndAggregatePathCoexist`, `QueryAsync_BankTextFilter_ExcludesForeignOwnerPostings`, `QueryAsync_SavingsPlanTextFilter_LinkedSource_FiltersLinkedBankPostings`, `QueryAsync_SecurityDividendNet_TextFilter_FiltersDividendAnchors`, **`QueryAsync_BankPostingTypesFilter_KeepsOnlySelectedTypes`** (Zeile 337 — deckt den Aggregatpfad ab, nicht die Projektion)

### `ReportAggregationProjectionTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs`

Methoden (Auswahl): `QueryAsync_SecurityDividendProjection_*` (Dividenden-Hochrechnung inkl. Holding-Checks, Valuta, YTD), `QueryAsync_BankProjection_AddsUnconfirmedPriorYearPosting`, `QueryAsync_Projection_ContradictingSecuritySubTypes_*` — keine `BankPostingTypes`-Abdeckung.

### `ReportAggregationCrossKindProjectionTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationCrossKindProjectionTests.cs`

Methoden (Auswahl): `QueryAsync_BankProjection_Annual_Confirmed`, `QueryAsync_BankProjection_MonthlyPattern`, `QueryAsync_ContactProjection_QuarterlyPattern`, `QueryAsync_SavingsPlanProjection_*` (inkl. `LinkedAccountPostings`), `QueryAsync_MultiKindProjection_*`, `QueryAsync_Projection_AllHistoryExcluded`, `QueryAsync_BankProjection_RespectsBankTextFilters`, `QueryAsync_BankProjection_RespectsAccountIdsFilter`, `QueryAsync_BankProjection_RespectsOwnerIsolation`, `QueryAsync_BankProjection_UsesValutaDateAndBookingFallback`, `QueryAsync_BankProjection_YtdInterval`, `QueryAsync_BankProjection_YearInterval`, `QueryAsync_BankProjection_NoPriorYearEvents`, `QueryAsync_Projection_ComparePrevious_LatestRowRetained`, `QueryAsync_BankProjection_Annual_RemainsExpected_WhenOtherCurrentEventExists`, `QueryAsync_BankProjection_Irregular_RemainsExpectedForUnmatchedCandidates`, `QueryAsync_SecurityProjection_Annual_SuppressedByAnyCurrentEvent`, `QueryAsync_MultiKindProjection_FullySoldSecurity_NoExpectation` — keine `BankPostingTypes`-Abdeckung.

### `ReportPostingsQueryServiceTests`
Datei: `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests.cs`

Umfangreiche Drilldown-Abdeckung (Periodenfenster, Gruppenschlüssel, Owner-Isolation, Entity-/Kategorie-/SubType-/Textfilter, Valuta, verknüpfte Sparplanquelle, Netto-Dividenden, 5000er-Limit); **`QueryAsync_BankPostingTypesFilter_KeepsOnlySelectedTypes`** (Zeile 976) deckt den Postenart-Filter im Drilldown ab.

### `ReportFavoriteServiceFilterTests`
Datei: `FinanceManager.Tests/Reports/ReportFavoriteServiceFilterTests.cs`

Methoden: `CreateListGet_ShouldRoundtripTextFilters`, `UpdateAsync_ShouldReplaceTextFilters`, `UpdateAsync_NullFilters_ClearsTextFilters`, `CreateAsync_ShouldThrow_WhenTextFilterExceedsMaxLength`, `GetAsync_ShouldReturnNullFilters_WhenNoFiltersConfigured`, **`CreateListGet_ShouldRoundtripBankPostingTypes`** (Zeile 162 — service-seitiger Roundtrip inkl. `BankPostingTypesCsv = "1,3"`)

### `ReportFavoriteBackupTests`
Datei: `FinanceManager.Tests/Reports/ReportFavoriteBackupTests.cs`

Methoden: `ToBackupDto_ShouldIncludeTextFilterJson`, `AssignBackupDto_ShouldRestoreTextFilters`, `AssignBackupDto_ShouldAcceptOldBackup_WithoutTextFilterFields` (prüft auch `BankPostingTypesCsv == null` bei Alt-Backup, Zeile 112), `BackupDto_ShouldRoundtripChartSettings`, `AssignBackupDto_ShouldTolerateMalformedTextFilterJson`, **`BackupDto_ShouldRoundtripBankPostingTypes`** (Zeile 168)

### `ReportDashboardViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs` (Moq-`IApiClient`)

Methoden: `LoadAsync_ReturnsPoints`, `SaveUpdateDelete_Favorites_Roundtrip`, `GetChartByPeriod_ComputesSums_PerMonth`, `Totals_And_ColumnVisibility_Work`, `ProjectionColumn_IsVisibleAndTotalsProjection_WhenServerComparedProjection`, `ReloadAsync_KeepsProjection_*` (Theory), `ReloadAsync_DisablesProjection_ForAllHistory`, `IsNegative_*`, `PerType_Children_When_IncludeCategory_Multi`, `SaveAndUpdateFavorite_ForwardSavingsPlanPostingSource`, `FiltersOpen_StaysEnabled_OutsideEditMode`, `FilterDialog_TempChanges_ApplyToActiveFilters`, `ApplyTempAndReload_SetsFiltersDirty_OnlyOnChange`, `ClearFilters_SetsFiltersDirty_OnlyWhenFiltersWereActive`, `SaveActions_EnabledInViewMode_OnlyWhenFiltersDirty`, `SubmitFavoriteDialog_ResetsFiltersDirty_AfterSave`, `ShowPostingsAsync_*` — keine `BankPostingTypes`-Payload-Assertion.

### `ReportDashboardViewModelTextFilterTests`
Datei: `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTextFilterTests.cs`

Methoden: `AddTempTextFilter_ShouldTrim_Dedupe_AndIgnoreEmpty`, `RemoveTempTextFilter_ShouldRemove_OnlyForGivenKind`, `ApplyTempAndReload_ShouldMapTextFilters_IntoRequestPayload`, `ClearFilters_ShouldClearTextFilters`, `OpenFilterDialog_ShouldCopySelectedToTemp`, `FilterCounters_ShouldIncludeTextFilters`, **`SaveFavorite_ShouldMapTextFilters`** (Zeile 170 — direktes Muster für den Fehler-2-Repro-Test)

### `PostingTypeMigrationBackfillTests`
Datei: `FinanceManager.Tests/Infrastructure/PostingTypeMigrationBackfillTests.cs`

Methode: `Migration_BackfillsPostingType_FromAmountSign` — Backfill der `PostingType`-Spalte (Postings + StatementDraftEntries) aus dem Betragsvorzeichen; `Unspecified` für Nicht-Bankpostings.

### `EnumCoverageTests`
Datei: `FinanceManager.Tests/Common/EnumCoverageTests.cs`

Methode `BankPostingType_Values` (Zeilen 169–175) assertiert die komplette Memberliste (`Unspecified`, `Inflow`, `Outflow`, `Interest`, `Tax`).

### Weitere Berührungspunkte
- `FinanceManager.Tests/Postings/PostingTypeEvaluatorTests.cs` — Klassifizierungslogik.
- `FinanceManager.Tests/Infrastructure/BackupRestoreCoverageTests.cs`, `ReportSuppressedExpectationBackupRestoreTests.cs` — erwähnen `BankPostingType` im Backup-Kontext.
- `FinanceManager.Tests/Reports/ReportAggregationService*Tests.cs` (weitere Klassen), `ReportAggregationSuppressionTests`, `ReportAggregationServiceSavingsPlanSourceTests` — Aggregation ohne Postenart-Bezug.
- Integration: `FinanceManager.Tests.Integration/ApiClient/ApiClientAccountPostingRulesTests.cs` — AccountPostingRules, nicht der Berichtsfilter.
- E2E: `FinanceManager.Tests.E2E/Tests/Accounts/AccountPostingRulesPlaywrightTests.cs` — Klassifizierungsregeln, nicht der Berichtsfilter.

## Hilfsmethoden

| Helper | Datei | Zweck |
|---|---|---|
| `Create()` → `(sut, db, conn)` | `ReportAggregationServiceTextFilterTests.cs:25`, `ReportPostingsQueryServiceTests.cs:24` | SQLite-InMemory `AppDbContext` (`EnsureCreated`) + Service-Instanz |
| `AddPosting(db, kind, ...)` | `ReportAggregationServiceTextFilterTests.cs:35`, `ReportPostingsQueryServiceTests.cs:41` | `Posting`-Seed; Postenart via `p.SetPostingType(BankPostingType.X)` (Fluent) |
| `SeedAccountAsync(db, owner)` | `ReportAggregationServiceTextFilterTests.cs:45` | Bank-`Contact` + `Account`-Seed mit Owner |
| `Query(...)` / `Filters(...)` | `ReportPostingsQueryServiceTests.cs:48,56` | `ReportPostingsQuery`-/`ReportAggregationFilters`-Factory (inkl. `bankPostingTypes`-Parameter) |
| `CreateDb()` / `CreateServiceAsync(db)` | `ReportFavoriteServiceFilterTests.cs:17,27` | SQLite-DB + `ReportFavoriteService` mit `User`-Seed |
| `CreateVm()` / `SetupAggregates(...)` | `ReportDashboardViewModelTests.cs:27`, `ReportDashboardViewModelTextFilterTests.cs:26,36` | `ReportDashboardViewModel` mit DI-`ServiceCollection` + `Mock<IApiClient>`; Request-Capture per Callback |
| `ExecAsync` / `ScalarIntAsync` | `PostingTypeMigrationBackfillTests.cs:86,93` | Rohe SQL-Ausführung gegen SQLite für Migrations-Tests |
