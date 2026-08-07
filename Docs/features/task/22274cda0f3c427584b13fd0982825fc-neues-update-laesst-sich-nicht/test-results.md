# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### FinanceManager.Tests

- **Infrastructure.Budget.BudgetReportServiceRawDataTests.GetRawDataAsync_ShouldExposeUnvaluedMatchingPostings_WhenPurposeUsesExactPostings** — Expected purposePostings.Where(x => x.IsValuedForBudgetPurpose).Sum(x => x.Amount) to contain a single item matching (x.PostingId == 9abaf16d-6695-4893-a805-273fde980490), but the collection is empty.
- **Budget.BudgetReportServiceTests.Test_GetRawData_ForEntireMonthAsync** — Expected string to differ at column 1 of line 14: Actual is missing "2441.43, Employer," line item

### FinanceManager.Tests.Integration

- **ApiClient.ApiClientBudgetReportUnbudgetedMirrorTests.BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact** — Expected collection to have an item matching (Kind == UnbudgetedPostings), but item was not found
- **ViewModels.BudgetReportViewModelIntegrationTests.ShowPurposePostingsAsync_ShouldMarkMatchingUnvaluedPostings_WhenPurposeUsesExactPostings** — Expected vm.PurposePostings to contain a single item matching (p.Posting.Amount == 9,40), but the collection is empty.
- **ApiClient.ApiClientBudgetKpiContactsSetupTests.BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts** — Expected kpi.ActualExpenseAbs to be 2817.56M, but found 2725.96M

### FinanceManager.Tests.E2E

- **UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus** — Timeout 10000ms exceeded waiting for Locator(".setup-update-tab [data-testid='update-status-value']")

## Zusammenfassung

- Gesamt: 1084
- Bestanden: 1075
- Fehlgeschlagen: 9
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 12.55 % (Durchschnitt)

| Paket | Zeilenabdeckung |
|-------|-----------------|
| FinanceManager.Infrastructure | 6.70 % |
| FinanceManager.Web | 34.43 % |
| FinanceManager.Shared | 65.51 % |
| FinanceManager.Domain | 73.72 % |
| FinanceManager.Application | 74.42 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `FinanceManager.Infrastructure` — 6.70 % Abdeckung (kritisch niedrig)
- `FinanceManager.Web` — 34.43 % Abdeckung
- `FinanceManager.Shared` — 65.51 % Abdeckung
