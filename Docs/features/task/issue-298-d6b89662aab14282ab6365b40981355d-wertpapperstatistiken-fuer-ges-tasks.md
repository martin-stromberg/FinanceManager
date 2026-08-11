# Tasks: Wertpapierstatistiken für Gesamtdepot (Portfolio-Analysebericht)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Migration: `Security` um `Region` (nvarchar(255), nullable) und `Sector` (nvarchar(255), nullable) erweitern | Offen | — |
| 2 | Datenmodell | Migration: `ReportCacheEntry` um `CacheValidUntilUtc` (datetime2, nullable) erweitern | Offen | — |
| 3 | Datenmodell | Migration: Neue Tabelle `PortfolioKpiConfigurations` mit Spalten (Id, OwnerUserId, ActiveTileIds, TileOrder, KpiVisibility, UpdatedUtc) anlegen | Offen | — |
| 4 | Datenmodell | `Security.Update()` Methode erweitern um `region` und `sector` Parameter | Offen | — |
| 5 | Datenmodell | `PortfolioKpiConfiguration` Entität (Domain) mit Properties anlegen | Offen | — |
| 6 | Datenmodell | `DbContext` Konfiguration für `PortfolioKpiConfiguration` hinzufügen | Offen | — |
| 7 | DTOs | `PortfolioAnalysisReportDto` DTO erstellen (übergeordnet) | Offen | — |
| 8 | DTOs | `PortfolioStructureDto` DTO für Depotstruktur-KPIs erstellen | Offen | — |
| 9 | DTOs | `PortfolioPerformanceDto` DTO für Performance-KPIs erstellen | Offen | — |
| 10 | DTOs | `PortfolioCashflowDto` DTO für Cashflow-KPIs erstellen | Offen | — |
| 11 | DTOs | `PortfolioRiskDto` DTO für Risikoanalyse-KPIs erstellen | Offen | — |
| 12 | Repository | `IPortfolioKpiConfigurationRepository` Interface erstellen (CRUD-Operationen) | Offen | — |
| 13 | Repository | `PortfolioKpiConfigurationRepository` Implementierung mit CRUD-Methoden | Offen | — |
| 14 | Services | `PortfolioAnalysisReportService` Klasse anlegen | Offen | — |
| 15 | Services | `PortfolioAnalysisReportService.GetPortfolioAnalysisReportAsync(userId)` Methode implementieren | Offen | — |
| 16 | Services | `PortfolioAnalysisReportService` Depotstruktur-Aggregation (Marktwert, Kapital, Gewinne) implementieren | Offen | — |
| 17 | Services | `PortfolioAnalysisReportService` Asset Allocation (Gruppierung nach SecurityCategory) implementieren | Offen | — |
| 18 | Services | `PortfolioAnalysisReportService` Regionale Verteilung (Gruppierung nach Security.Region) implementieren | Offen | — |
| 19 | Services | `PortfolioAnalysisReportService` Sektorverteilung (Gruppierung nach Security.Sector) implementieren | Offen | — |
| 20 | Services | `PortfolioAnalysisReportService` Top-10-Positionen (nach Marktwert) implementieren | Offen | — |
| 21 | Services | `PortfolioAnalysisReportService` Zeitgewichtete Rendite (TWR) auf Portfolio-Ebene implementieren | Offen | — |
| 22 | Services | `PortfolioAnalysisReportService` Performance pro Jahr/Monat implementieren | Offen | — |
| 23 | Services | `PortfolioAnalysisReportService` Cashflow-Analyse (Ein-/Auszahlungen, Dividenden, realisierte Gewinne, Liquidität) implementieren | Offen | — |
| 24 | Services | `PortfolioAnalysisReportCacheService` Klasse anlegen | Offen | — |
| 25 | Services | `PortfolioAnalysisReportCacheService.GetPortfolioReportAsync(userId)` mit Cache-Lookup und monatlicher Gültigkeitsprüfung implementieren | Offen | — |
| 26 | Services | `PortfolioAnalysisReportCacheService.InvalidateCacheAsync(userId)` implementieren | Offen | — |
| 27 | Services | `PortfolioAnalysisReportCacheService` Cache-Schlüssel-Generierung (UserId:Year:Month) implementieren | Offen | — |
| 28 | Services | `PortfolioAnalysisReportCacheService` Gültigkeitsdatum-Berechnung (bis Monatsende) implementieren | Offen | — |
| 29 | Events | Event-Handler für Posting-Änderungen (Create/Update) registrieren | Offen | — |
| 30 | Events | Event-Handler für SecurityPrice-Änderungen (Create/Update) registrieren | Offen | — |
| 31 | Events | Event-Handler rufen `PortfolioAnalysisReportCacheService.InvalidateCacheAsync()` auf | Offen | — |
| 32 | API | `PortfolioAnalysisReportController` anlegen | Offen | — |
| 33 | API | `PortfolioAnalysisReportController` `GET /api/portfolio/analysis-report` Endpoint implementieren | Offen | — |
| 34 | API | `PortfolioAnalysisReportController` `POST /api/portfolio/kpi-configuration` Endpoint implementieren | Offen | — |
| 35 | API | `PortfolioAnalysisReportController` `DELETE /api/portfolio/kpi-configuration/cache` Endpoint implementieren | Offen | — |
| 36 | API | Authentifizierung und OwnerUserId-Validierung in Controller-Endpoints einbauen | Offen | — |
| 37 | ViewModel | `PortfolioAnalysisReportPageViewModel` Klasse anlegen | Offen | — |
| 38 | ViewModel | `PortfolioAnalysisReportPageViewModel.LoadReportAsync()` Methode implementieren | Offen | — |
| 39 | ViewModel | `PortfolioAnalysisReportPageViewModel.EnterEditModeAsync()` Methode implementieren | Offen | — |
| 40 | ViewModel | `PortfolioAnalysisReportPageViewModel.SaveConfigurationAsync(newConfig)` Methode implementieren | Offen | — |
| 41 | ViewModel | `PortfolioAnalysisReportPageViewModel.RefreshReportAsync()` Methode implementieren | Offen | — |
| 42 | ViewModel | `PortfolioAnalysisReportPageViewModel` Ribbon-Befehle (Edit, Refresh) integrieren | Offen | — |
| 43 | UI | `PortfolioAnalysisReportPage.razor` Komponente anlegen | Offen | — |
| 44 | UI | `PortfolioAnalysisReportPage.razor` Ribbon mit Buttons (Edit, Refresh, etc.) implementieren | Offen | — |
| 45 | UI | `PortfolioAnalysisReportPage.razor` View-Mode (Kachel-Grid) implementieren | Offen | — |
| 46 | UI | `PortfolioAnalysisReportPage.razor` Edit-Mode (Drag-&-Drop, Toggles) implementieren | Offen | — |
| 47 | UI | `PortfolioKpiCard.razor` generische Kachel-Komponente anlegen | Offen | — |
| 48 | UI | `PortfolioStructureCard.razor` spezialisierte Kachel für Depotstruktur anlegen | Offen | — |
| 49 | UI | `PortfolioPerformanceCard.razor` spezialisierte Kachel für Performance anlegen | Offen | — |
| 50 | UI | `PortfolioCashflowCard.razor` spezialisierte Kachel für Cashflows anlegen | Offen | — |
| 51 | UI | `PortfolioRiskCard.razor` spezialisierte Kachel für Risikoanalyse (Phase 2, basic) anlegen | Offen | — |
| 52 | Ribbon | Ribbon-Button "Depot-Bericht" in Securities-Übersicht hinzufügen | Offen | — |
| 53 | Ribbon | Navigation vom Ribbon-Button zur `PortfolioAnalysisReportPage` implementieren | Offen | — |
| 54 | Tests | `PortfolioAnalysisReportServiceTests` Testklasse anlegen | Offen | — |
| 55 | Tests | Test: `GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure` | Offen | — |
| 56 | Tests | Test: `GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly` | Offen | — |
| 57 | Tests | Test: `GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly` | Offen | — |
| 58 | Tests | Test: `GetPortfolioReport_NoPostings_ReturnsEmptyStructure` | Offen | — |
| 59 | Tests | Test: `GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData` | Offen | — |
| 60 | Tests | `PortfolioAnalysisReportCacheServiceTests` Testklasse anlegen | Offen | — |
| 61 | Tests | Test: `CacheHit_WithinMonth_ReturnsCachedData` | Offen | — |
| 62 | Tests | Test: `CacheMiss_EndOfMonth_RecalculatesReport` | Offen | — |
| 63 | Tests | Test: `InvalidateCache_AfterPostingUpdate_DeletesCacheEntry` | Offen | — |
| 64 | Tests | `PortfolioAnalysisReportPageViewModelTests` Testklasse anlegen | Offen | — |
| 65 | Tests | Test: `LoadReport_ViewModel_CallsServiceAndSetsData` | Offen | — |
| 66 | Tests | Test: `EditMode_SaveConfiguration_PersistsAndInvalidatesCache` | Offen | — |
| 67 | Tests | Test: `Refresh_ViewModel_ClearsAndReloadsReport` | Offen | — |
| 68 | Tests | `PortfolioKpiConfigurationTests` Testklasse anlegen | Offen | — |
| 69 | Tests | Test: `Create_PortfolioKpiConfiguration_Persists` | Offen | — |
| 70 | Tests | Test: `Update_PortfolioKpiConfiguration_ReflectsChanges` | Offen | — |
| 71 | Tests | `PortfolioAnalysisReportControllerTests` Testklasse anlegen | Offen | — |
| 72 | Tests | Test: `GetAnalysisReport_Controller_Returns200AndData` | Offen | — |
| 73 | Tests | Test: `PostKpiConfiguration_Controller_SavesAndReturns200` | Offen | — |
| 74 | Tests | Test: `DeleteCache_Controller_InvalidatesCache` | Offen | — |
| 75 | Tests | Test-Utility: `BuildPortfolioReport_TestDataBuilder` Hilfsmethode erstellen | Offen | — |
| 76 | Tests | Test-Utility: `CreatePortfolioKpiConfiguration_TestDataBuilder` Hilfsmethode erstellen | Offen | — |
| 77 | E2E | `PortfolioAnalysisReportE2ETests` Testklasse anlegen | Offen | — |
| 78 | E2E | E2E-Test: `LoadReportScenario` (Happy Path) | Offen | — |
| 79 | E2E | E2E-Test: `EditConfigurationScenario` (Drag-&-Drop, Speichern) | Offen | — |
| 80 | E2E | E2E-Test: `CacheInvalidationAfterPostingScenario` | Offen | — |
| 81 | E2E | E2E-Test: `RibbonNavigationScenario` | Offen | — |
| 82 | E2E | E2E-Test: `MultiUserIsolationScenario` | Offen | — |
| 83 | E2E | E2E-Test: `LargePortfolioPerformanceScenario` (>1000 Positionen) | Offen | — |
| 84 | Dependency Injection | `PortfolioAnalysisReportService` in DI-Container registrieren | Offen | — |
| 85 | Dependency Injection | `PortfolioAnalysisReportCacheService` in DI-Container registrieren | Offen | — |
| 86 | Dependency Injection | `IPortfolioKpiConfigurationRepository` in DI-Container registrieren | Offen | — |
| 87 | Dependency Injection | Event-Handler-Registrierung für Cache-Invalidierung in DI-Container | Offen | — |
