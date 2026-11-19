# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `ReportAggregationBankPostingTypeProjectionTests` (Testklasse) — angelegt (`FinanceManager.Tests/Reports/`), alle drei geplanten Tests vorhanden
- [x] `ReportDashboardViewModelBankPostingTypeTests` (Testklasse) — angelegt (`FinanceManager.Tests/ViewModels/`), beide geplanten Tests mit Request-Capture vorhanden
- [x] `ReportBankPostingTypeFilterPlaywrightTests` (E2E-Testklasse) — angelegt (`FinanceManager.Tests.E2E/Tests/Reports/`), alle drei Szenarien der E2E-Tabelle vorhanden
- [x] `ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes` (Testklasse, Task 21) — angelegt, pinnt die symmetrisch behobene Drilldown-Filterfreiheit der verknüpften Quell-Bankposten
- [x] `ReportProjectionEventLoader.LoadEntityEventsAsync` — erweitert: `HashSet<BankPostingType>? bankPostingTypes`-Parameter, `p.PostingType` in der anonymen Selektion, In-Memory-Allow-List mit `kind != PostingKind.Bank ||`-Guard, XML-Doc ergänzt (`ReportProjectionEventLoader.cs` Z. 42–78)
- [x] `ReportAggregationService` — `bankPostingTypes` wird im Bank-Projektionsblock übergeben (Z. 760–763); Contact-/SavingsPlan-Aufrufe (Z. 773, 795) und `LoadSavingsPlanEventsAsync` (`ReportProjectionEventLoader.cs` Z. 140) übergeben `null` — Scope-Ausschluss 2 eingehalten
- [x] `ReportDashboardViewModel.SaveFavoriteAsync`/`UpdateFavoriteAsync` — `BankPostingTypes: filters.BankPostingTypes` über extrahierte Hilfsmethode `ToFavoriteFilters` (Z. ~816–833) in beiden Pfaden gemappt
- [x] `HomeKpiGrid.razor` `LoadFavoriteKpiDataAsync` — `BankPostingTypes: fav.Filters.BankPostingTypes` im `ReportAggregatesFiltersRequest`-Konstruktor (Z. 657)
- [x] `ReportDashboard.razor` — Literale `1`–`4` durch `BankPostingType.Inflow`/`Outflow`/`Interest`/`Tax` ersetzt (Z. 602–605); VM-Methoden `IsBankPostingTypeSelectedTemp`/`ToggleTempBankPostingType` nehmen nun das Enum entgegen
- [x] Task 21 — Drilldown-Asymmetrie symmetrisch behoben: `ReportPostingsQueryService.LoadLinkedSavingsPlanPostingsAsync` übergibt `null` als `bankPostingTypes` (Z. 224), direkte Bank-Pfade übergeben weiterhin `query.Filters?.BankPostingTypes` (Z. 121); `LoadPostingsAsync`-Signatur um `IReadOnlyCollection<int>? bankPostingTypes` erweitert (Z. 235/252)
- [x] `ReportDashboardGateway.BankPostingTypeCheckbox` (E2E-Helper) — angelegt mit sprachunabhängigem de/en-Label-Regex (`BankPostingTypeLabelRegex`)
- [x] `AccountsApiSeedHelper.SeededPosting.PostingType` (Feld) — vorhanden inkl. `SetPostingType`-Aufruf und `PostingTypeEvaluator.EvaluateByAmountSign`-Fallback für untypisierte Bankposten
- [x] Test `QueryAsync_BankProjection_RespectsBankPostingTypes` — vorhanden
- [x] Test `QueryAsync_BankProjection_BankPostingTypes_UnselectedTypesProduceNoExpectation` — vorhanden
- [x] Test `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` (Pinning Scope-Ausschluss 2) — vorhanden
- [x] Test `QueryAsync_LinkedSavingsPlanSource_IgnoresBankPostingTypes` (Pinning Task 21, Drilldown) — vorhanden
- [x] Test `SaveFavorite_ShouldMapBankPostingTypes` / `UpdateFavorite_ShouldMapBankPostingTypes` — vorhanden
- [x] E2E-Szenarien P1-1 (`BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations`), P1-2 (`SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload`), P2 (`KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal`) — vorhanden
- [x] Feature-Review Ablauf 4, Prüfschritte 1–10 — vollständig in `review-feature.md` dokumentiert (Endpunkte inkl. MCP, LinkedAccountPostings-Abgrenzung, `Unspecified`/Backfill, Filter-Counter, XML-Docs, Backup-Konformität, EnumCoverage, resx)
- [x] Rot-Lauf-Nachweis der Repro-Tests — in `review-feature.md` festgehalten
- [x] Gesamtverifikation — `test-results.1.md` + trx-Nachweise: Build 0 Fehler; Unit 2401/2401 (Iteration-2-Nachlauf 2402/2402); Integration 176/176; E2E 174/176 mit 2 transienten Flakes, im Nachlauf 2/2 grün

## Hinweise

- Alle vier offenen Aufgaben aus Runde 1 (`review.1.md`) sind geschlossen: Task 21 durch die Anwenderentscheidung konkret umgesetzt (statt nur als Folgeaufgabe notiert — damit entfällt auch der in Runde 1 beanstandete fehlende `continue.md`-Eintrag, da keine offene Folgeaufgabe mehr existiert), Tasks 28–30 durch dokumentierte Suite-Läufe mit trx-Nachweisen.
- Die Befunde aus `review-code.1.md` sind in Iteration 2 mitumgesetzt: `ToFavoriteFilters`-Extraktion im ViewModel, Wegfall der redundanten Filter-Mappings im `ReportsController` (direkte `req.Filters`-Durchreichung), `kind`-Guard im Loader-Filter, Parameter `bankPostingTypes` vor `ct`, Seed-Fallback per `EvaluateByAmountSign`, Enum-Signaturen der VM-Toggle-Methoden. Dass die VM-Methoden nun `BankPostingType` statt `int` annehmen, weicht vom Planwortlaut („Signatur bleibt") ab, erfüllt die Planintention aber besser.
- Der `LoadEntityEventsAsync`-Parameter trägt keinen `= null`-Default wie im Plan skizziert; alle vier Aufrufstellen übergeben `null` explizit — funktional gleichwertig und missbrauchssicherer.
