# Umsetzungsplan: Reparatur des ING-CSV-Kontoauszugimports

## Übersicht

ING hat dem CSV-Kontoauszug-Export eine neue Spalte `Referenz` zwischen `Verwendungszweck` und `Saldo` hinzugefügt (9 → 10 Spalten). Der positionsbasierte Template-Parser `ING_CSV_StatementFileParser` schlägt daran fehl, sodass die Datei als `Unknown` klassifiziert wird und der Review-Dialog „Massenimport prüfen" erscheint — dort aber leer bleibt, weil nicht importierbare Dateien herausgefiltert werden. Umgesetzt werden: (1) ein zweites Template für das neue Spaltenlayout (Alt- und Neuformat parallel), (2) sichtbare Darstellung nicht importierbarer Dateien inkl. `ValidationMessage` im Review-Dialog, (3) die Finalisierungs-Warnung „kann nicht rückgängig gemacht werden" nur noch, wenn tatsächlich mindestens eine Datei ausgeführt wird.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `ING_CSV_StatementFileParser._Templates` | Zweites XML-Template für das neue 10-Spalten-Layout; `Referenz` → `variable=''` (ignoriert) | Der `TemplateStatementFileParser` versucht Templates sequenziell und fällt bei `FormatException`/`IndexOutOfRangeException` sauber auf das nächste zurück — etablierter Mechanismus, Alt-Exporte bleiben lesbar. `StatementMovement` hat kein Referenz-Feld; ein Anhängen an `Subject` würde Buchungstexte und Duplikatserkennung gegenüber Alt-Importen verändern |
| Review-Dialog `Home.razor` | `Where(x => !x.Excluded)`-Filter entfernen; nicht selektierbare Dateien als `muted-row` mit `ValidationMessage` anzeigen | Die Klasse `muted-row` ist dafür bereits vorgesehen; der Benutzer sieht den Grund („File type could not be recognized."), statt einen leeren Dialog bestätigen zu müssen |
| `HomeViewModel.ConfirmMassImportAsync` | `Confirmation_Finalize`-Warnung nur abfragen, wenn `PendingMassImport.Files` mindestens eine Datei mit `!Excluded && CanImport` enthält | Bei ausschließlich übersprungenen Dateien wird nichts ausgeführt — die Irreversibilitätswarnung ist dort falsch und verwirrend |

## Programmabläufe

### ING-CSV-Import (neues Format)

1. `StatementFileFactory.Load` erkennt die Datei als `ING_Csv_StatementFile` (`Bank;ING` in den ersten 10 Zeilen) — unverändert.
2. `MassImportOrchestrator.AnalyzeFile` ruft alle `IStatementFileParser.Parse` auf. `ING_CSV_StatementFileParser.Parse` delegiert bei Einzel-IBAN-Dateien an `base.Parse` bzw. splittet Sammelkonten in `InlineING_CsvStatementFile`-Blöcke — unverändert.
3. `TemplateStatementFileParser.Parse` versucht Template 1 (alt): `Betrag` liest fälschlich den Währungswert `EUR` → `decimal.Parse` wirft `FormatException` → Template verworfen. Template 2 (neu): `Referenz`-Spalte wird ignoriert, alle Felder mappen korrekt → Movements entstehen.
4. `AnalyzeFile` liefert `MassImportFileType.AccountStatement` mit `ServiceKey = "ing"` → `IsDialogRequired` = `false` (bei Policy `OnMissingInformation`) → Import läuft ohne Dialog durch → Erfolgsbanner + Draft-Link auf der Startseite.

### Review-Dialog mit nicht importierbarer Datei

1. `AnalyzeFile` liefert `Unknown` → `RequiresConfirmation` → `PendingMassImport` gesetzt → Dialog öffnet sich.
2. `Home.razor` zeigt jetzt **alle** Dateien; die `Unknown`-Datei erscheint als `muted-row` mit ihrer `ValidationMessage`.
3. Benutzer bestätigt mit „Speichern" → `ConfirmMassImportAsync`: keine Datei mit `!Excluded && CanImport` → **keine** Finalisierungs-Warnung → Request mit `ConfirmExecution = true` → alle Dateien `Skipped` → Dialog schließt.

### Review-Dialog mit importierbaren Dateien

Unverändert: Warnung erscheint, sobald mindestens eine nicht ausgeschlossene, importierbare Datei ausgeführt wird.

Beteiligte Klassen/Komponenten: `ING_CSV_StatementFileParser`, `TemplateStatementFileParser`, `MassImportOrchestrator`, `HomeViewModel`, `Home.razor`, `ConfirmationDialogHost`

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `ING_CSV_StatementFileParser` (Parser)

- **Geänderte Member:** `_Templates` — zweites Template-Element mit 10 `field`-Einträgen anfügen: `Buchung`→`PostingDate`, `Wertstellungsdatum`→`ValutaDate`, `Auftraggeber/Empfänger`→`SourceName`, `Buchungstext`→`PostingDescription`, `Verwendungszweck`→`Description`, `Referenz`→`''`, `Saldo`→`''`, `Währung`→`CurrencyCode`, `Betrag`→`Amount`, `Währung`→`CurrencyCode`. Sektionsstruktur identisch zum bestehenden Template.

### `HomeViewModel` (ViewModel)

- **Geänderte Methoden:** `ConfirmMassImportAsync` — die `ConfirmationService.ConfirmAsync`-Abfrage nur ausführen, wenn `PendingMassImport.Files.Any(f => !f.Excluded && f.CanImport)` gilt; sonst direkt den Confirm-Request senden.

### `Home.razor` (Komponente)

- **Geändertes Markup:** `@foreach` über `_vm.PendingMassImport.Files` ohne `!x.Excluded`-Filter; Entfernen-Button bei nicht selektierbaren Einträgen deaktivieren (`disabled="@(!selectable)"`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Andere Parser:** Keine — das zweite Template ist ING-spezifisch; andere Parser und Dateitypen unverändert.
- **Altformat-Importe:** Bleiben über das erste Template lesbar; neue Template wird bei 9 Spalten mit `IndexOutOfRangeException` verworfen.
- **Sammelkonten:** Der Multi-Block-Split in `ING_CSV_StatementFileParser.Parse` arbeitet mit beiden Templates; sogar gemischte Alt/Neu-Blöcke wären lesbar.
- **Dialog-Verhalten:** Benutzer von `AlwaysConfirm`-Policy sehen weiterhin den Dialog; nur die zweite Warnung entfällt bei komplett leeren Batches. Der `muted-row`-Pfad in `Home.razor` wird erstmals tatsächlich erreichbar.
- **E2E `UploadCollectionAccountCsv_ViaUi_*`:** Klickt bei sichtbarem Dialog auf `button.btn` (Speichern) — unverändert funktionsfähig, da der Button gleich bleibt.

## Umsetzungsreihenfolge

1. **Neues Template in `ING_CSV_StatementFileParser._Templates`**
   - Voraussetzungen: Keine
   - Beschreibung: Zweites Template-Element mit dem 10-Spalten-Layout (inkl. ignoriertem `Referenz`) ergänzen.

2. **Unit-Tests für den Parser**
   - Voraussetzungen: Schritt 1
   - Beschreibung: In `StatementParserAdapterTests` Tests für neue CSV-Variante ergänzen (Einzelblock, korrekte Feldwerte inkl. `Amount`; Sammelkonto im neuen Format).

3. **`Home.razor`: ausgeschlossene/nicht importierbare Dateien anzeigen**
   - Voraussetzungen: Keine
   - Beschreibung: Filter `Where(x => !x.Excluded)` entfernen; Delete-Button für nicht selektierbare Einträge deaktivieren.

4. **`HomeViewModel.ConfirmMassImportAsync`: Finalisierungs-Warnung konditional**
   - Voraussetzungen: Keine
   - Beschreibung: `ConfirmAsync` nur aufrufen, wenn mindestens eine Datei `!Excluded && CanImport` ist.

5. **Unit-Tests für Dialogverhalten**
   - Voraussetzungen: Schritt 4
   - Beschreibung: `HomeViewModelTests` mit gemocktem `IConfirmationService` (statt `NullConfirmationService`): Warnung wird bei reinen Skip-Batches nicht aufgerufen, bei importierbaren Dateien schon.

6. **E2E-Tests**
   - Voraussetzungen: Schritte 1–5
   - Beschreibung: Playwright-Szenarien für (a) neuen ING-CSV-Upload via Home-Widget ohne Review-Dialog mit Erfolgsbanner, (b) nicht erkannte Datei: Review-Dialog zeigt Datei + Grund, Bestätigung ohne Finalize-Warnung.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Parse_IngCsvNewFormat_ShouldReturnSingleElementList` | `StatementParserAdapterTests` | 10-Spalten-CSV mit `Referenz` → 1 Ergebnis; `BookingDate`/`ValutaDate`/`Counterparty`/`Subject`/`Amount`/`CurrencyCode` korrekt gemappt |
| `Parse_IngCsvNewFormat_ShouldReturnMultipleResults_ForCollectionAccount` | `StatementParserAdapterTests` | Multi-IBAN-CSV im neuen Format → mehrere `StatementParseResult` |
| `ConfirmMassImportAsync_ShouldSkipFinalizeConfirmation_WhenNothingImportable` | `HomeViewModelTests` | Mock-`IConfirmationService`: `ConfirmAsync` wird nicht aufgerufen, Request geht trotzdem raus |
| `ConfirmMassImportAsync_ShouldShowFinalizeConfirmation_WhenFileImportable` | `HomeViewModelTests` | Mock-`IConfirmationService`: `ConfirmAsync` wird aufgerufen |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `HomeViewModelTests.CreateVm` | Muss optional einen `IConfirmationService`-Mock registrieren können, damit der Aufruf verifiziert werden kann |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | ING-CSV im neuen Format über das Home-Import-Widget hochladen → kein Review-Dialog, Erfolgsbanner sichtbar | `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs` | Import funktioniert wieder Ende-zu-Ende über den tatsächlichen Benutzerfluss | Der Fehler trat im realen Upload-Fluss auf; nur E2E beweist, dass Datei-Erkennung, Parser und UI zusammenspielen |
| Pflicht | Nicht erkennbare Datei hochladen → Review-Dialog zeigt Datei mit Fehlergrund; Bestätigen schließt den Dialog ohne zweite Warnung | `FinanceManager.Tests.E2E/Tests/Import/HomeMassImportPlaywrightTests.cs` | Dialog zeigt Grund, keine Irreversibilitäts-Warnung bei leerem Batch | UI-Sichtbarkeit der `ValidationMessage` und die entfallene Zweitbestätigung sind nur im Browser verifizierbar |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Bestehende E2E-CSVs nutzen das alte Format und bleiben über das erste Template gültig |

## Offene Punkte

Keine. (Beide Entscheidungen wurden vom Anwender bestätigt: `Referenz` wird verworfen, Dialog-Verhalten folgt dem Mittelweg — Fehlergrund im Dialog sichtbar, Finalize-Warnung nur bei tatsächlich auszuführenden Dateien.)
