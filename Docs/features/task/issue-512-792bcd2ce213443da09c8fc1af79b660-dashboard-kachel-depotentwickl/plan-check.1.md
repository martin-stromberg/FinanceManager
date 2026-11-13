# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Neue Kachel „Depotentwicklung Jahreswechsel" im Depot-Bericht, integriert in `PortfolioTileId`, `PortfolioKpiConfiguration`, Render-Verzweigung und `GetTileLabel` | Umsetzungsreihenfolge Schritte 1, 6, 9; `DepotDevelopment = 4`, `DefaultConfigurationDto`-Erweiterung, `else if`-Zweig, Label-Switch | `GetKpiConfiguration_Default_IncludesDepotDevelopment`; E2E `DepotDevelopmentScenario_ShouldRenderChartAndTable` | Abgedeckt |
| Kombiniertes Diagramm pro Kalenderjahr: Linie = Depotwert zum 31.12., Balken = Netto-Dividenden | Neue Shared-Komponente `PortfolioBarLineChart` (SVG, getrennte Skalierung, Legende); `BuildDepotDevelopment` liefert `YearEndValue`/`NetDividends` | E2E `…_ShouldRenderChartAndTable` (Sichtbarkeit); Service-Tests zu Jahreswerten und Netto-Dividenden | Abgedeckt (Darstellung); Skalierungs-/Legenden-Logik des Charts ohne eigenen Test — siehe Lücken |
| Tooltip je Jahreswechsel mit exaktem Wert (Erwartete Wirkung) | `<title>`-Tooltips und `TooltipFormatter`-Parameter in `PortfolioBarLineChart` geplant | **Kein** Test (weder bUnit noch E2E) assertiert Vorhandensein oder Inhalt der Tooltips | Lücke |
| Siebenspaltige Tabelle: Jahreswechsel, Depotwert €, Veränderung €, Veränderung %, Netto-Dividende €, Verhältnis €/€, Rendite % | Karten-Markup; Veränderung aus `YearEndValue − YearStartValue` ableitbar; Spaltenköpfe als resx-Keys | bUnit `Formatting_CurrencyAndSignedPercent`; `YieldBasisSwitch_UpdatesRatioAndYieldColumns` | Abgedeckt |
| Umschaltbare Rendite-Basis („Jahresanfangswert" ↔ „Eingezahltes Kapital / YoC") clientseitig ohne Neuladen, inkl. modusabhängiger Fußnote | Komponenteninternes `private enum DepotDevelopmentYieldBasis`, `<select @bind>`, alle Basen im DTO enthalten | bUnit `YieldBasisSwitch_UpdatesRatioAndYieldColumns`; E2E `…_SwitchingYieldBasisUpdatesTable` | Abgedeckt |
| Zeitraum-Filter-Dropdown, Standard „Gesamte Historie" | Komponenteninternes `DepotDevelopmentPeriodFilter` (All/5/10 Jahre), clientseitige Filterung | bUnit `PeriodFilter_LimitsRenderedRows`; E2E `…_PeriodFilterLimitsRows` | Abgedeckt |
| Benutzer ohne Wertpapierbuchungen → Leeranzeige, keine Jahr-Zeilen | `BuildDepotDevelopment` liefert leere `Years`; Leerzustand nach `MiniBarChart.EmptyText`-Muster | Service-Test `…_NoPostings_ReturnsEmptyYears`; bUnit `EmptyYears_RendersEmptyState`; E2E `…_NoPostings_ShowsEmptyState` | Abgedeckt |
| Fehlende Kurse am Stichtag → letzter bekannter Kurs ≤ Stichtag, sonst 0 (Konvention `GetPortfolioValueOnDate`/`LatestPriceOnOrBefore`) | Explizit in Scope-Klärung und Programmablauf beschrieben (Wiederverwendung des Helfers) | **Kein** geplanter Test deckt einen Jahresstichtag ohne bzw. mit veraltetem Kurs ab; `SecurityValuationHelper` hat keine dedizierte Testklasse | Lücke |
| Kein Vorjahres-Depotwert (Startjahr) → `YearStartValue` = eingezahltes Kapital; Veränderung/Rendite auf Anfangskapital | `year == firstYear → YearStartValue = CumulativeInvestedCapital`; Fußnote weist darauf hin | Service-Test `…_FirstYear_UsesInvestedCapitalAsStart`; `…_YearStartValue_EqualsPreviousYearEnd`; bUnit-Fußnotentest | Abgedeckt |
| Fehlende `PortfolioKpiConfiguration` → Server-Default enthält neue Kachel; gespeicherte Konfigurationen ohne TileId → definiertes Verhalten | Merge `MergeMissingTiles` in `GetKpiConfigurationAsync` (TileOrder-Ende + `ActiveTileIds` nur für Default-Menge; nicht persistiert) | 4 Controller-Tests (Default, Merge aktiv, deaktiviert bleibt deaktiviert, `Risk` nur Order); E2E `…_MergedIntoSavedConfiguration`, `…_CanBeDeactivatedInEditMode` | Abgedeckt |
| Veraltete Cache-Einträge → `CacheSchemaVersion`-Erhöhung | `"5"` → `"6"` (Schritt 5); `Parameter`-Vergleich im Cache-Service | Bestehender Test `CacheHit_EntryFromOlderDtoSchema_…` bleibt gültig; benannt | Abgedeckt |
| Kachel im Bearbeitungsmodus ein-/ausblendbar und verschiebbar | Generischer Mechanismus (`EditOrder`/`EditActive`, `ToggleTileActive`/`MoveEditTile`) greift über gemergte Konfiguration; Label via `GetTileLabel` | E2E `…_CanBeDeactivatedInEditMode`; bestehende ViewModel-/Edit-Tests decken den Mechanismus generisch | Abgedeckt |
| Serverseitige Berechnung aus Buchungen/Kursen des angemeldeten Benutzers (OwnerUserId) | `BuildDepotDevelopment` arbeitet auf `LoadPositionsAsync`-Ergebnis (Owner-gefiltert); kein Client-Parameter | Bestehender Test `GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData`; E2E `MultiUserIsolationScenario_…` bleibt gültig | Abgedeckt |
| Neues Enum-Mitglied in `EnumCoverageTests` (AGENT.md) | Schritt 1: `AssertValues<PortfolioTileId>` mit vollständiger Liste | Der Eintrag selbst ist der Testnachweis | Abgedeckt |
| Alle neuen UI-Texte in `Pages.*.resx` de + en, keine hartkodierten Strings | Vollständige Key-Liste in „Änderungen an bestehenden Klassen" (Titel, Filter, Optionen, Spalten, Fußnoten, Leerzustand, Aria, Legende) | bUnit-Tests via `PagesStringLocalizer` | Abgedeckt |
| Kulturabhängige Formate (`N0`/`N2`, Prozent mit `+`-Vorzeichen), Farben via Theme-Variablen (Linie dunkelblau, Balken grün) | Formatierungs-Konvention der bestehenden Karten; CSS in `app.PortfolioReport.css`/`theme.Dark.PortfolioReport.css` | bUnit `Formatting_CurrencyAndSignedPercent` (mit `WithInvariantCulture`) | Abgedeckt |
| Laufendes Jahr als eigene Zeile mit `IsYtd`/`*`-Markierung | `IsYtd`-Flag im Jahr-Record, `*`-Suffix + Fußnote (`PortfolioAnnualReturnPoint`-Konvention) | Service-Test `…_MarkedIsYtd`; bUnit `YtdRow_RendersAsteriskAndFootnote` | Abgedeckt |
| Serienende bei vollständig verkauften Beständen (`lastYear` = Jahr der jüngsten Transaktion, keine Phantom-YTD-Zeile ohne Bestände) | In Programmablauf Schritt 2 definiert (Konvention `BuildPerformance`) | **Kein** Test benannt (nur der `IsYtd = true`-Fall bei gehaltenen Beständen) | Lücke |
| Scope-Ausschluss „Keine Änderung bestehender Kacheln" | „Seiteneffekte und Risiken": `BuildStructure`/`BuildPerformance`/`BuildCashflow` unangetastet | Bestehende Service-/E2E-Tests bleiben unverändert gültig (benannt) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Tooltip-Nachweis: Die „Erwartete Wirkung" fordert einen Tooltip je Jahreswechsel mit exaktem Wert (`<title>`-Elemente/`TooltipFormatter` im `PortfolioBarLineChart`). Es gibt keinen Test (bUnit oder E2E), der Vorhandensein und Inhalt der Tooltips assertiert — z. B. bUnit-Assert auf `<title>`-Knoten mit formatiertem Depotwert/Dividende je Datenpunkt.
- [ ] Fehlende Kurse am Jahresstichtag: Der Plan beschreibt den Fallback (`LatestPriceOnOrBefore`, sonst 0) als explizites Verhalten im „Umgang mit fehlenden Objekten", benennt aber keinen Service-Test für eine Jahreszeile ohne Kurs bzw. mit veraltetem Kurs (z. B. Kauf in Jahr 1, letzter Kurs vor Jahresende → `YearEndValue` nutzt letzten Kurs; Position ganz ohne Kurs → Beitrag 0). `SecurityValuationHelper` besitzt keine eigene Testklasse, der bestehende Testbestand deckt den Stichtags-Fallback im Jahresschleifen-Kontext nicht ab.
- [ ] Serienende nach Komplettverkauf: Programmablauf legt `lastYear` = Jahr der jüngsten Transaktion fest, wenn heute keine Bestände gehalten werden (keine `IsYtd`-Zeile für das laufende Jahr). Dafür fehlt ein Test (Gegenstück zu `…_MarkedIsYtd`), der sicherstellt, dass nach einem Komplettverkauf keine laufende-Jahr-Zeile erscheint.
- [ ] `PortfolioBarLineChart` ohne eigenen Komponententest: Die getrennte Normalisierung von Linie und Balken ist neue, nicht-triviale Renderlogik; Projektkonvention sieht Chart-Tests vor (`MiniBarChartTests_BarScaling`). Weder Skalierung noch Legende, `EmptyText`-/`AriaLabel`-Parameter sind testseitig eingeplant — weder als eigene bUnit-Klasse noch als benannte Asserts in den Karten-Tests.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Happy Path: Öffnen von `/portfolio/analysis-report` → Kachel mit Diagramm und Jahrestabelle sichtbar | `DepotDevelopmentScenario_ShouldRenderChartAndTable` (Pflicht, Seed via `AppDbContext` nach `SeedSecurityAsync`-Muster) | Abgedeckt |
| Rendite-Basis umschalten → Spalten/Fußnote ohne Reload aktualisiert | `DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable` (Pflicht) | Abgedeckt |
| Zeitraum-Filter anwenden → Zeilen begrenzt | `DepotDevelopmentScenario_PeriodFilterLimitsRows` (Pflicht) | Abgedeckt |
| Bestandskonfiguration ohne neue TileId → Kachel erscheint aktiv am Ende | `DepotDevelopmentScenario_MergedIntoSavedConfiguration` (Pflicht, DB-Seed der Konfiguration) | Abgedeckt |
| Nutzer ohne Buchungen → Leeranzeige | `DepotDevelopmentScenario_NoPostings_ShowsEmptyState` (Empfohlen) | Abgedeckt |
| Bearbeitungsmodus: Kachel aufgeführt, deaktivierbar, nach Speichern verborgen | `DepotDevelopmentScenario_CanBeDeactivatedInEditMode` (Empfohlen) | Abgedeckt |
| Verschieben der Kachel im Bearbeitungsmodus | Kein eigener Test | Nicht erforderlich mit Begründung: Verschieben läuft über den generischen `EditOrder`/`MoveEditTile`-Mechanismus ohne kachelspezifischen Code; durch bestehende ViewModel-/Edit-Tests abgesichert |
| Tooltip mit exaktem Wert im Diagramm | Kein Test geplant | Lücke (siehe Testanforderungen; `<title>`-Inhalte wären per DOM-Assert prüfbar) |

## Fehlende oder unvollständige Planbestandteile

- [ ] Keine — die Umsetzungsschritte selbst sind vollständig: Scope-Klärung vollständig gefüllt, alle 7 Scope-Ausschlüsse mit Begründung und Status `bestätigt`, alle offenen Fragen der Anforderung als Designentscheidungen beantwortet, Umsetzungsreihenfolge mit Voraussetzungen, betroffene bestehende Tests und Test-Helfer benannt, Happy Path und Fehlfälle als Akzeptanzszenarien mit Ausgangszustand beschrieben. Die Lücken betreffen ausschließlich die Testplanung (siehe Abschnitt „Fehlende oder unvollständige Testanforderungen").

## Hinweise

- Für den Tooltip-Nachweis reicht ein bUnit-Test der Karte/des Charts, der pro Datenpunkt das `<title>`-Element mit formatiertem Depotwert und Netto-Dividende assertiert; ein E2E-Test ist hierfür nicht zwingend (kein Nutzerfluss-Element, sondern statisches Markup-Detail) — im E2E könnte der Happy-Path-Fact alternativ einen DOM-Assert auf `<title>` enthalten.
- Der Kurs-Fallback-Test lässt sich im `PortfolioAnalysisReportServiceTests`-Rahmen mit den vorhandenen Helfern (`AddBuyPosting`, `AddPrice`) aufsetzen: Kauf in Jahr N, letzter `AddPrice` vor dem 31.12. → `YearEndValue` = Stücke × letztem Kurs; zweite Position ganz ohne Kurs → Beitrag 0.
- Die leere gefilterte Liste (Zeitraum-Filter engt auf Zeitraum ohne Daten ein) ist im Programmablauf beschrieben, aber nicht testseitig benannt — kann im vorhandenen bUnit `PeriodFilter_LimitsRenderedRows` mit abgedeckt werden; als eigenständige Lücke nicht gewertet, da der Leerzustand generisch getestet ist.
