# KPI-Datenfluss und Systemgrenzen

## Aufrufkette

```text
MonthlyBudgetKpi
  -> MonthlyBudgetKpiViewModel.LoadAsync
  -> IApiClient.Budgets_GetMonthlyKpiAsync
  -> GET /api/budget/report/kpi-monthly
  -> BudgetReportsController.GetMonthlyKpiAsync
  -> IBudgetReportService.GetMonthlyKpiAsync
  -> BudgetReportService.BuildBudgetberichtAsync
  -> MonthlyBudgetKpiDto
```

## Client-Vertrag

`ApiClient.BudgetReport.cs` ruft den Endpoint mit `dateBasis=BookingDate` auf und mappt die Antwort auf `MonthlyBudgetKpiDto`. Das Interface `IApiClient` exponiert denselben Vertrag. Der Request akzeptiert ein optionales Datum und ein CancellationToken.

## Backend-Vertrag

`BudgetReportService.GetMonthlyKpiAsync` bildet den aktuellen Monat, baut den `Budgetbericht` aus Kategorien, Zwecken, Regeln und Buchungen auf und mappt das Ergebnis. Diese potentiell zeitintensive Berechnung ist der Grund fuer das asynchrone UI-Verhalten, soll aber in dieser Anforderung nicht optimiert oder fachlich veraendert werden.

## Fehler- und Leerdatenstatus

Das ViewModel behandelt `HttpRequestException` mit `api.LastError` und laesst `DataLoaded` auf `false`. Unerwartete Exceptions werden aktuell weitergereicht. Die Anforderung spezifiziert das Verhalten bei Fehlern und leeren Daten nicht; der Plan muss deshalb entscheiden, ob der bestehende Fehlerzustand beibehalten und fuer den Hintergrundaufruf sichtbar gemacht wird.
