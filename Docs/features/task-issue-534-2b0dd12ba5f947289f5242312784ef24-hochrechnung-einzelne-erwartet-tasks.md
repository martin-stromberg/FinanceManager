# Tasks — Issue #534: Einzelne erwartete Posten pro Favorit ausblenden

| # | Bereich | Aufgabe | Status | Verifikation |
|---|---------|---------|--------|--------------|
| 1 | Domain | `ReportSuppressedExpectation` Entity + `ReportSuppressedExpectationBackupDto` + ToBackupDto/AssignBackupDto | Offen | `dotnet build` FinanceManager.Domain |
| 2 | Persistenz | DbSet, Unique-Index `(OwnerUserId, ReportFavoriteId, Kind, EntityId, PriorYearDate)`, Cascade-FK, `ClearUserDataAsync` total 27→28 | Offen | Build + `ClearUserDataAsync`-Test grün |
| 3 | Persistenz | `dotnet ef migrations add AddReportSuppressedExpectations -p FinanceManager.Infrastructure -s FinanceManager.Web` | Offen | Generierte Dateien prüfen (Tabelle + Index) |
| 4 | Shared | `ReportSuppressedExpectationDto` + `ReportSuppressedExpectationRequest` | Offen | Build |
| 5 | Shared | `ReportFavoriteId` (Guid?) in `ReportAggregationQuery` + `ReportAggregatesQueryRequest` | Offen | Build; alte Request-Aufrufer kompilieren |
| 6 | Service | `IReportSuppressedExpectationService` + `ReportSuppressedExpectationService` (List/Suppress/Unsuppress, Owner-Filter, Favoriten-Ownership, EntityName) + DI | Offen | Service-Tests grün |
| 7 | API | `ReportsController`: Query-Mapping `ReportFavoriteId`; GET/POST/DELETE `report-favorites/{id}/suppressed-expectations` | Offen | Integration-Test oder manueller cURL-Check |
| 8 | ApiClient | `IApiClient` + `ApiClient.Reports.cs` drei Methoden | Offen | Build |
| 9 | Calculator | `suppressions`-Parameter + Kandidatenfilter vor `FindExpectedItems` | Offen | Aggregation-Tests grün |
| 10 | Aggregation | `LoadSuppressionKeysAsync` + Übergabe an beide `ApplyProjectionAmounts`-Aufrufe | Offen | Aggregation-Tests grün |
| 11 | Caller | `ReportDashboardViewModel` `ActiveFavoriteId` → Request | Offen | Build |
| 12 | Caller | `HomeKpiGrid` `fav.Id` → Request | Offen | Build |
| 13 | Caller | `FmReportingAggregateTool` `favorite.Id` bei favoriteId | Offen | Build; ad-hoc ohne favoriteId → null |
| 14 | UI | Overlay: Suppress-Button je Item (nur mit Favorit), „Ausgeblendete Erwartungen"-Liste + Unsuppress, Reload | Offen | Build + E2E |
| 15 | Resx | de + en Keys | Offen | Build; Keys auflösbar |
| 16 | Backup | Export `ReportSuppressedExpectations`; Import mit `reportMap` nach ReportFavorites | Offen | Backup-Tests grün |
| 17 | Tests | `ReportSuppressedExpectationServiceTests` | Offen | `dotnet test --filter` grün |
| 18 | Tests | `ReportAggregationSuppressionTests` (Filter, Schlüssel ohne Betrag, Fremdfavorit, Unsuppress-Rückkehr) | Offen | grün |
| 19 | Tests | `ReportSuppressedExpectationBackupRestoreTests` | Offen | grün |
| 20 | Tests | `ReportSuppressedExpectationPlaywrightTests` (hide → hidden list → restore) | Offen | E2E-Lauf grün |
| 21 | Docs | `Docs/help/berichtswesen/` Update + index | Offen | Review |
| 22 | Abschluss | Plan-Review, Code-Review, Test-Lauf, Cleanup, Commit | Offen | – |
