# Tasks — Issue #534: Einzelne erwartete Posten pro Favorit ausblenden

| # | Bereich | Aufgabe | Status | Verifikation |
|---|---------|---------|--------|--------------|
| 1 | Domain | `ReportSuppressedExpectation` Entity + `ReportSuppressedExpectationBackupDto` + ToBackupDto/AssignBackupDto | Erledigt | `dotnet build` FinanceManager.Domain |
| 2 | Persistenz | DbSet, Unique-Index `(OwnerUserId, ReportFavoriteId, Kind, EntityId, PriorYearDate)`, Cascade-FK, `ClearUserDataAsync` total 27→28 | Erledigt | Build + `ClearUserDataAsync`-Test grün |
| 3 | Persistenz | `dotnet ef migrations add AddReportSuppressedExpectations -p FinanceManager.Infrastructure -s FinanceManager.Web` | Erledigt | Generierte Dateien prüfen (Tabelle + Index) |
| 4 | Shared | `ReportSuppressedExpectationDto` + `ReportSuppressedExpectationRequest` | Erledigt | Build |
| 5 | Shared | `ReportFavoriteId` (Guid?) in `ReportAggregationQuery` + `ReportAggregatesQueryRequest` | Erledigt | Build; alte Request-Aufrufer kompilieren |
| 6 | Service | `IReportSuppressedExpectationService` + `ReportSuppressedExpectationService` (List/Suppress/Unsuppress, Owner-Filter, Favoriten-Ownership, EntityName) + DI | Erledigt | Service-Tests grün |
| 7 | API | `ReportsController`: Query-Mapping `ReportFavoriteId`; GET/POST/DELETE `report-favorites/{id}/suppressed-expectations` | Erledigt | Integration-Test oder manueller cURL-Check |
| 8 | ApiClient | `IApiClient` + `ApiClient.Reports.cs` drei Methoden | Erledigt | Build |
| 9 | Calculator | `suppressions`-Parameter + Kandidatenfilter vor `FindExpectedItems` | Erledigt | Aggregation-Tests grün |
| 10 | Aggregation | `LoadSuppressionKeysAsync` + Übergabe an beide `ApplyProjectionAmounts`-Aufrufe | Erledigt | Aggregation-Tests grün |
| 11 | Caller | `ReportDashboardViewModel` `ActiveFavoriteId` → Request | Erledigt | Build |
| 12 | Caller | `HomeKpiGrid` `fav.Id` → Request | Erledigt | Build |
| 13 | Caller | `FmReportingAggregateTool` `favorite.Id` bei favoriteId | Erledigt | Build; ad-hoc ohne favoriteId → null |
| 14 | UI | Overlay: Suppress-Button je Item (nur mit Favorit), „Ausgeblendete Erwartungen"-Liste + Unsuppress, Reload | Erledigt | Build + E2E |
| 15 | Resx | de + en Keys | Erledigt | Build; Keys auflösbar |
| 16 | Backup | Export `ReportSuppressedExpectations`; Import mit `reportMap` nach ReportFavorites | Erledigt | Backup-Tests grün |
| 17 | Tests | `ReportSuppressedExpectationServiceTests` | Erledigt | `dotnet test --filter` grün |
| 18 | Tests | `ReportAggregationSuppressionTests` (Filter, Schlüssel ohne Betrag, Fremdfavorit, Unsuppress-Rückkehr) | Erledigt | grün |
| 19 | Tests | `ReportSuppressedExpectationBackupRestoreTests` | Erledigt | grün |
| 20 | Tests | `ReportSuppressedExpectationPlaywrightTests` (hide → hidden list → restore) | Erledigt | E2E-Lauf grün |
| 21 | Docs | `Docs/help/berichtswesen/` Update + index | Erledigt | Review |
| 22 | Abschluss | Plan-Review, Code-Review, Test-Lauf, Cleanup, Commit | Erledigt | – |
