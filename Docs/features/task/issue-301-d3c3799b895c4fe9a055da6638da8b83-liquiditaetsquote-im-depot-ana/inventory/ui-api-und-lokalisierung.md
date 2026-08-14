# UI, API und Lokalisierung

## DTO/API

`FinanceManager.Shared/Dtos/Portfolio/PortfolioCashflowDto.cs` enthaelt aktuell:

- `NetDepositsCurrentYear`
- `DividendsCurrentYear`
- `RealizedGainsCurrentYear`

`LiquidityRatio` fehlt. Da der Record positional ist, muessen alle Konstruktoraufrufe in Services und Tests angepasst werden.

`FinanceManager.Shared/Dtos/Portfolio/PortfolioAnalysisReportDto.cs` kapselt `PortfolioCashflowDto` als `Cashflow`. Die Controller-Signatur muss voraussichtlich nicht geaendert werden, aber die JSON-Antwort erweitert sich.

`FinanceManager.Web/Controllers/PortfolioAnalysisReportController.cs` liefert den Bericht ueber `GET /api/portfolio/analysis-report` aus dem Cache-Service. Fuer die Liquiditaetsquote ist hier keine eigene Logik erforderlich.

## Razor-Komponente

`FinanceManager.Web/Components/Pages/Portfolio/PortfolioCashflowCard.razor`:

- rendert `PortfolioKpiCard` mit Cashflow-Titel.
- baut ein `MiniBarChart` aus den drei Betrag-KPIs.
- zeigt drei `portfolio-kpi-row`-Zeilen mit `KpiInfoButton`.

Die Liquiditaetsquote sollte als zusaetzliche KPI-Zeile mit Prozentformat angezeigt werden. Sie sollte nicht in das bestehende Betrag-Bar-Chart aufgenommen werden, weil sie eine Quote statt eines Geldbetrags ist.

## Lokalisierung

Die verwendeten Portfolio-Report-Keys liegen in:

- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`

Vorhandene Keys:

- `PortfolioReport_Tile_Cashflow`
- `PortfolioReport_NetDeposits`
- `PortfolioReport_Dividends`
- `PortfolioReport_RealizedGains`
- erklaerende `PortfolioReport_Explain_*`-Keys fuer vorhandene KPI-Info-Buttons.

Fuer die Quote werden neue Keys in allen drei Ressourcendateien benoetigt, z. B.:

- `PortfolioReport_LiquidityRatio`
- `PortfolioReport_Explain_LiquidityRatio_Title`
- `PortfolioReport_Explain_LiquidityRatio_Text`

## ViewModel/Page

`PortfolioAnalysisReportPage.razor` uebergibt `PortfolioReportData.Cashflow` direkt an `PortfolioCashflowCard`. `PortfolioAnalysisReportPageViewModel` muss voraussichtlich nicht fachlich geaendert werden, solange DTO und API-Client angepasst sind.
