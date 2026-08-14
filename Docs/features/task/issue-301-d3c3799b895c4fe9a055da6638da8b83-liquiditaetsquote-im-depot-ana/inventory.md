# Bestandsaufnahme - Liquiditaetsquote im Depot-Analysebericht

## Zusammenfassung

Der Depot-Analysebericht wird ueber `PortfolioAnalysisReportService` berechnet und ueber `PortfolioAnalysisReportCacheService` monatsgueltig gecacht. Die Cashflow-Kachel enthaelt aktuell nur Einzahlungen, Dividenden und realisierte Gewinne; eine Liquiditaetsquote ist weder im DTO noch in der Razor-Komponente vorhanden.

Fachlich relevante Kontosalden liegen in `Account.CurrentBalance`. Die Buchungsanlage in `StatementDraftService` passt diesen Saldo beim Commit eines Kontoauszugs an und erzeugt daneben Wertpapierbuchungen als `PostingKind.Security`. Der bestehende Bericht laedt derzeit nur Wertpapierpositionen und ignoriert Konto-/Cash-Salden. Fuer die Liquiditaetsquote muss daher eine zusaetzliche Ermittlung depotbezogener Konten in den Berichtspfad aufgenommen und der Cache bei relevanten Kontostandsaenderungen invalidiert werden.

## Detaildokumente

- [Datenmodell und Buchungspfad](inventory/datenmodell-und-buchungen.md)
- [Depot-Analysebericht und Cache](inventory/depot-analysebericht-und-cache.md)
- [UI, API und Lokalisierung](inventory/ui-api-und-lokalisierung.md)
- [Tests und Risiken](inventory/tests-und-risiken.md)

## Wichtige Fundstellen

| Bereich | Datei | Beobachtung |
|---|---|---|
| Berichtsermittlung | `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportService.cs` | `LoadPositionsAsync` laedt Securities, Security-Postings und Preise; `BuildCashflow` berechnet nur Jahreswerte aus Wertpapiertransaktionen. |
| Cache | `FinanceManager.Infrastructure/Portfolio/PortfolioAnalysisReportCacheService.cs` | Cache verwendet `CacheSchemaVersion = "2"` und muss bei DTO-Shape-Aenderung erhoeht werden. |
| DTO | `FinanceManager.Shared/Dtos/Portfolio/PortfolioCashflowDto.cs` | Record hat genau drei nicht-nullable `decimal`-Felder; `LiquidityRatio` fehlt. |
| UI | `FinanceManager.Web/Components/Pages/Portfolio/PortfolioCashflowCard.razor` | Rendert drei KPI-Zeilen und ein Mini-Bar-Chart; Prozent-KPI fuer Liquiditaet fehlt. |
| Kontosalden | `FinanceManager.Domain/Accounts/Account.cs` | `CurrentBalance` ist persistenter Saldo; `AdjustBalance` veraendert ihn. |
| Buchungsanlage | `FinanceManager.Infrastructure/Statements/StatementDraftService.cs` | Kontoauszugsbuchung erzeugt Bank-/Kontakt-/Security-Postings und passt den Kontosaldo an. |
| Cache-Invalidierung | `FinanceManager.Infrastructure/Securities/SecurityPriceService.cs`, `FinanceManager.Infrastructure/Postings/PostingReversalService.cs` | Bestehendes Muster: optional injizierter `IPortfolioAnalysisReportCacheService` wird nach Portfolio-relevanten Aenderungen aufgerufen. |

## Ableitungen fuer die Planung

- Die kleinste fachlich anschlussfaehige Loesung ist, depotbezogene Konten ueber bestehende Buchungsgruppen zu bestimmen. Im aktuellen Anlagepfad haben `PostingKind.Security`-Buchungen `AccountId = null`; die Bankbuchung derselben Gruppe traegt das Konto.
- Die Berechnung sollte den Cash-Bestand getrennt vom Positions-Snapshot laden, damit bestehende Struktur- und Performance-Berechnungen unveraendert bleiben koennen.
- Die Formel aus der Anforderung ist umsetzbar als `cashBalance / (structure.TotalMarketValue + cashBalance)`, wenn der Nenner groesser als `0` ist. Da vorhandene Cashflow-DTO-Werte nicht nullable sind, ist `0m` fuer undefinierte Faelle konsistent.
- Bei Erweiterung von `PortfolioCashflowDto` muessen alle Test-Fixtures und DTO-Konstruktoraufrufe angepasst werden.
- Der Cache muss sowohl per Schema-Version als auch bei Kontostandsaenderungen frisch werden. Fuer Statement-Draft-Commits fehlt aktuell eine Portfolio-Cache-Invalidierung; vorhanden ist nur Budget-Report-Cache-Logik.

## Offene Punkte aus der Bestandsaufnahme

- Die fachliche Abgrenzung gemischter Konten bleibt nicht eindeutig: ein Konto kann Wertpapierverrechnung und normalen Zahlungsverkehr enthalten.
- Negative Verrechnungskontensalden sind technisch moeglich; die Planung muss entscheiden, ob sie in die Quote eingehen oder auf `0` begrenzt werden.
- Der bestehende Bericht ist benutzerweit, nicht depotweise. Es gibt kein separates Depot-Aggregat.
