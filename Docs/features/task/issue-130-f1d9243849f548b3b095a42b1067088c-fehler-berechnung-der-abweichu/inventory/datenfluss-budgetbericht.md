# Datenfluss Budgetbericht

## Ablauf

1. Die UI-Seite `FinanceManager.Web/Components/Pages/BudgetReport.razor` verwendet `BudgetReportViewModel`.
2. `BudgetReportViewModel.LoadAsync` ruft `IApiClient.Budgets_GetReportAsync` mit `BudgetReportRequest` auf.
3. Der API-Endpunkt `BudgetReportsController.GetAsync` erzeugt die sichtbaren `BudgetReportDto`-Werte.
4. `BudgetReportsController.GetAsync` holt Rohdaten ueber `IBudgetReportService.GetRawDataAsync`.
5. `BudgetReportService.GetRawDataAsync` baut Kategorien, Zwecke, zugeordnete Postings und unbudgeted Postings auf.
6. Der Controller rechnet aus Rohdaten und DB-Regeln erneut die sichtbaren Perioden-, Kategorie- und Zweckwerte.
7. Das ViewModel mappt `BudgetReportDto` in `BudgetReportPeriodRow`, `BudgetReportCategoryRow` und `BudgetReportPurposeRow`.
8. Die Razor-Seite rendert die gelieferten Werte; einzelne Summen und Kategorie-Darstellungsfaelle werden lokal neu berechnet.

## Beobachtung

Die Rohdatenebene kennt bereits Kategorie- und Zweck-Budgets getrennt:

- `BudgetReportCategoryRawDataDto.BudgetedIncome`, `BudgetedExpense`, `BudgetedTarget`
- `BudgetReportPurposeRawDataDto.BudgetedIncome`, `BudgetedExpense`, `BudgetedTarget`

Der Controller verwendet fuer die sichtbaren Kategoriezeilen aber nicht diese Rohdaten-Budgetwerte, sondern filtert direkt die DB-Regeln:

- Kategorie-Budget: Regeln mit `BudgetCategoryId == cat.CategoryId`
- Zweck-Budget: Regeln mit `BudgetPurposeId == pur.PurposeId`

Damit fallen Zweck-Budgets in der Kategoriezeile heraus, obwohl die Kategorie-Istwerte aus Zweck-Postings addiert werden.

## Datenmodell-Relevanz

Ein Budget kann auf zwei Arten zur Kategorie gehoeren:

- Direkt ueber eine Regel mit `BudgetCategoryId`.
- Indirekt ueber einen Budget-Zweck, der `BudgetCategoryId` gesetzt hat, waehrend die Regel an `BudgetPurposeId` haengt.

Der Fehlerfall entspricht dem zweiten Pfad.
