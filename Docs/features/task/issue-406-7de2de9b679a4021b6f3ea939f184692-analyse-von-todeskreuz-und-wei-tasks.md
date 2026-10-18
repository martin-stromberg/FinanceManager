# Tasks: Analyse von Todeskreuz und weiteren Umkehrsignalen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `TrendReversalOptions` mit allen Parametern anlegen | Offen | — |
| 2 | Logik | Enums `TrendReversalSignalType`, `TrendReversalSignalStrength`, `TrendStatus`, `LineBreakClassification`, `MarketStructureClassification` anlegen | Offen | — |
| 3 | Logik | Records `ReversalSignal`, `DeathCrossResult`, `LongTermLineBreakResult`, `MarketStructureResult`, `MacdResult`, `RsiResult`, `TrendReversalAnalysis` anlegen | Offen | — |
| 4 | Logik | `TrendReversalAnalyzer.Analyze` — SMA + Todeskreuz-Erkennung | Offen | — |
| 5 | Logik | `TrendReversalAnalyzer` — 200-Tage-Linien-Bruch mit Klassifikation | Offen | — |
| 6 | Logik | `TrendReversalAnalyzer` — Marktstruktur (niedrigere Hochs/Tiefs) | Offen | — |
| 7 | Logik | `TrendReversalAnalyzer` — MACD + bärisches Crossover | Offen | — |
| 8 | Logik | `TrendReversalAnalyzer` — RSI + negative Divergenz | Offen | — |
| 9 | Logik | `TrendReversalAnalyzer` — Signalaggregation, Score, Trendstatus | Offen | — |
| 10 | DTO | `TrendReversalDto` + DTO-Enums/Records in Shared anlegen | Offen | — |
| 11 | Logik | `ITrendReversalService` in Application anlegen | Offen | — |
| 12 | Logik | `TrendReversalService` in Infrastructure implementieren + DI-Registrierung | Offen | — |
| 13 | API | `SecuritiesController.GetTrendReversalAsync` (GET `{id}/trend-reversal`) | Offen | — |
| 14 | API | `IApiClient.Securities_GetTrendReversalAsync` + Implementierung | Offen | — |
| 15 | Web | `ProgramExtensions`: `TrendReversalOptions` binden | Offen | — |
| 16 | UI | `TrendReversalWidget.razor` erstellen (Status, Signalliste, Panel, „keine Signale"-Hinweis) | Offen | — |
| 17 | UI | `TrendReversalWidget.de.resx` + `.en.resx` erstellen | Offen | — |
| 18 | UI | `trw-*`-Styles in `app.ReturnSummaryWidget.css` ergänzen | Offen | — |
| 19 | UI | `CardPage.razor`: Widget einbinden | Offen | — |
| 20 | Tests | `TrendReversalAnalyzerTests_DeathCross` | Offen | — |
| 21 | Tests | `TrendReversalAnalyzerTests_LineBreak` | Offen | — |
| 22 | Tests | `TrendReversalAnalyzerTests_MarketStructure` | Offen | — |
| 23 | Tests | `TrendReversalAnalyzerTests_Momentum` | Offen | — |
| 24 | Tests | `TrendReversalAnalyzerTests_Scoring` | Offen | — |
| 25 | Tests | `TrendReversalServiceTests` | Offen | — |
| 26 | Tests | `TrendReversalDtoEnumMappingTests` | Offen | — |
| 27 | Tests | `SecuritiesControllerTests.Create()` um `Mock<ITrendReversalService>` erweitern + 404-Test | Offen | — |
| 28 | E2E-Tests | `SecurityTrendReversalE2ETests` (Signale sichtbar / „keine Signale" sichtbar) | Offen | — |
| 29 | Dokumentation | `Docs/help/wertpapiermanagement/umkehrsignale.md` + `index.md`-Verlinkung | Offen | — |
