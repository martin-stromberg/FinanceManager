# Lifecycle Report – Feature: Renditeanalyse

**Feature-ID:** FA-WERT-REN-001  
**Erstellt am:** 2026-04-25  
**Status:** ✅ Abgeschlossen (mit bekannten offenen Punkten)

---

## 1. Planung

| Dokument | Pfad |
|----------|------|
| Anforderungsanalyse | [docs/requirements/FA-WERT-REN-001_Renditeanalyse.md](requirements/FA-WERT-REN-001_Renditeanalyse.md) |
| Architektur-Blueprint | [docs/architecture/architecture-blueprint-renditeanalyse.md](architecture/architecture-blueprint-renditeanalyse.md) |
| Entity-Relationship-Modell | [docs/architecture/entity-relationship-model-renditeanalyse.md](architecture/entity-relationship-model-renditeanalyse.md) |
| Architecture Review | [docs/improvements/review-renditeanalyse.md](improvements/review-renditeanalyse.md) |
| Planungsübersicht | [docs/planning-renditeanalyse.md](planning-renditeanalyse.md) |

Das Architecture Review ergab eine **bedingte Freigabe** mit 3 Blockern, die alle in der Implementierung adressiert wurden:
- **S-1**: User-Scoping via JOIN Security (kein direktes OwnerUserId auf Posting)
- **S-2**: Division-by-Zero-Guard in TWR beim ersten Kauf
- **S-3**: Benchmark-Ownership-Prüfung gegen Cross-User-Datenleck

---

## 2. Implementierung

**68 neue Dateien, 11.611 Zeilen Code, 0 Build-Fehler.**

### Domain
- `User.ReturnAnalysis.cs` – Benutzereinstellungen: `BenchmarkSecurityId`, `ShowSharpeRatio`, `RiskFreeRate`

### Application
- `IReturnAnalysisService` / `IReturnCalculationService` / `IFifoCostBasisCalculator` / `IReturnAnalysisCache` – Interfaces
- `ReturnCalculationService` – TWR (Modified Dietz), IRR/XIRR (Newton-Raphson + Bisection), CAGR, Volatilität, MaxDrawdown, Sharpe Ratio
- `FifoCostBasisCalculator` – FIFO mit Lot-Verwaltung, Oversell-Warnung, Gebühren auf Kostenbasis
- Vollständige DTOs für API-Vertrag

### Infrastructure
- `ReturnAnalysisService` – Orchestrierung mit EF Core, User-Scoped Abfragen
- `MemoryReturnAnalysisCache` – IMemoryCache mit 1h TTL, prefix-basierte Invalidierung
- EF Migration: `20260425085408_AddReturnAnalysisSettingsToUser`

### Web
- 10 neue REST-Endpunkte in `SecuritiesController`
- `ReturnSummaryWidget.razor` – kompakte Kennzahlen-Box auf der Wertpapier-Detailseite
- `SecurityPerformancePage.razor` (`/securities/{id}/performance`) – Detailseite mit 5 Tabs: Übersicht, Zeitliche Entwicklung, Cashflows, Kennzahlen, Benchmark

---

## 3. Tests

**58 neue Tests, 489 Tests gesamt, alle grün ✅**

| Datei | Tests |
|-------|-------|
| `ReturnCalculationServiceTests.cs` | 53 |
| `FifoCostBasisCalculatorTests.cs` | 18 |
| `ReturnAnalysisCacheTests.cs` | 8 |
| `ReturnAnalysisServiceTests.cs` | 16 |

Coverage-Analyse: [docs/tests/return-analysis-coverage-gaps.md](tests/return-analysis-coverage-gaps.md)  
Testplan: [docs/tests/return-analysis-test-plan.md](tests/return-analysis-test-plan.md)

---

## 4. Dokumentation

| Dokument | Beschreibung |
|----------|-------------|
| [docs/api/SecuritiesController.md](api/SecuritiesController.md) | API-Referenz mit allen 24 Endpunkten inkl. 10 neuer Return-Analysis-Endpoints |
| [docs/flows/return-analysis-service.md](flows/return-analysis-service.md) | Sequenz-/Flowcharts, Cache, Sicherheitsregeln |
| [docs/flows/return-calculation.md](flows/return-calculation.md) | TWR, IRR, CAGR, Volatilität, MaxDrawdown, Sharpe Ratio (mit Formeln) |
| [docs/flows/fifo-cost-basis.md](flows/fifo-cost-basis.md) | FIFO-Ablauf, Lot-Verwaltung, Oversell-Handling |
| [docs/business/features/F017-renditeanalyse.md](business/features/F017-renditeanalyse.md) | Endanwender-Dokumentation, 5 Tabs, FAQ |
| [docs/business/features/F017-renditeanalyse-domain.md](business/features/F017-renditeanalyse-domain.md) | Fachliche Doku: Formeln, FIFO-Regeln, Caching, DB |
| `README.md` | Features, Architektur, Doku-Links aktualisiert |

---

## 5. Offene Punkte & Hinweise

| ID | Priorität | Beschreibung |
|----|-----------|-------------|
| BUG-1 | 🔴 Hoch | `BuildTwrPeriods` verwendet `start` statt `end` → TWR-Berechnung ggf. fehlerhaft. Regressionstest vorhanden, wird beim Fix rot → grün. |
| TEST-P2 | 🟡 Mittel | Tests für periodische Renditen und Benchmark-Normalisierung noch ausstehend (im Testplan spezifiziert) |
| TEST-P3 | 🟢 Niedrig | bUnit UI-Tests für `ReturnSummaryWidget` und `SecurityPerformancePage` noch nicht implementiert |
| OPT-1 | 🟢 Niedrig | Sharpe Ratio erfordert konfigurierten risikofreien Zinssatz – Standardwert sinnvoll vorbelegen |
| OPT-2 | 🟢 Niedrig | Benchmark-Vergleich: Normalisierung auf gemeinsamen Startpunkt noch zu verifizieren |

---

## 6. Scope-Entscheidungen (aus Anforderungsklärung)

| Funktion | Entscheidung |
|----------|-------------|
| IRR (Interner Zinsfuß) | ✅ Implementiert |
| Mini-Chart auf Wertpapierseite | ✅ Implementiert |
| Sharpe Ratio | ✅ Per Einstellung aktivierbar |
| Benchmark-Vergleich | ✅ Vorhandenes Wertpapier als Benchmark wählbar (Einstellungen) |
| CSV-Export | ❌ Nicht implementiert |
| Kostenbasismethode | FIFO |
