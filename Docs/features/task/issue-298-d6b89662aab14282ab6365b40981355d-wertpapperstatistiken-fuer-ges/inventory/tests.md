# Bestandsaufnahme: Tests und Hilfsmethoden

Übersicht der bestehenden Testklassen und Test-Hilfsmethoden, die für die Portfolio-Analyse relevant sind oder als Muster dienen.

---

## Testklassen

### Für Return-Analyse (Single-Security – Muster für Portfolio)

#### `ReturnAnalysisServiceTests`
**Datei:** `FinanceManager.Tests/Securities/ReturnAnalysisServiceTests.cs`

Tests für `ReturnAnalysisService` (Single-Security Performance).

**Relevanz:** Zeigt Testmuster für komplexe Aggregations-Services mit Cache-Invalidierung und Multi-Tab-Szenarien. Portfolio-Tests sollten ähnliche Struktur haben.

#### `ReturnAnalysisCacheTests`
**Datei:** `FinanceManager.Tests/Securities/ReturnAnalysisCacheTests.cs`

Tests für Cache-Logik des Return-Analyse-Service.

**Relevanz:** Zeigt, wie Cache-Validierung und Invalidierung getestet werden. Kann als Vorlage für Portfolio-Cache-Tests dienen.

#### `ReturnCalculationServiceTests`
**Datei:** `FinanceManager.Tests/Securities/ReturnCalculationServiceTests.cs`

Tests für mathematische Rendite-Berechnungen.

**Relevanz:** Tests für reine Berechnungslogik (Rendite, Volatilität, etc.). Portfolio-Service braucht ähnliche Tests für aggregierte Kennzahlen.

#### `FifoCostBasisCalculatorTests`
**Datei:** `FinanceManager.Tests/Securities/FifoCostBasisCalculatorTests.cs`

Tests für FIFO-Kostenberechnung.

**Relevanz:** Kann für Portfolio-Gewinn/Verlust-Berechnung verwendet werden.

### Für Report-Caching

#### `ReportCacheServiceTests`
**Datei:** `FinanceManager.Tests/Infrastructure/Budget/ReportCacheServiceTests.cs`

Tests für generischen Report-Cache-Service.

**Relevanz:** Zeigt, wie Cache-Einträge angelegt, aktualisiert und invalidiert werden. Relevant für Portfolio-Cache-Service-Tests.

### Für Security-Verwaltung

#### `SecurityCardViewModelTests`
**Datei:** `FinanceManager.Tests/Web/ViewModels/Securities/SecurityCardViewModelTests.cs`

Tests für Security-Detail-View-Model.

**Relevanz:** Zeigt Testmuster für Web-ViewModels. Portfolio-PageViewModel braucht ähnliche Tests.

---

## Hilfsmethoden und Test-Utilities

### Test-Daten-Builder

**Relevante Hilfsklassen:**
- **TestDataBuilder** – Wird wahrscheinlich in mehreren Testklassen verwendet zur Erstellung von Test-Posting-/Security-/Price-Daten.
- **TestReturnAnalysisLocalizer** (`FinanceManager.Tests/TestHelpers/TestReturnAnalysisLocalizer.cs`) – Mock für Lokalisierung.

**Relevanz:** Bei Implementierung von Portfolio-Tests sollten ähnliche Builder für:
- Portfolio-Szenarien (mehrere Securities, Postings, Prices)
- KPI-Konfigurationen
- Cache-Einträge

erstellt werden.

### Annahmen über Test-Struktur

Basierend auf vorhandenen Tests können folgende Annahmen getroffen werden:
- **Datenbank-Tests** verwenden echte `AppDbContext` (oder TestDbContext).
- **Unit-Tests** mocken Abhängigkeiten.
- **Integrationstests** kombinieren DB und Service-Layer.

---

## Security-Klassen und Verwandte Tests

#### `IngSecurityPriceImportServiceTests`
**Datei:** `FinanceManager.Tests/Infrastructure/Securities/IngSecurityPriceImportServiceTests.cs`

Tests für Kursdaten-Import (z.B. von externen Datenquellen).

**Relevanz:** Zeigt, wie Kursdaten gepflegt und importiert werden. Wichtig für Portfolio-Analyse, die auf historischen Kursen basiert.

#### `SecurityPriceServiceUpsertTests`
**Datei:** `FinanceManager.Tests/Infrastructure/Securities/SecurityPriceServiceUpsertTests.cs`

Tests für Einfügen/Aktualisieren von Kursdaten.

**Relevanz:** Zeigt, wie SecurityPrice-Entitäten getestet werden. Portfolio-Tests müssen auch Price-Daten in unterschiedlichen Szenarien verarbeiten.

---

## Muster und Best Practices aus Tests

### 1. **Multi-Szenario-Tests**
- Bestehende Tests prüfen mehrere Fälle (z.B. leeres Portfolio, mit Positionen, mit Dividenden).

### 2. **Cache-Invalidierung testen**
- Tests validieren, dass Cache korrekt invalidiert wird nach bestimmten Ereignissen (Posting, Price-Update).

### 3. **User-Ownership validieren**
- Tests stellen sicher, dass Daten eines Benutzers nicht für anderen Benutzer zugänglich sind.

### 4. **Grenzfälle testen**
- Negative Gewinne, fehlende Daten, unvollständige Zeitreihen, etc.

---

## Handlungsempfehlungen für Portfolio-Tests

### Neue Testklassen (folgen bestehendem Muster):

1. **`PortfolioAnalysisReportServiceTests`**
   - Testet Aggregations-Logik für Depot-Struktur, Risikoanalyse, Performance, Cashflows.
   - Testet mit mehreren Securities, unterschiedliche Kategorien, Regionen, Sektoren.

2. **`PortfolioAnalysisReportCacheServiceTests`**
   - Testet monatliche Cache-Gültigkeit.
   - Testet Invalidierung nach Posting/Price-Updates.

3. **`PortfolioAnalysisReportPageViewModelTests`**
   - Testet Load, Edit-Mode, Save, Cache-Invalidierung im View-Model.

4. **`PortfolioKpiConfigurationTests`**
   - Testet Persistierung und Abruf von KPI-Konfigurationen.

### Existierende Test-Hilfsklassen nutzen:

- Test-Builder für Multi-Security-Szenarien.
- Mock-Localizer für Deutsch-Lokalisierung.
- AppDbContext-Setup aus bestehenden Tests übernehmen.
