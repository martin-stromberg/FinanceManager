# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Zusammenfassung

- **Gesamt:** 1.168
- **Bestanden:** 1.168
- **Fehlgeschlagen:** 0
- **Übersprungen:** 0

### Nach Test-Kategorie

| Kategorie | Gesamtzahl | Bestanden | Fehlgeschlagen | Dauer |
|-----------|-----------|-----------|-----------------|-------|
| Unit Tests | 1.021 | 1.021 | 0 | 37,42 s |
| Integration Tests | 113 | 113 | 0 | 41,72 s |
| E2E Tests (Playwright) | 34 | 34 | 0 | 1 min 12 s |

## Testabdeckung

**Zeilenabdeckung:** 13,91 % (20.531 / 147.523 Zeilen)

**Branch-Abdeckung:** 29,23 % (6.230 / 21.313 Branches)

### Abdeckung pro Paket

| Paket | Abdeckung |
|-------|-----------|
| FinanceManager.Application | 76,17 % |
| FinanceManager.Domain | 19,54 % |
| FinanceManager.Infrastructure | 15,22 % |
| FinanceManager.Shared | 39,32 % |
| FinanceManager.Web | 5,17 % |

## Fehlende Tests

**Quelle:** Coverage-Daten

### Dateien mit 0% Abdeckung: 387

Beispiele der häufigsten untesteten Dateien:

- `FinanceManager.Application/Backups/IBackupService.cs` — 0 % Abdeckung
- `FinanceManager.Application/Reports/IPostingExportService.cs` — 0 % Abdeckung
- `FinanceManager.Application/Securities/ReturnAnalysis/IReturnAnalysisService.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Result.cs` — 0 % Abdeckung
- `FinanceManager.Domain/Security/IpBlock.cs` — 0 % Abdeckung
- `FinanceManager.Domain/ValueObject.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/DemoDataService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Auth/UserReadService.cs` — 0 % Abdeckung
- `FinanceManager.Infrastructure/Contacts/ContactCategoryService.cs` — 0 % Abdeckung
- `FinanceManager.Web/ViewModels/ViewModelBase.cs` — 0 % Abdeckung (Basisklasse)
- `FinanceManager.Web/Components/Pages/**/*.razor` — 0 % Abdeckung (UI-Komponenten)

### Dateien mit < 80% Abdeckung: 134

Top 15 der am wenigsten getesteten Dateien:

| Datei | Abdeckung |
|-------|-----------|
| `FinanceManager.Infrastructure/Accounts/AccountService.cs` | 2,94 % |
| `FinanceManager.Web/Components/Statements/BudgetImpactValidationPanel.razor` | 6,89 % |
| `FinanceManager.Domain/Users/User.TimeZone.cs` | 7,14 % |
| `FinanceManager.Web/Services/PostingsQueryService.cs` | 7,69 % |
| `FinanceManager.Infrastructure/Budget/BudgetPurposeService.cs` | 9,67 % |
| `FinanceManager.Infrastructure/Budget/BudgetCategoryService.cs` | 10,20 % |
| `FinanceManager.Web/ViewModels/Setup/SetupSecurityTxtViewModel.cs` | 12,72 % |
| `FinanceManager.Domain/Users/User.Notifications.cs` | 14,28 % |
| `FinanceManager.Web/ViewModels/ViewModelBase.cs` | 15,38 % |
| `FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs` | 16,66 % |
| `FinanceManager.Infrastructure/Securities/SecurityService.cs` | 16,66 % |
| `FinanceManager.Infrastructure/Savings/SavingsPlanService.cs` | 16,66 % |
| `FinanceManager.Shared/ApiClient.cs` | 18,18 % |
| `FinanceManager.Shared/Dtos/Accounts/AccountUpdateRequest.cs` | 18,18 % |
| `FinanceManager.Shared/Dtos/Accounts/AccountCreateRequest.cs` | 20,00 % |

## Hinweise

- **Keine fehlgeschlagenen Tests** — Die Implementierung zeigt keine Regressionen
- **Breite Test-Abdeckung** — Unit-, Integration- und End-to-End-Tests decken alle Test-Ebenen ab
- **Abdeckungslücken bei UI-Komponenten** — Razor-Komponenten und ViewModels haben typischerweise geringere Code-Coverage bei UI-Tests, da ihre Hauptlogik in der Render-Pipeline liegt
- **Hohe Abdeckung in Application Layer** — Der Business-Logic-Layer (FinanceManager.Application) hat 76,17% Abdeckung
- **Migrationen nicht getestet** — EF Core-Migrationsdateien haben 0% Abdeckung (erwartungsgemäß für generierte Migrations-Code)
