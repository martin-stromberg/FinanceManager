# Tasks: Reparatur des ING-CSV-Kontoauszugimports

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | Zweites Template mit `Referenz`-Spalte in `ING_CSV_StatementFileParser._Templates` ergänzen | Offen | — |
| 2 | UI | `Home.razor`: ausgeschlossene/nicht importierbare Dateien als `muted-row` mit `ValidationMessage` anzeigen, Delete-Button deaktivieren | Offen | — |
| 3 | Logik | `HomeViewModel.ConfirmMassImportAsync`: Finalize-Warnung nur bei importierbaren Dateien | Offen | — |
| 4 | Tests | `StatementParserAdapterTests`: Test für neues 10-Spalten-Format (Einzelblock) | Offen | — |
| 5 | Tests | `StatementParserAdapterTests`: Test für Sammelkonto im neuen Format | Offen | — |
| 6 | Tests | `HomeViewModelTests`: Finalize-Warnung entfällt bei reinem Skip-Batch / bleibt bei Import | Offen | — |
| 7 | E2E-Tests | `HomeMassImportPlaywrightTests`: Upload neues ING-Format → Erfolg ohne Review-Dialog | Offen | — |
| 8 | E2E-Tests | `HomeMassImportPlaywrightTests`: Unbekannte Datei → Dialog zeigt Grund, ohne Finalize-Warnung | Offen | — |
