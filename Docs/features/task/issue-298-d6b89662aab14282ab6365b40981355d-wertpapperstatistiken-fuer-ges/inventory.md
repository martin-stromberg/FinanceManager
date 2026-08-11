# Bestandsaufnahme: Wertpapierstatistiken für Gesamtdepot

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezüglich der Anforderung "Wertpapierstatistiken für Gesamtdepot" (Portfolio-Analysebericht).

---

## Zusammenfassung

Die Anforderung sieht einen neuen **Portfolio-Analysebericht** vor, der aggregierte Statistiken über alle Wertpapiere zeigt (analog zur Single-Security Performance-Analyse). Der Bericht soll Statistiken in konfigurierbaren Kacheln mit Caching anbieten.

**Wesentliche Befunde:**

| Bereich | Status | Details |
|---------|--------|---------|
| **Datenmodelle** | TEILWEISE | Security, Posting, SecurityPrice, SecurityCategory existieren; **FEHLEND:** `Region` und `Sector` Felder in Security, `CacheValidUntilUtc` in ReportCacheEntry. |
| **Services** | VORHANDEN | ReturnAnalysisService (Single-Security) und ReportCacheService (generisch) existieren; **FEHLEND:** Portfolio-spezifische Services (PortfolioAnalysisReportService, PortfolioAnalysisReportCacheService). |
| **ViewModels** | VORHANDEN | SecurityPerformancePageViewModel existiert; **FEHLEND:** PortfolioAnalysisReportPageViewModel. |
| **UI-Komponenten** | VORHANDEN | SecurityPerformancePage.razor existiert; **FEHLEND:** PortfolioAnalysisReportPage.razor und spezialisierte Karten-Komponenten. |
| **Konfiguration** | FEHLEND | PortfolioKpiConfiguration Entität muss erstellt werden. |
| **Tests** | VORHANDEN | Tests für ReturnAnalysis und ReportCache existieren; **FEHLEND:** Tests für Portfolio-Services. |

---

## Detaillierte Analyse

### [Datenmodelle](inventory/models.md)

**Vorhanden:**
- `Security` – Zentrale Wertpapier-Klasse (mit CategoryId für Asset Allocation).
- `Posting` – Buchungen mit SecurityPostingSubType und Quantity.
- `SecurityPrice` – Historische Kursdaten.
- `SecurityCategory` – Kategorisierung von Wertpapieren.
- `ReportCacheEntry` – Allgemeine Cache-Verwaltung.

**Zu erweitern:**
- `Security` um Felder `Region` und `Sector` (für regionale/Sektoren-Verteilung).
- `ReportCacheEntry` um `CacheValidUntilUtc` (für monatliche Gültigkeitsdauer).

**Neu zu erstellen:**
- `PortfolioKpiConfiguration` – Speichert benutzerspezifische KPI-Auswahl und Reihenfolge.

---

### [Enums](inventory/enums.md)

**Vorhanden:**
- `PostingKind` – Art der Buchung (Security-Buchungen relevant).
- `SecurityPostingSubType` – Kategorisierung von Wertpapier-Buchungen (Buy, Sell, Dividend, Fee, Tax).

Diese Enums sind ausreichend für die Portfolio-Analyse und müssen nicht erweitert werden.

---

### [Geschäftslogik und Services](inventory/logic.md)

**Vorhanden:**
- `IReportCacheService` / `ReportCacheService` – Generischer Report-Cache-Service (für Budget, kann erweitert werden).
- `IReturnAnalysisService` / `ReturnAnalysisService` – Performance-Analyse für Einzelwertpapiere (relevantes Muster).
- `IReturnCalculationService` – Mathematische Rendite-Berechnungen.
- `IFifoCostBasisCalculator` – FIFO-Kostenberechnung.

**Neu zu erstellen:**
- `PortfolioAnalysisReportService` (Application/Domain) – Berechnung aggregierter Portfolio-Kennzahlen.
- `PortfolioAnalysisReportCacheService` (Infrastructure) – Cache-Verwaltung mit monatlicher Invalidierung.
- `PortfolioAnalysisReportController` (Web/Controllers) – REST-API-Endpunkte.
- `PortfolioAnalysisReportPageViewModel` (Web/ViewModels) – View-Model für Portfolio-Report-Seite.

**Wiederverwendbar:**
- Teile von `ReturnAnalysisService` für aggregierte Berechnung.
- `IReturnCalculationService` und `IFifoCostBasisCalculator` für Einzelpositionen.
- Cache-Muster aus `ReportCacheService`.

---

### [Tests und Hilfsmethoden](inventory/tests.md)

**Vorhanden:**
- `ReturnAnalysisServiceTests`, `ReturnAnalysisCacheTests` – Tests für Single-Security Performance.
- `ReportCacheServiceTests` – Tests für Cache-Service.
- `ReturnCalculationServiceTests`, `FifoCostBasisCalculatorTests` – Tests für mathematische Berechnungen.
- Diverse Test-Utilities und Builder.

**Neu zu erstellen:**
- `PortfolioAnalysisReportServiceTests` – Tests für Aggregationslogik.
- `PortfolioAnalysisReportCacheServiceTests` – Tests für monatliche Cache-Invalidierung.
- `PortfolioAnalysisReportPageViewModelTests` – Tests für View-Model.
- `PortfolioKpiConfigurationTests` – Tests für KPI-Konfiguration.

---

## UI-Komponenten

**Vorhanden:**
- `SecurityPerformancePage.razor` – Single-Security Performance-Seite (Vorlage).
- Multiple Tab-Komponenten (OverviewTab, TimeSeriesTab, CashflowTab, MetricsTab, BenchmarkTab).
- `SecurityPerformancePageViewModel` – View-Model mit Ribbon-Integration.

**Neu zu erstellen:**
- `PortfolioAnalysisReportPage.razor` – Hauptseite für Depot-Bericht.
- `PortfolioKpiCard.razor` – Generische KPI-Kachel.
- `PortfolioStructureCard.razor`, `PortfolioRiskCard.razor`, `PortfolioPerformanceCard.razor`, `PortfolioCashflowCard.razor` – Spezialisierte Kachel-Komponenten.

---

## Kritische Abhängigkeiten

1. **SecurityPrice Daten** – Müssen vorhanden sein für Performance-Berechnung.
2. **SecurityCategory Daten** – Müssen gepflegt sein für Asset Allocation.
3. **Region und Sector Felder** – Müssen in Security populiert sein für regionale/Sektoren-Verteilung.
4. **Posting mit SecurityPostingSubType** – Muss korrekt erfasst sein für Cashflow-Analyse.
5. **Benchmark-Konfiguration** – Muss im Benutzer-Setup vorhanden sein (existiert bereits).

---

## Implementierungsreihenfolge

### Phase 1 (MVP)
1. Erweiterung `Security` um `Region` und `Sector`.
2. Erweiterung `ReportCacheEntry` um `CacheValidUntilUtc`.
3. Neue `PortfolioKpiConfiguration` Entität + Migrationen.
4. `PortfolioAnalysisReportService` – Aggregations-Logik für:
   - Depotstruktur (komplett)
   - Performance (TWR/MWR basic)
   - Cashflow-Analyse (basic)
5. `PortfolioAnalysisReportCacheService` – Caching mit monatlicher Invalidierung.
6. `PortfolioAnalysisReportPageViewModel` – Load, Edit, Save, Ribbon.
7. `PortfolioAnalysisReportController` – REST-Endpunkte.
8. UI-Komponenten (Page + Karten).
9. Tests.

### Phase 2 (Optional, später)
- Risikoanalyse (Volatilität, Beta, VaR).
- MWR/IRR-Berechnung.
- Korrelationen.
- Steuerliche Auswirkungen.
- Entscheidungsqualität.

---

## Technische Bewertung

### Stärken der bestehenden Architektur
- User-Scoping ist durchgängig implementiert.
- Cache-Infrastruktur existiert und kann wiederverwendet werden.
- Single-Security Performance-Analyse zeigt Muster für komplexe Aggregationen.
- Enums und Datenmodelle sind vorhanden.

### Herausforderungen
- **Fehlende Portfolio-Aggregation:** Keine bestehende Infrastruktur für Multi-Security-Analysen.
- **Datensatz-Größe:** Bei >1000 Positionen könnten Queries optimiert werden müssen.
- **Neue Felder:** Region und Sector müssen in Security hinzugefügt und ggf. befüllt werden.
- **Monatliche Cache-Validität:** Nicht im bestehenden ReportCacheEntry implementiert.

---

## Empfehlungen

1. **Wiederverwendung:** Nutze `ReturnAnalysisService` und `ReportCacheService` als Vorlagen.
2. **Dependency Injection:** Folge bestehenden Patterns für Service-Registration.
3. **Testing:** Verwende bestehende Test-Utilities und Builder.
4. **Performance:** Prüfe Indexes und Query-Optimierungen bei großen Portfolios.
5. **Lokalisierung:** Nutze `IReturnAnalysisLocalizer`-Pattern für Portfolio-KPIs.
6. **Migration:** Berücksichtige Datenmigration für Region/Sector in Security.
