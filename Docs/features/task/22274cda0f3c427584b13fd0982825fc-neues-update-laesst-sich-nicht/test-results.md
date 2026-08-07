# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### FinanceManager.Tests

- **Infrastructure.Budget.BudgetReportServiceRawDataTests.GetRawDataAsync_ShouldExposeUnvaluedMatchingPostings_WhenPurposeUsesExactPostings** — Expected result.UnbudgetedPostings to contain a single item matching (x.PostingId == 6b2d55b9-98db-44db-b9ec-9c25d40a538e), but the collection is empty.
- **Budget.BudgetReportServiceTests.Test_GetRawData_ForEntireMonthAsync** — Expected actualUnbudgetPostings to be a match with the expectation, but it differs at column 1 of line 14 (index 223).

### FinanceManager.Tests.Integration

- **ApiClient.ApiClientBudgetReportUnbudgetedMirrorTests.BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact** — Expected report.Categories to have an item matching (Convert(c.Kind, Int32) == 2).
- **ViewModels.BudgetReportViewModelIntegrationTests.ShowPurposePostingsAsync_ShouldMarkMatchingUnvaluedPostings_WhenPurposeUsesExactPostings** — Expected vm.PurposePostings to contain a single item matching (p.Posting.Amount == 9,40), but the collection is empty.
- **ApiClient.ApiClientBudgetKpiContactsSetupTests.BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts** — Expected kpi.ActualExpenseAbs to be 2817.56M, but found 2725.96M (difference of -91.60).
- **ViewModels.BudgetReportViewModelIntegrationTests.InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear** — Expected unbudgeted.Actual[0] to be -169.90M, but found -49.9M (difference of 120.00).

### FinanceManager.Tests.E2E

- **UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus** — Timeout 10000ms exceeded waiting for Locator(".setup-update-tab [data-testid='update-status-value']").
- **UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate** — Timeout 10000ms exceeded waiting for Locator(".setup-update-tab [data-testid='update-check-now']").
- **UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload** — PlaywrightException: strict mode violation, Locator(".setup-update-tab input[type=checkbox]") resolved to 2 elements.

## Zusammenfassung

- Gesamt: 1085
- Bestanden: 1076
- Fehlgeschlagen: 9
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 84.92 % (Durchschnitt, gewichtet über alle Testprojekte)

| Paket | Zeilenabdeckung |
|-------|-----------------|
| FinanceManager.Web | 42.98 % |
| FinanceManager.Shared | 72.30 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `FinanceManager.Web` — 42.98 % Abdeckung; 0 % Zeilenabdeckung u. a. in `ReportDashboard.razor`, `BudgetPurposeCardViewModel.cs`, `BudgetRuleCardViewModel.cs`, `QuickEditTable.razor`, `BudgetReport.razor`, `Home.razor`, `MainLayout.razor`, `FileLoggerProvider.cs` (insgesamt 96 Dateien mit 0 % im Web-Projekt; viele davon sind Blazor-Komponenten, die nur über die fehlgeschlagenen Playwright/E2E-Tests erreicht werden und dort keine Coverage-Instrumentierung liefern)
- `FinanceManager.Shared` — 72.30 % Abdeckung; 0 % Zeilenabdeckung u. a. in `ApiClient.Attachments.cs`
- `FinanceManager.Infrastructure` — 95.27 % Abdeckung insgesamt, aber 0 % in `NagerDateHolidayProvider.cs`, `DemoDataService.cs`, `InMemoryHolidayProvider.cs`, Migration `20260719090000_ProtectAlphaVantageApiKeys.cs`
