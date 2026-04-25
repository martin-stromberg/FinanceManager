# Planungsübersicht: Renditeanalyse (FA-WERT-REN-001)

> Erstellt: 2025-01  
> Status: 🟡 Bedingte Freigabe (Blocker vor Implementierungsstart klären)

## Ziel

Erweiterung des FinanceManagers um eine zwei­stufige Wertpapier-Renditeanalyse:

1. **Kompakte Rendite-Box** direkt auf der Wertpapier-Detailseite (Total Return, CAGR, Marktwert, Mini-Chart)
2. **Detaillierte Analyse-Unterseite** `/securities/{id}/performance` mit 5 Tabs (Übersicht · Zeitliche Entwicklung · Cashflows · Kennzahlen · Benchmark)

---

## Planungsdokumente

| Schritt | Dokument | Inhalt |
|---------|----------|--------|
| 1 – Anforderungsanalyse | [FA-WERT-REN-001_Renditeanalyse.md](requirements/FA-WERT-REN-001_Renditeanalyse.md) | FR/NFR-Tabellen, Akzeptanzkriterien, Domänenmodell, Use Cases |
| 2 – Architektur-Blueprint | [architecture-blueprint-renditeanalyse.md](architecture/architecture-blueprint-renditeanalyse.md) | Schichtenmodell, neue Interfaces, Cache-Strategie, UI/UX-Konzept, Technologieentscheidungen |
| 3 – Entity-Relationship-Modell | [entity-relationship-model-renditeanalyse.md](architecture/entity-relationship-model-renditeanalyse.md) | ERM-Diagramm, neue User-Felder, DB-Migrationen, Beziehungsübersicht |
| 4 – Architektur-Review | [review-renditeanalyse.md](improvements/review-renditeanalyse.md) | Schwachstellen, Risiken, priorisierte Verbesserungsvorschläge, Freigabeempfehlung |

---

## Kernentscheidungen (Zusammenfassung)

| Bereich | Entscheidung |
|---------|--------------|
| **Neue DB-Felder** | `User`: `BenchmarkSecurityId`, `ShowSharpeRatio`, `RiskFreeRate` (2 EF-Migrationen) |
| **Neue Indizes** | `SecurityPrices(SecurityId, Date)` + `Postings(SecurityId, BookingDate)` |
| **Berechnungsmodul** | `IReturnCalculationService` (zustandslos) + `IFifoCostBasisCalculator` + `IReturnAnalysisService` (Orchestrierung) |
| **Caching** | `IMemoryCache` via `IReturnAnalysisCache` (TTL 1h, Invalidierung bei neuen Postings/Kursen) |
| **Chart-Bibliothek** | ApexCharts.Blazor (Line, Bar, Heatmap, Sparkline) |
| **FIFO** | Queue-basiert, Tiebreak über `Posting.Id`, Oversell-Verhalten zu definieren |
| **Finanzmathematik** | TWR (Modified Dietz), IRR (Newton-Raphson + Bisection-Fallback), CAGR, Volatilität (Annualisiert), Max. Drawdown, Sharpe Ratio |

---

## Offene Blocker (vor Implementierungsstart klären!)

| # | Risiko | Maßnahme |
|---|--------|----------|
| **S-1** | `Posting` hat kein direktes `OwnerUserId` – Security-Scoping via JOIN | Query immer über `JOIN Security ON Posting.SecurityId = Security.Id WHERE Security.OwnerUserId = @userId` |
| **S-2** | Division by Zero in TWR wenn Anfangswert = 0 (erster Kauf) | Guard: Wenn `Anfangswert + 0.5 × CF ≤ 0` → Subperiode überspringen oder IRR als Fallback |
| **S-3** | Benchmark-Ownership nur beim Setzen geprüft | DB-FK-Constraint oder Ownership-Check beim Laden der Benchmark-Kursdaten |

---

## Nächste Schritte

1. ☐ Blocker S-1, S-2, S-3 in Code-Kommentaren / Interface-Spezifikation festschreiben
2. ☐ FIFO-Oversell-Verhalten definieren (Exception? Warnung? Ignorieren?)
3. ☐ IRR-Tageszählung festlegen (Actual/365)
4. ☐ EF-Migrationen erstellen: `AddReturnAnalysisSettingsToUser` + `AddReturnAnalysisPerformanceIndexes`
5. ☐ ApexCharts.Blazor NuGet-Paket evaluieren und hinzufügen
6. ☐ `IReturnCalculationService` + `IFifoCostBasisCalculator` implementieren (inkl. Unit-Tests)
7. ☐ `IReturnAnalysisService` + Cache implementieren
8. ☐ Blazor-Komponenten: `ReturnSummaryWidget` + Detailseite mit Tabs
9. ☐ Lokalisierung (DE/EN) für alle neuen UI-Texte
10. ☐ Integrations-/UI-Tests für Renditeberechnungen

---

## Review-Ergebnis

**🟡 Bedingte Freigabe** – Die Architektur ist solide und gut strukturiert. Vor Implementierungsstart müssen die 3 Blocker (S-1 bis S-3) und 4 Major-Punkte aus dem [Review](improvements/review-renditeanalyse.md) adressiert werden.
