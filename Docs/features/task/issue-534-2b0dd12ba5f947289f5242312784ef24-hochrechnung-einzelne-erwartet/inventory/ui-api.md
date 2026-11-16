# Inventar — UI, API, MCP

## `ReportDashboard.razor`

- Overlay ~Z. 110–165: `_projectionDetails` (List<ReportProjectionExpectedItemDto>), `_projectionDetailsTitle`, `_projectionDetailsActualAmount`, `_projectionDetailsExpectedAmount`, `_projectionDetailsAmount`.
- Info-Buttons `.projection-info-btn` je Berichtszeile.
- `_vm.ActiveFavoriteId` (Guid?) — Favoriten-Kontext bereits vorhanden.
- `LoadAndApplyFavoriteAsync` → `Api.Reports_GetFavoriteAsync` → `ApplyFavorite` setzt `_vm.ActiveFavoriteId = fav.Id` + `LoadAsync()`.
- Erweiterung: Aktionsbutton je erwartetem Posten (nur mit `ActiveFavoriteId`), zweite Liste „Ausgeblendete Erwartungen", `_suppressedExpectations`-State, Hide/Restore → API-Aufruf + `LoadAsync()`.

## `ReportDashboardViewModel`

- `ActiveFavoriteId` vorhanden; `LoadAsync`/`ReloadAsync` baut `ReportAggregatesQueryRequest` → `ReportFavoriteId = ActiveFavoriteId`.

## `HomeKpiGrid.razor`

- `LoadFavoriteKpiDataAsync` erzeugt `ReportAggregatesQueryRequest` aus `ReportFavoriteDto` → `ReportFavoriteId = k.ReportFavoriteId` (bzw. `fav.Id`) übergeben — dadurch wirken Suppressions auf favoriten-gebundene KPI-Kacheln.

## API

- `ReportsController` (Web/Controllers): `POST /api/reports/aggregates` (QueryAsync-Mapping Query↔Request), Favoriten-CRUD.
- `ReportAggregatesQueryRequest` + `ReportAggregationQuery`: `Guid? ReportFavoriteId = null` ergänzen; Controller mappt es.
- Neue Endpunkte am `ReportsController`:
  - `GET /api/report-favorites/{favoriteId}/suppressed-expectations` → `ReportSuppressedExpectationDto[]` (EntityName serverseitig aufgelöst)
  - `POST /api/report-favorites/{favoriteId}/suppressed-expectations` → Body `{Kind, EntityId, PriorYearDate}`, idempotent
  - `DELETE /api/report-favorites/{favoriteId}/suppressed-expectations/{id}` → bool

## Service

- `IReportSuppressedExpectationService` (Application/Reports) + `ReportSuppressedExpectationService` (Infrastructure/Reports); List/Suppress/Unsuppress mit Owner-Filter + Favoriten-Ownership-Prüfung; EntityName-Auflösung je `Kind`; Registrierung in `ServiceCollectionExtensions`.

## `IApiClient` / `ApiClient.Reports.cs`

- `Reports_ListSuppressedExpectationsAsync(favoriteId, ct)`
- `Reports_SuppressExpectationAsync(favoriteId, request, ct)`
- `Reports_UnsuppressExpectationAsync(favoriteId, suppressionId, ct)`

## MCP (`FmReportingAggregateTool`)

- Bei `favoriteId`-Parameter: `ReportFavoriteId = favorite.Id` in Query setzen → Suppressions greifen im Favoriten-Kontext.
- Ohne `favoriteId`: `ReportFavoriteId` null → unbeeinflusst.

## Ressourcen

`ReportDashboard.de.resx` / `.en.resx` — neue Keys:

| Key | de | en |
|-----|----|----|
| `ProjectionDetails_Suppress` | Nicht mehr erwarten | Stop expecting |
| `ProjectionDetails_Unsuppress` | Wieder einbeziehen | Expect again |
| `ProjectionDetails_Suppressed_Title` | Ausgeblendete Erwartungen | Hidden expectations |
| `ProjectionDetails_Suppressed_None` | Keine ausgeblendeten Erwartungen | No hidden expectations |
