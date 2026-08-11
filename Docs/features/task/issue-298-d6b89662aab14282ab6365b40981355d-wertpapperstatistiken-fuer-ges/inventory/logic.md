# Bestandsaufnahme: Geschäftslogik und Services

Übersicht der bestehenden Services und der Geschäftslogik, die für die Portfolio-Analyse relevant sind.

---

## `IReportCacheService` und `ReportCacheService`

**Dateien:**
- Interface: `FinanceManager.Application/Budget/IReportCacheService.cs`
- Implementierung: `FinanceManager.Infrastructure/Budget/ReportCacheService.cs`

Generischer Service für die Verwaltung von gecachten Report-Daten. Kann für neue Report-Typen (z.B. Portfolio-Analyse) erweitert werden.

### Interface-Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetBudgetReportRawDataAsync` | Public | Liest gecachte Budget-Report-Daten (Cache kann null sein oder `NeedsRefresh` gesetzt). |
| `SetBudgetReportRawDataAsync` | Public | Speichert/aktualisiert Budget-Report-Daten im Cache. |
| `MarkAllReportCacheEntriesForUpdateAsync` | Public | Markiert alle Cache-Einträge eines Benutzers zum Aktualisieren. |
| `ClearReportCacheAsync` | Public | Löscht alle Cache-Einträge eines Benutzers. |
| `GetNextBudgetReportCacheToUpdateAsync` | Public | Ruft den nächsten zu aktualisierenden Cache-Eintrag ab. |
| `MarkBudgetReportCacheEntriesForUpdateAsync` | Public | Markiert Cache-Einträge mit überschneidendem Datumsbereich. |
| `EnqueueBudgetReportCacheRefresh` | Public | Enqueued Background-Task zum Cache-Refresh. |

### Implementierungs-Details

- Verwendet `ReportCacheEntry` Entität zur Persistierung.
- Serialisiert/Deserialisiert Daten als JSON via `JsonSerializer`.
- Nutzt `IBackgroundTaskManager` für asynchrone Cache-Invalidierung.
- Cache-Schlüssel-Format: `"budgetreportraw-{from:yyyyMMdd}-{to:yyyyMMdd}-{dateBasis}"`
- Speichert auch einen `Parameter` (z.B. Datumsbereich als JSON) zur Validierung.

### Relevanz für Portfolio-Analyse

- **Kann als Basis für Portfolio-Cache-Service verwendet werden** – Ähnliche Struktur mit monatlichem Gültigkeitszeitraum.
- Zeigt Muster für Invalidierungs-Strategien (`NeedsRefresh`-Flag).
- **Limitation:** Kein `CacheValidUntilUtc`-Feld zur Verfolgung von Monatsgrenzen erforderlich.

---

## `IReturnAnalysisService` und `ReturnAnalysisService`

**Dateien:**
- Interface: `FinanceManager.Application/Securities/ReturnAnalysis/IReturnAnalysisService.cs`
- Implementierung: `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs`

Service für Performance-Analysen von **Einzelwertpapieren**. Relevantes Muster für Portfolio-Analyse-Service.

### Wichtige Interface-Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetReturnSummaryAsync` | Public | Kompakte Rendite-Zusammenfassung (für Widget). |
| `GetSparklineDataAsync` | Public | Sparkline-Daten für Mini-Chart. |
| `GetDetailedMetricsAsync` | Public | Detaillierte Rendite-Kennzahlen. |
| `GetPeriodicReturnsAsync` | Public | Jährliche und monatliche Renditen. |
| `GetCashflowTimelineAsync` | Public | Cashflow-Timeline. |
| `GetPerformanceChartDataAsync` | Public | Performance-Chart-Daten für unterschiedliche Zeiträume. |
| `GetBenchmarkComparisonAsync` | Public | Benchmark-Vergleich (MSCI World, etc.). |
| `GetUserSettingsAsync` | Public | Benutzer-spezifische Return-Analyse-Einstellungen. |
| `UpdateUserSettingsAsync` | Public | Aktualisiert Benchmark und Sharpe-Ratio-Optionen. |

### Implementierungs-Details

- **Cache-TTL:** 1 Stunde (für Single-Security; Portfolio könnte monatlich sein).
- **Caching:** Via `IReturnAnalysisCache` und `ReturnAnalysisCacheKeys`.
- **Ownership-Check:** Validiert via `OwnerUserId`.
- **Abhängigkeiten:** 
  - `IReturnCalculationService` – Reine finanzielle Berechnungen.
  - `IFifoCostBasisCalculator` – FIFO-Kostenberechnung.
  - `IReturnAnalysisLocalizer` – Lokalisierung von KPI-Labels.

### Relevanz für Portfolio-Analyse

- **Muster für Aggregations-Service:** Zeigt, wie man multiple Daten (Postings, Prices, Settings) aggregiert.
- **Cache-Strategie:** Könnte angepasst werden für Portfolio-Level (monatliche statt 1-Stunden-TTL).
- **Kann teilweise wiederverwendet werden:** Einige Berechnungen (z.B. Dividenden pro Jahr, Cashflows) sind auch Portfolio-relevant.
- **Unterschied:** ReturnAnalysisService ist **Single-Security-fokussiert**; Portfolio braucht **Aggregation über alle Holdings**.

---

## `IReturnCalculationService`

**Datei:** `FinanceManager.Infrastructure/Securities/ReturnAnalysis/ReturnAnalysisService.cs` (aufgerufen, aber separat definiert)

Reine mathematische Berechnungen für Renditen, Volatilität, Beta, etc.

### Relevanz für Portfolio-Analyse

- **Wird wahrscheinlich benötigt** für aggregierte Portfolio-Berechnung (z.B. Depot-Volatilität, Portfolio-Rendite).
- Portfolio-Service wird ähnliche Berechnungen durchführen, aber auf Depot-Level.

---

## `IFifoCostBasisCalculator`

**Datei:** (separat definiert)

Berechnet Kostenbasis basierend auf FIFO-Methode.

### Relevanz für Portfolio-Analyse

- **Relevant** für Berechnung unrealisierter Gewinne/Verluste.
- **Kann wiederverwendet werden** für Portfolio-Gewinne/Verluste pro Position.

---

## Abhängigkeiten zwischen Services

```
IReturnAnalysisService
  ├── IReturnCalculationService (für Rendite-Berechnungen)
  ├── IFifoCostBasisCalculator (für Kostenberechnung)
  ├── IReturnAnalysisCache (für Caching)
  └── AppDbContext (Datenzugriff)

IReportCacheService
  ├── AppDbContext (Datenzugriff)
  └── IBackgroundTaskManager (für async Invalidierung)
```

**Für Portfolio-Analyse wird benötigt:**
- Ähnliche Struktur wie `IReturnAnalysisService`, aber auf Portfolio-Level.
- Cache-Mechanismus ähnlich wie `IReportCacheService` mit monatlicher Gültigkeit.
- Neue aggregierende Logik für Multi-Security-Analysen.

---

## Wichtige Erkenntnisse

1. **Existierende Performance-Analyse ist Single-Security-fokussiert** – Portfolio-Service muss neu gebaut werden.
2. **Cache-Infrastruktur existiert** – Kann adapiert werden (mit Erweiterung um `CacheValidUntilUtc`).
3. **Einzelne Berechnungskomponenten können teilweise wiederverwendet werden** – `IReturnCalculationService`, `IFifoCostBasisCalculator`.
4. **User-Scoping ist implementiert** – Gleiche Muster muss für Portfolio-Service gelten (`OwnerUserId`).
5. **Dependency Injection Pattern existiert** – Neue Services folgen gleichen Konventionen.
