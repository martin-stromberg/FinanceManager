# Bestandsaufnahme — Issue #534

## 1. Domain-Schicht

### Bestehende Entitäten/Strukturen
- `Entity` (FinanceManager.Domain): `Id`, `CreatedUtc`, `ModifiedUtc`, `SetDates`.
- `ReportFavorite` (Domain/Reports): besitzt `OwnerUserId`, Favorit-Konfiguration inkl. `CompareProjection`, `Take`, Filter-CSV, `SavingsPlanPostingSource`. Referenzmodell für neue owned Aggregate.
- `SavingsPlanAccountLink` (Domain/Savings): Referenzmuster für neue owned, einfach verknüpfte Entität mit BackupDto/ToBackupDto/AssignBackupDto.
- `PostingKind` (Domain): `Bank`, `Contact`, `SavingsPlan`, `Security` — wiederverwendet, keine neue Enum.

### Neue Entität (vorgesehen)
- `ReportSuppressedExpectation` (Domain/Reports): `OwnerUserId`, `ReportFavoriteId`, `Kind` (`PostingKind`), `EntityId`, `PriorYearDate`, `SuppressedAtUtc`; `IAggregateRoot`; Konstruktor-Guards; `ToBackupDto`/`AssignBackupDto` mit `*BackupDto`-Record.

## 2. Infrastructure

### Projektionsberechnung — `ReportProjectionCalculator.cs`
- `ApplyProjectionAmounts(targetPoints, events, context, nameResolver, hasActiveSecurity, securityDividendLeafOnly)` — zentraler Statik-Einstieg.
- Interne Records: `ReportProjectionContext`, `ProjectionEvent`, `ProjectionCandidate` (`Kind`, `EntityId`, `PriorYearDate`, `ExpectedDate`, `Amount`, `EntityName`).
- `ProjectionPattern`: `Monthly`, `Quarterly`, `Annual`, `Irregular`.
- `FindExpectedItems` führt die Bestätigungsprüfung durch: monatlich per Monat, quartalsweise per Quartal, jährlich bei Security über ganzes Jahr sonst Monat, unregelmäßig 30-Tage-Fenster.
- Kandidatenliste wird aus `ProjectionEvent`s des Referenzjahres gebaut (`PriorYearDate` = Ereignisdatum im Vorjahr).

### Aggregationsdienst — `ReportAggregationService.cs`
- `QueryAsync` (ab Z. 44): `projectionEnabled`/`comparedProjection` Z. 62–65; Standard-Projection-Aufruf ~Z. 823 (`ApplyProjectionAmounts`, `securityDividendLeafOnly: true`).
- `QuerySecurityDividendsNetAsync`: zwei `ApplyProjectionAmounts`-Aufrufe ~Z. 1200 und ~Z. 1225 (netto-Dividenden je `GroupId`).
- `ResolveProjectionEntityName`-Resolver für `EntityName` je `Kind`.
- Benutzerfilter: `OwnerUserId` kommt aus `ReportAggregationQuery` (vom Controller aus Auth-Kontext).

### Persistenz — `AppDbContext.cs`
- `DbSet`s + `OnModelCreating` per `modelBuilder.Entity<>`-Blöcken; neue Tabelle + Unique-Index hier ergänzen.
- `ClearUserDataAsync` löscht 27 Schritte benutzereigener Daten; neue Collection dort ergänzen.
- Migrations unter `FinanceManager.Infrastructure/Migrations` (+ `AppDbContextModelSnapshot`); Erzeugung ausschließlich via `dotnet ef migrations add --project FinanceManager.Infrastructure --startup-project FinanceManager.Web`.

### Backup
- `BackupService.BuildBackupDataAsync`: Payload-Sektionen, `RequiredDataProperties` (25 Sektionen) — neue Sektion **nicht** dort aufnehmen.
- `SetupImportService`: Version-3-Import mit `TryGetProperty`-Guards; `reportMap` (alt→neu `ReportFavorite`-Ids) existiert bereits für `HomeKpis`-Remapping; neue Sektion nach `ReportFavorites`-Import verarbeiten und `ReportFavoriteId` per `reportMap` remappen; nicht mappbare Einträge überspringen.

### Services
- `IReportAggregationService` (Application) + `ReportAggregationService` (Infra).
- `IReportFavoriteService` + `ReportFavoriteService` (CRUD, Owner-Filter).
- `IHomeKpiService` + `HomeKpiService` (KPIs mit `ReportFavoriteId`, Owner-Validierung).
- Service-Registrierung in `ServiceCollectionExtensions.AddInfrastructureServices`.

## 3. Shared/API-Schicht

### DTOs
- `ReportProjectionExpectedItemDto` (`Kind`, `EntityId`, `EntityName`, `ExpectedDate`, `PriorYearDate`, `Amount`).
- `ReportAggregationQuery` — kein Favoriten-Bezug; Erweiterung um `Guid? ReportFavoriteId = null`.
- `ReportAggregatesQueryRequest` (API-Request) — Erweiterung um `Guid? ReportFavoriteId = null`.
- Neue DTOs (vorgesehen): `ReportSuppressedExpectationDto` (Id, Kind, EntityId, EntityName, ExpectedDate, PriorYearDate, SuppressedAtUtc), `ReportSuppressedExpectationRequest` (Kind, EntityId, PriorYearDate).

### ApiClient
- `IApiClient` + `ApiClient.Reports.cs`: `Reports_QueryAggregatesAsync`, Favoriten-CRUD. Neue Methoden für Suppression-CRUD ergänzen.

### Controller
- `ReportsController` (Web/Controllers): `POST /api/reports/aggregates`, Favoriten-Endpunkte. Suppression-Endpunkte unter `report-favorites/{favoriteId}/suppressed-expectations` (GET liste, POST suppress, DELETE unsuppress).

### MCP
- `FmReportingAggregateTool`: nutzt reguläre Aggregations-API; bei `favoriteId` lädt es den Favoriten → Favoriten-Kontext → dort Suppression anwenden; ad-hoc ohne `favoriteId` bleibt unbeeinflusst (ReportFavoriteId bleibt null).

## 4. Web-UI

### `ReportDashboard.razor`
- Erwartungs-Overlay ~Z. 110–165: eine Tabelle `fm-table projection-detail-table` mit `_projectionDetails`; Spalten Entity/Kind/Expected/PriorYear/Amount.
- Info-Buttons `.projection-info-btn` je Zeile (top/child/grandchild/total) öffnen Overlay mit `_projectionDetailsTitle`, `_projectionDetailsActualAmount`, `_projectionDetailsExpectedAmount`, `_projectionDetailsAmount`.
- `_vm.ActiveFavoriteId` (ViewModel) ist im Dashboard verfügbar; `LoadAndApplyFavoriteAsync`/`ApplyFavorite` setzen ihn.
- Erweiterung: Aktions-Spalte „Nicht mehr erwarten" je erwartetem Posten (nur wenn `ActiveFavoriteId` gesetzt), zweite Liste „Ausgeblendete Erwartungen" mit „Wieder einbeziehen", Nachladen der Aggregation + Suppression-Liste.

### `ReportDashboardViewModel`
- `ActiveFavoriteId` vorhanden; `ReloadAsync`/`LoadAsync` baut `ReportAggregatesQueryRequest` → `ReportFavoriteId = ActiveFavoriteId` mitschicken.

### `HomeKpiGrid.razor`
- `LoadFavoriteKpiDataAsync` baut `ReportAggregatesQueryRequest` aus `ReportFavoriteDto` → `ReportFavoriteId` des Favoriten mitschicken.

### Ressourcen
- `Resources/Components/Pages/ReportDashboard.{de,en}.resx`: bestehende `ProjectionDetails_*`-Keys; neue Keys für Suppress/Unsuppress/Suppressed-Liste ergänzen.

## 5. Tests

- `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs` — Service-Level-Projection-Tests (SQLite In-Memory).
- `FinanceManager.Tests/Infrastructure/SavingsPlanLinkBackupRestoreTests.cs`, `BackupRestoreCoverageTests.cs`, `BackupServiceFullExportTests.cs`, `SetupImportServiceTests.cs` — Backup-Muster.
- `FinanceManager.Tests.Integration` — API/Service-Integration.
- `FinanceManager.Tests.E2E/Tests/Reports/ReportProjectionPlaywrightTests.cs` — Playwright-Overlay-Tests (seeded Vorjahrsposten, `.projection-info-btn`, `.projection-detail-overlay`, Screenshot-Artefakte).
- `InternalsVisibleTo` auf FinanceManager.Tests vorhanden → interne Calculator-Typen direkt testbar.

## 6. Dokumentation

- `Docs/help/berichtswesen/`: beschreibung.md, ablauf-technisch.md, ablauf-anwender.md, api.md, datenmodell.md, business-rules.md, index.md — Hochrechnung dort bereits dokumentiert (Rule „Hochrechnung für Posten aller Buchungsarten").
- `Docs/help/systemverwaltung-und-setup/` beschreibt Backup-Regeln.

## Detailinventare

- `inventory/projection.md` — Calculator/Service-Aufrufkette
- `inventory/persistence-backup.md` — EF/Backup/Import
- `inventory/ui-api.md` — Dashboard/HomeKpi/API/MCP
