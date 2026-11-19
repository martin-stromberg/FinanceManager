# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

- [x] `ReportAggregationBankPostingTypeProjectionTests` (Testklasse) — angelegt (`FinanceManager.Tests/Reports/`), alle drei geplanten Tests vorhanden und grün
- [x] `ReportDashboardViewModelBankPostingTypeTests` (Testklasse) — angelegt (`FinanceManager.Tests/ViewModels/`), beide geplanten Tests vorhanden und grün
- [x] `ReportBankPostingTypeFilterPlaywrightTests` (E2E-Testklasse) — angelegt (`FinanceManager.Tests.E2E/Tests/Reports/`), alle drei geplanten Szenarien vorhanden
- [x] `ReportProjectionEventLoader.LoadEntityEventsAsync` — erweitert: optionaler `HashSet<BankPostingType>? bankPostingTypes = null` am Signatur-Ende, `p.PostingType` in der anonymen Selektion, In-Memory-Allow-List neben `Amount`/`textPatterns`, XML-Doc ergänzt
- [x] `ReportAggregationService` — erweitert: `bankPostingTypes` wird im `kinds.Contains(PostingKind.Bank)`-Projektionsblock an `LoadEntityEventsAsync` übergeben (Z. 763); Contact-/SavingsPlan-Aufrufe und `LoadSavingsPlanEventsAsync` übergeben den Filter nicht (Scope-Ausschluss 2 korrekt)
- [x] Feld `BankPostingTypes` in `ReportFavoriteFiltersDto`-Konstruktion von `ReportDashboardViewModel.SaveFavoriteAsync` — vorhanden (Z. 768)
- [x] Feld `BankPostingTypes` in `ReportFavoriteFiltersDto`-Konstruktion von `ReportDashboardViewModel.UpdateFavoriteAsync` — vorhanden (Z. 829)
- [x] Feld `BankPostingTypes` im `ReportAggregatesFiltersRequest`-Konstruktor in `HomeKpiGrid.razor` `LoadFavoriteKpiDataAsync` — vorhanden (Z. 657)
- [x] Enum-Ints in `ReportDashboard.razor` — Literale `1`–`4` durch `(int)BankPostingType.Inflow`/`Outflow`/`Interest`/`Tax` ersetzt; `ToggleTempBankPostingType(int, bool)`-Signatur unverändert
- [x] `ReportDashboardGateway.BankPostingTypeCheckbox` (E2E-Helper) — angelegt: sprachunabhängiger de/en-Label-Regex (`BankPostingTypeLabelRegex`)
- [x] `AccountsApiSeedHelper.SeededPosting.PostingType` (Feld) — vorhanden inkl. `SetPostingType`-Aufruf beim Seed
- [x] Test `QueryAsync_BankProjection_RespectsBankPostingTypes` — vorhanden, grün
- [x] Test `QueryAsync_BankProjection_BankPostingTypes_UnselectedTypesProduceNoExpectation` — vorhanden, grün
- [x] Test `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` (Pinning Scope-Ausschluss 2) — vorhanden, grün
- [x] Test `SaveFavorite_ShouldMapBankPostingTypes` — vorhanden, grün
- [x] Test `UpdateFavorite_ShouldMapBankPostingTypes` — vorhanden, grün
- [x] E2E-Szenario „Filter → Hochrechnung" (`BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations`) — vorhanden
- [x] E2E-Szenario „Favorit-Roundtrip" (`SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload`) — vorhanden
- [x] E2E-Szenario „KPI-Kachel" (`KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal`) — vorhanden
- [x] Feature-Review Ablauf 4, Prüfschritte 1–10 — vollständig durchgeführt und in `review-feature.md` dokumentiert (Endpunkte, LinkedAccountPostings-Abgrenzung, `Unspecified`/Backfill, Filter-Counter, XML-Docs, Backup-Konformität, EnumCoverage, resx)
- [x] Rot-Lauf-Nachweis der Repro-Tests — in `review-feature.md` festgehalten („Repro-Tests rot vor dem Fix, grün danach")
- [x] `dotnet build FinanceManager.sln` — im Review verifiziert: 0 Fehler

## Offene Aufgaben

- [ ] `continue.md` mit Folgeaufgabe(n) — teilweise umgesetzt: die identifizierte Folgeaufgabe (Drilldown-Asymmetrie bei `LoadLinkedSavingsPlanPostingsAsync`) ist dokumentiert, jedoch nur im Abschnitt „Folgeaufgaben" der `review-feature.md`; die vom Plan benannte Datei `continue.md` im Feature-Verzeichnis existiert nicht (Tasks #21)
- [ ] Gesamtverifikation `FinanceManager.Tests` mit CI-Filter — kein dokumentierter vollständiger Lauf; `review-feature.md` belegt nur die 5 neuen Repro-Tests und Regression der unmittelbar betroffenen Klassen (im Review selbst verifiziert: die 5 neuen Tests sind grün, Build fehlerfrei) (Tasks #28)
- [ ] Gesamtverifikation `FinanceManager.Tests.Integration` — kein dokumentierter Lauf (Tasks #29)
- [ ] Gesamtverifikation `FinanceManager.Tests.E2E` inkl. Ergebnisdokumentation — kein dokumentierter Lauf; die drei neuen E2E-Tests existieren, wurden aber nachweislich nicht ausgeführt (Tasks #30)

## Hinweise

- Alle als behoben markierten Punkte in `review-feature.md` (Defekt 1, Defekt 2, Home-KPI-Lücke, Enum-Ints) sind im Code verifiziert umgesetzt.
- Die Filter-Durchreichung stimmt mit dem Plan überein: `bankPostingTypes` erreicht ausschließlich den Bank-Projektionspfad (`ReportAggregationService.cs` Z. 760–763); `LoadSavingsPlanEventsAsync` (`ReportProjectionEventLoader.cs` Z. 140) und die Contact-/SavingsPlan-Aufrufstellen (Z. 774, 796) bleiben über den Default-Parameter unverändert.
- Die dokumentierte Folgeaufgabe (Drilldown-Asymmetrie verknüpfter Sparplan-Posten) entspricht dem Plan-Vorgehen „größere Befunde als Folgeaufgaben notieren" — nur der Speicherort (`review-feature.md` statt `continue.md`) weicht vom benannten Planelement ab.
- E2E-Tests sind im Code korrekt aufgebaut (Fixture, Gateway, Seeds, Assertions), benötigen für die Verifikation einen Playwright-Lauf außerhalb dieses Reviews.
