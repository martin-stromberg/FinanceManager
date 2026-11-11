# Tests

## Testklassen

### `ReportAggregationServiceTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationServiceTests.cs`

Deckt den Standardpfad von `QueryAsync` ab (SQLite-InMemory-`AppDbContext`, `EnsureCreated`):
- `QueryAsync_ShouldAggregateCategoriesAndComparisons_ForContactsAcrossMonths` — Kategorie-Rollups + Prev/Year-Vergleiche
- `QueryAsync_ShouldAggregateYtdCategoriesAndComparisons_ForContacts` — YTD-Transform
- `QueryAsync_ShouldApplyEntityFilters_ForTwoKinds_WithTwoSelectedValuesEach` — Entity-Filter über zwei Kinds
- `QueryAsync_SecurityDividendCategory_*` (3 Tests) — Netto-Dividenden-Kategorien, Null-Monat-Injektion, Prev-Übertrag
- `QueryAsync_Monthly_AllKinds_VerifyPrevAndYear_ForTwoEntities`, `QueryAsync_Monthly_SelectOtherEntities_ShouldBeEmpty`, `QueryAsync_VariousIntervals_Bank_VerifySums` (Theory über Intervalle)

### `ReportAggregationServiceAdditionalTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationServiceAdditionalTests.cs`
- `QueryAsync_ShouldReturnOnlyEntityRows_WhenIncludeCategoryFalse`, `QueryAsync_ShouldCreateZeroRowForMissingLatestPeriod_WhenComparisonsEnabled`, `QueryAsync_ShouldRemoveEmptyGroupWithoutComparisonData`, `QueryAsync_ShouldRespectTake_PeriodLimitation`, `QueryAsync_ShouldAggregateQuarterHalfYearYear`, `QueryAsync_ShouldGroupUncategorizedContacts`

### `ReportAggregationServiceMultiKindTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationServiceMultiKindTests.cs`
- `QueryAsync_MultiKinds_WithCategories_ShouldCreateTypeCategoryEntityHierarchy`, `QueryAsync_MultiKinds_WithoutCategories_ShouldCreateTypeRowsWithEntityChildren`

### `ReportAggregation_ValutaBookingTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregation_ValutaBookingTests.cs`
- `QueryAsync_Monthly_ByValutaDate_ShouldAggregateSep2025`, `QueryAsync_Monthly_ByBookingDate_ShouldAggregateSep2025` — DateKind-Verhalten der Aggregate

### `ReportAggregationServiceSavingsPlanSourceTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationServiceSavingsPlanSourceTests.cs`
- `QueryAsync_PlanPostingsSource_UsesPlanPostings`, `QueryAsync_LinkedSource_UsesLinkedBankAggregates`, `QueryAsync_LinkedSource_UnlinkedPlanKeepsPlanPostings`, `QueryAsync_LinkedSource_ForeignOwner_Excluded` — `SavingsPlanPostingSource.LinkedAccountPostings`-Semantik inkl. Owner-Fremdpostings

### `ReportAggregationServiceSecurityMixedSubTypesTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationServiceSecurityMixedSubTypesTests.cs`
- Subtyp-Split je Security-Punkt, `SecuritySubTypes`-Filter, `null`-Subtyp auf Bank-Punkten, Subtyp-genaue Prev-Vergleiche, Kategorie-Netto über Subtypen

### `ReportAggregationProjectionTests`
Datei: `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs` (~24 Tests)
- `QuerySecurityDividendsNetAsync`-Pfad: Projektionsmuster (monthly/quarterly/annual/irregular), Holding-Prüfung inkl. Storno-`Quantity`-Behandlung, Valuta-Nutzung, Kategorie-Aggregation, Fremd-Owner-Ausschluss, YTD/Interval-Kantenfälle

### `ReportFavoriteServiceTests`
Datei: `FinanceManager.Tests/Reports/ReportFavoriteServiceTests.cs`
- `CreateAsync_ShouldPersistAndReturnDto`, `CreateAsync_ShouldThrow_OnDuplicateNamePerUser`, `CreateAsync_ShouldAllowSameNameForDifferentUsers`, `UpdateAsync_ShouldModifyFields_AndRejectDuplicate`, `CreateListGetAndUpdate_ShouldRoundtripCompareProjection`, `CreateGetListAndUpdate_ShouldRoundtripSavingsPlanPostingSource` (Vorbild für Filter-Roundtrip-Tests neuer Felder), `DeleteAsync_ShouldReturnFalse_WhenNotOwnedOrMissing`, `ListAndGet_ShouldRespectOwnershipAndOrdering`

### `ReportDashboardViewModelTests`
Datei: `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs` (Moq-`IApiClient`, `TestCurrentUserService`)
- `LoadAsync_ReturnsPoints`, `SaveUpdateDelete_Favorites_Roundtrip`, `GetChartByPeriod_ComputesSums_PerMonth`, `Totals_And_ColumnVisibility_Work`, `ProjectionColumn_IsVisibleAndTotalsProjection_WhenServerComparedProjection`, `ReloadAsync_DisablesProjection_*` (3), `IsNegative_*` (2), `PerType_Children_When_IncludeCategory_Multi`, `SaveAndUpdateFavorite_ForwardSavingsPlanPostingSource`, `FiltersOpen_StaysEnabled_OutsideEditMode`, `FilterDialog_TempChanges_ApplyToActiveFilters` — Letzterer testet das Temp→Selected-Übernahmemuster, das auch für Textfilter-Sammlungen relevant wird

### `HomeKpiGridTests`
Datei: `FinanceManager.Tests/Components/HomeKpiGridTests.cs` (bUnit, `BunitContext`, `PagesStringLocalizer`)
- `ReportFavoriteKpi_ForwardsCompareProjectionToAggregateRequest` — captured `ReportAggregatesQueryRequest` aus dem Favoriten; Muster für einen Test, der die Filter-Propagation in den KPI-Request assertiert
- `MonthlyBudgetKpi_DoesNotBlockOtherHomeKpiRendering`, `MonthlyBudgetKpi_ReRenderDoesNotCreateSecondRequest`

### `NumericKpiCacheTests`
Datei: `FinanceManager.Tests/Components/NumericKpiCacheTests.cs`
- Testet `KpiLocalStorageCache` gegen einen JS-Runtime-Stub (`js.Store["fm.kpi.test-key"]`) — Muster für Cache-Tests des `IKpiLocalStorageCache`

### `BackupRestoreCoverageTests`
Datei: `FinanceManager.Tests/Infrastructure/BackupRestoreCoverageTests.cs`
- `ImportAsync_Restores*` (StatementImports, Notifications, AccountShares, AccountLinkedIbans, PortfolioKpiConfiguration), `RoundTrip_PreservesNewSections`, `BudgetRule_PurposePattern_SurvivesBackupRestore` (Präzedenz: Pattern+Regex-Flag über Backup/Restore), `Security_ErrorAndNotifiedFields_SurviveBackupRestore`, `ClearUserDataAsync_RemovesCategoryBoundBudgetRules`, `ImportAsync_OldBackupWithoutNewSections_StillWorks` — Muster für den geforderten Alt-Backup-Kompatibilitätstest

### `BackupServiceTests` / `BackupServiceFullExportTests`
Dateien: `FinanceManager.Tests/Infrastructure/BackupServiceTests.cs`, `BackupServiceFullExportTests.cs`
- `BackupServiceTests`: Zip-Erzeugung, Upload-Validierung, Pflichtsektionen, `CreateAsync_IncludesBudgetRulesForCategoryAndPurpose`
- `BackupServiceFullExportTests.CreateAsync_FullBackup_IncludesAllSeededData`: Seed enthält `ReportFavorite` + `HomeKpi` mit FK; asserted `["ReportFavorites"] = typeof(ReportFavorite)` in der Property→Entity-Map — neue Felder müssten im Roundtrip-Vergleich dieser Tests mitlaufen

### `EnumCoverageTests`
Datei: `FinanceManager.Tests/Common/EnumCoverageTests.cs`
- Assertiert Memberlisten vieler Enums; die hier betroffenen Enums (`PostingKind`, `ReportInterval`, `SavingsPlanPostingSource`, `SecurityPostingSubType`, `AggregatePeriod`, `AggregateDateKind`, `HomeKpiKind`, `HomeKpiDisplayMode`, `SavingsPlanPostingSubType`) sind **nicht** abgedeckt. Nur relevant, falls doch ein Enum-Mitglied ergänzt wird.

## Hilfsmethoden

### `CreateDb` (je Testklasse privat dupliziert)
- `ReportAggregationServiceTests` u. a.: `SqliteConnection("DataSource=:memory:")` + `DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn)` + `EnsureCreated()` — damit stehen echte EF-Abfragen über `_db.Postings`/`_db.PostingAggregates` zur Verfügung (gut geeignet für Rohposting-Textfilter-Tests)
- `BackupRestoreCoverageTests`: `CreateDb(out SqliteConnection conn)` + `CreateBackupService(db, tempDir)` mit Fake-Host-Environment

### Seed-Helfer in den Aggregation-Tests
- `NewContact(db, ownerId, name, cat?)`, `NewSavingsPlan(db, ownerId, name, categoryId?)` (MultiKind-Tests); Postings werden direkt als `Posting`-Entities geseeded (`new Posting(sourceId, kind, accountId, contactId, savingsPlanId, securityId, bookingDate, amount, ...)` inkl. `subject`/`description`-Konstruktor-Overload)

### `CreateVm` (`ReportDashboardViewModelTests`)
- `ServiceCollection` + `TestCurrentUserService` + `Mock<IApiClient>` → `ReportDashboardViewModel`; `CreatePoints(int)` erzeugt `ReportAggregatePointDto`-Listen

### `CreatePredefinedKpi` (`HomeKpiGridTests`)
- Baut `HomeKpiDto` für Predefined-Kacheln; Favoriten-KPI-Tests nutzen `HomeKpiDto` mit `HomeKpiKind.ReportFavorite` + gemocktem `Reports_GetFavoriteAsync`/`Reports_QueryAggregatesAsync`

### `PagesStringLocalizer` (`HomeKpiGridTests`)
- Test-`IStringLocalizer<Pages>`-Stub für bUnit-Rendering
