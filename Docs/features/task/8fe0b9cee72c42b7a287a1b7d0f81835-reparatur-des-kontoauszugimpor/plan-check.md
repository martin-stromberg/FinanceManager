# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| ING-CSV im neuen Format (mit `Referenz`-Spalte) wird importiert | Zweites Template in `ING_CSV_StatementFileParser._Templates` (Schritt 1) | `Parse_IngCsvNewFormat_ShouldReturnSingleElementList` (Unit) + E2E Upload-Szenario | Abgedeckt |
| Altes ING-CSV-Format bleibt lesbar | Erstes Template bleibt unverändert; Fallback-Reihenfolge dokumentiert | Bestehende Tests `Parse_IngCsv_*` + `Parse_ShouldReturnMultipleResults_ForCollectionAccountCSV` bleiben unangetastet | Abgedeckt |
| Sammelkonten im neuen Format funktionieren | Multi-Block-Split in `Parse` nutzt Templates je Block | `Parse_IngCsvNewFormat_ShouldReturnMultipleResults_ForCollectionAccount` (Unit) | Abgedeckt |
| Review-Dialog zeigt Grund für nicht importierbare Dateien | `Home.razor`-Filter entfernen, `muted-row` + `ValidationMessage` (Schritt 3) | E2E-Szenario „nicht erkennbare Datei" | Abgedeckt |
| Finalize-Warnung nur bei tatsächlicher Ausführung | `ConfirmMassImportAsync` konditional (Schritt 4) | `ConfirmMassImportAsync_ShouldSkipFinalizeConfirmation_WhenNothingImportable` + `..._WhenFileImportable` (Unit) + E2E | Abgedeckt |
| `Referenz`-Inhalt wird verworfen | `variable=''` im neuen Template (Anwender-Entscheidung) | Feld-Mapping-Assertions im Einzelblock-Unit-Test | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Upload neues ING-Format über Home-Import-Widget → Erfolg ohne Dialog | Neuer Playwright-Test in `HomeMassImportPlaywrightTests` | Abgedeckt |
| Upload nicht erkennbarer Datei → Dialog mit Datei + Grund, Bestätigen ohne Finalize-Warnung | Neuer Playwright-Test in `HomeMassImportPlaywrightTests` | Abgedeckt |

## Hinweise

- Die Positionsbindung des Template-Parsers bleibt ein strukturelles Risiko bei künftigen ING-Formatänderungen; eine headerbasierte Spaltenerkennung wäre eine eigenständige Erweiterung außerhalb dieser Anforderung.
