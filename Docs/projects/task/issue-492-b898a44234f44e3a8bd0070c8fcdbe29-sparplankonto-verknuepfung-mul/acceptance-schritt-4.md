# Abnahmeprüfung – Entwicklungsschritt 4

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 4 ist in Commit `64afbfef` enthalten; die Nachkorrektur der Export-Kennzeichnung in `fa12b98b`) gegen Schritt 4 in `project-plan.md` (Zeilen 94–107) sowie `requirement.md` (FA-2, AK 4). Code-Review plus Testlauf.

- **AK 1 – `informal: true` in REST und `fm_postings_query`:** `PostingServiceDto` um `Informal` (und `SavingsPlanSubType`) erweitert — als nachgestellte Positional-Parameter mit Defaults, rückwärtskompatibel (`FinanceManager.Shared/Dtos/Postings/PostingServiceDto.cs:67–71`). Befüllung ableitungsbasiert über `SavingsPlanLinkAnalysis.GetInformalPostingIdsAsync` in `PostingsQueryService.GetSavingsPlanPostingsAsync` (Zeilen 374–411) und im Einzelposting `PostingsController.GetById` (Zeilen 119–128). MCP-Ausgabe in `FmPostingsQueryTool.cs` (Zeilen 160–161: `informal`, `savingsPlanSubType`); Descriptor-Text dokumentiert die Semantik (Zeile 33).

- **Export-Kennzeichnung (Nachkorrektur, Commit `fa12b98b`):** Das gemeinsame Export-Schema trägt jetzt die beiden nachgestellten Spalten `Informal` und `SavingsPlanSubType` (`PostingExportService.cs:16` — `CsvHeader`, zugleich `XlsxHeaders`). `PostingExportRow` wurde um `Informal`/`SavingsPlanSubType` erweitert (`IPostingExportService.cs:77–78`, Defaults rückwärtskompatibel). Die Ableitung nutzt dieselbe `SavingsPlanLinkAnalysis.GetInformalPostingIdsAsync`-Logik wie die REST-/MCP-Pfade — kein persistiertes Flag — aufgelöst in `ResolveInformalPostingIdsAsync` (`PostingExportService.cs:302`) für beide Exportpfade: `QueryAsync`/`GenerateAsync` (Zeilen 75–81, CSV via `CreateCsvColumns` ab Zeile 455, XLSX via `CreateXlsxValues` ab Zeile 490) und `StreamCsvAsync` (Zeilen 419–435). Bestehende Spalten bleiben unverändert; die Ableitung läuft mit den festen Batch-Lookups des Analysators (kein N+1).

- **Ableitung exakt wie geplant inkl. Fremdkonto-Präzisierung:** `IsInformalPosting` (`SavingsPlanLinkAnalysis.cs:143–158`) = Plan hat Links **und** Buchungsgruppe enthält Bankposting auf einem **nicht** verknüpften Konto. Gruppen-Bankposting ausschließlich auf verknüpften Konten → nicht informell; keine Gruppe/kein Bank-Gegenposting → nicht informell. Damit ist der Hinweis aus `project-plan-check.md` (Fremdkonto-Präzisierung) korrekt umgesetzt.

- **AK 2 – `informal: false` nach Link-Entfernung:** Reine Ableitung ohne persistiertes Flag — `GetLinkedAccountsByPlanAsync` liefert nach dem letzten Entfernen leere Menge → `false`. Getestet in `GetSavingsPlanPostings_AfterLinkRemoved_NotInformal` (Link entfernt, Ausgabe ohne Kennzeichnung). Historische Beträge werden nicht umgebucht (kein Datenupdate irgendwo im Pfad).

- **AK 3 – Nicht-Funding-Postings unmarkiert, Funding-Quelle bleibt auswertbar:** Disbursement ohne Bank-Gegenposting (`GetSavingsPlanPostings_NoBankCounterpart_NotInformal`), Plan-Posting mit Bank-Gegenposting auf verknüpftem Konto (`GetSavingsPlanPostings_LinkedBankCounterpart_NotInformal`) und Istmeldung (`GetSavingsPlanPostings_ActualReport_NotInformalAndMarked`) bleiben unmarkiert. Die Postings selbst bleiben unverändert in den Listen sichtbar.

- **AK 4 – Budgetsicht unverändert:** Die Kennzeichnung ist Auswertungs-/DTO-Eigenschaft; keine Budget-Query verwendet `Informal`. Explizit belegt durch `ListOverview_InformalFundingPosting_StillCountsForActuals` (Budget-Regressionstest, Schritt 10).

- **Testnachweis:** `dotnet test FinanceManager.Tests --filter "...PostingsQueryServiceInformalTests|...BudgetPurposeServiceSavingsPlanActualsTests..."` → Teil des erfolgreichen 33/33-Laufs; alle 5 Informal-Tests grün. Zusätzlich nach der Nachkorrektur: `dotnet test FinanceManager.Tests --filter "...PostingExportServiceInformalTests|...PostingsQueryServiceInformalTests|...SavingsPlanLinkedAnalysisTests|...ExportFileHelperTests|...StatementDraftExportServiceTests"` → 30/30 grün; `PostingExportServiceInformalTests` (neu, `FinanceManager.Tests/Reports/PostingExportServiceInformalTests.cs`) belegt Header und Werte der neuen Spalten im CSV-Stream (`True`/`False`/`ActualReport`) sowie im XLSX-Export.
