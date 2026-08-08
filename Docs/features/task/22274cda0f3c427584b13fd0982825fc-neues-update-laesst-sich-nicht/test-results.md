# Test-Ergebnisse

## Ergebnis

**Status:** Fehler vorhanden

## Fehlgeschlagene Tests

### Unit Tests (FinanceManager.Tests)

- **Test_GetRawData_ForEntireMonthAsync** — Expected actualUnbudgetPostings to be a match with the expectation, but it differs at column 1 of line 14

### Integration Tests (FinanceManager.Tests.Integration)

- **BudgetReport_UnbudgetedPostings_ShouldOnlyContainNonMirroredSelfContactPostings_WhenSavingsPlanPostingsMirrorSelfContact** — Expected report.Categories to have an item matching (Convert(c.Kind, Int32) == 2)
- **ShowPurposePostingsAsync_ShouldMarkMatchingUnvaluedPostings_WhenPurposeUsesExactPostings** — Expected vm.PurposePostings to contain a single item matching (p.Posting.Amount == 9,40), but the collection is empty
- **InitializeAsync_TotalRange_ShouldShowHousingBookingsBudgeted_AndTrafficBookingUnbudgeted_WhenLoadedForCurrentYear** — Expected unbudgeted.Actual[0] to be -169.90M, but found -49.9M (difference of 120.00)
- **BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts** — Expected kpi.ActualExpenseAbs to be 2817.56M, but found 2725.96M (difference of -91.60)

### E2E Tests (FinanceManager.Tests.E2E)

- **Admin_OpensUpdateTab_ShowsStatus** — Timeout 10000ms exceeded. waiting for Locator(".setup-update-tab [data-testid='update-status-value']")
- **Admin_TriggersCheck_ShowsAvailableUpdate** — Timeout 10000ms exceeded. waiting for Locator(".setup-update-tab [data-testid='update-check-now']")
- **Admin_SavesSettings_PersistsAcrossReload** — Error: strict mode violation: Locator(".setup-update-tab input[type=checkbox]") resolved to 2 elements

## Zusammenfassung

- Gesamt: 1094
- Bestanden: 1085
- Fehlgeschlagen: 9
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 14.1 %

| Projekt | Abdeckung |
|-------|-----------|
| FinanceManager.Application | 76.17 % |
| FinanceManager.Domain | 67.75 % |
| FinanceManager.Shared | 35.74 % |
| FinanceManager.Web | 32.15 % |
| FinanceManager.Infrastructure | 7.94 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `FinanceManager.Infrastructure` — 7.94 % Abdeckung (kritisch niedrig)
- `FinanceManager.Web` — 32.15 % Abdeckung (unter 80%)
- `FinanceManager.Shared` — 35.74 % Abdeckung (unter 80%)
