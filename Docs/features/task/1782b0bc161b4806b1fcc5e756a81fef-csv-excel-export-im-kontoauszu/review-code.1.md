# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### StatementDraftExportService.cs (StatementDraftExportService)

- **Doppelter Code** — Die Methoden `CsvEscape` (Zeilen 364–369), `CreateRow` (Zeilen 436–473), `WriteCsvAsync` (Zeilen 317–357) und `WriteXlsx` (Zeilen 380–429) sowie das Namensauflösungs-Muster in `GenerateAsync` (Zeilen 151–172) sind nahezu wortgleiche Kopien derselben Logik aus `FinanceManager.Infrastructure/Reports/PostingExportService.cs` (Zeilen 402–549). Fehlerkorrekturen oder Erweiterungen (z. B. CSV-Escaping) müssen künftig an zwei Stellen gepflegt werden.

  Empfehlung: Gemeinsame Export-Helfer (CSV-/XLSX-Schreiblogik, `CsvEscape`, `CreateRow`, Namensauflösung) in eine geteilte interne Hilfsklasse im `Reports`-Ordner extrahieren und von beiden Services nutzen.

- **God-Methode** — `GenerateAsync` (Zeilen 136–204, ~69 Zeilen) führt drei konzeptuell getrennte Aufgaben hintereinander aus: Zeilen einsammeln inkl. Overflow-Erkennung, Namensauflösung für drei Entitätstypen (Contacts, SavingsPlans, Securities) sowie Format-Dispatch und Datei-Erzeugung.

  Empfehlung: Die Namensauflösung (Zeilen 151–172) in eine eigene Methode (z. B. `ResolveEntityNamesAsync`) auslagern; ggf. im Zuge des Duplikat-Befunds gemeinsam mit dem `PostingExportService` lösen.

### StatementDraftsController.cs (StatementDraftsController)

- **Doppelter Code** — `TryParseFormat` (Zeilen 792–800), `SanitizeFileName` (Zeilen 802–809) und der `Exports:MaxRows`-Konfigurationsblock (Zeilen 728–733) sind identische Kopien aus `FinanceManager.Web/Controllers/PostingsController.cs` (Zeilen 448–454 und 539–556).

  Empfehlung: Die drei Bausteine in eine gemeinsame Hilfsklasse (z. B. statische `ExportRequestHelper` in `FinanceManager.Web.Infrastructure`) auslagern und in beiden Controllern verwenden.

### CardPage.razor

- **Fehlerbehandlung / Kodierung** — In den Fällen `"ExportCsv"`/`"ExportXlsx"` werden die Query-Parameter `from` und `to` ohne `Uri.EscapeDataString` in die URL eingesetzt (Zeilen 282–283), während `format` und `q` kodiert werden. Das `:O`-Format kann je nach `DateTimeKind` Sonderzeichen enthalten (bei `Local` ein `+hh:mm`-Offset; `+` wird im Query-String als Leerzeichen dekodiert), wodurch das serverseitige Datums-Binding fehlschlagen oder falsche Werte liefern kann.

  Empfehlung: `Uri.EscapeDataString` auch für `from`/`to` anwenden, z. B. `$"from={Uri.EscapeDataString(embedded.RangeFrom.Value.ToString("O", CultureInfo.InvariantCulture))}"`.

### StatementDraftExportServiceTests.cs (StatementDraftExportServiceTests)

- **Testqualität** — `StreamCsvAsync_ShouldRespectFromToAndQ` (Zeilen 125–145) prüft zwei fachlich getrennte Fälle in einer Testmethode: den `From`/`To`-Datumsfilter und die `Q`-Volltextsuche.

  Empfehlung: In zwei getrennte Tests aufteilen (z. B. `StreamCsvAsync_ShouldRespectFromToFilter` und `StreamCsvAsync_ShouldRespectSearchQuery`).

### StatementDraftsControllerTests.cs (StatementDraftsControllerTests)

- **Testqualität** — `ExportAsync_ShouldReturnNotFound_ForUnknownOrForeignDraft` prüft zwei fachlich getrennte Fälle in einer Testmethode: eine unbekannte Draft-ID und ein Draft eines anderen Benutzers.

  Empfehlung: In zwei getrennte Tests aufteilen (`..._ForUnknownDraft`, `..._ForForeignDraft`).

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `FinanceManager.Application/Reports/IStatementDraftExportService.cs` (neu)
- `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` (neu)
- `FinanceManager.Shared/Dtos/Statements/StatementDraftExportDtos.cs` (neu)
- `FinanceManager.Tests/Reports/StatementDraftExportServiceTests.cs` (neu)
- `FinanceManager.Tests.E2E/Tests/StatementDrafts/StatementDraftExportE2ETests.cs` (neu)
- `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` (geändert)
- `FinanceManager.Web/Controllers/StatementDraftsController.cs` (geändert)
- `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` (geändert)
- `FinanceManager.Web/Components/Pages/CardPage.razor` (geändert)
- `FinanceManager.Tests/Controllers/StatementDraftsControllerTests.cs` (geändert)
- `FinanceManager.Tests/ViewModels/StatementDraftCardViewModelTests.cs` (geändert)
- `FinanceManager.Tests.Integration/ApiClient/ApiClientStatementDraftsTests.cs` (geändert)
