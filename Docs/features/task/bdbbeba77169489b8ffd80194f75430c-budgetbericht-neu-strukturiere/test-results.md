# Test-Ergebnisse (Iteration 3, final)

## Ergebnis

**Status:** Fehler vorhanden (ausschließlich vorbestehende, feature-fremde E2E-Fehler)

## Fehlgeschlagene Tests

### FinanceManager.Tests.E2E (3 Fehler — vorbestehend, unabhängig vom Budgetbericht-Refactoring)

- **UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus** — System.TimeoutException: Timeout 10000ms exceeded waiting for Locator ".setup-update-tab [data-testid='update-status-value']"
- **UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate** — System.TimeoutException: Timeout 10000ms exceeded waiting for Locator ".setup-update-tab [data-testid='update-check-now']"
- **UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload** — Playwright strict mode violation: Locator ".setup-update-tab input[type=checkbox]" resolved to 2 elements instead of 1

Diese drei Tests betreffen den Admin/Update-Setup-UI-Tab und stehen in keinem Zusammenhang mit dem Budgetbericht. Sie wurden bereits in Iteration 2/3 als vorbestehend verifiziert und bewusst nicht angefasst.

## Zusammenfassung (verifiziert per direktem `dotnet test`-Lauf gegen den finalen Working-Tree-Stand)

- **FinanceManager.Tests** (Unit): 994/994 bestanden, 0 Fehler
- **FinanceManager.Tests.Integration**: 109/109 bestanden, 0 Fehler
- **FinanceManager.Tests.E2E**: 30/33 bestanden, 3 Fehler (vorbestehend, s. o.)

Die beiden zuvor in Iteration 2 gemeldeten echten Fehler sind in Iteration 3 behoben:
- `ApiClientBudgetReportUnbudgetedMirrorTests` — veraltete Testerwartung an neue, korrekte Kategorisierung (`UnbudgetedSelfCostNeutral`) angepasst.
- `ApiClientBudgetKpiContactsSetupTests.BudgetKpi_ContactsSetup_ShouldCreateAllContactsAndAccounts` — echter Berechnungsfehler behoben (Kostenneutral-Beträge fließen jetzt korrekt in `ActualIncome`/`ActualExpenseAbs` ein).

## Bekanntes, separates Problem außerhalb des Scopes

`BudgetReportEntry.BudgetedAmount` wird als Nettobetrag pro Zweck berechnet; bei Zwecken mit gemischten Vorzeichen-Regeln (Einnahme- und Ausgaberegel im selben Monat) verzerrt das `PlannedIncome`/`PlannedExpenseAbs`/`ExpectedExpenseAbs`. Dies ist ein vorbestehendes, eigenständiges Problem, das nicht Teil dieser Anforderung ist und separat nachverfolgt werden sollte.
