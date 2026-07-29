# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine — alle Tests bestanden.

## Zusammenfassung

- Gesamt: 1110
- Bestanden: 1110
- Fehlgeschlagen: 0
- Übersprungen: 1

### Nach Test-Suite

| Test-Suite | Bestanden | Übersprungen | Gesamt | Dauer |
|------------|-----------|--------------|--------|-------|
| FinanceManager.Tests | 891 | 0 | 891 | 43 s |
| FinanceManager.Tests.Integration | 103 | 0 | 103 | 70 s |
| SoftwareSchmiede.AutoUpdate.Tests | 88 | 1 | 89 | 643 ms |
| FinanceManager.Tests.E2E | 28 | 0 | 28 | 68 s |

## Testabdeckung

**Gesamtabdeckung:** 12.39 % (Line Coverage)

| Paket | Zeilenabdeckung | Branchcoverage |
|-------|-----------------|----------------|
| FinanceManager.Application | 74.59 % | 77.64 % |
| FinanceManager.Domain | 69.68 % | - |
| FinanceManager.Shared | 39.21 % | - |
| FinanceManager.Web | 33.81 % | - |
| SoftwareSchmiede.AutoUpdate | 7.74 % | - |
| FinanceManager.Infrastructure | 6.74 % | - |

## Fehlende Tests

Quelle: `Coverage-Daten`

**Insgesamt 494 Dateien mit 0 % Zeilenabdeckung.**

Top 30 (nach Dateiname):

- `FinanceManager.Application/BackgroundTaskRunner.cs` — 0 % Abdeckung
- `FinanceManager.Application/Backups/IBackupService.cs` — 0 % Abdeckung
- `FinanceManager.Application/Reports/IPostingExportService.cs` — 0 % Abdeckung
- `FinanceManager.Application/Securities/ReturnAnalysis/IReturnAnalysisService.cs` — 0 % Abdeckung
- `FinanceManager.Application/Statements/Dtos/BatchUpdateDtos.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Accounts/AccountLinkedIban.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Budget/BudgetCategory.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Budget/BudgetOverride.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Budget/BudgetPurpose.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Budget/BudgetRule.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Result.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Security/IpBlock.cs` — 0 % Abdeckung
- `FinanceManager.Domain/ValueObject.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Accounts/AccountService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/AppDbContext.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Attachments/AttachmentCategoryService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Attachments/AttachmentService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/DemoDataService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/UserAdminService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/UserAuthService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/UserReadService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Backups/BackupService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Budget/BudgetPlanningRepository.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Budget/ReportCacheService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Common/ParentAssignmentService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Contacts/ContactCategoryService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Contacts/ContactService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Data/Migrations/Identity/20251027051958_20251027_AddIdentityUsers.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Data/Migrations/Identity/20251027051958_20251027_AddIdentityUsers.Designer.cs` — 0 % Abdeckung

---

**Hinweis:** Die hohe Zahl ungetesteter Dateien ist auf Komponenten-Views (UI), Datenbankmigrationen und Legacy-Code zurückzuführen, deren Unit-Tests weniger sinnvoll sind. Der Kern der Business-Logik (Application, Domain) ist mit 69–75 % Coverage abgedeckt.
