# Interfaces

## `IReportAggregationService`
Datei: `FinanceManager.Application/Reports/IReportAggregationService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `QueryAsync` | `ReportAggregationQuery query`, `CancellationToken ct` | `Task<ReportAggregationResult>` | Führt die Aggregationsabfrage aus; einzige Methode des Contracts — Textfilter würden über `query.Filters` transportiert |

Implementiert von `ReportAggregationService` (`FinanceManager.Infrastructure`).

## `IReportFavoriteService`
Datei: `FinanceManager.Application/Reports/IReportFavoriteService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task<IReadOnlyList<ReportFavoriteDto>>` | Favoriten des Owners listen |
| `GetAsync` | `Guid id`, `Guid ownerUserId`, `CancellationToken ct` | `Task<ReportFavoriteDto?>` | Einzelnen Favoriten laden |
| `CreateAsync` | `Guid ownerUserId`, `ReportFavoriteCreateRequest request`, `CancellationToken ct` | `Task<ReportFavoriteDto>` | Favoriten anlegen |
| `UpdateAsync` | `Guid id`, `Guid ownerUserId`, `ReportFavoriteUpdateRequest request`, `CancellationToken ct` | `Task<ReportFavoriteDto?>` | Favoriten aktualisieren |
| `DeleteAsync` | `Guid id`, `Guid ownerUserId`, `CancellationToken ct` | `Task<bool>` | Favoriten löschen |

Implementiert von `ReportFavoriteService`. Die Filter-Signatur ist gekapselt im `Filters`-Record — neue Textfilter-Felder ändern die Interface-Signaturen nicht.

## `IKpiLocalStorageCache`
Datei: `FinanceManager.Web/Services/KpiLocalStorageCache.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Context` (Property) | — | `KpiLocalStorageContext` | `Enabled` + optionales `UserId` für Key-Scoping |
| `SetContext` | `KpiLocalStorageContext context` | `void` | Kontext setzen |
| `GetAsync<T>` | `string key`, `CancellationToken ct` | `ValueTask<T?>` | Typisierten Wert lesen |
| `SetAsync<T>` | `string key`, `T value`, `CancellationToken ct` | `ValueTask` | Wert speichern (No-op bei deaktiviertem Kontext) |
| `RemoveAsync` | `string key`, `CancellationToken ct` | `ValueTask` | Einzelnen Eintrag löschen |
| `RemoveAllAsync` | `CancellationToken ct` | `ValueTask` | Alle App-Einträge (Prefix `fm.kpi.`, optional User-scoped) löschen |

## `IApiClient` (Report-relevante Methoden)
Datei: `FinanceManager.Shared/IApiClient.cs` (Z. ~801–826); Implementierung `FinanceManager.Shared/ApiClient.Reports.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Reports_QueryAggregatesAsync` | `ReportAggregatesQueryRequest req`, `CancellationToken ct` | `Task<ReportAggregationResult>` | Aggregations-Abfrage gegen `api/report-aggregates` |
| `Reports_ListFavoritesAsync` | `CancellationToken ct` | `Task<IReadOnlyList<ReportFavoriteDto>>` | Favoriten listen |
| `Reports_GetFavoriteAsync` | `Guid id`, `CancellationToken ct` | `Task<ReportFavoriteDto?>` | Favoriten laden |
| `Reports_CreateFavoriteAsync` | `ReportFavoriteCreateApiRequest req`, `CancellationToken ct` | `Task<ReportFavoriteDto>` | Favoriten anlegen |
| `Reports_UpdateFavoriteAsync` | `Guid id`, `ReportFavoriteUpdateApiRequest req`, `CancellationToken ct` | `Task<ReportFavoriteDto?>` | Favoriten aktualisieren |
| `Reports_DeleteFavoriteAsync` | `Guid id`, `CancellationToken ct` | `Task<bool>` | Favoriten löschen |

Wird von `ReportDashboardViewModel` und `HomeKpiGrid` (sowie Tests via `Mock<IApiClient>`) verwendet.

## `IMcpToolHandler`
Datei: `FinanceManager.Application/Mcp/` (Contract für MCP-Tools)

`FmReportingAggregateHandler` implementiert `InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct) → Task<object>`; `ToolName` liefert `fm_reporting_aggregate`. Neue Filterfelder müssten im JSON-Schema des `FmReportingAggregateDescriptor` (Konstante `JsonSchema`) ergänzt werden, wenn sie per MCP steuerbar sein sollen.
