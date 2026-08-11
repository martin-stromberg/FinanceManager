# Übersetzung der Kundenanforderung: Wertpapperstatistiken für Gesamtdepot

**Aufgaben-ID:** d6b89662-aab1-4282-ab63-65b40981355d  
**Branch:** task/issue-298-d6b89662aab14282ab6365b40981355d-wertpapperstatistiken-fuer-ges  
**Übersetzungsdatum:** 2026-08-11

---

## Fachliche Zusammenfassung

Das System wird um einen Depotanalysebericht erweitert, der eine konsolidierte Übersicht über alle Wertpapiere und deren Kennzahlen bietet. Analog zur bestehenden Performance-Analyse für einzelne Wertpapiere (Security-Level) wird ein neuer Bericht auf Portfolio-Level implementiert. Der Bericht wird über einen neuen Menüpunkt im Ribbon der Wertpapierübersicht erreichbar und zeigt Statistiken in konfigurierbaren Kacheln mit Caching und Invalidierungsmechanismen.

---

## Betroffene Klassen und Komponenten

### Neue Klassen und Services

- **`PortfolioAnalysisReportService`** (Domain/Application)
  - Berechnet aggregierte Kennzahlen für das gesamte Depot auf Basis der `Posting`, `Security`, `SecurityPrice` und `SecurityCategory` Daten
  - Bietet Aggregations-Methoden für Depotstruktur, Risikoanalyse, Performance und Cashflows

- **`PortfolioAnalysisReportCacheService`** (Infrastructure)
  - Erweitert oder spezialisiert den bestehenden `ReportCacheService`
  - Verwaltet Cache-Einträge mit monatlicher Gültigkeit
  - Invalidiert bei Wertpapierbuchungen, Kursaktualisierungen oder Konfigurationsänderungen

- **`PortfolioKpiConfiguration`** (Domain)
  - Persistiert die benutzerspezifische KPI-Auswahl und Reihenfolge der Kacheln
  - Speichert pro Benutzer: welche KPIs angezeigt werden, Sortierung, Ein-/Ausblendungen einzelner Werte

- **`PortfolioAnalysisReportPageViewModel`** (Web/ViewModels)
  - Hauptseite für den Depotanalysebericht (analog `SecurityPerformancePageViewModel`)
  - Verwaltet Ladefunktionalität, Edit-Mode, KPI-Konfiguration und Cache-Invalidierung
  - Bindung an die Ribbon-UI

- **`PortfolioAnalysisReportController`** (Web/Controllers)
  - REST-API-Endpunkte für:
    - `GET /api/portfolio/analysis-report` — Abrufen des Berichts im Cache-Mode
    - `POST /api/portfolio/kpi-configuration` — Speichern der KPI-Konfiguration
    - `DELETE /api/portfolio/kpi-configuration/cache` — Manuelle Cache-Invalidierung

### Erweiterte Klassen

- **`Security`**
  - Neue Eigenschaften: `Region` (für regionale Verteilung) und `Sector` (für Sektorverteilung)
  - Beide Eigenschaften optional und editierbar

- **`ReportCacheEntry`** (falls noch nicht vorhanden)
  - Erwiterung um Feld `CacheValidUntilUtc` oder ähnlich, um monatliche Gültigkeit zu kodieren

### UI-Komponenten / Razor-Seiten

- **`PortfolioAnalysisReportPage.razor`** (Web/Components/Pages/Securities)
  - Neue Hauptseite für den Depotbericht
  - Ribbon mit Aktionen (Edit, Refresh, etc.)
  - View-Mode: Kachel-Grid mit gecachten Daten und KPI-Anzeige
  - Edit-Mode: Drag-&-Drop-Konfiguration von Kacheln, Toggle für KPI-Sichtbarkeit

- **`PortfolioKpiCard.razor`** (Web/Components)
  - Generische Komponente für eine einzelne KPI-Kachel
  - Unterstützt Titel, Wert(e), Trend, Status-Indikator

- **`PortfolioStructureCard.razor`**, **`PortfolioRiskCard.razor`**, **`PortfolioPerformanceCard.razor`**, **`PortfolioCashflowCard.razor`** (Web/Components)
  - Spezialisierte Kachel-Komponenten für die vier Hauptkategorien
  - Jede Komponente zeigt eine Gruppe zusammengehöriger KPIs

### Tests

- **`PortfolioAnalysisReportServiceTests`** (Tests)
  - Unit-Tests für Aggregations-Logik
  - Test-Daten: mehrere Wertpapiere in verschiedenen Kategorien, Regionen, Sektoren

- **`PortfolioAnalysisReportCacheServiceTests`** (Tests)
  - Cache-Gültigkeits-Tests
  - Cache-Invalidierungs-Szenarien

- **`PortfolioAnalysisReportViewModelTests`** (Tests)
  - View-Model-Logik (Load, Edit, Save)

- **`PortfolioKpiConfigurationTests`** (Tests)
  - Persistierung und Abruf der Konfiguration

---

## Implementierungsansatz

### 1. Datenmodell erweitern

- `Security` um `Region` und `Sector` Felder erweitern
- `PortfolioKpiConfiguration` Entität erstellen mit Benutzer, Kachel-IDs, Sortierung, KPI-Sichtbarkeit
- Migrationen für neue Tabellen und Spalten

### 2. Aggregationslogik (PortfolioAnalysisReportService)

Der Service berechnet auf Basis vorhandener `Posting`, `Security`, `SecurityPrice` und `SecurityCategory` Daten:

**Depotstruktur (implementierbar):**
- Gesamtmarktwert (Summe: `Quantity * CurrentPrice` pro Position)
- Investiertes Kapital (Summe aller Einzahlungen abzgl. Auszahlungen)
- Unrealisierte Gewinne/Verluste (Marktwert − investiertes Kapital pro Position)
- Asset Allocation (Gruppierung nach `SecurityCategory`)
- Regionale Verteilung (Gruppierung nach `Region` in `Security`)
- Sektorverteilung (Gruppierung nach `Sector` in `Security`)
- Top 10 Positionen (nach Marktwert)

**Risikoanalyse (teilweise oder später):**
- Depot-Volatilität: Berechnung auf Basis historischer `SecurityPrice` Zeitreihe
- Maximaler Drawdown: Peak-to-Trough aus Zeitreihendaten
- Beta gegen Benchmark: Regressionsanalyse (Depot-Returns gegen Benchmark-Returns)
- Value at Risk (VaR): Unter Annahme Normalverteilung oder historische Simulation
- Korrelationen: Zwischen Positionen (aufwändig, optional auf Phase 2 verschieben)
- Sharpe Ratio, Sortino Ratio: Auf Basis Portfolio-Returns und Volatilität

**Performanceanalyse (teilweise oder später):**
- Zeitgewichtete Rendite (TWR): Berechnung auf Basis `SecurityPrice`-Änderungen ohne Cashflow-Verzerrung
- Geldgewichtete Rendite (MWR/IRR): Interne Rendite unter Berücksichtigung von Ein-/Auszahlungen
- Performance pro Jahr/Monat: Zeitreihen der monatlichen/jährlichen Returns
- Benchmark-Vergleich (MSCI World, ACWI, Stoxx Europe 600): Abruf aus existierenden Benchmark-Daten (bereits im Setup konfigurierbar)
- Tracking Error: Standardabweichung der Differenz (Portfolio-Return − Benchmark-Return)

**Cashflow-Analyse (teilweise oder später):**
- Netto-Einzahlungen pro Jahr (Summe von `Posting.Amount` für Kontenbuchungen)
- Dividenden pro Jahr (Summe von `Posting.Amount` für `SecurityPostingSubType.Dividend`)
- Dividendenrendite (Summe Dividenden / Marktwert)
- Realisierte Gewinne/Verluste (Verkaufs-`Posting` mit Differenz Verkaufspreis − Kaufpreis)
- Steuerliche Auswirkungen: Abgeltungssteuer, Quellensteuer (diese Daten müssen ggf. neu erfasst werden oder aus Integrationsdaten herrühren)
- Liquiditätsquote (Cash / Gesamtmarktwert)

**Effizienz & Qualität (teilweise oder später):**
- Hit Ratio (Anteil Positionen mit positivem Ergebnis)
- Average Winner vs. Average Loser (durchschn. Gewinn vs. Verlust pro Position)
- Holding Period (durchschn. Haltedauer je Position)
- Turnover Rate (Verkaufsvolumen / Durchschnitts-Marktwert)
- Kostenquote (TER aus SecurityCategory oder Security; Handelsgebühren aus Posting-Audit)
- Entscheidungsqualität (manuell bewertbar, z. B. als Attribut in Security oder Posting)

### 3. Caching (PortfolioAnalysisReportCacheService)

- Cache-Schlüssel: `{UserId}:{CurrentYear}:{CurrentMonth}`
- Cache-Gültigkeitsdauer: Bis Ende des aktuellen Monats
- Invalidierungs-Trigger:
  - Nach `Posting.Create()` oder `Posting.Update()` wenn `Kind == Security`
  - Nach `SecurityPrice.Create()` oder `SecurityPrice.Update()`
  - Nach `PortfolioKpiConfiguration.Update()` im Edit-Mode
  - Nach Monatswechsel automatisch (via Datum-Check beim Abruf)
- Caching erfolgt auf Ebene der kompletten Report-DTO

### 4. KPI-Konfiguration (Edit-Mode)

Ein Edit-Mode ermöglicht:
- **Kachel-Sichtbarkeit:** Jede Kachel (Depotstruktur, Risikoanalyse, Performance, Cashflow, Effizienz) kann ein-/ausgeblendet werden
- **Kachel-Reihenfolge:** Drag-&-Drop zum Umsortieren
- **KPI-Sichtbarkeit pro Kachel:** Falls eine Kachel z. B. 5 Werte enthält, kann jeder einzeln togglebar sein
- **Speichern:** Persistiert in `PortfolioKpiConfiguration`
- **Cache-Invalidierung:** Nach dem Speichern wird der Cache gelöscht, damit der nächste Abruf im View-Mode frisch berechnet wird

### 5. Ribbon-Integration

Im Ribbon der Wertpapierübersicht:
- Neuer Button/Link "Depot-Bericht" (oder "Analysebericht")
- Navigation zur neuen `PortfolioAnalysisReportPage.razor`
- Ribbon-Aktionen der Report-Seite selbst:
  - "Aktualisieren" (Refresh)
  - "Bearbeiten" (Edit-Mode aktivieren)
  - Ggf. "Exportieren" (PDF/Excel, optional für Phase 2)

---

## Konfiguration

### Application-Level

- Cache-Verhalten über `ReportCacheService` (bereits vorhanden)
- Monatliche Gültigkeit als Konstante oder Configuration-Parameter

### Benutzer-Level

- **`PortfolioKpiConfiguration`:** Pro Benutzer eine Zeile, speichert:
  - Aktive Kacheln (Array von Enum-Werten oder String-IDs)
  - Sortierung (z. B. Array mit Positionen)
  - KPI-Sichtbarkeit (ggf. als verschachtelte Struktur oder JSON)

### Optional: Admin-Setup

- Benchmark-Auswahl (MSCI World, ACWI, Stoxx Europe 600) ist bereits im Benutzer-Setup konfigurierbar
- Standard-KPI-Set für neue Benutzer (kann vordefiniert sein)

---

## Offene Fragen und Einschränkungen

### Fragen vor Implementierung

1. **Steuerliche Daten:** Abgeltungssteuer und Quellensteuer werden derzeit nicht im System erfasst. Soll diese Funktionalität neu hinzugefügt werden, oder werden diese Zahlen manuell eingegeben / aus externen Quellen importiert?

2. **Historische Volatilität und Beta:** Benötigen wir tägliche oder nur monatliche Rendite-Zeitreihen zur Berechnung? Wie weit zurück soll die Volatilität gemessen werden (z. B. 1 Jahr, 3 Jahre, 5 Jahre)?

3. **Korrelationen:** Die Berechnung von Korrelationen zwischen allen Positionen kann rechenintensiv sein. Soll dies im Abruf berechnet oder zwischengespeichert werden?

4. **Entscheidungsqualität:** Wie soll bewertet werden, ob eine Nachkauf- oder Verkaufsentscheidung sinnvoll war? Benötigen wir dafür neue Attribute in `Security` oder `Posting`?

5. **Phasierung:** Welche KPIs sind Priorität für Phase 1 (MVP)? Empfehlung: Depotstruktur (vollständig), Performanceanalyse (TWR/MWR basic), Cashflow-Analyse (basic). Risikoanalyse und Effizienzmetriken könnten auf Phase 2 verschoben werden.

### Implementierungseinschränkungen

**Realisierbar in Phase 1:**
- Depotstruktur (alle Punkte)
- Basis Performance (TWR auf Basis historischer Kurse, Performance pro Jahr/Monat)
- Cashflow-Analyse (Ein-/Auszahlungen, Dividenden, realisierte Gewinne/Verluste)
- KPI-Konfiguration (Kachel-Auswahl und Reihenfolge)
- Caching mit monatlicher Invalidierung

**Optional auf Phase 2 verschieben:**
- Depot-Volatilität (benötigt Zeitreihendaten; möglich, aber rechenintensiv)
- Beta und VaR (benötigen fortgeschrittene Statistik)
- Korrelationen (sehr rechenintensiv für große Portfolios)
- Sortino Ratio (optional neben Sharpe)
- MWR/IRR (komplexere Berechnung)
- Tracking Error (nur relevant wenn Benchmark aktiv)
- Steuerliche Auswirkungen (müssen dafür neu erfasst werden)
- Entscheidungsqualität (benötigt neue Attribute und manuelle Bewertung)

### Abhängigkeiten und Voraussetzungen

- **`SecurityCategory`** muss für jedes Wertpapier gepflegt sein (für Asset Allocation)
- **`Region` und `Sector`** Felder in `Security` müssen für regionale und Sektorverteilung gepflegt sein
- **`SecurityPrice`** Daten müssen vorhanden sein (historische Kurse für Performance-Berechnung)
- **Benchmark-Daten:** MSCI World, ACWI, Stoxx Europe 600 müssen über bestehende Setup-Einrichtung abrufbar sein
- **`Posting` mit `SecurityPostingSubType`:** Muss korrekt erfasst sein für Cashflow-Analyse

---

## Designreferenz

Die Datei `stitch_advanced_portfolio_analysis_report.zip` enthält Mockups für folgende Kachel-Kategorien:
- **Depotstruktur** (`depotstruktur_bericht`): Marktwert, Kapital, Gewinne/Verluste, Asset Allocation, Top 10
- **Risikoanalyse** (`risikoanalyse_report`): Volatilität, Drawdown, Beta, VaR, Sharpe Ratio
- **Performance & Benchmark** (`performance_benchmark`): TWR, MWR, jährliche/monatliche Performance, Benchmark-Vergleich
- **Cashflows & Effizienz** (`cashflow_effizienz`): Ein-/Auszahlungen, Dividenden, Kostenquote, Hit Ratio

Die Desktop-Ansicht folgt dem gleichen Kachel-Layout wie die mobile Voransicht in den Mockups.

---

## Technische Bemerkungen

- **Namespace-Konvention:** Services in `FinanceManager.Application.Portfolio` und `FinanceManager.Infrastructure.Portfolio`
- **DTO-Struktur:** Analog `SecurityPerformanceDto` aus dem bestehenden Single-Security-Report
- **Event-Publishing:** Cache-Invalidierungs-Events müssen nach `Posting` und `SecurityPrice` Änderungen veröffentlicht werden
- **Benutzer-Kontext:** Alle Queries müssen `OwnerUserId` filtern
- **Pagination / Performance:** Bei großen Portfolios (>1000 Positionen) sollten Queries optimiert werden (Indizes, Batch-Loading)
