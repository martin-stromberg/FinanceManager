# Tasks: Reparatur des ING-CSV-Kontoauszugimports

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | Zweites Template mit `Referenz`-Spalte in `ING_CSV_StatementFileParser._Templates` ergänzen | Erledigt | `StatementParserAdapterTests.Parse_IngCsvNewFormat_ShouldReturnSingleElementList_WhenValidSingleBlockContent` |
| 2 | UI | `Home.razor`: ausgeschlossene/nicht importierbare Dateien als `muted-row` mit `ValidationMessage` anzeigen, Delete-Button deaktivieren | Erledigt | E2E `HomeMassImportPlaywrightTests.UploadUnrecognizedFile_ViaUi_ShouldShowReasonInReviewDialog_WithoutFinalizeWarning` |
| 3 | Logik | `HomeViewModel.ConfirmMassImportAsync`: Finalize-Warnung nur bei importierbaren Dateien | Erledigt | `HomeViewModelTests.ConfirmMassImportAsync_ShouldSkipFinalizeConfirmation_WhenNothingImportable`, `..._WhenFileImportable` |
| 4 | Tests | `StatementParserAdapterTests`: Test für neues 10-Spalten-Format (Einzelblock) | Erledigt | Test selbst, grün |
| 5 | Tests | `StatementParserAdapterTests`: Test für Sammelkonto im neuen Format | Erledigt | Test selbst, grün |
| 6 | Tests | `HomeViewModelTests`: Finalize-Warnung entfällt bei reinem Skip-Batch / bleibt bei Import | Erledigt | Tests selbst, grün |
| 7 | E2E-Tests | `HomeMassImportPlaywrightTests`: Upload neues ING-Format → Erfolg ohne Review-Dialog | Erledigt | Test selbst, grün |
| 8 | E2E-Tests | `HomeMassImportPlaywrightTests`: Unbekannte Datei → Dialog zeigt Grund, ohne Finalize-Warnung | Erledigt | Test selbst, grün |
