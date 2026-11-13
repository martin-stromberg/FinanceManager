# Datenmodell – Bestandsaufnahme

Betroffene DTOs, Entitäten und Hilfs-Records für die Kachel „Depotentwicklung & Dividenden" (Issue #512).
Pfadkürzel: `Shared` = `FinanceManager.Shared`, `Domain` = `FinanceManager.Domain`, `App` = `FinanceManager.Application`.

## `PortfolioAnalysisReportDto`
Datei: `FinanceManager.Shared/Dtos/Portfolio/PortfolioAnalysisReportDto.cs`

Top-Level-Record des Depot-Berichts; wird von `GET api/portfolio/analysis-report` geliefert und vom `PortfolioAnalysisReportCacheService` als JSON in `ReportCacheEntry.CacheValue` persistiert.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Structure` | `PortfolioStructureDto` | Struktur-KPIs (Marktwert, Allokation, Top-Positionen) |
| `Performance` | `PortfolioPerformanceDto` | Performance-KPIs (TWR, Jahres-/Monatsrenditen) |
| `Cashflow` | `PortfolioCashflowDto` | Cashflow-KPIs (Einzahlungen, Dividenden, realisierte Gewinne, Liquidität) |
| `Risk` | `PortfolioRiskDto` | Risiko-KPIs (Phase-2-Platzhalter, alle `null`) |
| `GeneratedUtc` | `DateTime` | UTC-Zeitpunkt der Berechnung |
| `CacheValidUntilUtc` | `DateTime` | Cache-Gültigkeit (Monatsende) |

Befund: **Kein** Record-Argument für eine Jahresserie (Depotentwicklung) vorhanden — neue Sektion erforderlich. Da `System.Text.Json` fehlende Properties bei Referenz-Record-Parametern zu `null` deserialisiert, erzwingt eine DTO-Erweiterung die Erhöhung von `CacheSchemaVersion` (siehe `PortfolioAnalysisReportCacheService`).

## `PortfolioStructureDto` (+ Hilfsrecords)
Datei: `FinanceManager.Shared/Dtos/Portfolio/PortfolioStructureDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TotalMarketValue` | `decimal` | Gesamtmarktwert aller Positionen |
| `InvestedCapital` | `decimal` | FIFO-Anschaffungskosten der gehaltenen Bestände (`TotalCostBasis − StandaloneFeeTotal`-Konvention in `BuildStructure`) |
| `UnrealizedGainLoss` | `decimal` | `TotalMarketValue − InvestedCapital` |
| `AssetAllocation` | `IReadOnlyList<PortfolioAllocationSlice>` | Verteilung nach Kategorie |
| `RegionalDistribution` | `IReadOnlyList<PortfolioAllocationSlice>` | Verteilung nach Region |
| `SectorDistribution` | `IReadOnlyList<PortfolioAllocationSlice>` | Verteilung nach Sektor |
| `TopPositions` | `IReadOnlyList<PortfolioTopPosition>` | Top-10 nach Marktwert |
| `AllPositions` | `IReadOnlyList<PortfolioTopPosition>` | Alle Positionen ≠ 0, absteigend |
| `InvestedCapitalBreakdown` | `IReadOnlyList<PortfolioInvestedCapitalPosition>` | FIFO-Lots je Wertpapier |

Hilfsrecords in derselben Datei: `PortfolioAllocationSlice(Label, Value, Percentage)`, `PortfolioTopPosition(SecurityId, Name, MarketValue, Percentage, UnrealizedGainLoss)`, `PortfolioInvestedCapitalLot(PurchaseDate, Quantity, CostPerUnit, TotalCost)`, `PortfolioInvestedCapitalPosition(SecurityId, Name, InvestedCapital, Lots)`.

## `PortfolioPerformanceDto` (+ Punkt-Records)
Datei: `FinanceManager.Shared/Dtos/Portfolio/PortfolioPerformanceDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TimeWeightedReturn` | `decimal?` | TWR (Modified Dietz, verkettet) seit Beginn |
| `YtdReturn` | `decimal?` | Jahresrendite des laufenden Jahres |
| `AnnualReturns` | `IReadOnlyList<PortfolioAnnualReturnPoint>` | Rendite je Kalenderjahr |
| `MonthlyReturns` | `IReadOnlyList<PortfolioMonthlyReturnPoint>` | Rendite je Kalendermonat |

- `PortfolioAnnualReturnPoint(int Year, decimal ReturnPercent, bool IsYtd)` — `IsYtd` markiert das laufende Jahr; Muster für das optionale `IsYtd`-Flag der neuen Jahreszeilen.
- `PortfolioMonthlyReturnPoint(int Year, int Month, decimal? ReturnPercent)`.

## `PortfolioCashflowDto`
Datei: `FinanceManager.Shared/Dtos/Portfolio/PortfolioCashflowDto.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `NetDepositsCurrentYear` | `decimal` | Σ\|Buy\| − Σ Sell (laufendes Jahr) |
| `DividendsCurrentYear` | `decimal` | Brutto-Dividenden des laufenden Jahres |
| `RealizedGainsCurrentYear` | `decimal` | FIFO-realisierte Gewinne des Jahres |
| `LiquidityRatio` | `decimal?` | Bargeld / (Marktwert + Bargeld), `null` wenn nicht sinnvoll |
| `LiquidityCashBalance` | `decimal` | Saldo verknüpfter Verrechnungskonten |
| `LiquidityTotalMarketValue` | `decimal` | Marktwertbasis der Liquiditätsquote |

## `PortfolioRiskDto`
Datei: `FinanceManager.Shared/Dtos/Portfolio/PortfolioRiskDto.cs`

Platzhalter (`Volatility`, `MaxDrawdown`, `SharpeRatio`, `Beta`, `ValueAtRisk` — alle `decimal?`, aktuell `null`). Nicht von der Anforderung betroffen.

## `PortfolioKpiConfigurationDto` / `PortfolioKpiConfigurationRequest`
Dateien: `FinanceManager.Shared/Dtos/Portfolio/PortfolioKpiConfigurationDto.cs`, `PortfolioKpiConfigurationRequest.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ActiveTileIds` | `IReadOnlyList<PortfolioTileId>` / `List<PortfolioTileId>` (`[Required, MinLength(1)]`) | Sichtbare Kacheln |
| `TileOrder` | `IReadOnlyList<PortfolioTileId>` / `List<PortfolioTileId>` (`[Required, MinLength(1)]`) | Anzeigereihenfolge (enthält auch inaktive Kacheln) |
| `UpdatedUtc` | `DateTime` | Nur im DTO: Zeitpunkt der letzten Änderung |

## `PortfolioKpiConfiguration` (Entität)
Datei: `FinanceManager.Domain/Portfolio/PortfolioKpiConfiguration.cs`

Persistiert Kachel-Auswahl/-Reihenfolge pro Benutzer; `OwnerUserId` wird im Konstruktor via `Guards.NotEmpty` gesetzt.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer (serverseitig aus Auth-Kontext) |
| `ActiveTileIds` | `string` | JSON-serialisiertes `PortfolioTileId`-Array |
| `TileOrder` | `string` | JSON-serialisiertes `PortfolioTileId`-Array |
| `UpdatedUtc` | `DateTime` | Letzter Update-Zeitpunkt |

Methoden: `Update(activeTileIds, tileOrder)`, `ToBackupDto()`, `AssignBackupDto(dto)`; Backup-Record `PortfolioKpiConfigurationBackupDto(Id, OwnerUserId, ActiveTileIds, TileOrder, UpdatedUtc, CreatedUtc, ModifiedUtc)` bereits vorhanden.
Befund: TileIds werden als JSON-String gespeichert — neue Enum-Werte sind ohne Migration/Backup-Änderung kompatibel.

## `ReportCacheEntry` (Entität)
Datei: `FinanceManager.Domain/Reports/ReportCacheEntry.cs`

Generischer DB-Cache-Eintrag; `PortfolioAnalysisReportCacheService` nutzt `CacheKey = "portfolio-analysis-report-{ownerUserId:N}"` und speichert die `CacheSchemaVersion` in `Parameter`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `OwnerUserId` | `Guid` | Besitzer des Cache-Eintrags |
| `CacheKey` | `string` | Berichts-Schlüssel |
| `CacheValue` | `string` | Serialisiertes JSON |
| `NeedsRefresh` | `bool` | Erzwingt Neuberechnung |
| `Parameter` | `string` | Hier: `CacheSchemaVersion` („5") |
| `CacheValidUntilUtc` | `DateTime?` | Zeitbasierte Gültigkeit (Monatsende) |

## Hilfsrecords (Berechnungs-Inputs/Outputs)

`FinanceManager.Application/Securities/ReturnAnalysis/ReturnAnalysisInputTypes.cs`:
- `SecurityTransaction(Guid Id, DateTime Date, SecurityPostingSubType Type, decimal Amount, decimal? Quantity, Guid GroupId)` — normalisierte Transaktion für FIFO/Analysen; `Amount` negativ = Abfluss.
- `TwrPeriodInput(DateTime Start, DateTime End, decimal StartValue, decimal EndValue, decimal ExternalCashflow)`.
- `CashflowPoint(DateTime Date, decimal Amount)`.

`FinanceManager.Application/Securities/ReturnAnalysis/ReturnAnalysisDtos.cs`:
- `FifoCostBasisResult(TotalCostBasis, RealizedGains, RemainingLots, TotalSharesHeld, HasOversellWarning, OversellWarningMessage, StandaloneFeeTotal)` — `TotalCostBasis` enthält Standalone-Fees (Option-A-Konvention).
- `FifoLot(PurchaseDate, Quantity, CostPerUnit)`.

`FinanceManager.Shared/Dtos/Securities/ReturnAnalysisDtos.cs`:
- `AnnualDividendPoint(int Year, decimal GrossDividend, decimal NetDividend, decimal CumulativeNet)` — Referenzmodell der Netto-Dividendenkonvention (`Σ Dividend − Σ |Tax|`), erzeugt von `ReturnAnalysisService.BuildAnnualDividends` für ein einzelnes Wertpapier.
