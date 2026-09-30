# Übersetzte Anforderung: Reparatur des ING-CSV-Kontoauszugimports

## Fachliche Zusammenfassung

Der Import von ING-CSV-Kontoauszügen schlägt fehl, weil ING das Exportformat geändert hat: In der Bewegungstabelle wurde eine neue Spalte `Referenz` zwischen `Verwendungszweck` und `Saldo` eingefügt. Der `ING_CSV_StatementFileParser` arbeitet positionsbasiert und scheitert daher am geänderten Spaltenlayout. Als Folge erkennt der `MassImportOrchestrator` die Datei nicht (`MassImportFileType.Unknown`), sodass der Dialog „Massenimport prüfen" erscheint und nach Bestätigung die Warnung „Die Aktion kann nicht rückgängig gemacht werden" — ohne dass irgendetwas importiert wird. Zusätzlich soll bewertet werden, ob diese doppelte Abfolge aus Review-Dialog und Finalisierungs-Bestätigung in diesem Fall sinnvoll ist.

## Betroffene Klassen und Komponenten

- `ING_CSV_StatementFileParser` (FinanceManager.Infrastructure/Statements/Parsers) — Template `_Templates` muss das neue Spaltenlayout unterstützen
- `TemplateStatementFileParser` (FinanceManager.Infrastructure/Statements/Parsers) — Basisklasse, positionsbasierte Feldmapping-Logik (`ParseTableRecord`, `ParseField`)
- `ING_Csv_StatementFile` (FinanceManager.Infrastructure/Statements/Files) — Dateityp-Erkennung (unverändert, prüft auf `Bank;ING`)
- `MassImportOrchestrator` (FinanceManager.Infrastructure/Statements) — `AnalyzeFile`/`IsDialogRequired` bestimmen, wann der Review-Dialog erscheint
- `HomeViewModel` (FinanceManager.Web/ViewModels/Home) — `ProcessMassImportSelectionAsync`, `ConfirmMassImportAsync` (Finalize-Bestätigung via `ConfirmationService`)
- `Home.razor` (FinanceManager.Web/Components/Pages) — Review-Dialog „Massenimport prüfen"
- Tests: `FinanceManager.Tests/Statements/StatementParserAdapterTests.cs` (Parser-Tests), `FinanceManager.Tests/Statements/MassImportOrchestratorTests.cs`, `FinanceManager.Tests/ViewModels/HomeViewModelTests.cs`, E2E: `FinanceManager.Tests.E2E/Tests/Import/`

## Implementierungsansatz

- Zweites Template in `_Templates` des `ING_CSV_StatementFileParser` für das neue Layout (mit `Referenz`-Spalte), damit alte und neue Exporte parallel funktionieren. Die Basisklasse versucht die Templates sequenziell und fällt bei Formatfehlern auf das nächste zurück.
- Die `Referenz`-Spalte besitzt kein Zielfeld in `StatementMovement`; wie `Saldo` wird sie auf `variable=''` gemappt (ignoriert). *(Annahme — siehe Offene Fragen.)*
- Bewertung des Dialogverhaltens: Wenn keine einzige Datei des Batches importierbar ist (alle `Unknown`/`Excluded`), ist die Finalisierungs-Warnung „kann nicht rückgängig gemacht werden" irreführend, da nichts ausgeführt wird. Außerdem werden ausgeschlossene Dateien im Dialog aktuell gar nicht angezeigt (`Where(x => !x.Excluded)`), sodass der Benutzer den Grund (z. B. „File type could not be recognized.") nicht sieht.

## Konfiguration

Keine neue Konfiguration. Die bestehende Benutzereinstellung `MassImportDialogPolicy` (`AlwaysConfirm` / `OnMissingInformation`) bleibt maßgeblich.

## Offene Fragen

1. Soll der Inhalt der neuen `Referenz`-Spalte irgendwo übernommen werden (z. B. an `Verwendungszweck`/`Subject` angehängt) oder verworfen werden? *Empfehlung: verwerfen — kein Zielfeld vorhanden, und ein Anhängen verändert Buchungstexte/Duplikatserkennung gegenüber Alt-Importen.*
2. Soll der Review-Dialog bei komplett nicht importierbaren Batches weiterhin mit Finalisierungs-Bestätigung arbeiten, oder soll der Dialog informativ bleiben (Datei mit Fehlergrund anzeigen) und die „kann nicht rückgängig gemacht werden"-Warnung nur dann erscheinen, wenn tatsächlich mindestens eine Datei importiert wird? *Empfehlung: Warnung nur bei tatsächlicher Ausführung; nicht importierbare Dateien mit `ValidationMessage` im Dialog sichtbar machen.*
