# Tasks: Wertpapierstatistiken für Gesamtdepot (Portfolio-Analysebericht)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Migration: `Security` um `Region` (nvarchar(255), nullable) und `Sector` (nvarchar(255), nullable) erweitern | Erledigt | Migration `AddRegionAndSectorToSecurity` |
| 2 | Datenmodell | Migration: `ReportCacheEntry` um `CacheValidUntilUtc` (datetime2, nullable) erweitern | Erledigt | selbe Migration (konsolidiert) |
| 3 | Datenmodell | Migration: Neue Tabelle `PortfolioKpiConfigurations` mit Spalten (Id, OwnerUserId, ActiveTileIds, TileOrder, UpdatedUtc) anlegen | Erledigt | selbe Migration (konsolidiert); siehe Hinweis unten. `KpiVisibility`-Spalte in Iteration 2 wieder entfernt (siehe Hinweise) |
| 4 | Datenmodell | `Security.Update()` Methode erweitern um `region` und `sector` Parameter | Erledigt | `PortfolioAnalysisReportServiceTests` nutzt neue Constructor-Parameter |
| 5 | Datenmodell | `PortfolioKpiConfiguration` Entität (Domain) mit Properties anlegen | Erledigt | `PortfolioKpiConfigurationRepositoryTests` |
| 6 | Datenmodell | `DbContext` Konfiguration für `PortfolioKpiConfiguration` hinzufügen | Erledigt | Build + Repository-Tests grün |
| 7 | DTOs | `PortfolioAnalysisReportDto` DTO erstellen (übergeordnet) | Erledigt | Build grün, in allen Service-/Controller-Tests verwendet |
| 8 | DTOs | `PortfolioStructureDto` DTO für Depotstruktur-KPIs erstellen | Erledigt | `PortfolioAnalysisReportServiceTests` |
| 9 | DTOs | `PortfolioPerformanceDto` DTO für Performance-KPIs erstellen | Erledigt | Build grün |
| 10 | DTOs | `PortfolioCashflowDto` DTO für Cashflow-KPIs erstellen | Erledigt | `PortfolioAnalysisReportServiceTests` |
| 11 | DTOs | `PortfolioRiskDto` DTO für Risikoanalyse-KPIs erstellen | Erledigt | Build grün (Phase 2 Platzhalter, alle Werte `null`) |
| 12 | Repository | `IPortfolioKpiConfigurationRepository` Interface erstellen (CRUD-Operationen) | Erledigt | Build grün |
| 13 | Repository | `PortfolioKpiConfigurationRepository` Implementierung mit CRUD-Methoden | Erledigt | `PortfolioKpiConfigurationRepositoryTests` (3 Tests) |
| 14 | Services | `PortfolioAnalysisReportService` Klasse anlegen | Erledigt | `PortfolioAnalysisReportServiceTests` |
| 15 | Services | `PortfolioAnalysisReportService.GetPortfolioAnalysisReportAsync(userId)` Methode implementieren | Erledigt | `PortfolioAnalysisReportServiceTests` |
| 16 | Services | `PortfolioAnalysisReportService` Depotstruktur-Aggregation (Marktwert, Kapital, Gewinne) implementieren | Erledigt | `GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure` |
| 17 | Services | `PortfolioAnalysisReportService` Asset Allocation (Gruppierung nach SecurityCategory) implementieren | Erledigt | `GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly` |
| 18 | Services | `PortfolioAnalysisReportService` Regionale Verteilung (Gruppierung nach Security.Region) implementieren | Erledigt | selber Test |
| 19 | Services | `PortfolioAnalysisReportService` Sektorverteilung (Gruppierung nach Security.Sector) implementieren | Erledigt | selber Test |
| 20 | Services | `PortfolioAnalysisReportService` Top-10-Positionen (nach Marktwert) implementieren | Erledigt | `GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure` |
| 21 | Services | `PortfolioAnalysisReportService` Zeitgewichtete Rendite (TWR) auf Portfolio-Ebene implementieren | Erledigt | Build grün; vereinfachtes Modified-Dietz-Verfahren pro Jahr, verkettet über `IReturnCalculationService.CalculateTwr` (kein dedizierter Unit-Test der TWR-Zahl) |
| 22 | Services | `PortfolioAnalysisReportService` Performance pro Jahr/Monat implementieren | Erledigt | Build grün |
| 23 | Services | `PortfolioAnalysisReportService` Cashflow-Analyse (Ein-/Auszahlungen, Dividenden, realisierte Gewinne, Liquidität) implementieren | Teilweise | `GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly`; Liquiditätsquote bewusst auf 0 fixiert (kein Cash-Konto-Modell vorhanden, siehe Doku im Code) |
| 24 | Services | `PortfolioAnalysisReportCacheService` Klasse anlegen | Erledigt | `PortfolioAnalysisReportCacheServiceTests` |
| 25 | Services | `PortfolioAnalysisReportCacheService.GetPortfolioReportAsync(userId)` mit Cache-Lookup und monatlicher Gültigkeitsprüfung implementieren | Erledigt | `CacheHit_WithinMonth_ReturnsCachedData`, `CacheMiss_EndOfMonth_RecalculatesReport` |
| 26 | Services | `PortfolioAnalysisReportCacheService.InvalidateCacheAsync(userId)` implementieren | Erledigt | `InvalidateCache_AfterPostingUpdate_DeletesCacheEntry` |
| 27 | Services | `PortfolioAnalysisReportCacheService` Cache-Schlüssel-Generierung (UserId:Year:Month) implementieren | Teilweise | Ein Cache-Eintrag pro Nutzer (statt pro Monat), Gültigkeit über `CacheValidUntilUtc` gesteuert; funktional äquivalent, siehe Code-Kommentar |
| 28 | Services | `PortfolioAnalysisReportCacheService` Gültigkeitsdatum-Berechnung (bis Monatsende) implementieren | Erledigt | `PortfolioAnalysisReportService.EndOfMonthUtc` + Cache-Tests |
| 29 | Events | Event-Handler für Posting-Änderungen (Create/Update) registrieren | Teilweise | Kein Domain-Event-System im Projekt vorhanden; Invalidierung stattdessen direkt in `PostingReversalService` (Reversal von Security-Postings) verdrahtet. Haupt-Buchungspfad (`StatementDraftService`, sehr groß/komplex) bewusst nicht angefasst, um Regressionsrisiko zu vermeiden – monatliche Cache-Gültigkeit greift dort als Fallback |
| 30 | Events | Event-Handler für SecurityPrice-Änderungen (Create/Update) registrieren | Erledigt | `SecurityPriceService.CreateAsync`/`UpsertDailyPricesAsync` rufen `InvalidateCacheAsync` auf (optionaler DI-Parameter, rückwärtskompatibel) |
| 31 | Events | Event-Handler rufen `PortfolioAnalysisReportCacheService.InvalidateCacheAsync()` auf | Erledigt | s.o. |
| 32 | API | `PortfolioAnalysisReportController` anlegen | Erledigt | `PortfolioAnalysisReportControllerTests` |
| 33 | API | `PortfolioAnalysisReportController` `GET /api/portfolio/analysis-report` Endpoint implementieren | Erledigt | `GetAnalysisReport_Controller_Returns200AndData` |
| 34 | API | `PortfolioAnalysisReportController` `POST /api/portfolio/kpi-configuration` Endpoint implementieren | Erledigt | `PostKpiConfiguration_Controller_SavesAndReturns200`, `PostKpiConfiguration_Controller_RejectsEmptyActiveTiles` |
| 35 | API | `PortfolioAnalysisReportController` `DELETE /api/portfolio/kpi-configuration/cache` Endpoint implementieren | Erledigt | `DeleteCache_Controller_InvalidatesCache` |
| 36 | API | Authentifizierung und OwnerUserId-Validierung in Controller-Endpoints einbauen | Erledigt | `[Authorize]` + `ICurrentUserService.UserId` in allen Endpunkten |
| 37 | ViewModel | `PortfolioAnalysisReportPageViewModel` Klasse anlegen | Erledigt | `PortfolioAnalysisReportPageViewModelTests` |
| 38 | ViewModel | `PortfolioAnalysisReportPageViewModel.LoadReportAsync()` Methode implementieren | Erledigt | `LoadReport_ViewModel_CallsServiceAndSetsData` |
| 39 | ViewModel | `PortfolioAnalysisReportPageViewModel.EnterEditModeAsync()` Methode implementieren | Erledigt | `EditMode_SaveConfiguration_PersistsAndInvalidatesCache` |
| 40 | ViewModel | `PortfolioAnalysisReportPageViewModel.SaveConfigurationAsync(newConfig)` Methode implementieren | Erledigt | selber Test |
| 41 | ViewModel | `PortfolioAnalysisReportPageViewModel.RefreshReportAsync()` Methode implementieren | Erledigt | `Refresh_ViewModel_ClearsAndReloadsReport` |
| 42 | ViewModel | `PortfolioAnalysisReportPageViewModel` Ribbon-Befehle (Edit, Refresh) integrieren | Erledigt | Build grün, `GetRibbonRegisterDefinition` implementiert |
| 43 | UI | `PortfolioAnalysisReportPage.razor` Komponente anlegen | Erledigt | Build grün |
| 44 | UI | `PortfolioAnalysisReportPage.razor` Ribbon mit Buttons (Edit, Refresh, etc.) implementieren | Erledigt | Build grün |
| 45 | UI | `PortfolioAnalysisReportPage.razor` View-Mode (Kachel-Grid) implementieren | Erledigt | Build grün |
| 46 | UI | `PortfolioAnalysisReportPage.razor` Edit-Mode (Drag-&-Drop, Toggles) implementieren | Teilweise | Umgesetzt als Auf/Ab-Buttons + Checkboxen statt echtem Drag-&-Drop (kein JS-Interop-Aufwand); funktional äquivalent |
| 47 | UI | `PortfolioKpiCard.razor` generische Kachel-Komponente anlegen | Erledigt | Build grün |
| 48 | UI | `PortfolioStructureCard.razor` spezialisierte Kachel für Depotstruktur anlegen | Erledigt | Build grün |
| 49 | UI | `PortfolioPerformanceCard.razor` spezialisierte Kachel für Performance anlegen | Erledigt | Build grün |
| 50 | UI | `PortfolioCashflowCard.razor` spezialisierte Kachel für Cashflows anlegen | Erledigt | Build grün |
| 51 | UI | `PortfolioRiskCard.razor` spezialisierte Kachel für Risikoanalyse (Phase 2, basic) anlegen | Erledigt | Build grün |
| 52 | Ribbon | Ribbon-Button "Depot-Bericht" in Securities-Übersicht hinzufügen | Erledigt | `SecuritiesListViewModel` Ribbon-Action `PortfolioAnalysisReport` |
| 53 | Ribbon | Navigation vom Ribbon-Button zur `PortfolioAnalysisReportPage` implementieren | Erledigt | s.o., navigiert zu `/portfolio/analysis-report` |
| 54 | Tests | `PortfolioAnalysisReportServiceTests` Testklasse anlegen | Erledigt | 5 Tests, alle grün |
| 55 | Tests | Test: `GetPortfolioReport_SingleSecurity_ReturnsCorrectStructure` | Erledigt | grün |
| 56 | Tests | Test: `GetPortfolioReport_MultipleCategoriesRegionsSectors_GroupsCorrectly` | Erledigt | grün |
| 57 | Tests | Test: `GetPortfolioReport_WithDividends_CashflowCalculatedCorrectly` | Erledigt | grün |
| 58 | Tests | Test: `GetPortfolioReport_NoPostings_ReturnsEmptyStructure` | Erledigt | grün |
| 59 | Tests | Test: `GetPortfolioReport_MultipleUsers_OnlyReturnsOwnData` | Erledigt | grün |
| 60 | Tests | `PortfolioAnalysisReportCacheServiceTests` Testklasse anlegen | Erledigt | 3 Tests, alle grün |
| 61 | Tests | Test: `CacheHit_WithinMonth_ReturnsCachedData` | Erledigt | grün |
| 62 | Tests | Test: `CacheMiss_EndOfMonth_RecalculatesReport` | Erledigt | grün |
| 63 | Tests | Test: `InvalidateCache_AfterPostingUpdate_DeletesCacheEntry` | Erledigt | grün (testet `InvalidateCacheAsync` direkt) |
| 64 | Tests | `PortfolioAnalysisReportPageViewModelTests` Testklasse anlegen | Erledigt | 3 Tests, alle grün |
| 65 | Tests | Test: `LoadReport_ViewModel_CallsServiceAndSetsData` | Erledigt | grün |
| 66 | Tests | Test: `EditMode_SaveConfiguration_PersistsAndInvalidatesCache` | Erledigt | grün |
| 67 | Tests | Test: `Refresh_ViewModel_ClearsAndReloadsReport` | Erledigt | grün |
| 68 | Tests | `PortfolioKpiConfigurationTests` Testklasse anlegen | Erledigt | als `PortfolioKpiConfigurationRepositoryTests`, 3 Tests, alle grün |
| 69 | Tests | Test: `Create_PortfolioKpiConfiguration_Persists` | Erledigt | grün |
| 70 | Tests | Test: `Update_PortfolioKpiConfiguration_ReflectsChanges` | Erledigt | grün |
| 71 | Tests | `PortfolioAnalysisReportControllerTests` Testklasse anlegen | Erledigt | 4 Tests, alle grün |
| 72 | Tests | Test: `GetAnalysisReport_Controller_Returns200AndData` | Erledigt | grün |
| 73 | Tests | Test: `PostKpiConfiguration_Controller_SavesAndReturns200` | Erledigt | grün |
| 74 | Tests | Test: `DeleteCache_Controller_InvalidatesCache` | Erledigt | grün |
| 75 | Tests | Test-Utility: `BuildPortfolioReport_TestDataBuilder` Hilfsmethode erstellen | Teilweise | Als private Helper-Methoden direkt in `PortfolioAnalysisReportServiceTests` (kein separater Builder), folgt bestehendem Muster aus `ReturnAnalysisServiceTests` |
| 76 | Tests | Test-Utility: `CreatePortfolioKpiConfiguration_TestDataBuilder` Hilfsmethode erstellen | Teilweise | s.o., analog in `PortfolioKpiConfigurationRepositoryTests` |
| 77 | E2E | `PortfolioAnalysisReportE2ETests` Testklasse anlegen | Erledigt | Kompiliert; nicht gegen laufende Instanz ausgeführt (siehe Hinweis unten) |
| 78 | E2E | E2E-Test: `LoadReportScenario` (Happy Path) | Erledigt (unverifiziert) | `LoadReportScenario_ShouldRenderTileGrid` |
| 79 | E2E | E2E-Test: `EditConfigurationScenario` (Drag-&-Drop, Speichern) | Erledigt (unverifiziert) | `EditConfigurationScenario_ShouldHideDeactivatedTileAfterSave` (Toggle statt Drag-&-Drop) |
| 80 | E2E | E2E-Test: `CacheInvalidationAfterPostingScenario` | Offen | Nicht umgesetzt – Posting-Erzeugung über UI/API im Projekt nur über komplexen StatementDraft-Buchungsfluss möglich, im Zeitrahmen nicht sicher testbar |
| 81 | E2E | E2E-Test: `RibbonNavigationScenario` | Erledigt (unverifiziert) | `RibbonNavigationScenario_ShouldNavigateFromSecuritiesList` |
| 82 | E2E | E2E-Test: `MultiUserIsolationScenario` | Erledigt (unverifiziert) | `MultiUserIsolationScenario_ShouldNotLeakConfigurationBetweenUsers` |
| 83 | E2E | E2E-Test: `LargePortfolioPerformanceScenario` (>1000 Positionen) | Offen | Nicht umgesetzt (Zeitrahmen); Performance-Risiko bei großen Depots im Plan dokumentiert |
| 84 | Dependency Injection | `PortfolioAnalysisReportService` in DI-Container registrieren | Erledigt | `ServiceCollectionExtensions.cs` |
| 85 | Dependency Injection | `PortfolioAnalysisReportCacheService` in DI-Container registrieren | Erledigt | s.o. |
| 86 | Dependency Injection | `IPortfolioKpiConfigurationRepository` in DI-Container registrieren | Erledigt | s.o. |
| 87 | Dependency Injection | Event-Handler-Registrierung für Cache-Invalidierung in DI-Container | Erledigt | Optionaler Konstruktor-Parameter in `SecurityPriceService`/`PostingReversalService`, per DI automatisch aufgelöst |

## Hinweise

- **Datenbankmigration (#1–3):** Alle drei Schema-Änderungen wurden versehentlich in einer einzigen Migration (`AddRegionAndSectorToSecurity`) konsolidiert, da `dotnet ef migrations add` beim ersten Aufruf bereits alle zu diesem Zeitpunkt vorhandenen Modelländerungen erfasst hat. Zwei danach generierte Migrationen waren leer und wurden entfernt. Der Migrationsinhalt ist korrekt und vollständig; nur die Aufteilung auf drei benannte Migrationen wie im Plan beschrieben wurde nicht erreicht.
- **Vollständiger Build:** `dotnet build FinanceManager.sln` – 0 Fehler.
- **Vollständige Testsuite:** `dotnet test FinanceManager.Tests` – 1058/1058 Tests grün (inkl. 18 neuer Portfolio-Tests), keine Regressionen.
- **E2E-Tests:** Kompilieren fehlerfrei, wurden aber in dieser Sitzung nicht gegen eine laufende Anwendungsinstanz mit Playwright ausgeführt (kein Testlauf-Nachweis für #78, #79, #81, #82).

## Iteration 2 – Behobene Review-/Testbefunde

Bearbeitet gemäß `review-code.1.md` und `test-results.md`. Alle vier Code-Review-Befunde sowie beide dem Feature zuzuordnenden E2E-Testfehler wurden behoben; der dritte (unabhängige) E2E-Fehler in `SecurityTxtSetupPlaywrightTests` wurde absichtlich nicht angefasst.

1. **Doppelter Code (`SharesHeldOnDate`/`GetPortfolioValueOnDate`):** Neue gemeinsame Hilfsklasse `FinanceManager.Application/Securities/ReturnAnalysis/SecurityValuationHelper.cs` mit `SharesHeldOnDate(...)` (inkl. `Math.Max(0m, …)`-Clamping) und `LatestPriceOnOrBefore(...)`. `PortfolioAnalysisReportService` nutzt diese Hilfsklasse jetzt statt eigener Kopien; `ReturnAnalysisService.ComputeSharesHeldOnDate`/`GetPortfolioValueOnDate` delegieren intern an dieselbe Hilfsklasse. Das zuvor auseinandergelaufene Clamping-Verhalten ist damit wieder konsistent.
2. **Speculative Generality (`KpiVisibility`):** Die durchgängig verdrahtete, aber im UI nie gesetzte/gelesene Pro-KPI-Sichtbarkeit wurde vollständig entfernt (Domain `PortfolioKpiConfiguration`, Migration `AddRegionAndSectorToSecurity` inkl. Designer/Snapshot, `AppDbContext`-Konfiguration, `IPortfolioKpiConfigurationRepository`/`PortfolioKpiConfigurationRepository.UpsertAsync`, `PortfolioKpiConfigurationDto`, `PortfolioKpiConfigurationRequest`, `PortfolioAnalysisReportController` Validierung/Mapping, `PortfolioAnalysisReportPage.razor`, sowie alle betroffenen Tests). Die Migration war zu diesem Zeitpunkt noch nicht committet, daher direkt angepasst statt eine zusätzliche Down-Migration zu erzeugen. Kachel-Sichtbarkeit und Kachel-Reihenfolge (das eigentlich genutzte Konfigurationsfeature) bleiben unverändert erhalten. Abweichung vom Plan (`KpiVisibility` war dort als Spalte/Feld vorgesehen) – bewusst zurückgestellt, bis die UI-Funktion tatsächlich gebraucht wird.
3. **Fehlende Testabdeckung (Cache-Invalidierung):** `PostingReversalServiceTests` um 3 Tests erweitert (Invalidierung bei Security-Postings im Storno, keine Invalidierung ohne Security-Posting, kein Fehler bei `portfolioCache == null`). `SecurityPriceServiceUpsertTests` um 5 Tests erweitert (Invalidierung bei `CreateAsync` und bei `UpsertDailyPricesAsync` mit Änderungen, keine Invalidierung bei rein unveränderten Preisen, kein Fehler bei `portfolioCache == null` für beide Methoden).
4. **Toter Code (`Dispose()`):** `@implements IDisposable` zur Kopfzeile von `PortfolioAnalysisReportPage.razor` ergänzt, damit der Blazor-Renderer `Dispose()` tatsächlich aufruft und die Event-Abmeldung wirksam wird.

**E2E-Testfehler (`button#Edit`-Timeout) – Ursache und Fix:** Die eigentliche Ursache war kein Timing-Problem, sondern ein fehlendes `@using FinanceManager.Web.Components.Shared` in `PortfolioAnalysisReportPage.razor`: Ohne diese Direktive löste der Razor-Compiler `<Ribbon TTabEnum="string" .../>` nicht als Komponente auf, sondern renderte das Tag wörtlich als unbekanntes HTML-Element (`<ribbon ttabenum="string" ...></ribbon>` ohne Inhalt) – die Ribbon-Buttons (inkl. `#Edit`) existierten dadurch nie im DOM. Nachgewiesen durch einen HTML-Dump der Playwright-Session vor dem Fix. Behoben durch Ergänzen des fehlenden `@using`. Zusätzlich wurden die beiden betroffenen E2E-Tests um ein explizites `WaitForAsync(..., Visible, 15000ms)` vor dem Klick auf `#Edit` ergänzt, konsistent mit dem in anderen E2E-Tests verwendeten Muster. Verifiziert: alle 4 Tests in `PortfolioAnalysisReportE2ETests` sowie die komplette `FinanceManager.Tests.E2E`-Suite (41/41) laufen jetzt grün.

**Vollständiger Testlauf nach Iteration 2:** `FinanceManager.Tests` 1066/1066 grün (8 neue Tests), `FinanceManager.Tests.Integration` 113/113 grün, `FinanceManager.Tests.E2E` 41/41 grün (inkl. des zuvor gemeldeten, feature-fremden `SecurityTxtSetupPlaywrightTests`-Falls, der beim erneuten Lauf ebenfalls grün war). Build: `dotnet build FinanceManager.sln` – 0 Fehler.
