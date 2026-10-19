# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Zusammenfassung

- Gesamt: 1603
- Bestanden: 1603
- Fehlgeschlagen: 0
- Übersprungen: 0

Ausgeführte Testläufe:

| Projekt | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|---------|--------|-----------|----------------|--------------|
| FinanceManager.Tests (Unit) | 1464 | 1464 | 0 | 0 |
| FinanceManager.Tests.Integration | 137 | 137 | 0 | 0 |
| FinanceManager.Tests.E2E (Filter: `StatementDraftExportE2ETests`) | 2 | 2 | 0 | 0 |

Hinweise zur Ausführung:

- Build vorab: `dotnet build FinanceManager.sln` — erfolgreich (0 Fehler, nur NU1510-Warnungen).
- Test-Runner: Microsoft.Testing.Platform (xunit.v3); `dotnet test` auf die `.csproj` ohne `--no-build`. Der erste Unit-Test-Aufruf scheiterte einmalig mit einem transienten MTP-Handshake-Fehler (Exitcode 5); der Wiederholungslauf war erfolgreich.
- E2E-Feature-Tests direkt über `FinanceManager.Tests.E2E.exe -class "*StatementDraftExportE2ETests*"` aus `FinanceManager.Tests.E2E/bin/Debug/net10.0/` (2 Tests). Die übrige E2E-Suite und `FinanceManager.Tests.Integration.ApiClient` waren nicht Teil dieses Laufs.
- Coverage über `Microsoft.Testing.Extensions.CodeCoverage` (`-- --coverage --coverage-output-format cobertura`), Artefakte: `TestResults/coverage.cobertura.xml` (Unit) und `TestResults/coverage.integration.cobertura.xml` (Integration).

## Testabdeckung

**Abdeckung:** 74,90 % (Zeilenabdeckung, gemergte Cobertura-Daten aus `FinanceManager.Tests` und `FinanceManager.Tests.Integration`; E2E-Lauf ohne Coverage)

Im Rahmen dieses Features neu/geänderte Quelldateien unter 80 % (Werte beziehen sich auf die Gesamtdatei; Controller/ViewModel/CardPage sind größtenteils Bestandscode):

| Datei | Abdeckung |
|-------|-----------|
| `FinanceManager.Infrastructure/Reports/PostingExportService.cs` | 1,8 % |
| `FinanceManager.Web/Controllers/PostingsController.cs` | 26,0 % |
| `FinanceManager.Web/ViewModels/StatementDrafts/StatementDraftCardViewModel.cs` | 27,5 % |
| `FinanceManager.Web/Components/Pages/CardPage.razor` | 37,1 % |
| `FinanceManager.Web/Controllers/StatementDraftsController.cs` | 56,6 % |
| `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` | 66,3 % |

Feature-Dateien ohne Eintrag in der Tabelle (>= 80 %): `FinanceManager.Infrastructure/Reports/StatementDraftExportService.cs` (93,2 %), `FinanceManager.Web/Infrastructure/ExportRequestHelper.cs` (87,5 %), `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs` (100 %), `FinanceManager.Application/Reports/IStatementDraftExportService.cs` (100 %), `FinanceManager.Shared/Dtos/Statements/StatementDraftExportDtos.cs` (100 %).

## Fehlende Tests

Quelle: `Coverage-Daten` + `Dateinamen-Konvention`

Im Rahmen dieses Features neu/geänderte Quelldateien ohne eigene Testdatei bzw. ohne nennenswerte Abdeckung:

- `FinanceManager.Infrastructure/Reports/PostingExportService.cs` — Keine Testdatei gefunden (1,8 % Abdeckung; im Feature refaktoriert, nutzt jetzt `ExportFileHelper`)
- `FinanceManager.Web/Controllers/PostingsController.cs` — Keine Testdatei gefunden (26,0 % Abdeckung; im Feature um Export-Endpunkt erweitert)
- `FinanceManager.Infrastructure/Reports/ExportFileHelper.cs` — Keine dedizierte Testdatei (66,3 % indirekt über `StatementDraftExportServiceTests` abgedeckt)
- `FinanceManager.Web/Infrastructure/ExportRequestHelper.cs` — Keine dedizierte Testdatei (87,5 % indirekt über Controller-Tests abgedeckt)

Hinweis: Darüber hinaus existieren Bestandslücken — zahlreiche ältere Quelldateien (v. a. Razor-Komponenten, ViewModels, Migrations, diverse Services) liegen bei 0 % Zeilenabdeckung; diese wurden hier nicht einzeln aufgeführt, da sie nicht Teil dieses Features sind.
