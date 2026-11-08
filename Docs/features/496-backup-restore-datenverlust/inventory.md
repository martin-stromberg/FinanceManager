# Bestandsaufnahme: Backup/Restore – Datenverlust durch fehlende Import- und DTO-Abdeckung (Issue #496)

Analysiert wurde der bestehende Backup-/Restore-Pfad (`BackupService` → `SetupImportService` → `AppDbContext.ClearUserDataAsync`) sowie die zugehörigen Datenmodellklassen, Backup-DTOs, Enums, Interfaces und Tests — bezogen auf die Anforderung in `requirement.md`.

## Zusammenfassung

- **Export vorhanden, Import fehlt (Datenverlust beim Restore):** `BackupService.BuildBackupDataAsync` (`FinanceManager.Infrastructure/Backups/BackupService.cs`, Z. 568) exportiert `StatementImports`, `StatementEntries`, `Notifications` und `AccountShares`; `SetupImportService.ImportVersion3` (`FinanceManager.Infrastructure/Setup/SetupImportService.cs`, Z. 196) stellt keine dieser vier Sektionen wieder her. `AppDbContext.ClearUserDataAsync` (`FinanceManager.Infrastructure/AppDbContext.cs`, Z. 970) löscht alle vier — jeder Restore mit `replaceExisting: true` vernichtet sie damit aktuell unwiederbringlich.
- **Ganz ohne Backup-Abdeckung:** `AccountLinkedIbans` (Unter-IBANs von Sammelkonten, werden per DB-Kaskade mit dem Konto gelöscht, `AppDbContext` Z. 463–466) und `PortfolioKpiConfigurations` (eine Zeile pro Benutzer, Unique-Index auf `OwnerUserId`, `AppDbContext` Z. 725; wird von `ClearUserDataAsync` nicht gelöscht) fehlen vollständig im Export und Import.
- **DTO-Lücken:** `BudgetRuleBackupDto` (`BudgetRule.cs` Z. 223–232) enthält die persistierten Felder `PurposePattern` (Z. 77) und `PurposePatternIsRegex` (Z. 82) nicht. `SecurityBackupDto` (`Security.cs` Z. 271) enthält `HasPriceError`, `PriceErrorMessage`, `PriceErrorSinceUtc`, `GoldenCrossNotifiedPhase` und `TrendReversalNotifiedStatus` nicht. Domain-Setter zum Wiederherstellen existieren bereits (`SetPurposePattern`, `SetPriceError` — stempelt `UtcNow`, `SetGoldenCrossNotifiedPhase`, `SetTrendReversalNotifiedStatus`).
- **Vorhandene DTO-Infrastruktur:** `StatementImportBackupDto`, `StatementEntryBackupDto`, `NotificationBackupDto`, `AccountShareBackupDto` und `AccountLinkedIbanBackupDto` existieren bereits; `ToBackupDto()`/`AssignBackupDto()` fehlen nur bei `AccountLinkedIban` (DTO ist top-level, nicht nested) und `PortfolioKpiConfiguration` (gar kein DTO).
- **`ClearUserDataAsync`-Lücke:** Die `BudgetRules`-Löschung (Z. 1134–1144) filtert nur `BudgetPurposeId`-gebundene Regeln; rein `BudgetCategoryId`-gebundene Regeln überleben den Clear.
- **MCP-Sektionen:** `McpAccesses`, `McpAccessScopes`, `McpAuditEntries`, `McpAuditEntryChanges`, `AiActionRequests` werden exportiert, aber bewusst nicht importiert und nicht gelöscht (Kommentare `SetupImportService.cs` Z. 1232–1236, `AppDbContext.cs` Z. 981–985).
- **`StatementEntry.SecurityTransactionId`:** Wird nur gelesen (`StatementImportQueryService` Z. 114, `FmStatementImportsReviewTool` Z. 116), nirgends im Produktivcode geschrieben (außer `AssignBackupDto`); kein FK konfiguriert.
- **Tests:** Ein Export-Abdeckungs-Guard (`BackupServiceFullExportTests`), Service-Tests (`BackupServiceTests`), ein Import-Test (`SetupImportServiceTests`) und ein Round-Trip-Muster (`SavingsPlanLinkBackupRestoreTests`) existieren; Round-Trip-Tests für die betroffenen Sektionen fehlen noch.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
