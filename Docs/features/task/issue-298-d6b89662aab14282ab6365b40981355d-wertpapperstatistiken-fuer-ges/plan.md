# Umsetzungsplan: Wertpapierstatistiken für Gesamtdepot (Portfolio-Analysebericht)

## Übersicht

Das System wird um einen **Portfolio-Analysebericht** erweitert, der aggregierte Statistiken über alle Wertpapiere eines Depots zeigt. Der Bericht wird analog zur bestehenden Performance-Analyse für Einzelwertpapiere (`ReturnAnalysisService`) implementiert, aber auf Portfolio-Ebene. Der neue Bericht ist über einen neuen Menüpunkt im Ribbon der Wertpapierübersicht erreichbar, zeigt Statistiken in konfigurierbaren Kacheln an und nutzt monatliches Caching mit Invalidierungsmechanismen.

## Designentscheidungen

Keine — folgt bestehenden Mustern.

Die Implementierung folgt durchgängig bestehenden Konventionen des Projekts:
- Aggregationslogik und Datenmodellierung orientieren sich an `ReturnAnalysisService` (Single-Security-Muster).
- Cache-Verwaltung folgt dem `ReportCacheService`-Muster mit Erweiterung um monatliche Gültigkeit.
- View-Model und Ribbon-Integration folgen `SecurityPerformancePageViewModel`-Struktur.
- UI-Komponenten nutzen bestehende Kachel-Patterns (z.B. Tabs, Card-Layouts).
- Dependency Injection und User-Scoping sind konsistent mit bestehender Architektur.

## Programmabläufe

### Bericht laden (View-Mode)

1. Benutzer navigiert zur neuen `PortfolioAnalysisReportPage.razor` (via Ribbon-Button oder direkt).
2. Page-ViewModel (`PortfolioAnalysisReportPageViewModel`) wird initialisiert und lädt den Bericht:
   - Cache-Schlüssel wird erzeugt: `{UserId}:{CurrentYear}:{CurrentMonth}`.
   - `PortfolioAnalysisReportCacheService.GetPortfolioReportAsync()` wird aufgerufen.
3. Cache-Service prüft:
   - Ist ein Cache-Eintrag vorhanden und noch gültig (Monatsgültigkeitsprüfung via `CacheValidUntilUtc`)?
   - Wenn ja: Rückgabe gecachter Daten; View rendert sie.
   - Wenn nein: `PortfolioAnalysisReportService` wird aufgerufen zur Berechnung.
4. `PortfolioAnalysisReportService` aggregiert:
   - Alle Postings und Securities des Benutzers (`OwnerUserId`-Filter).
   - Historische Kursdaten aus `SecurityPrice`.
   - Kategorien, Regionen, Sektoren.
5. Service berechnet Kennzahlen:
   - **Depotstruktur:** Marktwert, investiertes Kapital, unrealisierte Gewinne/Verluste, Asset Allocation, regionale Verteilung, Sektorverteilung, Top-10-Positionen.
   - **Performance (Phase 1):** Zeitgewichtete Rendite (TWR), Performance pro Jahr/Monat.
   - **Cashflow (Phase 1):** Netto-Ein-/Auszahlungen, Dividenden pro Jahr, realisierte Gewinne/Verluste, Liquiditätsquote.
6. Ergebnisse werden als `PortfolioAnalysisReportDto` strukturiert und gecacht (bis Monatsende).
7. KPI-Konfiguration des Benutzers (`PortfolioKpiConfiguration`) wird geladen:
   - Aktive Kacheln werden bestimmt.
   - Reihenfolge wird angewendet.
   - KPI-Sichtbarkeit pro Kachel wird beachtet.
8. Page rendert Kachel-Grid im View-Mode mit gecachten Daten und Konfiguration.

Beteiligte Klassen/Komponenten: `PortfolioAnalysisReportPageViewModel`, `PortfolioAnalysisReportCacheService`, `PortfolioAnalysisReportService`, `PortfolioKpiConfiguration`, `PortfolioAnalysisReportPage.razor`

### Konfiguration bearbeiten (Edit-Mode)

1. Benutzer klickt "Bearbeiten" im Ribbon der Report-Seite.
2. Page wechselt in Edit-Mode:
   - Kacheln werden als Drag-&-Drop-Liste angezeigt.
   - Toggle für Kachel-Sichtbarkeit und KPI-Sichtbarkeit pro Kachel werden aktiviert.
3. Benutzer ändert Konfiguration (Reihenfolge, Ein-/Ausblenden, KPI-Sichtbarkeit).
4. Benutzer klickt "Speichern":
   - `PortfolioAnalysisReportPageViewModel.SaveConfigurationAsync()` wird aufgerufen.
   - Neue Konfiguration wird in `PortfolioKpiConfiguration` persistiert via Controller.
   - `PortfolioAnalysisReportCacheService.InvalidateCacheAsync()` wird aufgerufen, um Cache zu löschen.
5. Page wechselt zurück in View-Mode.
6. Beim nächsten Abruf wird der Bericht mit neuer Konfiguration frisch berechnet.

Beteiligte Klassen/Komponenten: `PortfolioAnalysisReportPageViewModel`, `PortfolioAnalysisReportController`, `PortfolioKpiConfiguration`, `PortfolioAnalysisReportCacheService`, `PortfolioAnalysisReportPage.razor`

### Cache-Invalidierung (Events)

**Automatisch ausgelöst durch:**

1. **Nach Posting-Änderungen:**
   - Neue Posting mit `Kind == Security` wird erstellt/aktualisiert → Event `SecurityPostingCreatedEvent` oder ähnlich wird veröffentlicht.
   - `PortfolioAnalysisReportCacheService` abonniert Event und invalidiert Cache.

2. **Nach SecurityPrice-Änderungen:**
   - Neue oder aktualisierte `SecurityPrice` → Event veröffentlicht.
   - Cache wird invalidiert.

3. **Nach Monatswechsel:**
   - Beim nächsten Abruf des Reports wird `CacheValidUntilUtc` geprüft.
   - Wenn `CurrentDate > CacheValidUntilUtc`, wird Cache als ungültig betrachtet.

4. **Nach KPI-Konfiguration-Änderung (Edit-Mode):**
   - Nach `PortfolioKpiConfiguration.Update()` wird Cache explizit gelöscht via API-Endpunkt.

Beteiligte Klassen/Komponenten: `PortfolioAnalysisReportCacheService`, `IBackgroundTaskManager`, Domain Events

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PortfolioAnalysisReportService` | Service (Application) | Berechnet aggregierte KPIs für das gesamte Depot auf Basis von Postings, Securities, SecurityPrices, Kategorien, Regionen, Sektoren. |
| `PortfolioAnalysisReportCacheService` | Service (Infrastructure) | Verwaltet Cache-Einträge mit monatlicher Gültigkeit; invalidiert bei Posting/Price-Updates oder Konfigurationsänderungen. |
| `PortfolioKpiConfiguration` | Datenmodell-Klasse (Domain) | Persistiert benutzerspezifische KPI-Auswahl, Sortierung und Sichtbarkeit der Kacheln. |
| `PortfolioAnalysisReportDto` | DTO | Strukturiert Report-Daten für API und Caching; enthält alle berechneten KPIs gruppiert nach Kachel-Kategorien. |
| `PortfolioStructureDto` | DTO | Bündelt Depotstruktur-KPIs (Marktwert, Kapital, Gewinne, Asset Allocation, Top-10). |
| `PortfolioPerformanceDto` | DTO | Bündelt Performance-KPIs (TWR, jährliche/monatliche Performance). |
| `PortfolioCashflowDto` | DTO | Bündelt Cashflow-KPIs (Ein-/Auszahlungen, Dividenden, realisierte Gewinne, Liquidität). |
| `PortfolioRiskDto` | DTO | Bündelt Risikoanalyse-KPIs (Phase 2; Volatilität, Drawdown, Beta, VaR, Sharpe Ratio). |
| `PortfolioAnalysisReportPageViewModel` | ViewModel (Web) | Verwaltet Laden, Edit-Mode, KPI-Konfiguration, Cache-Invalidierung für Portfolio-Report-Seite. |
| `PortfolioAnalysisReportController` | Controller (Web) | REST-API-Endpunkte: `GET /api/portfolio/analysis-report`, `POST /api/portfolio/kpi-configuration`, `DELETE /api/portfolio/kpi-configuration/cache`. |
| `PortfolioAnalysisReportPage.razor` | Razor-Komponente (UI) | Hauptseite für Portfolio-Analysebericht mit Ribbon, View-Mode (Kachel-Grid), Edit-Mode (Drag-&-Drop, Toggles). |
| `PortfolioKpiCard.razor` | Razor-Komponente (UI) | Generische KPI-Kachel-Komponente für Anzeige von Titel, Wert(en), Trend, Status-Indikator. |
| `PortfolioStructureCard.razor` | Razor-Komponente (UI) | Spezialisierte Kachel für Depotstruktur-KPIs (Marktwert, Kapital, Gewinne, Asset Allocation, Top-10). |
| `PortfolioPerformanceCard.razor` | Razor-Komponente (UI) | Spezialisierte Kachel für Performance-KPIs (TWR, jährliche/monatliche Performance). |
| `PortfolioCashflowCard.razor` | Razor-Komponente (UI) | Spezialisierte Kachel für Cashflow-KPIs (Ein-/Auszahlungen, Dividenden, Liquidität). |
| `PortfolioRiskCard.razor` | Razor-Komponente (UI) | Spezialisierte Kachel für Risikoanalyse-KPIs (Phase 2). |

## Änderungen an bestehenden Klassen

### `Security` (Domain/Securities)

- **Neue Eigenschaften:**
  - `Region` (`string?`) — Optionale Region des Wertpapiers (z.B. "Europa", "Nordamerika") für regionale Verteilung.
  - `Sector` (`string?`) — Optionaler Sektor des Wertpapiers (z.B. "Technologie", "Pharma") für Sektorverteilung.

- **Geänderte Methoden:**
  - `Update(...)` — Signatur wird erweitert um Parameter `region` und `sector`, damit diese Felder editierbar sind.

### `ReportCacheEntry` (Domain/Reports)

- **Neue Eigenschaft:**
  - `CacheValidUntilUtc` (`DateTime?`) — Zeitpunkt, bis zu dem der Cache gültig ist (z.B. Ende des laufenden Monats). Ermöglicht monatliche Cache-Gültigkeitsdauer.

### `IReportCacheService` / `ReportCacheService` (Application/Infrastructure)

- **Neue Methoden (optional, wenn generisch gehalten werden soll):**
  - `GetPortfolioReportAsync(userId, cacheKey)` oder allgemein `GetReportCacheAsync(...)` — generische Methode für beliebige Report-Typen mit `CacheValidUntilUtc`-Prüfung.
  - `SetPortfolioReportAsync(userId, cacheKey, cacheValue, validUntilUtc)` — setzt Cache mit Gültigkeitsdatum.

- **Falls spezialisiert:** Neue spezialisierte Methoden für Portfolio-Cache in neuer `PortfolioAnalysisReportCacheService` Klasse (als Wrapper/Specialization von `ReportCacheService`).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddRegionAndSectorToSecurity` | `Securities` Tabelle: neue Spalten `Region` (nvarchar(255), nullable), `Sector` (nvarchar(255), nullable) | Fügt zwei optionale Felder zur Security-Entität für regionale und Sektor-Verteilung hinzu. |
| `AddCacheValidUntilUtcToReportCacheEntry` | `ReportCacheEntry` Tabelle: neue Spalte `CacheValidUntilUtc` (datetime2, nullable) | Ermöglicht Verwaltung von Monatsgültigkeitsdauer für Report-Caches. |
| `CreatePortfolioKpiConfigurationTable` | Neue Tabelle `PortfolioKpiConfigurations` | Speichert benutzerspezifische KPI-Konfiguration (UserId, aktive Kacheln, Sortierung, KPI-Sichtbarkeit). |

**Hinweis:** `CreatePortfolioKpiConfigurationTable` Tabellenschema (konkrete Spalten):
```
PortfolioKpiConfigurations
  - Id (GUID, PK)
  - OwnerUserId (GUID, FK to Users, unique)
  - ActiveTileIds (nvarchar(max), JSON-serialisierte Array von Tile-Enum-Werten)
  - TileOrder (nvarchar(max), JSON-serialisierte Array mit Positionen)
  - KpiVisibility (nvarchar(max), JSON-serialisierte verschachtelte Struktur: {TileId: [kpi1, kpi2, ...]})
  - UpdatedUtc (datetime2)
```

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Security.Region` | Optional, max. 255 Zeichen (wenn angegeben) | Längenvalidierung bei Update. |
| `Security.Sector` | Optional, max. 255 Zeichen (wenn angegeben) | Längenvalidierung bei Update. |
| `PortfolioKpiConfiguration.ActiveTileIds` | Mindestens eine Kachel muss aktiv sein; Werte müssen gültige Tile-IDs sein. | Fehler wenn keine Kachel aktiv oder ungültige ID. |
| `PortfolioKpiConfiguration.TileOrder` | Muss alle aktiven Kachel-IDs enthalten; keine Duplikate. | Fehler bei Inkonzistenz. |
| `PortfolioKpiConfiguration.KpiVisibility` | Gültige Struktur mit Kachel-IDs als Keys, Arrays gültiger KPI-IDs als Values. | Parsing-Fehler oder ungültige IDs. |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `PortfolioAnalysisCacheTtl` | Enum / Constant | `EndOfMonth` | Definiert monatliche Cache-Gültigkeitsdauer. Standardmäßig: bis Ende des laufenden Monats. |
| `PortfolioAnalysisDefaultTiles` | List<TileId> | `[Structure, Performance, Cashflow]` | Definiert Standard-Kachel-Set für neue Benutzer. |
| `PortfolioAnalysisDefaultKpiVisibility` | Dictionary<TileId, List<KpiId>> | Alle KPIs pro Kachel aktiviert | Definiert, welche KPIs pro Kachel standardmäßig sichtbar sind. |

Falls diese Konfigurationen nicht als globale Einstellungen benötigt werden, können sie fest als Konstanten in der Implementierung codiert sein.

## Seiteneffekte und Risiken

- **Ribbon der Wertpapierübersicht:** Neuer Button/Link muss zur Ribbon-Definition der Securities-Page hinzugefügt werden. **Risiko:** Ändert Layout/Funktionalität der bestehenden Seite.
  - **Mitigation:** Button wird konsistent mit anderen Report-Buttons des Systems (z.B. Budget-Report) hinzugefügt.

- **Event-System:** Cache-Invalidierung wird über Events nach `Posting`- und `SecurityPrice`-Änderungen gehandhabt. **Risiko:** Neue Event-Handler-Registrierung muss korrekt in Dependency-Injection erfolgen.
  - **Mitigation:** Folgt bestehenden Event-Patterns (z.B. `IBackgroundTaskManager`).

- **Datenbankperformanz:** Bei Portfolios mit >1000 Positionen könnten Aggregations-Queries länger dauern. **Risiko:** Hohe Last bei Cache-Miss.
  - **Mitigation:** Cache mit monatlicher Gültigkeit reduziert Hit-Rate; ggf. Query-Optimierungen (Indizes) erforderlich.

- **Neue Felder in `Security` (Region, Sector):** Historische Daten müssen nicht nachträglich befüllt werden (optional); aber fehlende Daten beeinflussen Genauigkeit der regionalen/Sektor-Verteilung. **Risiko:** Unvollständige Analysen bei leeren Feldern.
  - **Mitigation:** Dokumentation für Endnutzer; optionales Backfill-Skript bereitstellen.

- **Keine bekannten Seiteneffekte auf bestehende Tests** — Die neuen Funktionalitäten sind isoliert; bestehende Tests sollten unverändert laufen.

## Umsetzungsreihenfolge

1. **Datenbankmigrationen erstellen und testen**
   - Voraussetzungen: Keine
   - Beschreibung: Erstelle Migrationen für:
     - `Security` um `Region` und `Sector` Felder erweitern
     - `ReportCacheEntry` um `CacheValidUntilUtc` Feld erweitern
     - Neue Tabelle `PortfolioKpiConfiguration` anlegen
   - Migrationen müssen in Testumgebung laufen ohne Fehler.

2. **`PortfolioKpiConfiguration` Entität und Datenzugriff implementieren**
   - Voraussetzungen: Migrations ausgeführt, `DbContext` Setup
   - Beschreibung: Erstelle:
     - `PortfolioKpiConfiguration` Klasse mit Eigenschaften: `Id`, `OwnerUserId`, `ActiveTileIds`, `TileOrder`, `KpiVisibility`, `UpdatedUtc`
     - DbContext Konfiguration für `PortfolioKpiConfiguration`
     - Repository-Interface `IPortfolioKpiConfigurationRepository` mit CRUD-Methoden
     - Implementierung des Repositories
   - Unit-Tests für CRUD-Operationen

3. **DTO-Klassen erstellen**
   - Voraussetzungen: Keine speziellen; nur Struktur
   - Beschreibung: Erstelle DTOs für Datenfluss:
     - `PortfolioAnalysisReportDto` (übergeordnet)
     - `PortfolioStructureDto` (Marktwert, Kapital, Gewinne, Asset Allocation, Top-10)
     - `PortfolioPerformanceDto` (TWR, jährliche/monatliche Performance)
     - `PortfolioCashflowDto` (Ein-/Auszahlungen, Dividenden, realisierte Gewinne, Liquidität)
     - `PortfolioRiskDto` (Phase 2; für zukünftige Erweiterung)
   - Keine Tests nötig (reine Datenstrukturen)

4. **`PortfolioAnalysisReportService` implementieren**
   - Voraussetzungen: DTOs vorhanden, `IReturnCalculationService`, `IFifoCostBasisCalculator`, `AppDbContext`, User-Kontext
   - Beschreibung: Implementiere Service mit Methoden für:
     - Aggregation der Depotstruktur (Marktwert, investiertes Kapital, unrealisierte Gewinne/Verluste)
     - Asset Allocation (Gruppierung nach `SecurityCategory`)
     - Regionale Verteilung (Gruppierung nach `Security.Region`)
     - Sektorverteilung (Gruppierung nach `Security.Sector`)
     - Top-10-Positionen (nach Marktwert)
     - Zeitgewichtete Rendite (TWR) auf Portfolio-Ebene
     - Performance pro Jahr/Monat
     - Cashflow-Analyse (netto Ein-/Auszahlungen, Dividenden, realisierte Gewinne, Liquiditätsquote)
     - Hauptmethode: `GetPortfolioAnalysisReportAsync(userId)` → `PortfolioAnalysisReportDto`
   - Unit-Tests mit verschiedenen Portfolio-Szenarien (mehrere Securities, Kategorien, Regionen, Sektoren, Postings, Prices)

5. **`PortfolioAnalysisReportCacheService` implementieren**
   - Voraussetzungen: `PortfolioAnalysisReportService` vorhanden, `IReportCacheService`, `AppDbContext`, `IBackgroundTaskManager`
   - Beschreibung: Implementiere Service mit Methoden:
     - `GetPortfolioReportAsync(userId)` — Cache-Lookup mit monatlicher Gültigkeitsprüfung (`CacheValidUntilUtc`); bei Cache-Miss: Service aufrufen und Ergebnis cachen
     - `InvalidateCacheAsync(userId)` — Löscht Cache-Einträge für Benutzer (wird nach KPI-Konfiguration-Änderung aufgerufen)
     - Private Hilfsmethoden für Cache-Schlüssel-Generierung, Gültigkeitsdatum-Berechnung (bis Monatsende)
   - Unit-Tests für Cache-Gültigkeitsprüfung und Invalidierung

6. **Event-Handler registrieren für Cache-Invalidierung**
   - Voraussetzungen: `PortfolioAnalysisReportCacheService` vorhanden, Event-System Setup
   - Beschreibung: 
     - Event-Handler für `SecurityPostingCreatedEvent` / `SecurityPostingUpdatedEvent` (oder ähnlich)
     - Event-Handler für `SecurityPriceCreatedEvent` / `SecurityPriceUpdatedEvent` (oder ähnlich)
     - Beide Handler rufen `PortfolioAnalysisReportCacheService.InvalidateCacheAsync()` auf
     - Abhängig von: Identifikation der korrekten Domain Events im Projekt (via Grep nach `Event` oder `DomainEvent`)
   - Integration-Tests für Event-Handling

7. **`PortfolioAnalysisReportController` implementieren**
   - Voraussetzungen: `PortfolioAnalysisReportCacheService`, `IPortfolioKpiConfigurationRepository`, Authentifizierungs-Framework
   - Beschreibung: Erstelle Controller mit Endpoints:
     - `GET /api/portfolio/analysis-report` — Ruft `PortfolioAnalysisReportCacheService.GetPortfolioReportAsync(userId)` auf; gibt `PortfolioAnalysisReportDto` zurück
     - `POST /api/portfolio/kpi-configuration` — Speichert neue KPI-Konfiguration in `PortfolioKpiConfiguration`; invalidiert Cache
     - `DELETE /api/portfolio/kpi-configuration/cache` — Manueller Trigger für Cache-Invalidierung
     - Alle Endpoints prüfen `OwnerUserId`
   - Unit-Tests für Endpoints

8. **`PortfolioAnalysisReportPageViewModel` implementieren**
   - Voraussetzungen: `PortfolioAnalysisReportController`, `IPortfolioKpiConfigurationRepository`, Ribbon-Framework
   - Beschreibung: Implementiere ViewModel mit:
     - Property `PortfolioReportData` (`PortfolioAnalysisReportDto`)
     - Property `CurrentConfiguration` (`PortfolioKpiConfiguration`)
     - Property `IsEditMode` (`bool`)
     - Methode `LoadReportAsync()` — Ruft Controller auf
     - Methode `EnterEditModeAsync()` — Lädt aktuelle Konfiguration
     - Methode `SaveConfigurationAsync(newConfig)` — Speichert Konfiguration via Controller und invalidiert Cache
     - Methode `RefreshReportAsync()` — Erzwingt Neuberechnung (löscht Cache)
     - Ribbon-Befehle: Edit, Refresh, etc.
   - Unit-Tests für ViewModel-Logik

9. **Razor-Komponenten implementieren**
   - Voraussetzungen: ViewModel vorhanden, DTOs definiert, Komponenteninfrastruktur (Layout, Styles)
   - Beschreibung:
     - `PortfolioAnalysisReportPage.razor` — Hauptseite mit:
       - Ribbon mit Buttons (Edit, Refresh, etc.)
       - View-Mode: Kachel-Grid mit Daten aus ViewModel
       - Edit-Mode: Drag-&-Drop-Liste, Toggles für Kachel/KPI-Sichtbarkeit
     - `PortfolioKpiCard.razor` — Generische Kachel-Komponente
     - `PortfolioStructureCard.razor` — Spezialisierte Kachel für Depotstruktur
     - `PortfolioPerformanceCard.razor` — Spezialisierte Kachel für Performance
     - `PortfolioCashflowCard.razor` — Spezialisierte Kachel für Cashflows
     - `PortfolioRiskCard.razor` — Spezialisierte Kachel für Risikoanalyse (Phase 2, basic structure)
   - Keine Unit-Tests erforderlich (Razor-Komponenten getestet via E2E)

10. **Ribbon-Integration (SecurityPerformancePage oder Securities Ribbon)**
    - Voraussetzungen: `PortfolioAnalysisReportPage.razor` vorhanden, Ribbon-Definition für Securities-Page
    - Beschreibung: 
      - Finde Ribbon-Definition der Securities-Übersicht (wahrscheinlich in `SecurityPerformancePage.razor` oder `SecurityPageViewModel`)
      - Füge neuen Ribbon-Button "Depot-Bericht" / "Analysebericht" hinzu
      - Navigation zur neuen `PortfolioAnalysisReportPage`
    - Keine speziellen Tests erforderlich (wird durch E2E abgedeckt)

11. **Unit-Tests erweitern/schreiben**
    - Voraussetzungen: Alle Dienste und Klassen vorhanden
    - Beschreibung: Umfassende Unit-Tests für:
      - `PortfolioAnalysisReportService` — Verschiedene Portfolio-Szenarien, Edge Cases, Korrektheit der Aggregationen
      - `PortfolioAnalysisReportCacheService` — Monatliche Gültigkeitsprüfung, Cache-Invalidierung
      - `PortfolioAnalysisReportPageViewModel` — Load, Edit-Mode, Save, Refresh
      - `PortfolioKpiConfiguration` — CRUD-Operationen
      - `PortfolioAnalysisReportController` — API-Responses, Error-Handling
    - Tests sollten bestehende Test-Patterns folgen (Builder, Mocks, etc.)

12. **E2E-Tests implementieren (Pflicht)**
    - Voraussetzungen: Alle Komponenten funktionsfähig, E2E-Framework Setup
    - Beschreibung: E2E-Tests für:
      - Bericht laden (Happy Path)
      - Konfiguration ändern (Edit-Mode)
      - Cache-Invalidierung nach Posting-Update
      - Navigation via Ribbon-Button
      - Mehrbenutzer-Szenarios (Datenisolation)
    - Mindestens ein Test pro Hauptablauf

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure` | `PortfolioAnalysisReportServiceTests` | Depot mit 1 Security: Marktwert, Kapital, Gewinne berechnet korrekt. |
| `GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly` | `PortfolioAnalysisReportServiceTests` | Aggregation nach Category, Region, Sector funktioniert korrekt. |
| `GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly` | `PortfolioAnalysisReportServiceTests` | Dividenden-Postings werden korrekt aggregiert. |
| `GetPortfolioReport_NoPostings_ReturnsEmptyStructure` | `PortfolioAnalysisReportServiceTests` | Leeres Depot wird korrekt gehandhabt. |
| `GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData` | `PortfolioAnalysisReportServiceTests` | User-Ownership-Filter funktioniert. |
| `CacheHit_WithinMonth_ReturnsCachedData` | `PortfolioAnalysisReportCacheServiceTests` | Cache wird korrekt zurückgegeben wenn noch gültig. |
| `CacheMiss_EndOfMonth_RecalculatesReport` | `PortfolioAnalysisReportCacheServiceTests` | Nach Monatswechsel wird Cache neu berechnet. |
| `InvalidateCache_AfterPostingUpdate_DeletesCacheEntry` | `PortfolioAnalysisReportCacheServiceTests` | Cache-Invalidierung nach Event funktioniert. |
| `LoadReport_ViewModel_CallsServiceAndSetsData` | `PortfolioAnalysisReportPageViewModelTests` | ViewModel.LoadReportAsync() ruft Service korrekt auf. |
| `EditMode_SaveConfiguration_PersistsAndInvalidatesCache` | `PortfolioAnalysisReportPageViewModelTests` | Edit-Mode Speichern funktioniert, Cache wird invalidiert. |
| `Refresh_ViewModel_ClearsAndReloadsReport` | `PortfolioAnalysisReportPageViewModelTests` | Refresh-Befehl erzwingt Neuberechnung. |
| `Create_PortfolioKpiConfiguration_Persists` | `PortfolioKpiConfigurationTests` | Konfiguration wird in Datenbank gespeichert. |
| `Update_PortfolioKpiConfiguration_ReflectsChanges` | `PortfolioKpiConfigurationTests` | Änderungen an Konfiguration werden persistiert. |
| `GetAnalysisReport_Controller_Returns200AndData` | `PortfolioAnalysisReportControllerTests` | GET `/api/portfolio/analysis-report` gibt korrekte Daten zurück. |
| `PostKpiConfiguration_Controller_SavesAndReturns200` | `PortfolioAnalysisReportControllerTests` | POST `/api/portfolio/kpi-configuration` speichert und antwortet. |
| `DeleteCache_Controller_InvalidatesCache` | `PortfolioAnalysisReportControllerTests` | DELETE `/api/portfolio/kpi-configuration/cache` löscht Cache. |
| `BuildPortfolioReport_TestDataBuilder` | Test-Utilities | Helper-Klasse zum Aufbau von Test-Portfolios (mehrere Securities, Postings, Prices). |
| `CreatePortfolioKpiConfiguration_TestDataBuilder` | Test-Utilities | Helper-Klasse zum Erstellen Test-Konfigurationen. |

### Betroffene bestehende Tests

Keine. Die neuen Funktionalitäten sind isoliert; bestehende Tests (z.B. `ReturnAnalysisServiceTests`, `SecurityCardViewModelTests`) sind nicht von den Änderungen betroffen.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy Path: Bericht laden | `PortfolioAnalysisReportE2ETests.LoadReportScenario` | Benutzer navigiert zur Portfolio-Seite, Bericht wird geladen und Kacheln angezeigt. |
| Edit-Mode: Konfiguration ändern | `PortfolioAnalysisReportE2ETests.EditConfigurationScenario` | Benutzer öffnet Edit-Mode, ordnet Kacheln per Drag-&-Drop, speichert, View wird aktualisiert. |
| Cache-Invalidierung: Nach Posting | `PortfolioAnalysisReportE2ETests.CacheInvalidationAfterPostingScenario` | Nach Neue Posting wird Report automatisch aktualisiert. |
| Navigation: Ribbon-Button | `PortfolioAnalysisReportE2ETests.RibbonNavigationScenario` | Benutzer klickt "Depot-Bericht" im Ribbon, navigiert zur neuen Seite. |
| Multi-User: Datenisolation | `PortfolioAnalysisReportE2ETests.MultiUserIsolationScenario` | Benutzer A sieht nur sein Depot, Benutzer B sieht sein Depot; keine Datenvermischung. |
| Performance: Großes Portfolio (>1000 Positionen) | `PortfolioAnalysisReportE2ETests.LargePortfolioPerformanceScenario` | Report lädt auch für großes Portfolio in angemessener Zeit. |

Welche bestehenden E2E-Tests müssen angepasst werden?

**Keine.** Die neue Report-Seite ist isoliert von bestehenden E2E-Szenarien.

## Offene Punkte

Keine.

Alle Anforderungen sind durch Anforderungs-Dokument und Bestandsaufnahme hinreichend geklärt. Designentscheidungen folgen bestehenden Konventionen. Phasierung (Phase 1 vs. Phase 2) ist dokumentiert. Technische Machbarkeit wurde in Bestandsaufnahme bestätigt.

---

## Anhang: Tile-Kategorien und KPIs (Phase 1)

**Depotstruktur (Tile: `Structure`):**
- Gesamtmarktwert
- Investiertes Kapital
- Unrealisierte Gewinne/Verluste
- Asset Allocation (Pie Chart nach Category)
- Regionale Verteilung (Bar Chart nach Region)
- Sektorverteilung (Bar Chart nach Sector)
- Top 10 Positionen (Table nach Marktwert)

**Performance (Tile: `Performance`):**
- Zeitgewichtete Rendite (TWR)
- Performance Jahr-zu-Datum (YTD)
- Performance letzte 1/3/5 Jahre (wenn Daten vorhanden)
- Performance pro Monat (Chart)
- Performance pro Jahr (Chart)

**Cashflow (Tile: `Cashflow`):**
- Netto-Einzahlungen (dieses Jahr)
- Dividenden (dieses Jahr)
- Realisierte Gewinne/Verluste (dieses Jahr)
- Liquiditätsquote (Cash / Gesamtwert)
- Cashflow-Timeline (Chart)

**Risikoanalyse (Tile: `Risk`, Phase 2):**
- Depot-Volatilität
- Maximaler Drawdown
- Sharpe Ratio
- Beta gegen Benchmark
- Value at Risk (VaR)
