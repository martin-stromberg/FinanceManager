# Inventar — Persistenz & Backup

## `AppDbContext.cs`

- DbSet `ReportSuppressedExpectations` ergänzen.
- `OnModelCreating`: Key `Id`, Required-Felder, `Kind` als int, Unique-Index `(OwnerUserId, ReportFavoriteId, Kind, EntityId, PriorYearDate)`, FK `ReportFavoriteId → ReportFavorites` mit `Cascade` (Favorit-Löschung räumt Vermerke ab).
- `ClearUserDataAsync`: neuen Löschschritt ergänzen (Total 27 → 28), Reihenfolge vor `ReportFavorites` (oder Cascade überlassen — explizit ist konsistent mit bestehendem Stil).

## EF-Migration

`dotnet ef migrations add AddReportSuppressedExpectations --project FinanceManager.Infrastructure --startup-project FinanceManager.Web`
(SQLite via `ServiceCollectionExtensions.UseSqlite`; EF-Tools 10.0.10 vorhanden.)

## `BackupService.BuildBackupDataAsync`

- Neue optionale Sektion `ReportSuppressedExpectations` (Liste von `ReportSuppressedExpectation.ReportSuppressedExpectationBackupDto`).
- **Nicht** in `RequiredDataProperties` aufnehmen (alte Backups müssen importierbar bleiben).

## `SetupImportService`

- `TryGetProperty("ReportSuppressedExpectations")`-Guard nach `ReportFavorites`-Import (wegen `reportMap`-Abhängigkeit).
- `ReportFavoriteId` per `reportMap` remappen; Eintrag überspringen, wenn der Favorit nicht im Backup enthalten war.
- `AssignBackupDto` stellt ID/Owner/Felder wieder her; Owner = importierender Benutzer.
- `EnumCoverageTests`: kein neuer Enum → keine Änderung.

## Referenzmuster

- Domain: `SavingsPlanAccountLink` (Ctor-Guards, BackupDto, ToBackupDto/AssignBackupDto).
- Tests: `SavingsPlanLinkBackupRestoreTests` (Export+Import, Remapping, fehlende Sektion).
