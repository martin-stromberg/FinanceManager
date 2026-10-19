# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### StatementDraftExportService.cs (StatementDraftExportService)

- **Doppelter Code** — `StreamCsvAsync` baut das CSV-Spaltenarray inline (Zeilen 279–299) mit exakt denselben 18 Spalten und identischer Formatierung wie `CreateCsvColumns` (Zeilen 325–345). Der einzige Unterschied ist die Herkunft der drei aufgelösten Namen (`r.ContactName`/`r.SavingsName`/`r.SecurityName` aus dem Join vs. `con`/`sav`/`sec` aus den Dictionaries). Spaltenänderungen oder Formatierungsfixes müssen weiterhin an zwei Stellen gepflegt werden.

  Empfehlung: `CreateCsvColumns` so umbauen, dass es die drei bereits aufgelösten Namen als Parameter entgegennimmt (z. B. `CreateCsvColumns(StatementDraftEntryExportRow r, string contactName, string savingsName, string securityName)` oder die Rohfelder), und in `StreamCsvAsync` mit den Join-Namen aufrufen; die Dictionary-Auflösung in `GenerateAsync` bleibt beim Aufrufer.

### PostingExportService.cs (PostingExportService)

- **Doppelter Code** — Gleiches Muster wie im neuen Service: `StreamCsvAsync` erzeugt das 13-spaltige CSV-Array inline (Zeilen 364–379), während das im Refactoring neu eingeführte `CreateCsvColumns` (Zeilen 408–423) dieselbe Spaltenfolge und Formatierung enthält; nur die Namensauflösung (Join-Felder vs. Dictionary-Lookups) unterscheidet sich. Der Extraktionsschritt hat die Duplikation zwischen Streaming- und Buffered-Pfad nicht beseitigt.

  Empfehlung: Analog `CreateCsvColumns` auf Parameter mit bereits aufgelösten Namen umstellen und aus beiden Pfaden (`StreamCsvAsync`, `GenerateAsync`) aufrufen.

### StatementDraftsController.cs (StatementDraftsController)

- **Toter Code** — Das im Branch neu hinzugefügte `using System.Globalization;` (Zeile 22) wird nicht verwendet: Der einzige `CultureInfo`-Zugriff in der Datei (Zeile 141) ist vollqualifiziert (`System.Globalization.CultureInfo.CurrentCulture`). Der unnötige using-Eintrag erzeugt eine IDE0005-Warnung.

  Empfehlung: `using System.Globalization;` entfernen.

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Application/Reports/IStatementDraftExportService.cs` (neu)
- `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` (neu)
- `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` (neu)
- `FinanceManager.Shared/Dtos/Statements/StatementDraftExportDtos.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs` (neu)
- `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs` (neu)
- `FinanceManager.Web/Infrastructure/ExportRequestHelper.cs` (neu)
- `FinanceManager.Infrastructure/Reports/PostingExportService.cs` (geändert)
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` (geändert)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs` (geändert)
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs` (geändert)
- `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs` (geändert)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (geändert)
- `FinanceManager.Web/Controllers/PostingsController.cs` (geändert)
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` (geändert)
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` (geändert)
