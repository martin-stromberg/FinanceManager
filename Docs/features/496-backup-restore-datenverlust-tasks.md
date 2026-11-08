# Tasks: Backup/Restore – Datenverlust durch fehlende Import- und DTO-Abdeckung (Issue #496)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `BudgetRuleBackupDto` um `PurposePattern` (`string? = null`) und `PurposePatternIsRegex` (`bool = false`) erweitern | Offen | — |
| 2 | Datenmodell | `BudgetRule.ToBackupDto()`/`AssignBackupDto()` um Pattern-Felder ergänzen (`AssignBackupDto` nutzt `SetPurposePattern`) | Offen | — |
| 3 | Datenmodell | `SecurityBackupDto` um `HasPriceError`, `PriceErrorMessage`, `PriceErrorSinceUtc`, `GoldenCrossNotifiedPhase`, `TrendReversalNotifiedStatus` (mit Defaults) erweitern | Offen | — |
| 4 | Datenmodell | `Security.ToBackupDto()`/`AssignBackupDto()` um neue Felder ergänzen (Fehlerfelder direkt setzen, `SetGoldenCrossNotifiedPhase`/`SetTrendReversalNotifiedStatus` nutzen) | Offen | — |
| 5 | Datenmodell | `AccountLinkedIban.ToBackupDto()` und `AssignBackupDto()` ergänzen | Offen | — |
| 6 | Datenmodell | `PortfolioKpiConfigurationBackupDto` (nested record) sowie `ToBackupDto()`/`AssignBackupDto()` in `PortfolioKpiConfiguration` anlegen | Offen | — |
| 7 | Logik | `BackupService.BuildBackupDataAsync`: Export-Block + Payload-Property für `AccountLinkedIbans` (Filter über `importAccountIds`) | Offen | — |
| 8 | Logik | `BackupService.BuildBackupDataAsync`: Export-Block + Payload-Property für `PortfolioKpiConfigurations` (Filter `OwnerUserId == userId`) | Offen | — |
| 9 | Logik | `SetupImportService.ImportVersion3`: `statementImportMap` (`Dictionary<Guid,Guid>`) anlegen | Offen | — |
| 10 | Logik | `SetupImportService.ImportVersion3`: `TryGetProperty`-Importblock `AccountLinkedIbans` (accountMap-Remap, Skip ohne Treffer, `(AccountId,Iban)`-Dedup) | Offen | — |
| 11 | Logik | `SetupImportService.ImportVersion3`: `TryGetProperty`-Importblock `StatementImports` (accountMap-Remap, `AssignBackupDto`, `statementImportMap` füllen) | Offen | — |
| 12 | Logik | `SetupImportService.ImportVersion3`: `TryGetProperty`-Importblock `AccountShares` (accountMap-Remap mit Original-Fallback, `AssignBackupDto`) | Offen | — |
| 13 | Logik | `SetupImportService.ImportVersion3`: `TryGetProperty`-Importblock `StatementEntries` nach `Postings` (Remap via `statementImportMap`/`contactMap`/`savingsMap`/`postingMap`, `RawHash`-Dedup) | Offen | — |
| 14 | Logik | `SetupImportService.ImportVersion3`: `TryGetProperty`-Importblock `Notifications` (`AssignBackupDto`, `OwnerUserId = userId`, Id-Existenzprüfung) | Offen | — |
| 15 | Logik | `SetupImportService.ImportVersion3`: `TryGetProperty`-Importblock `PortfolioKpiConfigurations` (Upsert: bestehende Zeile aktualisieren oder neu anlegen) | Offen | — |
| 16 | Logik | `SetupImportService.ImportVersion3`: `BudgetRules`-Konstruktoraufruf um `PurposePattern`/`PurposePatternIsRegex` erweitern inkl. `ArgumentException`-Fallback | Offen | — |
| 17 | Logik | `SetupImportService.ImportVersion3`: `progress.Total` auf `42 - (replaceExisting ? 0 : 1)` korrigieren | Offen | — |
| 18 | Logik | `AppDbContext.ClearUserDataAsync`: `BudgetRules`-Filter auf `OwnerUserId == userId` vereinfachen; `total` auf 27 korrigieren | Offen | — |
| 19 | Tests | Neue Testklasse `BackupRestoreCoverageTests` mit Hilfsmethoden (`CreateDb`, `CreateBackupService`, `ReadBackupNdjsonAsync`) nach `SavingsPlanLinkBackupRestoreTests`-Muster anlegen | Offen | — |
| 20 | Tests | Test `ImportAsync_RestoresStatementImportsAndEntries_WithRemappedIds` schreiben | Offen | — |
| 21 | Tests | Test `ImportAsync_RestoresNotifications_WithOwnerUserId` schreiben | Offen | — |
| 22 | Tests | Test `ImportAsync_RestoresAccountShares_RemapsOwnAccount` schreiben | Offen | — |
| 23 | Tests | Test `ImportAsync_RestoresAccountLinkedIbans_SkipsUnmapped` schreiben | Offen | — |
| 24 | Tests | Test `ImportAsync_RestoresPortfolioKpiConfiguration_AsUpsert` schreiben | Offen | — |
| 25 | Tests | Test `RoundTrip_PreservesNewSections` (Export → `ClearUserDataAsync` → Import → Vergleich) schreiben | Offen | — |
| 26 | Tests | Test `BudgetRule_PurposePattern_SurvivesBackupRestore` schreiben | Offen | — |
| 27 | Tests | Test `Security_ErrorAndNotifiedFields_SurviveBackupRestore` schreiben | Offen | — |
| 28 | Tests | Test `ClearUserDataAsync_RemovesCategoryBoundBudgetRules` schreiben | Offen | — |
| 29 | Tests | Test `ImportAsync_OldBackupWithoutNewSections_StillWorks` (Abwärtskompatibilität) schreiben | Offen | — |
| 30 | Tests | `SetupImportServiceTests`: Test `ImportAsync_RestoresBudgetRulePattern` ergänzen | Offen | — |
| 31 | Tests | `BackupServiceFullExportTests`: Seeding + `map`-Tabelle um `AccountLinkedIbans` und `PortfolioKpiConfigurations` erweitern (Sonderfälle top-level DTO / kein `OwnerUserId`) | Offen | — |
| 32 | E2E-Tests | `ApiClientBackupsWithDemoDataTests`: `Snapshot`/`CaptureSnapshotAsync`/`AlignBeforeSnapshot`/`SortSnapshot` um `AccountLinkedIbans` und `PortfolioKpiConfigurations` erweitern; Business-Key-Remapping für `StatementImports`/`StatementEntries`/`AccountShares`; `BudgetRules`-Snapshot-Filter auf `OwnerUserId` umstellen; neue Entitäten im Test seeden | Offen | — |
