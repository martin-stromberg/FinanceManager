# Bestandsaufnahme: Reparatur des ING-CSV-Kontoauszugimports

Analyse des CSV-Importpfads für ING-Kontoauszüge und des Massenimport-Dialogs auf der Startseite, bezogen auf die Anforderung „ING-CSV-Import funktioniert nach Formatänderung (neue Spalte `Referenz`) wieder und die Review-/Bestätigungsabfolge ist sinnvoll".

## Zusammenfassung

- Der ING-CSV-Import läuft über ein XML-Template in `ING_CSV_StatementFileParser._Templates`; die Basisklasse `TemplateStatementFileParser` mappt Tabellenspalten **rein positionsbasiert** auf `StatementMovement`-Felder. Die Kopfzeile wird bei `containsheader='true'` übersprungen und nicht ausgewertet — die `field name`-Attribute sind nur Dokumentation.
- Das Template kennt nur das alte 9-Spalten-Layout. Mit der neuen `Referenz`-Spalte (10 Spalten) verschiebt sich `Betrag` auf die Position der Währung `EUR`; `decimal.Parse` wirft `FormatException` → Template schlägt fehl → `Parse` liefert `null`.
- In `MassImportOrchestrator.AnalyzeFile` wird die Datei zwar als `ING_Csv_StatementFile` geladen (`Bank;ING` erkannt), aber kein Parser liefert Movements → Fallback auf Wertpapierkurs-Erkennung scheitert → `FileType = Unknown`, `ValidationMessage = "File type could not be recognized."`.
- `IsDialogRequired` liefert bei `Unknown`/`!CanImport` `true` → `RequiresConfirmation` → `HomeViewModel.PendingMassImport` wird gesetzt → Review-Dialog erscheint.
- Im Dialog (`Home.razor`) werden nur `!Excluded`-Dateien angezeigt. `NormalizePendingMassImportResult` setzt nicht importierbare Dateien auf `Excluded = true` → die Datei ist im Dialog **unsichtbar**, inklusive ihrer `ValidationMessage`. Der Dialog zeigt somit keinen Eintrag und keinen Grund.
- `ConfirmMassImportAsync` fragt **immer** die Finalisierungs-Bestätigung („Abschließen bestätigen / Die Aktion kann nicht rückgängig gemacht werden") ab — auch wenn keine einzige Datei importierbar ist und nur `Skipped` passieren würde.
- Ergebnis beim Benutzer: leerer Review-Dialog → Warnung „nicht rückgängig" → nichts wird importiert, keine Fehlermeldung sichtbar.

Test-Ausgangszustand: siehe [Tests](inventory/tests.md) — Unit-Suite `FinanceManager.Tests` (Release): 1336 erfolgreich, 0 fehlgeschlagen. Integration-Suite läuft noch / Ergebnis in `tests.md`.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [UI](inventory/ui.md)
- [Tests](inventory/tests.md)
