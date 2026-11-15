# Interfaces – Hochrechnung (CompareProjection)

## `IReportAggregationService`
Datei: `FinanceManager.Application/Reports/IReportAggregationService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `QueryAsync` | `ReportAggregationQuery query`, `CancellationToken ct` | `Task<ReportAggregationResult>` | Führt die Aggregationsabfrage aus (FA-REP-008). Einzige Methode; `CompareProjection` ist Feld der Query — Vertrag voraussichtlich unverändert. |

Implementiert von `ReportAggregationService`; registriert in `FinanceManager.Infrastructure/ServiceCollectionExtensions.cs`. Konsumenten: `ReportsController` (API), `FmReportingAggregateHandler` (MCP), Tests.

## `IReportPostingsQueryService`
Datei: `FinanceManager.Application/Reports/IReportPostingsQueryService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `QueryAsync` | `ReportPostingsQuery query`, `CancellationToken ct` | `Task<ReportPostingsResult>` | Posteneinsicht einer Betragszelle; führt `CompareProjection` im Query-Record (für das identische Lade-Fenster des Netto-Dividendenpfads). |

## `IApiClient` (report-relevante Methoden)
Datei: `FinanceManager.Shared/IApiClient.cs` (+ `ApiClient.Reports.cs` Implementierung)

| Methode | Zweck |
|---------|-------|
| `Reports_QueryAggregatesAsync(ReportAggregatesQueryRequest, CancellationToken)` | POST `report-aggregates`; Ergebnis `ReportAggregationResult` inkl. `ProjectionAmount`/`ProjectionExpectedDividends` — DTOs sind artneutral deserialisierbar. |
| `Reports_QueryPostingsAsync(ReportPostingsQueryRequest, CancellationToken)` | POST Posteneinsicht. |
| `Reports_GetFavoriteAsync` / `Reports_CreateFavoriteAsync` / `Reports_UpdateFavoriteAsync` / `Reports_DeleteFavoriteAsync` | Favoriten-CRUD; `CompareProjection` ist Teil von `ReportFavoriteDto`/Create-/Update-ApiRequests. |

## `IReportFavoriteService`
Datei: `FinanceManager.Application/Reports/IReportFavoriteService.cs` (Implementierung `ReportFavoriteService`)

Favoriten-CRUD mit `CompareProjection` in allen Request-/DTO-Typen (`ReportFavoriteCreateRequest`, `ReportFavoriteUpdateRequest`, `ReportFavoriteDto`); keine kindbezogene Validierung der Option — die Hochrechnung ist heute schon artunabhängig persistierbar, sie wirkt nur nicht.
