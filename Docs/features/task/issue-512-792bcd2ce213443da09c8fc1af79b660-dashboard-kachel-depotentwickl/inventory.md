# Bestandsaufnahme: Dashboard-Kachel „Depotentwicklung & Dividenden" (Issue #512)

Analyse des Depot-Berichts (`/portfolio/analysis-report`, `PortfolioAnalysisReportPage`) und aller Komponenten, die die Anforderung `Docs/features/task/issue-512-…/requirement.md` betrifft: Kachel-Mechanismus (`PortfolioTileId`, `PortfolioKpiConfiguration`), Berichts-Pipeline (`PortfolioAnalysisReportService` → `PortfolioAnalysisReportCacheService` → `PortfolioAnalysisReportController` → ViewModel/Karten), Bewertungs-/FIFO-Hilfen und Diagramm-Muster.

## Zusammenfassung

**Vorhanden (wiederverwendbar):**
- Kachel-Mechanismus vollständig etabliert: `PortfolioTileId`-Enum (4 Werte: `Structure`, `Performance`, `Cashflow`, `Risk`), benutzerbezogene `PortfolioKpiConfiguration` (TileIds als JSON — neue Enum-Werte ohne Migration/Backup-Impact), Edit-Modus (Checkbox + Verschieben), Controller-Validierung und `DefaultConfigurationDto()` als Server-Standard.
- Berichts-Pipeline mit monatlichem DB-Cache (`ReportCacheEntry`); `CacheSchemaVersion = "5"` ist explizit für DTO-Strukturänderungen vorgesehen und **muss bei der neuen Sektion erhöht werden** (sonst teilweise `null`-deserialisierte DTOs; abgesichert durch bestehenden Test `CacheHit_EntryFromOlderDtoSchema_…`).
- Alle Rechenbausteine für die Jahresserie: `GetPortfolioValueOnDate`/`SecurityValuationHelper` (Stichtags-Depotwert, letzter bekannter Kurs bzw. 0), `_fifo.Calculate(...)` auf Transaktions-Teilmengen (`BuildCashflow`-`beforeYear`-Muster für kumuliertes Kapital je Stichtag), Netto-Dividendenkonvention `Σ Dividend − Σ |Tax|` (`ReturnAnalysisService.BuildAnnualDividends`/`AnnualDividendPoint`), `ComputePeriodMetrics` (Jahresanfang = 31.12. Vorjahr), `IReturnCalculationService.CalculateDividendYield` (Basis=0 → `null`).
- UI-Muster: `PortfolioKpiCard`-Rahmen, `MiniBarChart` (Balken, `EmptyText`-Leerzustand), `BudgetLineChart` (SVG-Linie, `<title>`-Tooltips), `IsYtd`/`*`-Fußnoten-Konvention, `KpiInfoButton`, resx-Namespace `PortfolioReport_*` (74 Keys de+en), CSS-Dateien `app.PortfolioReport.css`/`theme.Dark.PortfolioReport.css`.
- `@rendermode InteractiveServer` auf der Seite gesetzt; Umschalten der Rendite-Basis ist reine Komponenten-Logik (`StateHasChanged`), kein Server-Roundtrip nötig, da die Jahresserie alle benötigten Basen liefern kann.

**Nicht vorhanden (neu zu schaffen):**
- `PortfolioTileId`-Mitglied für die Kachel; `PortfolioTileId` ist aktuell auch **nicht** in `EnumCoverageTests` abgedeckt (AGENT.md-Pflicht ergänzend zur Anforderung).
- DTO-Sektion/Jahreszeilen-Record in `PortfolioAnalysisReportDto`; `BuildDepotDevelopment`-Methode im Service; Karten-Komponente und kombiniertes Linie+Balken-Diagramm (keine JS-Chartbibliothek im Projekt); Render-Verzweigung + `GetTileLabel`-Eintrag; resx-Texte de+en.
- Beachtenswert: `Risk`-TileId existiert, ist aber nicht in `DefaultConfigurationDto` — Präzedenz für „Kachel existiert, standardmäßig inaktiv" (relevant für offene Frage 5). Für Benutzer mit bereits gespeicherter Konfiguration wäre eine neue TileId ohne Merge-Logik unsichtbar.
- `task.json` im Repo-Root ist veraltet (Inhalt bezieht sich auf Issue #141) — irrelevante Arbeitsdatei, nicht zu verwechseln.

**Test-Ausgangszustand:** Alle ausführbaren Suiten liefen durch: `FinanceManager.Tests` 2244/0/0, `FinanceManager.Tests.Integration` 172/0/0, `FinanceManager.Tests.E2E` 147 gesamt mit **1 nachgewiesenem Fehlschlag** (`ChangePasswordWellKnownPlaywrightTests.AuthenticatedUser_ChangesPassword_ViaUi`, `net::ERR_ABORTED` bei `/login`-Navigation — nicht portfolio-bezogen); alle 4 Portfolio-E2E-Tests bestanden. Details und Nachweise: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `PortfolioAnalysisReportDto` und Sektions-DTOs, `PortfolioKpiConfiguration`, `ReportCacheEntry`, Berechnungs-Records (`SecurityTransaction`, `FifoCostBasisResult`, `AnnualDividendPoint` u. a.)
- [Logik](inventory/logic.md) — `PortfolioAnalysisReportService`, `PortfolioAnalysisReportCacheService` (`CacheSchemaVersion`), `PortfolioAnalysisReportController` (`DefaultConfigurationDto`), `PortfolioAnalysisReportPageViewModel`, Bewertungs-/FIFO-Hilfen, UI-Komponenten und Diagramm-Muster
- [Enums](inventory/enums.md) — `PortfolioTileId` (4 Werte), `SecurityPostingSubType` (5 Werte)
- [Interfaces](inventory/interfaces.md) — Service-/Repository-Contracts, `IFifoCostBasisCalculator`, `IReturnCalculationService`, `IApiClient`-Methoden
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit echten Läufen (TRX/Konsolenlogs), bestehende Testklassen und Hilfsmethoden für den Bereich

## Relevante Projektkonventionen (aus `AGENT.md` + `.github/copilot-instructions.md`)

- Neue Enum-Mitglieder in `EnumCoverageTests` ergänzen (Test assertiert vollständige Memberliste).
- Neue UI-Texte ausschließlich via `Resources/Pages.*.resx` für de+en; keine hartkodierten Strings.
- `@rendermode InteractiveServer` explizit auf jeder Blazor-Seite (auf der Berichtsseite bereits vorhanden).
- Benutzerbezug: Filterung serverseitig über `OwnerUserId` aus Auth-Kontext — im Service/Controller bereits durchgezogen.
- EF-Migrationen nur via `dotnet ef migrations add` — für diese Anforderung laut Scope nicht nötig (keine neuen persistierten Felder).
- Backup-Regel: `PortfolioKpiConfiguration` besitzt bereits Backup-DTO; TileIds als JSON sind enum-kompatibel — kein Backup-Impact, solange keine Persistenz der Filterwahl eingeführt wird.
- Ribbon: Berichtsseite nutzt eine `Ribbon_Group_Actions`-Gruppe (Back/Refresh/Edit); keine LinkedInfo-/Detailaktionen betroffen.
