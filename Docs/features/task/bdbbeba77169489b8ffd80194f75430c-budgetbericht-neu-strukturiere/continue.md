# Offene Aufgaben

Erstellt am: 2026-08-08
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` (Iteration 3) hat den Status „Vollständig umgesetzt".

## Code-Review-Befunde

- [ ] `BudgetberichtMapper`: Kategorie-direkte Regeln werden auf Mapper-Ebene nicht dediziert getestet (`accumulator.Income`/`accumulator.Expense` bei direkten Kategorie-Regeln ohne Zweck).
- [ ] `BudgetberichtMapper`: Die einnahmenseitige Restplanungs-/Erwartungsberechnung (`RemainingPlannedIncome`/`ExpectedIncome`) wird nur ausgabenseitig getestet, nicht einnahmenseitig.

## Fehlgeschlagene Tests

- [ ] `UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` — System.TimeoutException: Timeout 10000ms exceeded waiting for Locator ".setup-update-tab [data-testid='update-status-value']" — **vorbestehend, unabhängig vom Budgetbericht-Refactoring** (Admin/Update-Setup-UI); in Iteration 2 und 3 verifiziert und bewusst nicht angefasst.
- [ ] `UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` — System.TimeoutException: Timeout 10000ms exceeded waiting for Locator ".setup-update-tab [data-testid='update-check-now']" — **vorbestehend, unabhängig vom Budgetbericht-Refactoring**.
- [ ] `UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — Playwright strict mode violation: Locator ".setup-update-tab input[type=checkbox]" resolved to 2 elements instead of 1 — **vorbestehend, unabhängig vom Budgetbericht-Refactoring**.

## Zusätzlich bekanntes, separates Problem (außerhalb des Scopes dieser Anforderung)

`BudgetReportEntry.BudgetedAmount` wird als Nettobetrag pro Zweck berechnet; bei Zwecken mit gemischten Vorzeichen-Regeln (Einnahme- und Ausgaberegel im selben Monat) verzerrt das `PlannedIncome`/`PlannedExpenseAbs`/`ExpectedExpenseAbs`. Dieses Problem existierte unabhängig von diesem Refactoring nicht in dieser Form vorher (neue Struktur), ist aber nicht Teil des ursprünglichen Anforderungskatalogs und sollte als eigene, separate Anforderung nachverfolgt werden.
