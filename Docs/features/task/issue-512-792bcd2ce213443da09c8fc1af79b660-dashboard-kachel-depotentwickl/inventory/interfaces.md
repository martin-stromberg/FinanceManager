# Interfaces – Bestandsaufnahme

## `IPortfolioAnalysisReportService`
Datei: `FinanceManager.Application/Portfolio/IPortfolioAnalysisReportService.cs`
Implementierung: `PortfolioAnalysisReportService` (Infrastructure).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPortfolioAnalysisReportAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task<PortfolioAnalysisReportDto>` | Berechnet den vollständigen Depot-Bericht für den Besitzer |

Befund: Rückgabetyp ist das Gesamt-DTO — eine neue Kachelsektion erfordert **keine** Interface-Änderung (nur DTO-Erweiterung).

## `IPortfolioAnalysisReportCacheService`
Datei: `FinanceManager.Application/Portfolio/IPortfolioAnalysisReportCacheService.cs`
Implementierung: `PortfolioAnalysisReportCacheService` (Infrastructure).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetPortfolioReportAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task<PortfolioAnalysisReportDto>` | Monatlich gecachter Report (Schema-Version + `CacheValidUntilUtc`) |
| `InvalidateCacheAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task` | Löscht den Cache-Eintrag (Ribbon „Refresh", Config-Save) |

## `IPortfolioKpiConfigurationRepository`
Datei: `FinanceManager.Application/Portfolio/IPortfolioKpiConfigurationRepository.cs`
Implementierung: `PortfolioKpiConfigurationRepository` (Infrastructure).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task<PortfolioKpiConfiguration?>` | Gespeicherte Kachel-Konfiguration oder `null` |
| `UpsertAsync` | `Guid ownerUserId`, `string activeTileIds`, `string tileOrder`, `CancellationToken ct` | `Task<PortfolioKpiConfiguration>` | Konfiguration anlegen/aktualisieren (JSON-Strings) |
| `DeleteAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task` | Konfiguration löschen |

## `IFifoCostBasisCalculator`
Datei: `FinanceManager.Application/Securities/ReturnAnalysis/IFifoCostBasisCalculator.cs`
Implementierung: `FifoCostBasisCalculator`. Stateless/thread-safe.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Calculate` | `IReadOnlyList<SecurityTransaction> transactions` | `FifoCostBasisResult` | FIFO-Kostenbasis + realisierte Gewinne; erwartet chronologisch sortierte Transaktionen; Fees per `GroupId` lotgebunden, sonst standalone |

## `IReturnCalculationService`
Datei: `FinanceManager.Application/Securities/ReturnAnalysis/IReturnCalculationService.cs`
Implementierung: `ReturnCalculationService`. Reine Finanzmathematik, stateless.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `CalculateTwr` | `IReadOnlyList<TwrPeriodInput>` | `decimal?` | Zeitgewichtete Rendite (Modified Dietz, verkettet) |
| `CalculateTotalReturn` | `investedCapital`, `currentMarketValue`, `netDividends` | `decimal?` | Gesamtrendite |
| `CalculateIrr` | `IReadOnlyList<CashflowPoint>`, `maxIterations` | `decimal?` | XIRR (Newton-Raphson + Bisektion) |
| `CalculateCagr` | `startValue`, `endValue`, `years` | `decimal?` | CAGR |
| `CalculateVolatility` | `IReadOnlyList<decimal>` | `decimal?` | Annualisierte Volatilität |
| `CalculateMaxDrawdown` | `IReadOnlyList<decimal>` | `decimal?` | Maximaler Drawdown |
| `CalculateSharpeRatio` | `annualisedReturn`, `riskFreeRate`, `volatility` | `decimal?` | Sharpe Ratio |
| `CalculateDividendYield` | `totalDividends`, `investedCapital` | `decimal?` | **Ausschüttungsrendite = Dividenden / Basis; `null` bei Basis = 0 — direkte Referenz für die Rendite-Spalte** |
| `CalculateTaxRate` | `totalTaxes`, `grossReturn` | `decimal?` | Steuerquote |

## `IApiClient` (relevante Methoden)
Datei: `FinanceManager.Shared/IApiClient.cs`; Implementierung `ApiClient.Portfolio.cs` (partial).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Portfolio_GetAnalysisReportAsync` | `CancellationToken` | `Task<PortfolioAnalysisReportDto>` | Report laden |
| `Portfolio_GetKpiConfigurationAsync` | `CancellationToken` | `Task<PortfolioKpiConfigurationDto>` | Kachel-Konfiguration laden |
| `Portfolio_SaveKpiConfigurationAsync` | `PortfolioKpiConfigurationRequest`, `CancellationToken` | `Task<PortfolioKpiConfigurationDto?>` | Konfiguration speichern (`null` bei 400) |
| `Portfolio_ResetCacheAsync` | `CancellationToken` | `Task` | Cache invalidieren |
