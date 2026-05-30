# Flow-Dokumentation – Übersicht

Dieser Ordner enthält Programmablaufpläne und technische Beschreibungen der zentralen Abläufe im FinanceManager.

## Dokumentierte Flows

| Datei | Bereich | Kurzbeschreibung |
|---|---|---|
| [import-classification.md](import-classification.md) | Import | Upload → Parser → Drafts → Klassifizierung → Persistierung |
| [posting-aggregates.md](posting-aggregates.md) | Buchhaltung | Erstellung und Aktualisierung von Buchungsaggregaten (Perioden-Buckets) |
| [split-uploadgroup.md](split-uploadgroup.md) | Import | Aufspaltung von UploadGroups in einzelne Drafts |
| [statement-draft-booking.md](statement-draft-booking.md) | Buchhaltung | Überführung eines Statement-Drafts in gebuchte Postings |
| [return-analysis-service.md](return-analysis-service.md) | Renditeanalyse | `ReturnAnalysisService` als Orchestrierungsschicht: Cache-First-Muster, alle 10 öffentlichen Methoden, Ownership-Enforcement (S-1), BUG-1-Hinweis |
| [return-calculation.md](return-calculation.md) | Renditeanalyse | Stateless-Berechnungen: TWR (Modified Dietz), IRR (Newton-Raphson + Bisection), CAGR, Volatilität, MaxDrawdown, Sharpe Ratio, DividendYield, TaxRate |
| [fifo-cost-basis.md](fifo-cost-basis.md) | Renditeanalyse | FIFO-Kostenbasisberechnung: Lot-Verwaltung, Fee-Verknüpfung via GroupId, Oversell-Handling |
