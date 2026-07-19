# Bestandsaufnahme

## Betroffene Stellen

- `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs`
  - `ShowPurposePostingsAsync` oeffnet die Postenauflistung fuer einen Budgetzweck.
  - Lokale Vorarbeit fuehrte einen ViewModel-Cache fuer `Budgets_GetReportRawAsync` ein.
  - Der erste Overlay-Klick bleibt langsam, wenn der Server den Rawbericht erneut berechnet.

- `FinanceManager.Web/Controllers/BudgetReportsController.cs`
  - `GetAsync` baut den Budgetbericht aus `GetRawDataAsync(..., ignoreCache: true)`.
  - `GetRawAsync` liefert Rohdaten fuer die UI, ruft aber ebenfalls `ignoreCache: true` auf.
  - Dadurch wird beim Overlay-Rohdatenabruf der vorhandene Report-Cache umgangen.

- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`
  - `GetRawDataAsync` nutzt den Report-Cache nur bei `ignoreCache: false`.
  - Auch bei `ignoreCache: true` wird das berechnete Ergebnis am Ende wieder in den Cache geschrieben.

- `FinanceManager.Web/Components/Pages/BudgetReport.razor`
  - Die Postenauflistung verwendet bereits `IsValuedForBudgetPurpose`, um nicht gewertete Posten mit einer Klasse auszugeben.

- `FinanceManager.Web/wwwroot/css/app.BudgetReport.css`
  - Lokale Vorarbeit enthaelt CSS fuer Tabellenkopf, nicht budgetierte Zeile und Badge.
  - Der neue Wunsch verlangt explizit `opacity: 0.8` bei nicht budgetierten Zeilen.

- `FinanceManager.Web/wwwroot/css/theme.Dark.BudgetReport.css`
  - Dark-Theme-Kontraste fuer Tabellenkopf, nicht budgetierte Zeile und Badge sind separat definiert.

- `FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs`
  - Enthalten ist bereits ein Test, der `IsValuedForBudgetPurpose` fuer genaue Buchungen prueft.
  - Ein zusaetzlicher Test kann absichern, dass nach geladenem Report nur ein Rawdatenabruf fuer mehrere Zweck-Overlays erfolgt.

## Risiken

- Wird der Raw-Endpunkt dauerhaft ungecached genutzt, bleibt der erste Klick langsam.
- Wird die Budgetwertungsinformation nicht aus Rohdaten gewonnen, kann die Anzeige fachlich falsch werden.
- Zu schwache Farben plus reduzierte Deckkraft verschlechtern die Lesbarkeit.

## Empfehlung

Den bestehenden ViewModel-Cache behalten und serverseitig `GET /api/budget/report/raw` beziehungsweise den Raw-POST-Endpunkt so nutzen, dass er vorhandene valide Cache-Eintraege wiederverwendet. Der normale Tabellenabruf befuellt den Cache bereits durch den Rawdatenaufbau. Die Darstellung der nicht budgetierten Zeilen wird ueber CSS auf `opacity: 0.8` und kontrastreiche Farben angepasst.
