# Tasks: Dashboard-Kachel „Depotentwicklung & Dividenden" (Issue #512)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | DTO | `PortfolioTileId`-Enum um `DepotDevelopment = 4` erweitern | Offen | — |
| 2 | DTO | `PortfolioDepotDevelopmentDto` + `PortfolioDepotDevelopmentYear` in `FinanceManager.Shared/Dtos/Portfolio/PortfolioDepotDevelopmentDto.cs` anlegen | Offen | — |
| 3 | DTO | `PortfolioAnalysisReportDto` um Record-Parameter `DepotDevelopment` erweitern | Offen | — |
| 4 | Logik | `PortfolioAnalysisReportService.BuildDepotDevelopment` implementieren (Jahresschleife, Stichtags-Depotwert, Netto-Dividenden, Stichtags-FIFO, Startjahr-Regel) | Offen | — |
| 5 | Logik | `GetPortfolioAnalysisReportAsync` um `BuildDepotDevelopment`-Aufruf und DTO-Argument erweitern | Offen | — |
| 6 | Logik | `CacheSchemaVersion` in `PortfolioAnalysisReportCacheService` von `"5"` auf `"6"` erhöhen | Offen | — |
| 7 | API | `DefaultConfigurationDto` um `PortfolioTileId.DepotDevelopment` ergänzen (`DefaultActiveTiles` auslagern) | Offen | — |
| 8 | API | `MergeMissingTiles`-Hilfsmethode in `PortfolioAnalysisReportController` implementieren und in `GetKpiConfigurationAsync` anwenden | Offen | — |
| 9 | UI | `PortfolioBarLineChart.razor` in `Components/Shared` anlegen (SVG Linie+Balken, getrennte Skalen, Tooltips, Legende, Leerzustand) | Offen | — |
| 10 | UI | `PortfolioDepotDevelopmentCard.razor` in `Components/Pages/Portfolio` anlegen (Dropdowns, Diagramm, Tabelle, Fußnoten, komponenteninterne Enums) | Offen | — |
| 11 | UI | `PortfolioAnalysisReportPage.razor`: Render-Verzweigung und `GetTileLabel`-Eintrag für `DepotDevelopment` | Offen | — |
| 12 | UI | `Pages.resx`/`Pages.de.resx`/`Pages.en.resx`: alle `PortfolioReport_Dev_*`-Keys und `PortfolioReport_Tile_DepotDevelopment` (de+en) | Offen | — |
| 13 | UI | `app.PortfolioReport.css` und `theme.Dark.PortfolioReport.css`: Filterzeile, Chart-Klassen (Linie dunkelblau, Balken grün via Theme-Variablen), Tabellen-Scroll-Wrapper | Offen | — |
| 14 | Tests | `EnumCoverageTests`: `AssertValues<PortfolioTileId>` mit vollständiger Memberliste ergänzen | Offen | — |
| 15 | Tests | `CreateReport`-Helfer in `PortfolioAnalysisReportControllerTests`, `PortfolioAnalysisReportCacheServiceTests`, `PortfolioAnalysisReportPageViewModelTests` an neuen DTO-Parameter anpassen | Offen | — |
| 16 | Tests | `PortfolioAnalysisReportServiceTests`: Depotentwicklungs-Tests (Jahreszeilen, Vorjahres-Basis, Startjahr-Basis, Netto-Dividenden, FIFO-Kapital, IsYtd, Leerzustand) | Offen | — |
| 17 | Tests | `PortfolioAnalysisReportControllerTests`: Merge-Tests (Default enthält TileId, Merge aktiv am Ende, deaktivierte Kachel bleibt inaktiv, `Risk` nur in `TileOrder`) | Offen | — |
| 18 | Tests | `PortfolioDepotDevelopmentCardTests` (bUnit, neu): Moduswechsel, Zeitraum-Filter, `*`-Fußnote, Leerzustand, Basis-0-`—`, Formatierung | Offen | — |
| 19 | E2E-Tests | `PortfolioAnalysisReportE2ETests`: Happy Path mit geseedetem Wertpapier (Buy/Dividend/Kurse via `AppDbContext`) — `DepotDevelopmentScenario_ShouldRenderChartAndTable` | Offen | — |
| 20 | E2E-Tests | `DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable` (Dropdown-Wechsel ohne Reload) | Offen | — |
| 21 | E2E-Tests | `DepotDevelopmentScenario_PeriodFilterLimitsRows` (Zeitraum-Filter) | Offen | — |
| 22 | E2E-Tests | `DepotDevelopmentScenario_MergedIntoSavedConfiguration` (Bestandskonfiguration via DB-Seed) | Offen | — |
| 23 | E2E-Tests | `DepotDevelopmentScenario_NoPostings_ShowsEmptyState` und `DepotDevelopmentScenario_CanBeDeactivatedInEditMode` | Offen | — |
| 24 | Tests | `PortfolioAnalysisReportServiceTests`: Stichtags-Kurs-Fallback-Test `…_StaleOrMissingPrice_FallsBackToLatestKnownOrZero` (letzter Kurs ≤ 31.12.; Position ohne Kurs → Beitrag 0) | Offen | — |
| 25 | Tests | `PortfolioAnalysisReportServiceTests`: Serienende-Test `…_AllPositionsSold_SeriesEndsAtLastTransactionYear` (keine Phantom-YTD-Zeile nach Komplettverkauf) | Offen | — |
| 26 | Tests | `PortfolioBarLineChartTests` (bUnit, neu in `FinanceManager.Tests/Components/Shared/`): getrennte Skalierung, `<title>`-Tooltips mit exakt formatiertem Wert, Legende, `EmptyText`/`AriaLabel` (Muster `MiniBarChartTests_BarScaling`) | Offen | — |
