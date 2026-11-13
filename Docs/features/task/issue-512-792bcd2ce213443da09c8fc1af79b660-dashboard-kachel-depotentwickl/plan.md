# Umsetzungsplan: Dashboard-Kachel „Depotentwicklung & Dividenden" (Issue #512)

## Übersicht

Der Depot-Bericht (`/portfolio/analysis-report`, `PortfolioAnalysisReportPage`) wird um eine neue, fünfte Kachel „Depotentwicklung Jahreswechsel" erweitert. Die Kachel zeigt pro Kalenderjahr ein kombiniertes SVG-Diagramm (Linie = Depotwert zum Jahresende, Balken = Netto-Dividenden), eine siebenspaltige Jahrestabelle sowie zwei clientseitige Filter (Zeitraum, Rendite-Basis). Die Jahresserie wird serverseitig in `PortfolioAnalysisReportService` als neue Sektion des bereits gecachten Gesamtberichts berechnet; Umschalten der Rendite-Basis und des Zeitraums erfolgt ohne Server-Roundtrip. Betroffen sind `FinanceManager.Shared` (Enum + DTOs), `FinanceManager.Infrastructure` (Berechnung, Cache-Schema-Version), `FinanceManager.Web` (Controller-Merge, neue Karte, neues Diagramm, resx, CSS) sowie die Testprojekte.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Ein Kalenderjahr der Depot-Historie des angemeldeten Benutzers, aggregiert über alle `SecurityTransaction` (`SecurityPostingSubType` Buy/Sell/Dividend/Fee/Tax) und `SecurityPrice`-Kurse seiner Wertpapierpositionen. Pro Jahr eine `PortfolioDepotDevelopmentYear`-Zeile: `Year`, `IsYtd`, `YearStartValue`, `YearEndValue`, `NetDividends`, `CumulativeInvestedCapital`. |
| Auslösende Ereignisse | (a) Öffnen von `/portfolio/analysis-report` → Kachel wird als Teil des gecachten Gesamtberichts geladen. (b) Ändern des Dropdowns „Rendite-Basis" → Aktualisierung der Spalten *Verhältnis (€/€)* und *Rendite (%)* clientseitig (`StateHasChanged`). (c) Ändern des Dropdowns „Zeitraum" → clientseitige Filterung der Jahresserie. (d) Ribbon-Aktion „Refresh" / Speichern der Kachel-Konfiguration / Monatsende → Cache-Invalidierung, Neuberechnung beim nächsten Laden. |
| Erwartete Wirkung | Neue Kachel im `.portfolio-tile-grid` mit Diagramm (dunkelblaue Linie, grüne Balken, `<title>`-Tooltips, Legende), Tabelle (Jahreswechsel, Depotwert €, Veränderung €, Veränderung %, Netto-Dividende €, Verhältnis €/€, Rendite %), modusabhängiger Fußnote und `*`-Markierung des laufenden Jahres. Die Kachel ist im Bearbeitungsmodus ein-/ausblendbar und verschiebbar wie die übrigen. |
| Umgang mit fehlenden Objekten | *Keine Wertpapierbuchungen:* `BuildDepotDevelopment` liefert leere `Years`-Liste; Karte zeigt Leeranzeige (Muster `MiniBarChart.EmptyText`/`.mini-bar-chart-empty`). *Fehlende Kurse am Stichtag:* letzter bekannter Kurs ≤ Stichtag via `SecurityValuationHelper.LatestPriceOnOrBefore`, sonst 0 (Konvention `GetPortfolioValueOnDate`). *Kein Vorjahreswert (Startjahr):* `YearStartValue` = `CumulativeInvestedCapital` des Startjahres (= Anschaffungskosten des Jahres). *Gespeicherte Benutzerkonfiguration ohne neue TileId:* Merge in `PortfolioAnalysisReportController.GetKpiConfigurationAsync` (siehe Designentscheidungen) — TileId wird ans Ende von `TileOrder` gehängt und, da Mitglied der Standard-Aktiv-Menge, in `ActiveTileIds` aufgenommen; keine Migration, keine Persistierung beim Lesen. *Veraltete Cache-Einträge:* `CacheSchemaVersion` wird von `"5"` auf `"6"` erhöht — alte Einträge gelten als Miss. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Drill-down / keine Posteneinsicht aus Diagramm oder Tabelle | Gehört zu Issue #510, nicht zu dieser Anforderung | bestätigt (2026-11-13, Issue-Kommentar) |
| 2 | Keine Aufschlüsselung auf Positionsebene (einzelne Wertpapiere) innerhalb der Kachel | Issue verlangt ausschließlich depotaggregierte Jahreswerte | bestätigt (2026-11-13, Issue-Kommentar) |
| 3 | Kein Export der Kacheldaten und keine Bearbeitungsfunktion (reine Anzeige) | Nicht im Issue gefordert | bestätigt (2026-11-13, Issue-Kommentar) |
| 4 | Keine Berücksichtigung im Home-Dashboard (`HomeKpiGrid`) | Kachel betrifft nur den Depot-Bericht | bestätigt (2026-11-13, Issue-Kommentar) |
| 5 | Depotwert enthält nur den Wertpapier-Marktwert, nicht den Verrechnungskonto-Saldo | Konsistent mit `GetPortfolioValueOnDate` und allen bestehenden Depot-Kacheln; der Konto-Saldo ist eine eigenständige KPI der Cashflow-Kachel (`LiquidityRatio`) | bestätigt (2026-11-13, Issue-Kommentar) |
| 6 | Zeitraum-Filter bietet nur vordefinierte Optionen (Gesamte Historie / Letzte 5 Jahre / Letzte 10 Jahre), keinen frei wählbaren Jahresbereich | Issue nennt nur die Standardoption; feste Optionen decken den typischen Bedarf ab und halten die Interaktion einfach | bestätigt (2026-11-13, Issue-Kommentar) |
| 7 | Dropdown-Wahl (Rendite-Basis, Zeitraum) wird nicht persistiert — Rückfall auf „Jahresanfangswert" / „Gesamte Historie" bei jedem Laden | Issue fordert keine Persistenz; sitzungslokaler Komponenten-State vermeidet ein neues Persistenzfeld und damit Backup-/Migrations-Impact | bestätigt (2026-11-13, Issue-Kommentar) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| YoC-Nenner („eingezahltes Kapital") | FIFO-Anschaffungskostenbasis der gehaltenen Bestände zum Jahresende: `FifoCostBasisResult.TotalCostBasis − StandaloneFeeTotal`, berechnet auf den Transaktionen `Date <= Stichtag` je Position (Muster `BuildCashflow`-`beforeYear`) | Die Issue-Fußnote definiert den Nenner explizit als „historische Anschaffungskosten"; die Konvention ist identisch zu `Structure.InvestedCapital` und damit konsistent zur Struktur-Kachel. Yield on Cost setzt per Definition auf die Anschaffungskosten der gehaltenen Positionen auf, nicht auf Netto-Cashflows. |
| `YearStartValue` im Startjahr | `CumulativeInvestedCapital` des Startjahres (Anschaffungskosten zum ersten Jahresende) | Entspricht „initial eingezahltem Kapital (Anschaffungskosten)" aus dem Issue und hält die Definition der Basis-Felder einheitlich (beide = FIFO-Anschaffungskosten). Bei buy-and-hold identisch mit den tatsächlich eingezahlten Kauf summen. |
| Laufendes Jahr | Eigene Zeile mit `IsYtd = true` und `*`-Markierung in Label und Tabelle; Stichtag = Tagesdatum statt 31.12. | Exakte Wiederverwendung der `PortfolioAnnualReturnPoint`-/`IsYtd`-Konvention inkl. `portfolio-chart-footnote`-Fußnote; Issue-Prototyp zeigt Reihe „bis 2024+" (laufendes Jahr eingeschlossen). |
| Berechnungsort der Rendite-Spalten | Server liefert je Jahr alle Basen (`YearStartValue`, `YearEndValue`, `NetDividends`, `CumulativeInvestedCapital`); *Verhältnis* und *Rendite* werden clientseitig daraus abgeleitet | AC „Echtzeit-Umschalten ohne Neuladen" erfordert, dass beide Nenner clientseitig verfügbar sind; ein Server-Roundtrip pro Moduswechsel ist unnötig. |
| Sichtbarkeit bei gespeicherten Benutzerkonfigurationen | Merge in `GetKpiConfigurationAsync` (private Hilfsmethode `MergeMissingTiles`): Jede `PortfolioTileId`, die im geladenen `TileOrder` fehlt, wird ans Ende angehängt; gehört sie zusätzlich zur Default-Aktiv-Menge (`DefaultActiveTiles` = {Structure, Performance, Cashflow, DepotDevelopment}), wird sie auch in `ActiveTileIds` ergänzt. Das gemergte DTO wird nur zurückgegeben, nicht persistiert (Persistenz erfolgt beim nächsten `SaveKpiConfigurationAsync`). | Ohne Merge wäre die Kachel für alle Bestandsnutzer unsichtbar und — da `EditOrder` aus `TileOrder` gespeist wird — im Bearbeitungsmodus nicht einmal aktivierbar. Die Regel „nur in `TileOrder` fehlende IDs sind unbekannt" stellt sicher, dass eine bewusste Deaktivierung (ID in `TileOrder`, nicht in `ActiveTileIds`) respektiert bleibt. `Risk` profitiert ebenfalls: Es erscheint im Bearbeitungsmodus als aktivierbar, bleibt aber inaktiv (nicht in `DefaultActiveTiles`). |
| Spalte „Verhältnis (€/€)" | Anzeige als Wertepaar `Netto-Dividende / Basis` (beide `N0`-formatiert mit €), z. B. `1.234 € / 17.760 €`; bei Basis 0 → `—` | „Mathematischer Bruch" ist in der tabellarischen Darstellung am ehesten als Paar der Operanden lesbar; die Zeile dokumentiert damit direkt die im jeweiligen Modus verwendete Basis — Mehrwert gegenüber einem reinen Dezimalbruch, der redundant zur Prozent-Spalte wäre. |
| Zeitraum-Filter-Optionen | Feste Auswahl: „Gesamte Historie" (Standard), „Letzte 5 Jahre", „Letzte 10 Jahre"; Filterung clientseitig über `Year > currentYear − N` | Das Issue nennt nur die Standardoption „Gesamte Historie"; eine sinnvolle Minimalauswahl ist ableitbar, ein freier Jahresbereich wird nicht gefordert. |
| Kombiniertes Diagramm | Neue Shared-Komponente `PortfolioBarLineChart.razor` (SVG, `viewBox`-Ansatz und `<title>`-Tooltips wie `BudgetLineChart`), keine JS-Bibliothek | Kein JS-Charting im Projekt; SVG-Muster `BudgetLineChart`/`DonutChart` ist etabliert und dark-theme-fähig via CSS. |
| Skalierung Linie vs. Balken | Getrennte Normalisierung: Linie über volle Zeichenhöhe (eigener Min/Max-Bereich), Balken gegen die untere Hälfte skaliert; je Datenpunkt ein `<title>` mit beiden formatierten Werten | Depotwert- und Dividendengrößen liegen typischerweise eine Größenordnung auseinander; eine gemeinsame Achse würde die Balken unsichtbar machen. Getrennte Skalen entsprechen der Prototyp-Optik (Linie oben, Balken unten). |
| Dropdowns | Natives `<select @bind>`/`@bind:after` wie die Intervallwahl in `AggregateBarChart.razor` | Etabliertes UI-Muster im Projekt; keine eigene Dropdown-Komponente nötig. |
| Rendite-Basis-/Zeitraum-Enums | Komponenteninterne `private enum`-Mitglieder im `@code`-Block der Karte (`DepotDevelopmentYieldBasis`, `DepotDevelopmentPeriodFilter`) | Reiner UI-Zustand ohne DTO-/Serialisierungsbedarf; kein Eintrag in `EnumCoverageTests` erforderlich (nur `PortfolioTileId` ist öffentliches Enum mit neuem Mitglied). |
| CSS | Erweiterung der vorhandenen `app.PortfolioReport.css` und `theme.Dark.PortfolioReport.css` (kein neues Stylesheet, kein `App.razor`-Eingriff); Farben über CSS-Variablen des Themes | Diagramm- und Kartenstile sind seitenbezogen; die beiden Dateien werden auf der Berichtsseite bereits geladen. AC „Farbgestaltung passt zur Anwendung" wird über die vorhandenen Theme-Variablen erreicht (Linie dunkelblau, Balken grün). |

## Programmabläufe

### Laden des Berichts inkl. neuer Kachel

1. Nutzer öffnet `/portfolio/analysis-report`; `PortfolioAnalysisReportPageViewModel.LoadReportAsync` ruft `ApiClient.Portfolio_GetAnalysisReportAsync` und `Portfolio_GetKpiConfigurationAsync`.
2. `PortfolioAnalysisReportController.GetAnalysisReportAsync` delegiert an `PortfolioAnalysisReportCacheService.GetPortfolioReportAsync` (Cache-Hit bei `Parameter == "6"` und gültigem `CacheValidUntilUtc`, sonst Neuberechnung).
3. `PortfolioAnalysisReportService.GetPortfolioAnalysisReportAsync` ruft nach `BuildCashflow` zusätzlich `BuildDepotDevelopment(positions)` auf und setzt das Ergebnis als `DepotDevelopment`-Argument des `PortfolioAnalysisReportDto`.
4. `GetKpiConfigurationAsync` liefert entweder `DefaultConfigurationDto()` (neu inkl. `DepotDevelopment` am Ende) oder die gespeicherte Konfiguration nach `MergeMissingTiles` (fehlende TileIds an `TileOrder`-Ende; Default-Mitglieder zusätzlich in `ActiveTileIds`).
5. Die Seite iteriert `TileOrder ∩ ActiveTileIds`; der neue `else if (tile == PortfolioTileId.DepotDevelopment)`-Zweig rendert `PortfolioDepotDevelopmentCard` mit `Data.DepotDevelopment`.

Beteiligte Klassen/Komponenten: `PortfolioAnalysisReportPage`, `PortfolioAnalysisReportPageViewModel`, `ApiClient`, `PortfolioAnalysisReportController`, `PortfolioAnalysisReportCacheService`, `PortfolioAnalysisReportService`, `PortfolioDepotDevelopmentCard`.

### Jahresserie berechnen (`BuildDepotDevelopment`)

1. Abbruch bei `positions.Count == 0` → `PortfolioDepotDevelopmentDto` mit leerer Liste.
2. `firstYear` = Jahr der ältesten Transaktion über alle Positionen; `lastYear` = laufendes Jahr, wenn heute Bestände gehalten werden (`SecurityValuationHelper.SharesHeldOnDate(..., today) > 0`), sonst Jahr der jüngsten Transaktion — identische Konvention wie `BuildPerformance`.
3. Schleife `year = firstYear..lastYear`: `isYtd = year == currentYear && sharesHeldToday > 0`; Stichtag = `today` (YTD) bzw. `31.12. year`.
4. `YearEndValue` = `GetPortfolioValueOnDate(positions, stichtag)`.
5. `NetDividends` = Σ `Amount` der `Dividend`-Buchungen des Jahres − Σ `|Amount|` der `Tax`-Buchungen des Jahres, über alle Positionen (Konvention `ReturnAnalysisService.BuildAnnualDividends`).
6. `CumulativeInvestedCapital` = Σ über alle Positionen `_fifo.Calculate(tx.Where(t => t.Date <= stichtag))` → `TotalCostBasis − StandaloneFeeTotal` (leere Teilmengen liefern 0).
7. `YearStartValue` = `year == firstYear ? CumulativeInvestedCapital` des Jahres : `YearEndValue` des Vorjahres (gepuffert aus der Schleife).
8. Ergebnis: `PortfolioDepotDevelopmentDto` mit aufsteigend sortierter Zeilenliste.

Beteiligte Klassen: `PortfolioAnalysisReportService`, `SecurityValuationHelper`, `IFifoCostBasisCalculator`, `FifoCostBasisResult`, `SecurityTransaction`, `PortfolioDepotDevelopmentDto`, `PortfolioDepotDevelopmentYear`.

### Rendite-Basis umschalten

1. Nutzer ändert das `<select>` „Rendite-Basis" (Optionen: `YearStartValue` = „Jahresanfangswert", `InvestedCapital` = „Eingezahltes Kapital (Yield on Cost)"; Standard `YearStartValue`).
2. `@bind` aktualisiert den privaten Enum-State der Karte; Blazor rendert neu — kein Server-Aufruf.
3. Pro angezeigter Zeile wählt die Karte die Basis: Modus 1 → `YearStartValue`; Modus 2 → `CumulativeInvestedCapital`. Spalte *Verhältnis* zeigt `NetDividends / Basis` als Wertepaar; Spalte *Rendite* zeigt `NetDividends / Basis` als Prozentsatz mit Vorzeichen (`null`/`—` bei Basis 0 — Konvention `CalculateDividendYield`).
4. Die Fußnote unter der Tabelle wechselt auf den modusspezifischen resx-Text.

Beteiligte Komponenten: `PortfolioDepotDevelopmentCard` (reine Komponentenlogik).

### Zeitraum-Filter anwenden

1. Nutzer ändert das `<select>` „Zeitraum" (`AllHistory` = „Gesamte Historie", `Last5Years`, `Last10Years`; Standard `AllHistory`).
2. Die Karte filtert `Data.Years` clientseitig (`year > currentYear − 5` bzw. `− 10`) und übergibt die gefilterte Liste an Diagramm und Tabelle.
3. Leere gefilterte Liste → Leeranzeige wie bei leerer Gesamtserie.

Beteiligte Komponenten: `PortfolioDepotDevelopmentCard`, `PortfolioBarLineChart`.

### Kachel im Bearbeitungsmodus ein-/ausblenden und verschieben

1. Ribbon „Edit" → `EnterEditModeAsync` füllt `EditOrder`/`EditActive` aus der (gemergten) Konfiguration; die neue Kachel erscheint mit Label aus `GetTileLabel` (`PortfolioReport_Tile_DepotDevelopment`).
2. Checkbox/Verschiebe-Buttons wirken über `ToggleTileActive`/`MoveEditTile`; „SaveEdit" postet `SaveKpiConfigurationAsync` (bestehende Validierung: ≥1 aktive Kachel, `TileOrder` Obermenge ohne Duplikate) und invalidiert den Cache.

Beteiligte Klassen: `PortfolioAnalysisReportPage`, `PortfolioAnalysisReportPageViewModel`, `PortfolioAnalysisReportController`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PortfolioDepotDevelopmentDto` | Record (Shared, `Dtos/Portfolio/PortfolioDepotDevelopmentDto.cs`) | Neue DTO-Sektion des Berichts: `IReadOnlyList<PortfolioDepotDevelopmentYear> Years` |
| `PortfolioDepotDevelopmentYear` | Record (Shared, gleiche Datei) | Jahreszeile: `(int Year, bool IsYtd, decimal YearStartValue, decimal YearEndValue, decimal NetDividends, decimal CumulativeInvestedCapital)` |
| `PortfolioDepotDevelopmentCard` | Blazor-Komponente (`Components/Pages/Portfolio/PortfolioDepotDevelopmentCard.razor`) | Kachel-UI: `PortfolioKpiCard`-Rahmen, zwei `<select>`-Filter, Diagramm, Tabelle, Fußnoten, Leerzustand; `[Parameter, EditorRequired] Data` vom Typ `PortfolioDepotDevelopmentDto`; komponenteninterne `private enum`s `DepotDevelopmentYieldBasis`, `DepotDevelopmentPeriodFilter` |
| `PortfolioBarLineChart` | Blazor-Komponente (`Components/Shared/PortfolioBarLineChart.razor`) | Kombiniertes SVG-Balken-/Liniendiagramm; Parameter `Points` (nested Record `PortfolioBarLineChartPoint(string Label, decimal LineValue, decimal BarValue)`), `HeightPx`, `AriaLabel`, `LineLabel`, `BarLabel`, `EmptyText`, `TooltipFormatter` |

## Änderungen an bestehenden Klassen

### `PortfolioTileId` (Enum, Shared)

- **Neue Enum-Mitglieder:** `DepotDevelopment = 4` — identifiziert die neue Kachel in Konfiguration und Render-Verzweigung.

### `PortfolioAnalysisReportDto` (Record, Shared)

- **Geänderte Signatur:** neuer positionaler Record-Parameter `PortfolioDepotDevelopmentDto DepotDevelopment` (vor `GeneratedUtc` einordnen, damit die Sektions-Reihenfolge den Kacheln entspricht) — trägt die Jahresserie; erfordert `CacheSchemaVersion`-Erhöhung und Anpassung aller Test-`CreateReport`-Helfer.

### `PortfolioAnalysisReportService` (Infrastructure)

- **Neue Methoden:** `BuildDepotDevelopment(IReadOnlyList<PositionSnapshot> positions)` → `PortfolioDepotDevelopmentDto` — Jahresschleife gemäß Programmablauf „Jahresserie berechnen" (private, nicht static, da `_fifo` benötigt).
- **Geänderte Methoden:** `GetPortfolioAnalysisReportAsync` — Aufruf von `BuildDepotDevelopment(positions)` und Übergabe als `DepotDevelopment`-Argument des Rückgabe-Records.

### `PortfolioAnalysisReportCacheService` (Infrastructure)

- **Geänderte Konstante:** `CacheSchemaVersion` von `"5"` auf `"6"` — alte Cache-Einträge ohne `DepotDevelopment` gelten als Miss (sonst teilweise `null`-deserialisiertes DTO).

### `PortfolioAnalysisReportController` (Web)

- **Geänderte Methoden:** `DefaultConfigurationDto()` — `defaultTiles` um `PortfolioTileId.DepotDevelopment` am Ende ergänzen; dazu die Menge in eine statische `DefaultActiveTiles`-Referenz auslagern, damit sie `DefaultConfigurationDto` und die Merge-Logik gemeinsam nutzen.
- **Geänderte Methoden:** `GetKpiConfigurationAsync` — Ergebnis von `ToDto(entity)` durch `MergeMissingTiles` schicken.
- **Neue Methoden:** `MergeMissingTiles(PortfolioKpiConfigurationDto)` (private static) — fehlende `PortfolioTileId`-Werte an `TileOrder` anhängen; Default-Aktiv-Mitglieder zusätzlich an `ActiveTileIds` anhängen (siehe Designentscheidungen); `UpdatedUtc` unverändert übernehmen, nicht persistieren.

### `PortfolioAnalysisReportPage` (Blazor-Seite, Web)

- **Geändertes Markup:** zusätzlicher `else if (tile == PortfolioTileId.DepotDevelopment)`-Zweig im `.portfolio-tile-grid`, der `PortfolioDepotDevelopmentCard` mit `_vm.PortfolioReportData.DepotDevelopment` rendert.
- **Geänderte Methoden:** `GetTileLabel` — `PortfolioTileId.DepotDevelopment => Localizer["PortfolioReport_Tile_DepotDevelopment"]`.

### `Pages.resx` / `Pages.de.resx` / `Pages.en.resx` (Ressourcen, Web)

- **Neue resx-Keys (de + en):** `PortfolioReport_Tile_DepotDevelopment` (Kachel-/Edit-Label), `PortfolioReport_Dev_Filter_Period`, `PortfolioReport_Dev_Filter_YieldBasis`, `PortfolioReport_Dev_Period_AllHistory`, `PortfolioReport_Dev_Period_Last5Years`, `PortfolioReport_Dev_Period_Last10Years`, `PortfolioReport_Dev_Basis_YearStartValue`, `PortfolioReport_Dev_Basis_InvestedCapital`, Spaltenköpfe `PortfolioReport_Dev_Th_Year`/`_DepotValue`/`_ChangeAbs`/`_ChangePct`/`_NetDividend`/`_Ratio`/`_Yield`, Fußnoten `PortfolioReport_Dev_Footnote_YearStartValue`/`_InvestedCapital` (inkl. `*`-Hinweis auf das Anfangskapital im Startjahr und laufendes Jahr), `PortfolioReport_Dev_YtdSuffix` (oder Wiederverwendung `PortfolioReport_YtdSuffix`), `PortfolioReport_Dev_Empty`, `PortfolioReport_Dev_Chart_Aria` sowie Legenden-Texte `PortfolioReport_Dev_Legend_DepotValue`/`_NetDividends`.

### `app.PortfolioReport.css` / `theme.Dark.PortfolioReport.css` (CSS, Web)

- **Neue Styles:** Filterzeile (zwei `<select>` in der Kachel), `portfolio-bar-line-chart-*`-Klassen (SVG-Container, Linie, Balken, Grid, Legende — Farben über Theme-Variablen: Linie dunkelblau, Balken grün), Tabellen-Klassen für horizontales Scrollen auf kleinen Screens (`overflow-x: auto`-Wrapper, Mindestbreite).

## Datenbankmigrationen

Keine. `PortfolioKpiConfiguration` speichert TileIds als JSON-String — das neue Enum-Mitglied ist kompatibel. Es entstehen keine neuen persistierten Felder (Filterwahl wird nicht persistiert), damit auch kein Backup-/Restore-Impact.

## Validierungsregeln

Keine. Es gibt keine neuen Benutzereingaben serverseitig: Die beiden Dropdowns bieten ausschließlich vordefinierte Optionen (kein Freitext), und die bestehende `SaveKpiConfigurationAsync`-Validierung (≥1 aktive Kachel, `TileOrder` Obermenge von `ActiveTileIds` ohne Duplikate) deckt die neue TileId generisch ab.

## Konfigurationsänderungen

Keine. Kein `appsettings`-Eintrag; die einzige „Konfiguration" ist der Code-basierte `CacheSchemaVersion`-Bump (unter „Änderungen an bestehenden Klassen" geführt).

## Seiteneffekte und Risiken

- **Alle Test-Hilfskonstruktoren von `PortfolioAnalysisReportDto`:** Der neue nicht-nullable Record-Parameter bricht kompilierend in `PortfolioAnalysisReportControllerTests.CreateReport()`, `PortfolioAnalysisReportCacheServiceTests.CreateReport(...)` und `PortfolioAnalysisReportPageViewModelTests.CreateReport(...)` — gewünschter Guard; alle drei Helfer sind um das neue DTO zu ergänzen.
- **Merge in `GetKpiConfigurationAsync`:** Verändert das geladene Konfigurations-DTO für Bestandsnutzer (neue Kachel erscheint aktiv am Ende; `Risk` wird erstmals im Bearbeitungsmodus aktivierbar). Risiko: Ein Nutzer, der die Kachel deaktiviert und speichert, darf sie nicht wieder automatisch aktiv bekommen — abgesichert durch die Regel „nur in `TileOrder` fehlende IDs werden ergänzt" (Test pflicht).
- **Berechnungskosten:** `BuildDepotDevelopment` ruft `_fifo.Calculate` je Jahr und Position auf Transaktions-Teilmengen auf — O(Jahre × Positionen × Transaktionen). Bei typischen Historien unkritisch und nur bei Cache-Miss relevant; keine Optimierung vorgesehen.
- **Bestehende Kacheln/Berechnungen:** Bleiben unverändert; `BuildStructure`/`BuildPerformance`/`BuildCashflow` werden nicht angefasst.
- **Leere Basis-Werte:** Jahre ohne gehaltene Positionen (z. B. nach Komplettverkauf) liefern `CumulativeInvestedCapital = 0` und ggf. `YearStartValue = 0` → Verhältnis/Rendite `—` statt Division durch 0.

## Umsetzungsreihenfolge

1. **`PortfolioTileId.DepotDevelopment` + `EnumCoverageTests`-Eintrag**
   - Voraussetzungen: Keine.
   - Beschreibung: Enum-Mitglied `DepotDevelopment = 4` anlegen; `AssertValues<PortfolioTileId>` in `EnumCoverageTests` mit der vollständigen Liste `{ Structure, Performance, Cashflow, Risk, DepotDevelopment }` ergänzen (Enum war bisher nicht abgedeckt).
2. **DTOs `PortfolioDepotDevelopmentDto` + `PortfolioDepotDevelopmentYear`**
   - Voraussetzungen: Keine.
   - Beschreibung: Neue Datei in `FinanceManager.Shared/Dtos/Portfolio/` mit beiden Records und XML-Doku.
3. **`PortfolioAnalysisReportDto` um `DepotDevelopment` erweitern + Test-Helfer reparieren**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Neuen Record-Parameter einfügen; `CreateReport`-Helfer in `PortfolioAnalysisReportControllerTests`, `PortfolioAnalysisReportCacheServiceTests`, `PortfolioAnalysisReportPageViewModelTests` kompilierfähig machen (leere Jahre-Liste).
4. **`BuildDepotDevelopment` in `PortfolioAnalysisReportService`**
   - Voraussetzungen: Schritt 3 (Service muss kompilieren).
   - Beschreibung: Jahresschleife, Stichtags-Depotwert via `GetPortfolioValueOnDate`, Netto-Dividenden, Stichtags-FIFO für `CumulativeInvestedCapital`, `YearStartValue`-Startjahr-Regel; Aufruf in `GetPortfolioAnalysisReportAsync`.
5. **`CacheSchemaVersion` erhöhen**
   - Voraussetzungen: Schritt 3 (DTO-Struktur geändert).
   - Beschreibung: `"5"` → `"6"` in `PortfolioAnalysisReportCacheService`.
6. **Controller: Default-Konfiguration + Merge**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `DefaultActiveTiles` auslagern, `DepotDevelopment` ergänzen, `MergeMissingTiles` implementieren und in `GetKpiConfigurationAsync` anwenden.
7. **`PortfolioBarLineChart` (Shared)**
   - Voraussetzungen: Keine (reine UI-Komponente; Datenstruktur aus Schritt 2 sinnvoll, aber nicht zwingend — Parameter sind generisch).
   - Beschreibung: SVG-Komponente mit getrennt skaliertem Linien- und Balkenbereich, `<title>`-Tooltips, Legende, `EmptyText`-Leerzustand, `AriaLabel`.
8. **`PortfolioDepotDevelopmentCard` + resx + CSS**
   - Voraussetzungen: Schritte 2, 7.
   - Beschreibung: Karte mit `PortfolioKpiCard`, zwei `<select>`-Dropdowns (Muster `AggregateBarChart`-Intervallwahl), Diagramm, siebenspaltige Tabelle mit `*`-YTD-Markierung und Vorzeichen-Prozenten, modusabhängige Fußnote, Leeranzeige; alle Texte in `Pages.*.resx` (de+en); Styles in `app.PortfolioReport.css`/`theme.Dark.PortfolioReport.css`.
9. **Render-Verzweigung + `GetTileLabel` in `PortfolioAnalysisReportPage.razor`**
   - Voraussetzungen: Schritte 1, 8.
   - Beschreibung: `else if`-Zweig und Switch-Eintrag ergänzen.
10. **Unit-/bUnit-Tests**
    - Voraussetzungen: Schritte 4, 6, 7, 8.
    - Beschreibung: Service-Tests (inkl. Stichtags-Kurs-Fallback und Serienende nach Komplettverkauf), Controller-Merge-Tests, bUnit-Tests der Karte sowie die neue bUnit-Testklasse `PortfolioBarLineChartTests` (Skalierung, `<title>`-Tooltips, Legende, `EmptyText`/`AriaLabel` — Muster `MiniBarChartTests_BarScaling`); siehe Abschnitt Tests.
11. **E2E-Tests**
    - Voraussetzungen: Schritte 4–9.
    - Beschreibung: Playwright-Szenarien in `PortfolioAnalysisReportE2ETests` (siehe E2E-Abschnitt).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetPortfolioReport_DepotDevelopment_MultipleYears_ReturnsYearRows` | `PortfolioAnalysisReportServiceTests` | Käufe über ≥2 Jahre + Kurse → eine Zeile je Jahr, aufsteigend, korrekte `YearEndValue` |
| `GetPortfolioReport_DepotDevelopment_YearStartValue_EqualsPreviousYearEnd` | `PortfolioAnalysisReportServiceTests` | `YearStartValue` des zweiten Jahres = `YearEndValue` des ersten |
| `GetPortfolioReport_DepotDevelopment_FirstYear_UsesInvestedCapitalAsStart` | `PortfolioAnalysisReportServiceTests` | Startjahr: `YearStartValue` = `CumulativeInvestedCapital` (Anschaffungskosten des Jahres) |
| `GetPortfolioReport_DepotDevelopment_NetDividends_SubtractsTax` | `PortfolioAnalysisReportServiceTests` | Dividend- minus Tax-Buchungen je Jahr |
| `GetPortfolioReport_DepotDevelopment_CumulativeCapital_FollowsFifoCostBasis` | `PortfolioAnalysisReportServiceTests` | Teilverkauf → `CumulativeInvestedCapital` sinkt auf FIFO-Restbasis; `StandaloneFeeTotal`-Abzug |
| `GetPortfolioReport_DepotDevelopment_CurrentYear_MarkedIsYtd` | `PortfolioAnalysisReportServiceTests` | Laufendes Jahr mit gehaltenen Beständen → `IsYtd = true`, Stichtag Tagesdatum |
| `GetPortfolioReport_DepotDevelopment_StaleOrMissingPrice_FallsBackToLatestKnownOrZero` | `PortfolioAnalysisReportServiceTests` | Stichtags-Kurs-Fallback: Kauf in Jahr N mit letztem `AddPrice` vor dem 31.12. → `YearEndValue` = Stücke × letztem Kurs (`LatestPriceOnOrBefore`-Konvention); zweite Position ganz ohne Kurs → Beitrag 0. Aufbau über die vorhandenen Helfer `AddBuyPosting`/`AddPrice` |
| `GetPortfolioReport_DepotDevelopment_AllPositionsSold_SeriesEndsAtLastTransactionYear` | `PortfolioAnalysisReportServiceTests` | Komplettverkauf in einem früheren Jahr → `lastYear` = Jahr der jüngsten Transaktion: keine Zeile für das laufende Jahr, kein `IsYtd` (Gegenstück zu `…_MarkedIsYtd`) |
| `GetPortfolioReport_DepotDevelopment_NoPostings_ReturnsEmptyYears` | `PortfolioAnalysisReportServiceTests` | Leerzustand ohne Buchungen |
| `GetKpiConfiguration_Default_IncludesDepotDevelopment` | `PortfolioAnalysisReportControllerTests` | `DefaultConfigurationDto` enthält neue TileId in `ActiveTileIds` und `TileOrder` |
| `GetKpiConfiguration_SavedConfigMissingTile_MergesIntoOrderAndActive` | `PortfolioAnalysisReportControllerTests` | Gespeicherte Konfiguration ohne `DepotDevelopment` → nach Merge aktiv am Ende |
| `GetKpiConfiguration_SavedConfigWithDeactivatedTile_StaysInactive` | `PortfolioAnalysisReportControllerTests` | TileId in `TileOrder`, aber nicht `ActiveTileIds` → Merge aktiviert nicht erneut |
| `GetKpiConfiguration_SavedConfigMissingRisk_MergesIntoOrderOnly` | `PortfolioAnalysisReportControllerTests` | `Risk` landet in `TileOrder`, nicht in `ActiveTileIds` |
| `YieldBasisSwitch_UpdatesRatioAndYieldColumns` | `PortfolioDepotDevelopmentCardTests` (bUnit, neu) | Moduswechsel ändert Basis/Prozentwerte + Fußnote ohne Datenänderung |
| `PeriodFilter_LimitsRenderedRows` | `PortfolioDepotDevelopmentCardTests` (bUnit, neu) | „Letzte 5 Jahre" blendet ältere Zeilen aus |
| `YtdRow_RendersAsteriskAndFootnote` | `PortfolioDepotDevelopmentCardTests` (bUnit, neu) | `IsYtd`-Zeile mit `*`-Suffix + Fußnote (Muster `PortfolioPerformanceCardTests_YtdFootnote`) |
| `EmptyYears_RendersEmptyState` | `PortfolioDepotDevelopmentCardTests` (bUnit, neu) | Leeranzeige bei leerer `Years`-Liste |
| `ZeroBasis_RendersDashInsteadOfYield` | `PortfolioDepotDevelopmentCardTests` (bUnit, neu) | Basis 0 → `—` statt Division |
| `Formatting_CurrencyAndSignedPercent` | `PortfolioDepotDevelopmentCardTests` (bUnit, neu, mit `CultureTestHelper.WithInvariantCulture`) | `N0`-€-Beträge, `+x,xx %`-Renditen, Wertepaar in *Verhältnis* |
| `Render_Points_EmitsTitleTooltipWithFormattedValues` | `PortfolioBarLineChartTests` (bUnit, neu, `FinanceManager.Tests/Components/Shared/`) | Je Datenpunkt genau ein `<title>`-Element; Inhalt = exakt formatierter Linien- und Balkenwert (Ausgabe des `TooltipFormatter`, assertiert mit `WithInvariantCulture`) — Nachweis „Tooltip je Jahreswechsel mit exaktem Wert" |
| `Render_LineAndBars_ScaledIndependently` | `PortfolioBarLineChartTests` (bUnit, neu) | Getrennte Normalisierung: Linie über volle Zeichenhöhe (eigener Min/Max-Bereich), Balken gegen die untere Hälfte — Max-Werte füllen jeweils ihren Bereich (Muster `MiniBarChartTests_BarScaling`) |
| `Render_Always_ShowsLegendAndAriaLabel` | `PortfolioBarLineChartTests` (bUnit, neu) | Legende enthält `LineLabel`/`BarLabel`; `aria-label`-Attribut am SVG aus `AriaLabel` |
| `Render_NoPoints_RendersEmptyText` | `PortfolioBarLineChartTests` (bUnit, neu) | `EmptyText`-Leerzustand, keine Linie/Balken (Muster `MiniBarChart.Render_NoPoints_RendersEmptyText`) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PortfolioAnalysisReportControllerTests.CreateReport()` | Neuer `PortfolioAnalysisReportDto`-Parameter (Compile-Break) |
| `PortfolioAnalysisReportCacheServiceTests.CreateReport(...)` | Neuer `PortfolioAnalysisReportDto`-Parameter (Compile-Break); `CacheHit_EntryFromOlderDtoSchema_…` bleibt durch den Bump fachlich gültig |
| `PortfolioAnalysisReportPageViewModelTests.CreateReport(...)` | Neuer `PortfolioAnalysisReportDto`-Parameter (Compile-Break) |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Happy Path: Authentifizierter Nutzer mit geseedetem Wertpapier (Buy + Dividend + Kurse über ≥2 Jahre, direkt via `AppDbContext`/`Security`/`Posting`/`SecurityPrice` — Muster `SecurityTrendReversalE2ETests.SeedSecurityAsync`) öffnet `/portfolio/analysis-report` → Kachel „Depotentwicklung Jahreswechsel" mit Diagramm (inkl. DOM-Assert auf `<title>`-Tooltip je Datenpunkt) und Tabelle (Jahreszeilen > 0) sichtbar | `PortfolioAnalysisReportE2ETests` (neuer Fact `DepotDevelopmentScenario_ShouldRenderChartAndTable`) | Kachel wird für Standard-/Neunutzer angezeigt; Daten fließen vom Service bis ins DOM | Gesamtfluss DB → Service → Cache → API → Blazor-DOM ist nur integriert prüfbar |
| Pflicht | Interaktivität: Wechsel des „Rendite-Basis"-`<select>` auf „Eingezahltes Kapital" ändert *Rendite*-Werte und Fußnote ohne Seiten-Reload | `PortfolioAnalysisReportE2ETests` (neuer Fact `DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable`) | AC „Echtzeit-Update ohne Neuladen" | Blazor-Server-Interaktivität (`@bind` + `StateHasChanged`) ist nur im echten Browser/Circuit nachweisbar |
| Pflicht | Zeitraum-Filter: Wechsel auf „Letzte 5 Jahre" reduziert die Tabellenzeilen bei Historie > 5 Jahre | `PortfolioAnalysisReportE2ETests` (neuer Fact `DepotDevelopmentScenario_PeriodFilterLimitsRows`) | Filter-Funktionalität | Clientseitige Filterlogik im Browser |
| Pflicht | Bestandsnutzer: Gespeicherte `PortfolioKpiConfiguration` ohne neue TileId (via DB-Seed) → Kachel erscheint trotzdem aktiv am Ende des Grids | `PortfolioAnalysisReportE2ETests` (neuer Fact `DepotDevelopmentScenario_MergedIntoSavedConfiguration`) | Merge-Verhalten für Bestandskonfigurationen | Nur über API+UI prüfbar, ob gemergte Konfiguration auch tatsächlich rendert |
| Empfohlen | Leerzustand: Nutzer ohne Wertpapierbuchungen → Kachel sichtbar mit Leeranzeige-Text statt Tabelle | `PortfolioAnalysisReportE2ETests` (neuer Fact `DepotDevelopmentScenario_NoPostings_ShowsEmptyState`) | Umgang mit fehlenden Objekten | Leerzustand ist eine sichtbare Benutzerwirkung |
| Empfohlen | Bearbeitungsmodus: Neue Kachel ist in `.portfolio-edit-list` aufgeführt und per Checkbox deaktivierbar; nach Speichern im Grid nicht mehr sichtbar | `PortfolioAnalysisReportE2ETests` (neuer Fact `DepotDevelopmentScenario_CanBeDeactivatedInEditMode`) | Integration in Kachel-Mechanismus | Erweitert bestehendes Edit-Szenario; Nachweis, dass Merge + Edit konsistent funktionieren |

Bestehende E2E-Tests, die angepasst werden müssen: Keine — die vier vorhandenen `PortfolioAnalysisReportE2ETests` prüfen Grid, Navigation, Deaktivierung (Cashflow) und Mehrbenutzer-Isolation und bleiben unverändert gültig.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Kachel zeigt Jahresserie | Öffnen von `/portfolio/analysis-report` | Authentifizierter Nutzer mit Wertpapierbuchungen in ≥2 Kalenderjahren inkl. Dividenden und Kursen; keine gespeicherte `PortfolioKpiConfiguration`; kein Cache-Eintrag | Seitenaufruf → API liefert Report inkl. `DepotDevelopment`-Sektion → Default-Konfiguration enthält neue TileId → Karte rendert | Kachel „Depotentwicklung Jahreswechsel" im Grid: SVG-Diagramm mit Linie (Depotwert) und Balken (Netto-Dividenden), Tabelle mit sieben Spalten und einer Zeile je Jahr, Modus „Jahresanfangswert" aktiv, Zeitraum „Gesamte Historie" | E2E `DepotDevelopmentScenario_ShouldRenderChartAndTable`; Service-Unit-Tests zur Jahresserie; bUnit `PortfolioBarLineChartTests` (Skalierung, `<title>`-Tooltips) |
| 2 | Moduswechsel Rendite-Basis | Nutzer wählt „Eingezahltes Kapital" im Dropdown | Zustand wie Szenario 1, Kachel gerendert | `<select>`-Change → Blazor-Update | *Verhältnis*- und *Rendite*-Spalten zeigen YoC-Basis (`CumulativeInvestedCapital`), Fußnote wechselt auf YoC-Text, kein Seiten-Reload | E2E `DepotDevelopmentScenario_SwitchingYieldBasisUpdatesTable`; bUnit `YieldBasisSwitch_UpdatesRatioAndYieldColumns` |
| 3 | Zeitraum einschränken | Nutzer wählt „Letzte 5 Jahre" | Historie > 5 Jahre | `<select>`-Change → clientseitige Filterung | Diagramm und Tabelle zeigen nur die letzten 5 Jahre | E2E `DepotDevelopmentScenario_PeriodFilterLimitsRows`; bUnit `PeriodFilter_LimitsRenderedRows` |
| 4 | Leere Historie | Öffnen der Seite | Authentifizierter Nutzer ohne Wertpapierbuchungen | Seitenaufruf → `BuildDepotDevelopment` liefert leere Liste | Kachel sichtbar, Leeranzeige-Text statt Diagramm/Tabelle | E2E `DepotDevelopmentScenario_NoPostings_ShowsEmptyState`; bUnit `EmptyYears_RendersEmptyState` |
| 5 | Startjahr ohne Vorjahreswert | Öffnen der Seite | Nutzer, dessen älteste Buchung im laufenden oder einem einzigen Jahr liegt | Berechnung → erste Zeile ohne Vorjahr | Erste Zeile nutzt `CumulativeInvestedCapital` als `YearStartValue`; Veränderung/Rendite bezogen auf Anfangskapital, Fußnote weist darauf hin | Unit `…_FirstYear_UsesInvestedCapitalAsStart`; bUnit-Fußnotentest |
| 6 | Gespeicherte Konfiguration ohne neue TileId | Öffnen der Seite | Nutzer mit bereits persistierter `PortfolioKpiConfiguration` (alte TileId-Liste) | `GetKpiConfigurationAsync` → `MergeMissingTiles` | Neue Kachel erscheint aktiv am Ende des Grids und in der Edit-Liste | E2E `DepotDevelopmentScenario_MergedIntoSavedConfiguration`; Controller-Tests |
| 7 | Vom Nutzer deaktivierte Kachel bleibt deaktiviert | Erneutes Öffnen der Seite | Nutzer hat die neue Kachel im Bearbeitungsmodus deaktiviert und gespeichert (TileId in `TileOrder`, nicht in `ActiveTileIds`) | `GetKpiConfigurationAsync` → Merge fügt nichts zu `ActiveTileIds` hinzu | Kachel nicht im Grid, aber in der Edit-Liste als deaktivierte Option sichtbar | Controller-Test `…_WithDeactivatedTile_StaysInactive`; E2E `…_CanBeDeactivatedInEditMode` |
| 8 | Veralteter Cache-Stand | Öffnen der Seite | `ReportCacheEntry` mit `Parameter = "5"` (vor DTO-Erweiterung geschrieben) | `GetPortfolioReportAsync` → Schema-Miss → Neuberechnung + Upsert | Kachel mit aktuellen Daten statt `null`-Sektion/Fehler | Bestehender Test `CacheHit_EntryFromOlderDtoSchema_…` bleibt grün |
| 9 | Laufendes Jahr markiert | Öffnen der Seite | Nutzer mit gehaltenen Beständen im laufenden Jahr | Berechnung → `IsYtd`-Zeile | Jahr mit `*`-Suffix in Tabelle und Diagrammlabel; `*`-Fußnote | bUnit `YtdRow_RendersAsteriskAndFootnote`; Service-Test `…_MarkedIsYtd` |
| 10 | Fehlende/veraltete Kurse am Jahresstichtag | Öffnen der Seite | Nutzer mit Position, deren letzter Kurs vor dem 31.12. des Jahres liegt, sowie Position ganz ohne Kurs | Berechnung → `LatestPriceOnOrBefore`-Fallback je Position | Jahreszeile nutzt den letzten bekannten Kurs ≤ Stichtag; Position ohne Kurs trägt 0 bei — kein Fehler, keine fehlende Zeile | Unit `…_StaleOrMissingPrice_FallsBackToLatestKnownOrZero` |
| 11 | Serienende nach Komplettverkauf | Öffnen der Seite | Nutzer, dessen sämtliche Positionen in einem früheren Jahr vollständig verkauft wurden | Berechnung → `lastYear` = Jahr der jüngsten Transaktion | Serie endet im letzten Transaktionsjahr; keine Zeile für das laufende Jahr, keine Phantom-`IsYtd`-Markierung | Unit `…_AllPositionsSold_SeriesEndsAtLastTransactionYear` |

## Offene Punkte

Keine. Alle in der `requirement.md` gelisteten offenen Fragen wurden aus Anforderung, Issue-Prototyp-Beschreibung und Projektkonventionen abgeleitet und sind als Designentscheidungen bzw. Scope-Ausschlüsse (alle vom Anwender bestätigt, siehe Abschnitt „Scope-Ausschlüsse") festgelegt:

- **YoC-Nenner:** FIFO-Anschaffungskostenbasis (`TotalCostBasis − StandaloneFeeTotal`) zum Stichtag — Issue-Fußnote „historische Anschaffungskosten" + `InvestedCapital`-Konvention.
- **Depotwert:** Nur Wertpapier-Marktwert (`GetPortfolioValueOnDate`-Konvention aller Kacheln).
- **Laufendes Jahr:** Eigene Zeile mit `IsYtd`/`*` (Muster `PortfolioAnnualReturnPoint`; Prototyp „bis 2024+").
- **Zeitraum-Filter:** „Gesamte Historie" (Standard), „Letzte 5 Jahre", „Letzte 10 Jahre".
- **Bestandskonfigurationen:** Merge in `GetKpiConfigurationAsync` — neue Kachel aktiv am Ende, Deaktivierung bleibt respektiert, `Risk` wird editierbar.
- **Persistenz der Dropdown-Wahl:** Keine (sitzungslokal) — als Scope-Ausschluss geführt.
- **„Verhältnis (€/€)":** Wertepaar `Netto-Dividende / Basis` — dokumentiert die gewählte Basis je Zeile.
