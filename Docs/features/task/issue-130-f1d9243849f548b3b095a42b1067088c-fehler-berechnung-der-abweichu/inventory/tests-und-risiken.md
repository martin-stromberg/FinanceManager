# Test- und Risikoanalyse

## Bestehende Tests

`FinanceManager.Tests/Budget/BudgetReportServiceTests.cs`

- Deckt Rohdatenaufbau, Budget-Regeln, Posting-Allokation und KPI-Berechnung umfangreich ab.
- Enthaelt Szenarien mit Kategorie-Budgets und Zweck-Budgets.
- Prueft vor allem `GetRawDataAsync` und nicht die finale sichtbare `BudgetReportDto`-Kategoriezeile aus dem API-Controller.

`FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs`

- Erzeugt Kategorie `Wohnen` mit Zwecken `Miete` und `Strom`.
- Budget-Regeln haengen an den Zwecken, nicht direkt an der Kategorie.
- Prueft aktuell Zweck-Budget und Zweck-Ist (`Miete`, `Strom`) sowie unbudgeted Werte.
- Prueft nicht, dass die Kategorie `Wohnen` selbst das aufsummierte Budget der Zwecke zeigt.

## Empfohlene Tests

Minimaler Regressionstest im bestehenden Integrationstest:

- Kategorie mit mehreren Zwecken anlegen.
- Regeln nur an den Zwecken hinterlegen.
- Buchungen nur fuer einige Zwecke erfassen.
- `BudgetReportViewModel` laden.
- Kategoriezeile pruefen:
  - `Budget == Summe der Zweckbudgets`
  - `Actual == Summe der Zweck-Istwerte`
  - `Delta == Actual - Budget`

Konkreter Akzeptanzfall:

- Kategorie `Unterhaltung & Aktivitaeten`
- Zwecke:
  - `Fitnessstudio`, Budget `-15`, Ist `-15`, Delta `0`
  - `Gluecksspiel`, Budget `-15`, Ist `-15`, Delta `0`
  - `Streaming`, Budget `-10`, Ist `0`, Delta `10`
- Kategorie:
  - Budget `-40`
  - Ist `-30`
  - Delta `10`

Zusaetzliche Testfaelle:

- Kategorie mit direkten Kategorie-Regeln und Zweck-Regeln, um Addition bzw. Vorrangregel abzusichern.
- `BudgetReportValueScope.TotalRange` und `LastInterval`, da beide im Controller dieselbe Kategorieformel mit unterschiedlichem Zeitraum nutzen.
- Export-Current-Month, falls XLSX-Werte Teil der fachlichen Ausgabe sind.

## Risiken

- `sumBudget = categories.Sum(c => c.Budget + c.Purposes.Sum(p => p.Budget))` kann nach einer Kategorie-Budget-Korrektur doppelt zaehlen, wenn Kategorie-Budget bereits Zweck-Budgets enthaelt. Die Summenzeile muss dann angepasst werden.
- KPI-Berechnung in `BudgetReportService.GetMonthlyKpiAsync` addiert aktuell Kategorie- und Zweckbudgets separat. Eine Aenderung der Rohdaten-Kategorie-Budgets kann dort zu Doppelzaehlung fuehren, falls Rohdaten angepasst werden statt nur der Controller-Ausgabe.
- Export und UI nutzen eigene Berechnungen; eine Korrektur nur an einer Stelle erzeugt Inkonsistenzen.
- Cache: `BudgetReportService.GetRawDataAsync` nutzt `IReportCacheService`, der Controller ruft fuer den Report aber `ignoreCache: true` auf. Aenderungen an Rohdaten koennen trotzdem andere Endpunkte wie KPI oder Export beeinflussen.
