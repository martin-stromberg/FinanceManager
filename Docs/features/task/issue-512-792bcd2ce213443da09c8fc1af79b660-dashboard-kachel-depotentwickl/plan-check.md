# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde. Alle vier in `plan-check.1.md` gemeldeten Testlücken sind im aktualisierten `plan.md` verifiziert behoben (Nachweis je Lücke in den Tabellen unten).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Neue Kachel „Depotentwicklung Jahreswechsel" im Depot-Bericht, integriert in `PortfolioTileId`, `PortfolioKpiConfiguration`, Render-Verzweigung und `GetTileLabel` | Umsetzungsreihenfolge Schritte 1, 6, 9; `DepotDevelopment = 4`, `DefaultConfigurationDto`-Erweiterung, `else if`-Zweig, Label-Switch | `GetKpiConfiguration_Default_IncludesDepotDevelopment`; E2E `DepotDevelopmentScenario_ShouldRenderChartAndTable` | Abgedeckt |
| Kombiniertes Diagramm pro Kalenderjahr: Linie = Depotwert zum 31.12., Balken = Netto-Dividenden | Neue Shared-Komponente `PortfolioBarLineChart` (SVG, getrennte Skalierung, Legende); `BuildDepotDevelopment` liefert `YearEndValue`/`NetDividends` | E2E `…_ShouldRenderChartAndTable`; bUnit `Render_LineAndBars_ScaledIndependently` (neu) | Abgedeckt |
| Tooltip je Jahreswechsel mit exaktem Wert (Erwartete Wirkung) | `<title>`-Tooltips und `TooltipFormatter`-Parameter in `PortfolioBarLineChart` | **Neu:** bUnit `Render_Points_EmitsTitleTooltipWithFormattedValues` (Inhalt via `TooltipFormatter`, `WithInvariantCulture`); zusätzlich DOM-Assert auf `<title>` je Datenpunkt im E2E-Happy-Path | Abgedeckt (Lücke aus Runde 1 behoben) |
| Siebenspaltige Tabelle: Jahreswechsel, Depotwert €, Veränderung €, Veränderung %, Netto-Dividende €, Verhältnis €/€, Rendite % | Karten-Markup; Veränderung aus `YearEndValue − YearStartValue`; Spaltenköpfe als resx-Keys | bUnit `Formatting_CurrencyAndSignedPercent`; `YieldBasisSwitch_UpdatesRatioAndYieldColumns` | Abgedeckt |
| Umschaltbare Rendite-Basis („Jahresanfangswert" ↔ „Eingezahltes Kapital / YoC") clientseitig ohne Neuladen, inkl. modusabhängiger Fußnote | Komponenteninternes `private enum DepotDevelopmentYieldBasis`, `<select @bind>`, alle Basen im DTO | bUnit `YieldBasisSwitch_UpdatesRatioAndYieldColumns`; E2E `…_SwitchingYieldBasisUpdatesTable` | Abgedeckt |
| Zeitraum-Filter-Dropdown, Standard „Gesamte Historie" | Komponenteninternes `DepotDevelopmentPeriodFilter` (All/5/10 Jahre), clientseitige Filterung | bUnit `PeriodFilter_LimitsRenderedRows`; E2E `…_PeriodFilterLimitsRows` | Abgedeckt |
| Benutzer ohne Wertpapierbuchungen → Leeranzeige, keine Jahr-Zeilen | `BuildDepotDevelopment` liefert leere `Years`; Leerzustand nach `MiniBarChart.EmptyText`-Muster | Service `…_NoPostings_ReturnsEmptyYears`; bUnit `EmptyYears_RendersEmptyState` + `Render_NoPoints_RendersEmptyText`; E2E `…_NoPostings_ShowsEmptyState` | Abgedeckt |
| Fehlende Kurse am Stichtag → letzter bekannter Kurs ≤ Stichtag, sonst 0 (`LatestPriceOnOrBefore`-Konvention) | Explizit in Scope-Klärung und Programmablauf (Wiederverwendung `GetPortfolioValueOnDate`) | **Neu:** Service-Test `…_StaleOrMissingPrice_FallsBackToLatestKnownOrZero` (letzter Kurs vor 31.12. → Stücke × Kurs; Position ohne Kurs → 0; Helfer `AddBuyPosting`/`AddPrice` verifiziert vorhanden) + Akzeptanzszenario 10 | Abgedeckt (Lücke aus Runde 1 behoben) |
| Kein Vorjahres-Depotwert (Startjahr) → `YearStartValue` = eingezahltes Kapital; Veränderung/Rendite auf Anfangskapital | `year == firstYear → YearStartValue = CumulativeInvestedCapital`; Fußnote weist darauf hin | Service `…_FirstYear_UsesInvestedCapitalAsStart`; `…_YearStartValue_EqualsPreviousYearEnd`; bUnit-Fußnotentest | Abgedeckt |
| Fehlende `PortfolioKpiConfiguration` → Server-Default enthält neue Kachel; gespeicherte Konfigurationen ohne TileId → definiertes Verhalten | Merge `MergeMissingTiles` in `GetKpiConfigurationAsync` (TileOrder-Ende + `ActiveTileIds` nur für `DefaultActiveTiles`; nicht persistiert) | 4 Controller-Tests (Default, Merge aktiv, deaktiviert bleibt deaktiviert, `Risk` nur Order); E2E `…_MergedIntoSavedConfiguration`, `…_CanBeDeactivatedInEditMode` | Abgedeckt |
| Veraltete Cache-Einträge → `CacheSchemaVersion`-Erhöhung | `"5"` → `"6"` (Schritt 5); `Parameter`-Vergleich im Cache-Service | Bestehender Test `CacheHit_EntryFromOlderDtoSchema_…` bleibt gültig; Akzeptanzszenario 8 | Abgedeckt |
| Kachel im Bearbeitungsmodus ein-/ausblendbar und verschiebbar | Generischer Mechanismus (`EditOrder`/`EditActive`, `ToggleTileActive`/`MoveEditTile`) über gemergte Konfiguration; Label via `GetTileLabel` | E2E `…_CanBeDeactivatedInEditMode`; bestehende ViewModel-/Edit-Tests decken den Mechanismus generisch | Abgedeckt |
| Serverseitige Berechnung aus Buchungen/Kursen des angemeldeten Benutzers (OwnerUserId) | `BuildDepotDevelopment` arbeitet auf `LoadPositionsAsync`-Ergebnis (Owner-gefiltert); kein Client-Parameter | Plan nennt bestehende E2E-Tests inkl. `MultiUserIsolationScenario` als unverändert gültig; Service-Ebene: `GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData` bleibt bestehen und prüft den Report-Endpunkt | Abgedeckt |
| Neues Enum-Mitglied in `EnumCoverageTests` (AGENT.md) | Schritt 1: `AssertValues<PortfolioTileId>` mit vollständiger Liste `{Structure, Performance, Cashflow, Risk, DepotDevelopment}` | Der Eintrag selbst ist der Testnachweis | Abgedeckt |
| Alle neuen UI-Texte in `Pages.*.resx` de + en, keine hartkodierten Strings | Vollständige Key-Liste unter „Änderungen an bestehenden Klassen" (Titel, Filter, Optionen, Spalten, Fußnoten, Leerzustand, Aria, Legende) | bUnit-Tests via `PagesStringLocalizer` | Abgedeckt |
| Kulturabhängige Formate (`N0`/`N2`, Prozent mit `+`-Vorzeichen), Farben via Theme-Variablen (Linie dunkelblau, Balken grün) | Formatierungs-Konvention der bestehenden Karten; CSS in `app.PortfolioReport.css`/`theme.Dark.PortfolioReport.css` | bUnit `Formatting_CurrencyAndSignedPercent` (mit `WithInvariantCulture`) | Abgedeckt |
| Laufendes Jahr als eigene Zeile mit `IsYtd`/`*`-Markierung | `IsYtd`-Flag im Jahr-Record, `*`-Suffix + Fußnote (`PortfolioAnnualReturnPoint`-Konvention) | Service `…_MarkedIsYtd`; bUnit `YtdRow_RendersAsteriskAndFootnote` | Abgedeckt |
| Serienende bei vollständig verkauften Beständen (`lastYear` = Jahr der jüngsten Transaktion, keine Phantom-YTD-Zeile) | Programmablauf Schritt 2 (Konvention `BuildPerformance`) | **Neu:** Service-Test `…_AllPositionsSold_SeriesEndsAtLastTransactionYear` + Akzeptanzszenario 11 | Abgedeckt (Lücke aus Runde 1 behoben) |
| `PortfolioBarLineChart`-Renderlogik (getrennte Skalierung Linie/Balken, Legende, `EmptyText`, `AriaLabel`) — Projektkonvention Chart-Tests (`MiniBarChartTests_BarScaling`) | Neue Shared-Komponente (Schritt 7); Testklasse in Schritt 10 eingeplant | **Neu:** bUnit-Klasse `PortfolioBarLineChartTests` (`Components/Shared/`): `Render_Points_EmitsTitleTooltipWithFormattedValues`, `Render_LineAndBars_ScaledIndependently`, `Render_Always_ShowsLegendAndAriaLabel`, `Render_NoPoints_RendersEmptyText` | Abgedeckt (Lücke aus Runde 1 behoben) |
| Scope-Ausschluss „Keine Änderung bestehender Kacheln" | „Seiteneffekte und Risiken": `BuildStructure`/`BuildPerformance`/`BuildCashflow` unangetastet | Bestehende Service-/E2E-Tests bleiben unverändert gültig (benannt) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Happy Path: Öffnen von `/portfolio/analysis-report` → Kachel mit Diagramm und Jahrestabelle sichtbar | `DepotDevelopmentScenario_ShouldRenderChartAndTable` (Pflicht; Seed via `AppDbContext` nach `SecurityTrendReversalE2ETests.SeedSecurityAsync`-Muster — Helfer im Code verifiziert; inkl. DOM-Assert auf `<title>`-Tooltip je Datenpunkt) | Abgedeckt |
| Rendite-Basis umschalten → Spalten/Fußnote ohne Reload aktualisiert | `DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable` (Pflicht) | Abgedeckt |
| Zeitraum-Filter anwenden → Zeilen begrenzt | `DepotDevelopmentScenario_PeriodFilterLimitsRows` (Pflicht) | Abgedeckt |
| Bestandskonfiguration ohne neue TileId → Kachel erscheint aktiv am Ende | `DepotDevelopmentScenario_MergedIntoSavedConfiguration` (Pflicht, DB-Seed der Konfiguration) | Abgedeckt |
| Nutzer ohne Buchungen → Leeranzeige | `DepotDevelopmentScenario_NoPostings_ShowsEmptyState` (Empfohlen) | Abgedeckt |
| Bearbeitungsmodus: Kachel aufgeführt, deaktivierbar, nach Speichern verborgen | `DepotDevelopmentScenario_CanBeDeactivatedInEditMode` (Empfohlen) | Abgedeckt |
| Tooltip mit exaktem Wert im Diagramm | DOM-Assert auf `<title>` im Happy-Path-E2E + dedizierter bUnit-Test `Render_Points_EmitsTitleTooltipWithFormattedValues` | Abgedeckt (Lücke aus Runde 1 behoben) |
| Verschieben der Kachel im Bearbeitungsmodus | Kein eigener Test | Nicht erforderlich mit Begründung: generischer `EditOrder`/`MoveEditTile`-Mechanismus ohne kachelspezifischen Code; durch bestehende ViewModel-/Edit-Tests abgesichert |

## Fehlende oder unvollständige Planbestandteile

Keine. Scope-Klärung ist vollständig gefüllt; alle 7 Scope-Ausschlüsse tragen Begründung und Status `bestätigt`; alle offenen Fragen der Anforderung sind als Designentscheidungen beantwortet; Umsetzungsreihenfolge enthält Voraussetzungen je Schritt; betroffene bestehende Tests (3 `CreateReport`-Helfer) und benötigte Test-Helfer (`AddBuyPosting`, `AddPrice`, `CultureTestHelper.WithInvariantCulture`, `PagesStringLocalizer`, `SeedSecurityAsync`-Muster) sind benannt und im Code verifiziert; Happy Path und zehn weitere Akzeptanzszenarien benennen jeweils Ausgangszustand, Auslöser, sichtbare Wirkung und Testnachweis.

## Hinweise

- Die leere gefilterte Liste (Zeitraum-Filter engt auf Zeitraum ohne Daten ein) ist im Programmablauf beschrieben; der generische Leerzustand ist per `EmptyYears_RendersEmptyState`/`Render_NoPoints_RendersEmptyText` abgesichert — kann bei Bedarf in `PeriodFilter_LimitsRenderedRows` mitassertiert werden, ist aber keine Lücke.
- Die im Plan zitierte Muster-Referenz `MiniBarChart.Render_NoPoints_RendersEmptyText` liegt faktisch in `MiniBarChartTests_BarScaling.cs` (Zeile 99) — rein benennungsbedingte Ungenauigkeit, der referenzierte Test existiert.
- `tasks.md` wurde konsistent nachgezogen: Tasks 24–26 bilden die drei neuen Service-/Chart-Testblöcke ab.
