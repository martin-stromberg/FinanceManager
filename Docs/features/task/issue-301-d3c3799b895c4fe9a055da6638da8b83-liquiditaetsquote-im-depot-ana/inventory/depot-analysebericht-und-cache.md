# Depot-Analysebericht und Cache

## PortfolioAnalysisReportService

`FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportService.cs` berechnet den Bericht in `GetPortfolioAnalysisReportAsync`:

- `LoadPositionsAsync` laedt Wertpapiere des Benutzers, Kategorien, Security-Postings und Preise.
- `BuildStructure` berechnet Marktwert, investiertes Kapital, Allokationen und Top-Positionen.
- `BuildPerformance` berechnet Jahres-/Monatsrenditen.
- `BuildCashflow` berechnet aktuelle Jahreswerte fuer Nettoeinzahlungen, Dividenden und realisierte Gewinne.

Relevante Zeilen:

- `GetPortfolioAnalysisReportAsync`: Struktur und Cashflow werden getrennt aufgebaut (`PortfolioAnalysisReportService.cs:54`, `:56`).
- `LoadPositionsAsync`: Positionsdaten werden ab `:80` geladen.
- Security-Posting-Filter: `SecurityId != null` und `SecuritySubType != null` (`:99`).
- `BuildCashflow`: beginnt bei `:366`, Rueckgabe ohne Liquiditaet bei `:396`.

## Aktuelle Datenluecke

Der Service laedt keine Accounts und keine Bank-Postings. Dadurch kennt `BuildCashflow` keinen nicht investierten Bestand. `PortfolioStructureDto.TotalMarketValue` enthaelt nur Wertpapier-Marktwerte, nicht Cash.

## Moegliche technische Einbindung

Naheliegende Erweiterung:

- internes Snapshot-/Context-Record erweitern oder einen separaten `decimal depotCashBalance` neben `positions` laden.
- zusaetzliche Query in `GetPortfolioAnalysisReportAsync` oder `LoadPositionsAsync`, die ueber Security-Posting-Gruppen die zugehoerigen Bank-Postings und Accounts findet.
- `BuildCashflow` um Parameter `totalMarketValue` oder `depotCashBalance` erweitern.
- `LiquidityRatio` als `cashBalance / (totalMarketValue + cashBalance)` berechnen, sofern Nenner `> 0m`, sonst `0m`.

## PortfolioAnalysisReportCacheService

`FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportCacheService.cs` speichert den Bericht als JSON in `ReportCacheEntry.CacheValue`. Die Gueltigkeit ist an Monat und Schema gebunden:

- `CacheSchemaVersion = "2"` aktuell.
- Cache-Hit nur, wenn `entry.Parameter == CacheSchemaVersion`.
- `InvalidateCacheAsync` entfernt vorhandene Eintraege.

Da `PortfolioCashflowDto` ein neues Feld bekommt, muss `CacheSchemaVersion` erhoeht werden. Andernfalls koennten alte JSON-Eintraege ohne `LiquidityRatio` in ein neues DTO-Shape deserialisiert oder irrefuehrend weitergenutzt werden.
