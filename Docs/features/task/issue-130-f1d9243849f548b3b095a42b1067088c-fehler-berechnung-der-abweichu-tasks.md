# Tasks: Fehlerhafte Berechnung von Budget- und Abweichungswerten im Budgetbericht

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | API-Logik | In `BudgetReportsController` Delta-Berechnung auf `Actual - Budget` vereinheitlichen | Offen | - |
| 2 | API-Logik | In `BudgetReportsController.GetAsync` Perioden-`Delta` und `DeltaPct` korrigieren | Offen | - |
| 3 | API-Logik | In `BudgetReportsController.GetAsync` Kategorie-Budget aus direkten Kategorie-Regeln plus Zweck-Regeln berechnen | Offen | - |
| 4 | API-Logik | In `BudgetReportsController.GetAsync` Zweck-`Delta` und `DeltaPct` korrigieren | Offen | - |
| 5 | API-Logik | In `BudgetReportsController.GetAsync` Kategorie-`Delta` und `DeltaPct` korrigieren | Offen | - |
| 6 | API-Logik | In `BudgetReportsController.GetAsync` Unbudgeted-Delta auf `Actual - Budget` ausrichten | Offen | - |
| 7 | API-Logik | In `BudgetReportsController.GetAsync` Summenbudget ohne Zweck-Doppelzaehlung berechnen | Offen | - |
| 8 | UI | In `BudgetReport.razor` lokale Prozentberechnung der Periodensumme auf `Math.Abs(sumBudget)` angleichen | Offen | - |
| 9 | UI | In `BudgetReport.razor` lokale Kategorie-/Zweck-Deltaberechnungen auf Doppelzaehlung und Delta-Richtung pruefen | Offen | - |
| 10 | Export | In `BudgetReportExportService.BuildPeriods` Delta und DeltaPct korrigieren | Offen | - |
| 11 | Export | In `BudgetReportExportService.BuildCurrentMonthRows` Kategorie-Budget aus direkten Kategorie-Regeln plus Zweck-Regeln berechnen | Offen | - |
| 12 | Export | In `CurrentMonthRow.CreateCategory` Delta und DeltaPct korrigieren | Offen | - |
| 13 | Export | In `CurrentMonthRow.CreatePurpose` Delta und DeltaPct korrigieren | Offen | - |
| 14 | Tests | Akzeptanzfall `Unterhaltung & Aktivitaeten` in `BudgetReportViewModelIntegrationTests` anlegen | Offen | - |
| 15 | Tests | `TotalRange`-Integrationstest um Kategorie-Budget, Actual und Delta erweitern | Offen | - |
| 16 | Tests | `LastInterval`-Integrationstest um Kategorie-Budget, Actual und Delta erweitern | Offen | - |
| 17 | Tests | Export-Test fuer XLSX-`CurrentMonth`-Kategorie-Budget und Delta ergaenzen | Offen | - |
| 18 | Tests | Erwartete Delta-Werte in betroffenen bestehenden Exporttests anpassen | Offen | - |
| 19 | Tests | Relevante Budgetbericht- und Exporttests ausfuehren und Ergebnisse dokumentieren | Offen | - |
