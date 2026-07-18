# Berechnungsstellen

## API: `BudgetReportsController.GetAsync`

Kritische Stellen:

- Perioden: `budget = ComputeBudgetedAmountForPeriod(rules, periodFrom, periodTo)`, `delta = budget - actual`.
- Kategorie: `categoryRules = rules.Where(r => r.BudgetCategoryId == cat.CategoryId)`, danach `catBudget = ComputeBudgetedAmountForPeriod(categoryRules, categoryFrom, categoryTo)`.
- Zweck: `purposeRules = rules.Where(r => r.BudgetPurposeId == pur.PurposeId)`, danach `purBudget = ComputeBudgetedAmountForPeriod(purposeRules, categoryFrom, categoryTo)`.
- Kategorie-DTO: `Delta = catBudget - catActual`.
- Zweck-DTO: `Delta = purBudget - purActual`.
- Summe: `sumBudget = categories.Sum(c => c.Budget + c.Purposes.Sum(p => p.Budget))`, danach `sumDelta = sumBudget - sumActual`.

Hauptbefund:

`catBudget` enthaelt nur direkte Kategorie-Regeln. Bei Kategorien, deren Budgets in Detailpositionen bzw. Kontakten/Zwecken geplant sind, bleibt `catBudget` deshalb `0`, obwohl die zugeordneten Zwecke Budgets haben.

Folge:

Bei Beispielwerten `Budget 0`, `Ist -30` ergibt die aktuelle Kategorie-Abweichung `0 - (-30) = 30`. Erwartet ist `Budget -40`, `Ist -30`, `Abweichung 10`, also `Ist - Budget`.

## Rohdaten: `BudgetReportService.GetRawDataAsync`

Wichtige Stellen:

- Wenn eine Kategorie keine eigenen Regeln hat, werden Zweck-Regeln ueber `BuildUncategorizedPurposeDtosAsync` verarbeitet.
- Zweck-Rohdaten enthalten dann `BudgetedTarget` aus `GetBudgetedAmountForPurposeAsync`.
- Kategorie-Rohdaten erhalten aktuell `BudgetedTarget` nur aus `GetBudgetedAmountForCategoryAsync`, also nur direkte Kategorie-Regeln.

Bewertung:

Der Rohdaten-Service bildet Zweck-Budgets korrekt auf Zweckebene ab. Kategorie-Rohdaten enthalten nicht automatisch die Summe ihrer Zweck-Budgets. Da der Controller ohnehin eigene Regelberechnung nutzt, ist der unmittelbar sichtbare Bug im Controller.

## UI: `BudgetReportViewModel` und `BudgetReport.razor`

`BudgetReportViewModel.LoadAsync` uebernimmt die API-Werte fuer Perioden, Kategorien und Zwecke ohne eigene Korrektur.

`BudgetReport.razor` rendert die meisten Delta-Werte aus dem ViewModel, berechnet aber einzelne Zeilen selbst:

- Periodensummen-Zeile: `sumDelta = sumActual - sumBudget`.
- Kategoriezeile mit Budget und Zweckzeilen: lokales `delta = sumActualPurposes - cat.Budget`.

Bewertung:

Die Razor-Seite ist teilweise schon in Richtung `Ist - Budget` unterwegs, aber nicht konsistent. Eine Korrektur nur im Controller kann trotzdem durch lokale UI-Berechnungen ueberlagert werden.

## Export: `BudgetReportExportService`

Der Export baut Monatsuebersicht und Current-Month-Zeilen separat:

- `BuildPeriods`: `delta = budget - actual`.
- `BuildCurrentMonthRows`: Kategorie- und Zweckbudgets werden wie im Controller aus direkten Regel-Filtern berechnet.
- `CurrentMonthRow.CreateCategory` und `CreatePurpose`: `delta = budget - actual`.

Bewertung:

Wenn der Budgetbericht-Export zur fachlichen Ausgabe gehoert, muss er analog zum Controller angepasst werden. Sonst zeigen UI und XLSX unterschiedliche Werte bzw. behalten den alten Fehler.

## Prozentwerte

Aktuelle Berechnung nutzt je nach Stelle Varianten wie:

- `(Budget - Ist) / Abs(Budget)`
- `(Ist - Budget) / Budget`

Bei negativen Budgets fuehren Nenner mit und ohne `Abs` zu unterschiedlichen Vorzeichen. Die Planung sollte festlegen, ob `DeltaPct` mit der neuen Abweichungsrichtung ebenfalls negiert wird und welcher Nenner fachlich gilt.
