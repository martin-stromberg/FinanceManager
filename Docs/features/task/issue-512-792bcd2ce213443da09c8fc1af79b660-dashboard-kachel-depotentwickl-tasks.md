# Tasks: Dashboard-Kachel „Depotentwicklung & Dividenden" (Issue #512)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | DTO | `PortfolioTileId`-Enum um `DepotDevelopment = 4` erweitern | Erledigt | `EnumCoverageTests.PortfolioTileId_Values` |
| 2 | DTO | `PortfolioDepotDevelopmentDto` + `PortfolioDepotDevelopmentYear` in `FinanceManager.Shared/Dtos/Portfolio/PortfolioDepotDevelopmentDto.cs` anlegen | Erledigt | `PortfolioAnalysisReportServiceTests_DepotDevelopment` (Records werden in allen 9 Tests verwendet) |
| 3 | DTO | `PortfolioAnalysisReportDto` um Record-Parameter `DepotDevelopment` erweitern | Erledigt | Alle `CreateReport`-Helfer übergeben das neue DTO; Unit-Tests kompilieren und laufen |
| 4 | Logik | `PortfolioAnalysisReportService.BuildDepotDevelopment` implementieren (Jahresschleife, Stichtags-Depotwert, Netto-Dividenden, Stichtags-FIFO, Startjahr-Regel) | Erledigt | `PortfolioAnalysisReportServiceTests_DepotDevelopment` (9 Tests, bestanden) |
| 5 | Logik | `GetPortfolioAnalysisReportAsync` um `BuildDepotDevelopment`-Aufruf und DTO-Argument erweitern | Erledigt | `PortfolioAnalysisReportServiceTests_DepotDevelopment` (9 Tests, bestanden) |
| 6 | Logik | `CacheSchemaVersion` in `PortfolioAnalysisReportCacheService` von `"5"` auf `"6"` erhöhen | Erledigt | `PortfolioAnalysisReportCacheServiceTests` (Schema-Miss-Tests bestehen weiter) |
| 7 | API | `DefaultConfigurationDto` um `PortfolioTileId.DepotDevelopment` ergänzen (`DefaultActiveTiles` auslagern) | Erledigt | `GetKpiConfiguration_Default_IncludesDepotDevelopment` |
| 8 | API | `MergeMissingTiles`-Hilfsmethode in `PortfolioAnalysisReportController` implementieren und in `GetKpiConfigurationAsync` anwenden | Erledigt | `GetKpiConfiguration_SavedConfigMissingTile_MergesIntoOrderAndActive`, `…_WithDeactivatedTile_StaysInactive`, `…_MissingRisk_MergesIntoOrderOnly` |
| 9 | UI | `PortfolioBarLineChart.razor` in `Components/Shared` anlegen (SVG Linie+Balken, getrennte Skalen, Tooltips, Legende, Leerzustand) | Erledigt | `PortfolioBarLineChartTests` (4 bUnit-Tests, bestanden) |
| 10 | UI | `PortfolioDepotDevelopmentCard.razor` in `Components/Pages/Portfolio` anlegen (Dropdowns, Diagramm, Tabelle, Fußnoten, komponenteninterne Enums) | Erledigt | `PortfolioDepotDevelopmentCardTests` (6 bUnit-Tests, bestanden) |
| 11 | UI | `PortfolioAnalysisReportPage.razor`: Render-Verzweigung und `GetTileLabel`-Eintrag für `DepotDevelopment` | Erledigt | E2E `DepotDevelopmentScenario_ShouldRenderChartAndTable`, `…_CanBeDeactivatedInEditMode` |
| 12 | UI | `Pages.resx`/`Pages.de.resx`/`Pages.en.resx`: `PortfolioReport_Tile_DepotDevelopment` und `PortfolioReport_Dev_*`-Keys (Filter, Basis, Perioden, 7 Spaltenköpfe, Fußnoten, Empty, Chart-Aria, Legende) in de+en | Erledigt | bUnit-Tests rendern über Localizer; E2E prüft sichtbare Texte |
| 13 | UI | `app.PortfolioReport.css` und `theme.Dark.PortfolioReport.css`: Filterzeile, Chart-Klassen (Linie dunkelblau, Balken grün via Theme-Variablen), Tabellen-Scroll-Wrapper | Erledigt | Kein direkter Test (visuell via E2E: Elemente vorhanden/sichtbar) |
| 14 | Tests | `EnumCoverageTests`: `AssertValues<PortfolioTileId>` mit vollständiger Memberliste ergänzen | Erledigt | `EnumCoverageTests.PortfolioTileId_Values` bestanden |
| 15 | Tests | `CreateReport`-Helfer in `PortfolioAnalysisReportControllerTests`, `PortfolioAnalysisReportCacheServiceTests`, `PortfolioAnalysisReportPageViewModelTests` an neuen DTO-Parameter anpassen | Erledigt | Kompiliert; betroffene Testklassen laufen |
| 16 | Tests | `PortfolioAnalysisReportServiceTests`: Depotentwicklungs-Tests (Jahreszeilen, Vorjahres-Basis, Startjahr-Basis, Netto-Dividenden, FIFO-Kapital, IsYtd, Leerzustand) | Erledigt | `PortfolioAnalysisReportServiceTests_DepotDevelopment` (9 Tests, bestanden) |
| 17 | Tests | `PortfolioAnalysisReportControllerTests`: Merge-Tests (Default enthält TileId, Merge aktiv am Ende, deaktivierte Kachel bleibt inaktiv, `Risk` nur in `TileOrder`) | Erledigt | 4 Tests, bestanden |
| 18 | Tests | `PortfolioDepotDevelopmentCardTests` (bUnit, neu): Moduswechsel, Zeitraum-Filter, `*`-Fußnote, Leerzustand, Basis-0-`—`, Formatierung | Erledigt | 6 Tests, bestanden |
| 19 | E2E-Tests | `PortfolioAnalysisReportE2ETests`: Happy Path mit geseedetem Wertpapier (Buy/Dividend/Kurse via `AppDbContext`) — `DepotDevelopmentScenario_ShouldRenderChartAndTable` | Erledigt | Test vorhanden (E2E-Datei Z. 170) |
| 20 | E2E-Tests | `DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable` (Dropdown-Wechsel ohne Reload) | Erledigt | Test vorhanden (E2E-Datei Z. 200) |
| 21 | E2E-Tests | `DepotDevelopmentScenario_PeriodFilterLimitsRows` (Zeitraum-Filter) | Erledigt | Test vorhanden (E2E-Datei Z. 230) |
| 22 | E2E-Tests | `DepotDevelopmentScenario_MergedIntoSavedConfiguration` (Bestandskonfiguration via DB-Seed) | Erledigt | Test vorhanden (E2E-Datei Z. 260) |
| 23 | E2E-Tests | `DepotDevelopmentScenario_NoPostings_ShowsEmptyState` und `DepotDevelopmentScenario_CanBeDeactivatedInEditMode` | Erledigt | Tests vorhanden (E2E-Datei Z. 285, 310) |
| 24 | Tests | `PortfolioAnalysisReportServiceTests`: Stichtags-Kurs-Fallback-Test `…_StaleOrMissingPrice_FallsBackToLatestKnownOrZero` (letzter Kurs ≤ 31.12.; Position ohne Kurs → Beitrag 0) | Erledigt | Bestanden |
| 25 | Tests | `PortfolioAnalysisReportServiceTests`: Serienende-Test `…_AllPositionsSold_SeriesEndsAtLastTransactionYear` (keine Phantom-YTD-Zeile nach Komplettverkauf) | Erledigt | Bestanden |
| 26 | Tests | `PortfolioBarLineChartTests` (bUnit, neu in `FinanceManager.Tests/Components/Shared/`): getrennte Skalierung, `<title>`-Tooltips mit exakt formatiertem Wert, Legende, `EmptyText`/`AriaLabel` | Erledigt | 4 Tests, bestanden |
| 27 | UI | YTD-`*`-Suffix lokalisieren: resx-Key `PortfolioReport_Dev_YtdSuffix` (Wert `*`) in `Pages.resx`/`Pages.de.resx`/`Pages.en.resx` anlegen (oder `PortfolioReport_YtdSuffix` wiederverwenden, falls geänderte Markierung akzeptabel) und in `PortfolioDepotDevelopmentCard.razor` (Tabellenzelle Z. 54, Chart-Label Z. 115) statt des hartkodierten `"*"` verwenden | Erledigt | Neuer Key `PortfolioReport_Dev_YtdSuffix` = `*` in allen drei resx angelegt; beide Stellen umgestellt. `PortfolioDepotDevelopmentCardTests.YtdRow_RendersAsteriskAndFootnote` bestanden (8/8 Card-Tests) |

## Nachbesserungen aus IssueComment (PR #513, comment-6873507722)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 28 | Logik | `PortfolioDepotDevelopmentYear` um `NetFlow` (Buy−Sell des Jahres) erweitern; `BuildDepotDevelopment` füllt es je Jahr; `CacheSchemaVersion` `"6"`→`"7"` | Erledigt | `…DepotDevelopment.NetFlow_*` (2 Tests, bestanden) |
| 29 | UI | `PortfolioGroupedBarChart.razor` (2 Balken/Jahr: Ein-/Auszahlungen + Dividenden, gemeinsame Skala mit Baseline 0, Tooltips, Legende, Empty) | Erledigt | `PortfolioGroupedBarChartTests` (bUnit, bestanden) |
| 30 | UI | Karte: YoC-Modus nutzt Grouped-Chart + 4-Spalten-Tabelle (Jahr, Σ Ein-/Auszahlungen, Netto-Dividende, Rendite) | Erledigt | `PortfolioDepotDevelopmentCardTests` YoC-Tests + E2E `…_SwitchingYieldBasisUpdatesTable` |
| 31 | UI | Karte: Mobile (<640 px) auf 4 Spalten reduzieren (Jahr, Dividende/Depotwert, Veränderung €/%, Rendite), „Verhältnis" weg, kein horizontales Scrollen | Erledigt | Compact-Klassen-Tests (bUnit); `overflow-x` auf `hidden` |
| 32 | UI | Anzeigemodus + Zeitraum im `localStorage` persistieren (`fm.portfolio.dev.yieldBasis`, `fm.portfolio.dev.period`), Restore beim ersten Render, Fehler tolerant | Erledigt | bUnit-Restore/Persist-Tests (FakeJSRuntime) + E2E `…_FilterChoicesPersistAcrossReload` |
| 33 | Recht | Impressum: Hinweis auf localStorage-Nutzung für UI-Einstellungen (`Privacy_UiPreferences`, de+en) | Erledigt | resx-Keys vorhanden; Render in `Legal.razor` |
| 34 | resx | Neue Keys (Chart-Legende Ein-/Auszahlungen, Spaltenkopf Ein-/Auszahlungen, Mobile-Kombiköpfe, YoC-Fußnote) in `Pages.resx`/`.de`/`.en` | Erledigt | bUnit rendern über Localizer |
| 35 | CSS | `.dev-mobile-only`/`.dev-col-*`-Umschaltung, `overflow-x: hidden` für dev-Tabelle, Grouped-Chart-Theme-Variablen (Light+Dark) | Erledigt | Kein direkter Test (Layout via E2E geprüft) |
| 36 | Tests | bUnit: Karte (YoC-Tabelle, Compact-Klassen, Persistenz), GroupedChart, Service `NetFlow` | Erledigt | alle bestanden (2283 Unit-Tests gesamt) |
| 37 | E2E | YoC-Umschaltung (Grouped-Chart + 4 Header) und Persistenz über Reload | Erledigt | 7 DepotDevelopment-E2E-Tests bestanden |
| 38 | Doku | `Docs/help/wertpapiermanagement/*` + `RELEASE_NOTES.md` an YoC-Modus, Mobile-Layout und localStorage-Persistenz anpassen | Erledigt | – |

## Nachbesserungen aus IssueComment (PR #513, comment-6873954751)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 39 | UI | „Verhältnis (€/€)"-Spalte auch in der Desktopansicht entfernen (Jahresanfangs-Tabelle: 6 Spalten; Key `PortfolioReport_Dev_Th_Ratio` und `FormatRatio` entfernt) | Erledigt | `PortfolioDepotDevelopmentCardTests` (angepasste Zell-/Header-Assertions, 19 Tests bestanden) |
| 40 | CSS | Spaltenreduktion an Kachelbreite koppeln statt Viewport: `.portfolio-dev-table-wrap` als `container-type: inline-size` + `@container (max-width: 560px)`; `min-width` und `overflow-x: auto` ersetzt durch `table-layout: fixed` + `overflow-x: hidden` (Scrollen unmöglich) | Erledigt | E2E `DepotDevelopmentScenario_NarrowViewport_TableFitsWithoutHorizontalScroll` (390-px-Viewport: `scrollWidth <= clientWidth`, 4 sichtbare Spalten in beiden Modi) |
| 41 | Tests | E2E-Mobile-Test ergänzt; bUnit-Assertions auf 6-Spalten-Tabelle angepasst | Erledigt | 8/8 DepotDevelopment-E2E-Tests bestanden |
| 42 | Doku | `beschreibung.md`, `ablauf-anwender.md`, `ablauf-technisch.md`, `business-rules.md`, `RELEASE_NOTES.md` an Verhältnis-Entfernung und Container-Query-Mechanik angepasst | Erledigt | – |
