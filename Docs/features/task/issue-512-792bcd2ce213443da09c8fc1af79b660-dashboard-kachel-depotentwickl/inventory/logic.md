# Logik – Bestandsaufnahme

Betroffene Services, Controller, ViewModels und UI-Komponenten für die Kachel „Depotentwicklung & Dividenden".

## `PortfolioAnalysisReportService`
Datei: `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportService.cs`
Implementiert `IPortfolioAnalysisReportService`. Konstruktor: `(AppDbContext db, IFifoCostBasisCalculator fifo, IReturnCalculationService calc)`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPortfolioAnalysisReportAsync(ownerUserId, ct)` | public | Orchestriert: `LoadPositionsAsync` + `LoadDepotCashBalanceAsync`, dann `BuildStructure`/`BuildPerformance`/`BuildCashflow`; `Risk` = Null-DTO; setzt `GeneratedUtc`/`CacheValidUntilUtc` |
| `EndOfMonthUtc(utcNow)` | public static | Monatsende-UTC für die Cache-Gültigkeit |
| `LoadPositionsAsync(ownerUserId, ct)` | private | Lädt Wertpapiere (`OwnerUserId`-Filter), Postings mit `SecuritySubType != null` (→ `SecurityTransaction`), `SecurityPrices`; berechnet pro Position `_fifo.Calculate(transactions)` und `CurrentPrice` = letzter Kurs; Positionen ohne Transaktionen werden übersprungen. Ergebnis: `List<PositionSnapshot>` |
| `LoadDepotCashBalanceAsync(ownerUserId, ct)` | private | Salden der über `GroupId` verknüpften Verrechnungskonten (für Liquiditätsquote) |
| `BuildStructure(positions)` | private static | Marktwert = `Fifo.TotalSharesHeld × CurrentPrice`; `InvestedCapital = Fifo.TotalCostBasis − Fifo.StandaloneFeeTotal` (Konvention!) |
| `BuildPerformance(positions)` | private | Jahresschleife `firstDate.Year`..`lastYear`; `isYtd` wenn aktuelles Jahr und Bestände > 0; Jahresende = `today` (YTD) bzw. 31.12.; ruft `ComputePeriodMetrics` je Jahr/Monat; TWR via `_calc.CalculateTwr` |
| `BuildCashflow(positions, depotCashBalance, totalMarketValue)` | private | Laufendes Jahr: Netto-Einzahlungen, Brutto-Dividenden, realisierte Gewinne (Delta `_fifo.Calculate(beforeYear)` — **Muster für Stichtags-FIFO**); Liquiditätsquote |
| `GetPortfolioValueOnDate(positions, date)` | private static | Σ `SharesHeldOnDate × LatestPriceOnOrBefore` über alle Positionen — **wiederverwendbar für Jahresendwerte** |
| `ExternalCashflowInPeriod(positions, start, end)` | private static | Buy/Fee = +abs, Sell = −Amount |
| `ComputePeriodReturn(startValue, endValue, cashflow)` | private static | Modified Dietz; `null` bei Nenner = 0 |
| `ComputePeriodMetrics(positions, periodStart, periodEnd)` | private static | Startwert = Wert am `periodStart.AddDays(-1)` — **Konvention für „Jahresanfangswert = 31.12. Vorjahr"** |
| `CalculateLiquidityRatio(...)` | private static | `null` bei negativem Saldo oder Marktwert ≤ 0 |

Interner Record `PositionSnapshot(SecurityId, Name, CategoryId, CategoryName, Region, Sector, Transactions, Prices, Fifo, CurrentPrice)` (Zeile 37–47).
Befund: Keine Jahresserien-/Depotentwicklungs-Methode vorhanden; alle Bausteine (`GetPortfolioValueOnDate`, `SecurityValuationHelper`, `_fifo.Calculate` auf Transaktions-Teilmengen, Dividenden-/Tax-Summen) existieren bereits.

## `PortfolioAnalysisReportCacheService`
Datei: `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportCacheService.cs`
Implementiert `IPortfolioAnalysisReportCacheService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPortfolioReportAsync(ownerUserId, ct)` | public | Cache-Hit nur wenn `!NeedsRefresh`, `CacheValidUntilUtc >= now` **und** `Parameter == CacheSchemaVersion`; sonst Neuberechnung + Upsert |
| `InvalidateCacheAsync(ownerUserId, ct)` | public | Löscht den Cache-Eintrag |
| `BuildCacheKey(ownerUserId)` | private static | `portfolio-analysis-report-{ownerUserId:N}` |

`private const string CacheSchemaVersion = "5";` (Zeile 24) — dokumentiert ausdrücklich, dass sie bei DTO-Strukturänderungen erhöht werden muss.

## `PortfolioAnalysisReportController`
Datei: `FinanceManager.Web\Controllers/PortfolioAnalysisReportController.cs`
Route `api/portfolio`, JWT-Auth, UserId aus `ICurrentUserService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAnalysisReportAsync(ct)` | public | `GET analysis-report` → `Ok(cache.GetPortfolioReportAsync(...))` |
| `GetKpiConfigurationAsync(ct)` | public | `GET kpi-configuration` → gespeicherte Konfiguration oder `DefaultConfigurationDto()` |
| `SaveKpiConfigurationAsync(req, ct)` | public | `POST kpi-configuration`; validiert: ≥1 aktive Kachel, `TileOrder` ohne Duplikate und Obermenge von `ActiveTileIds`; speichert + invalidiert Cache |
| `ResetCacheAsync(ct)` | public | `POST cache/reset` → 204 |
| `DefaultConfigurationDto()` | private static | **`defaultTiles = { Structure, Performance, Cashflow }`** — `Risk` ist NICHT in der Standardkonfiguration (Präzedenzfall: Kachel existiert, ist aber standardmäßig deaktiviert) |
| `ToDto(entity)` | private static | Deserialisiert `ActiveTileIds`/`TileOrder` aus JSON |

## `PortfolioAnalysisReportPageViewModel`
Datei: `FinanceManager.Web/ViewModels/Portfolio/PortfolioAnalysisReportPageViewModel.cs` (`BaseViewModel`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadReportAsync(ct)` | public | Lädt Report + KPI-Konfiguration via `ApiClient` |
| `EnterEditModeAsync(ct)` | public | Füllt `EditOrder`/`EditActive` aus `CurrentConfiguration` |
| `CancelEditMode()` | public | Edit-Modus verlassen |
| `SaveEditConfigurationAsync(ct)` | public | Request aus `EditOrder`/`EditActive` → `SaveConfigurationAsync` |
| `SaveConfigurationAsync(newConfig, ct)` | public | `Portfolio_SaveKpiConfigurationAsync`, danach Report-Reload |
| `RefreshReportAsync(ct)` | public | `Portfolio_ResetCacheAsync` + Reload (Ribbon „Refresh") |
| `ToggleTileActive(tile, isActive)` / `MoveEditTile(index, delta)` | public | Edit-State |
| `GetRibbonRegisterDefinition(localizer)` | protected override | Gruppe `Ribbon_Group_Actions` (Sort 0): Back, Refresh, Edit bzw. SaveEdit/CancelEdit |

Properties: `PortfolioReportData`, `CurrentConfiguration`, `IsEditMode`, `EditOrder`, `EditActive`.
Abonnierte/publizierte Events: `StateChanged` (geerbt, via `RaiseStateChanged()`); die Seite abonniert `VmOnStateChanged → InvokeAsync(StateHasChanged)`.

## `ApiClient` (Portfolio-Teilklasse)
Datei: `FinanceManager.Shared/ApiClient.Portfolio.cs`

| Methode | Zweck |
|---------|-------|
| `Portfolio_GetAnalysisReportAsync(ct)` | GET `/api/portfolio/analysis-report` |
| `Portfolio_GetKpiConfigurationAsync(ct)` | GET `/api/portfolio/kpi-configuration` |
| `Portfolio_SaveKpiConfigurationAsync(request, ct)` | POST; `null` bei 400 |
| `Portfolio_ResetCacheAsync(ct)` | POST `/api/portfolio/cache/reset` |

## Berechnungs-Hilfen (Bestand, wiederverwendbar)

### `SecurityValuationHelper`
Datei: `FinanceManager.Application/Securities/ReturnAnalysis/SecurityValuationHelper.cs` (statisch)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `SharesHeldOnDate(transactions, date)` | Gehaltene Stücke am Stichtag (inkl.), Buy +, Sell −, min. 0; bricht bei `tx.Date > date` ab |
| `LatestPriceOnOrBefore(prices, date)` | Letzter Kurs ≤ Stichtag; `0` wenn keiner existiert (Projektkonvention für fehlende Kurse) |

### `FifoCostBasisCalculator`
Datei: `FinanceManager.Application/Securities/ReturnAnalysis/FifoCostBasisCalculator.cs` (`sealed`, implementiert `IFifoCostBasisCalculator`)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `Calculate(transactions)` | FIFO-Lots; Fee mit passendem `GroupId` → Lot-Kostenbasis, sonst `StandaloneFeeTotal` (wird zu `TotalCostBasis` addiert — Option A); Sell → `RealizedGains`; Oversell → Warning-Flag. Sortiert intern nach Datum, Typ, Id. Dividend/Tax werden ignoriert |

### `ReturnCalculationService`
Datei: `FinanceManager.Application/Securities/ReturnAnalysis/ReturnCalculationService.cs` — `CalculateTwr`, `CalculateDividendYield` (Formel `TotalDividends / InvestedCapital`, `null` bei 0), u. a.

### `ReturnAnalysisService`
Datei: `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildAnnualDividends(transactions, firstYear, lastYear)` | private static (Zeile 1693) | **Netto-Dividendenkonvention:** `net = Σ Dividend − Σ \|Tax\|` je Jahr, plus `CumulativeNet`; liefert `AnnualDividendPoint` — Einzelwertpapier-Pendant zur geforderten Depot-Aggregation |

## UI-Komponenten

### `PortfolioAnalysisReportPage.razor`
Datei: `FinanceManager.Web/Components/Pages/Portfolio/PortfolioAnalysisReportPage.razor` — Route `/portfolio/analysis-report`, `@rendermode InteractiveServer` gesetzt (Zeile 2).

- Edit-Modus: `.portfolio-edit-list` mit Checkbox + ↑/↓-Buttons je `EditOrder`-Eintrag; Label via `GetTileLabel`-Switch (Zeile 101–108: `PortfolioReport_Tile_{Structure|Performance|Cashflow|Risk}`, Fallback `tile.ToString()`).
- View-Modus: `.portfolio-tile-grid`, iteriert `TileOrder ∩ ActiveTileIds`; `if/else if`-Kette rendert `PortfolioStructureCard`/`PortfolioPerformanceCard`/`PortfolioCashflowCard`/`PortfolioRiskCard` (Zeilen 59–74) — **Render-Verzweigung für neue TileId erforderlich**.

### `PortfolioKpiCard.razor`
Datei: `FinanceManager.Web/Components/Pages/Portfolio/PortfolioKpiCard.razor` — generischer Kachel-Rahmen (`portfolio-tile`, `portfolio-tile-title`, `portfolio-tile-body`); Parameter `Title`, `ChildContent`.

### Karten-Komponenten (Muster)
- `PortfolioStructureCard.razor` — `DonutChart`, KPI-Zeilen mit `KpiInfoButton`, `PositionsExplanationTable`, `MaxListEntries`-Konstante (200).
- `PortfolioPerformanceCard.razor` — `MiniBarChart` für Jahresrenditen (`IsYtd` → `*`-Suffix + `.portfolio-chart-footnote`-Fußnote), TWR/YTD-KPI-Zeilen, `.portfolio-return-table`.
- `PortfolioCashflowCard.razor` — `MiniBarChart` + KPI-Zeilen.
- `PortfolioRiskCard.razor` — Gauge-Platzhalter.
- Alle: `[Parameter, EditorRequired] Data { get; set; }`, `IStringLocalizer<Pages>`, Zahlenformat `N2`/`P2` kulturell.

### Diagramm-Komponenten (Muster, kein JS-Charting)
- `MiniBarChart.razor` (`Components/Shared`) — CSS-Balken pos/neg um Null-Linie; Parameter `Points` (`MiniBarChartPoint(Label, Value)`), `HeightPx`, `AriaLabel`, `EmptyText` (Leeranzeige `.mini-bar-chart-empty`), `TooltipFormatter`.
- `BudgetLineChart.razor` (`Components/Shared`) — SVG-Liniendiagramm (`viewBox 0 0 1000 325`): Grid, Achsen, Y-Labels (`N0`), zwei Pfade (`line budget`/`line actual`), Punkte mit `<title>`-Tooltips, X-Labels `MMM yy`, Legende; Parameter `Data` (`(DateOnly PeriodStart, decimal Budget, decimal Actual)`), Labels, `AriaLabel`.
- `DonutChart.razor` — SVG-Donut für Allokationen.
- **Kein** kombiniertes Linie+Balken-Diagramm vorhanden.

### CSS
`FinanceManager.Web/wwwroot/css/app.PortfolioReport.css` + `theme.Dark.PortfolioReport.css` (portfolio-tile-*, mini-bar-chart-*, portfolio-chart-footnote, u. a.); `app.BudgetLineChart.css` + `theme.Dark.BudgetLineChart.css`. Leerzustand: `.mini-bar-chart-empty` (app.PortfolioReport.css:418).

## `PortfolioKpiConfigurationRepository`
Datei: `FinanceManager.Infrastructure/Portfolio/PortfolioKpiConfigurationRepository.cs` — `GetAsync`/`UpsertAsync`/`DeleteAsync` gemäß Interface, `OwnerUserId`-Filter.

## resx-Ressourcen
`FinanceManager.Web/Resources/Pages.resx`, `Pages.de.resx`, `Pages.en.resx` — 74 `PortfolioReport_*`-Keys vorhanden (Kacheltitel `PortfolioReport_Tile_*`, `Th_*`-Spaltenköpfe, `Explain_*`, `YtdChartFootnote`, `Chart_Aria_*` u. a.). Zusätzlich existieren committete `Pages.*.resx.new`-Dateien (generierte Übersetzungs-Arbeitsdateien).
