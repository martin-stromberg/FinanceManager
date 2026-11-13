# Enums – Bestandsaufnahme

## `PortfolioTileId`
Datei: `FinanceManager.Shared/Dtos/Portfolio/PortfolioTileId.cs`

Identifiziert eine Kachel des Depot-Berichts; wird in `PortfolioKpiConfigurationDto`, `PortfolioKpiConfigurationRequest`, der persistierten JSON-Konfiguration (`PortfolioKpiConfiguration.ActiveTileIds`/`TileOrder`) und der Render-Verzweigung von `PortfolioAnalysisReportPage.razor` verwendet.

| Wert | Bedeutung |
|------|-----------|
| `Structure = 0` | Struktur-Kachel (Marktwert, investiertes Kapital, Allokation, Top-Positionen) |
| `Performance = 1` | Performance-Kachel (TWR, Jahres-/Monatsrenditen) |
| `Cashflow = 2` | Cashflow-Kachel (Netto-Einzahlungen, Dividenden, realisierte Gewinne, Liquiditätsquote) |
| `Risk = 3` | Risiko-Kachel (Phase 2: Volatilität, Drawdown, Beta, VaR, Sharpe) |

Befunde:
- Kein Mitglied für „Depotentwicklung" vorhanden.
- `Risk` existiert als TileId, ist aber **nicht** in `DefaultConfigurationDto()` enthalten — Beleg, dass neue Kacheln nicht zwingend standardmäßig aktiv sein müssen (relevant für offene Frage 5 der Anforderung).
- `PortfolioTileId` ist in `FinanceManager.Tests/Common/EnumCoverageTests.cs` **nicht** abgedeckt (kein `AssertValues<PortfolioTileId>`-Eintrag vorhanden); laut `AGENT.md` sind neue Enum-Mitglieder dort zu ergänzen — die Anforderung verlangt den Eintrag ohnehin.

## `SecurityPostingSubType`
Datei: `FinanceManager.Shared/Dtos/Securities/SecurityPostingSubType.cs`

Buchungs-Untertypen für Wertpapierposten; Basis aller Jahres-Aggregationen (`SecurityTransaction.Type`, `Posting.SecuritySubType`).

| Wert | Bedeutung |
|------|-----------|
| `Buy = 0` | Kauf (Betrag negativ/Abfluss, `Quantity` positiv) |
| `Sell = 1` | Verkauf (Betrag positiv/Zufluss; `Quantity` domänenseitig negativ, FIFO nutzt `Math.Abs`) |
| `Dividend = 2` | Dividende (Brutto, positiv) |
| `Fee = 3` | Gebühr (negativ; via `GroupId` einem Buy-Lot zuordenbar) |
| `Tax = 4` | Steuer (negativ; wird in der Netto-Dividendenkonvention als `\|Tax\|` abgezogen) |
