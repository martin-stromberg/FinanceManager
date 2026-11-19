# Interfaces

## `IReportAggregationService`
Datei: `FinanceManager.Application/Reports/IReportAggregationService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `QueryAsync` | `ReportAggregationQuery query`, `CancellationToken ct` | `Task<ReportAggregationResult>` | Führt die Berichtsaggregation inkl. Filter und optionaler Projektion aus; `query.Filters` trägt `BankPostingTypes` |

## `IReportPostingsQueryService`
Datei: `FinanceManager.Application/Reports/IReportPostingsQueryService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `QueryAsync` | `ReportPostingsQuery query`, `CancellationToken ct` | `Task<ReportPostingsResult>` | Drilldown-Postenliste einer Berichtszelle; `query.Filters` trägt `BankPostingTypes` (umgesetzt in `ReportPostingsQueryService.LoadPostingsAsync`) |

## `IReportFavoriteService`
Datei: `FinanceManager.Application/Reports/IReportFavoriteService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---|---|---|---|
| `ListAsync` | `Guid ownerUserId`, `CancellationToken ct` | `Task<IReadOnlyList<ReportFavoriteDto>>` | Favoritenliste inkl. `Filters.BankPostingTypes` |
| `GetAsync` | `Guid id`, `Guid ownerUserId`, `CancellationToken ct` | `Task<ReportFavoriteDto?>` | Einzelner Favorit inkl. `Filters.BankPostingTypes` |
| `CreateAsync` | `Guid ownerUserId`, `ReportFavoriteCreateRequest request`, `CancellationToken ct` | `Task<ReportFavoriteDto>` | Legt Favoriten an; `request.Filters` trägt `BankPostingTypes` |
| `UpdateAsync` | `Guid id`, `Guid ownerUserId`, `ReportFavoriteUpdateRequest request`, `CancellationToken ct` | `Task<ReportFavoriteDto?>` | Aktualisiert Favoriten; `request.Filters` trägt `BankPostingTypes` |
| `DeleteAsync` | `Guid id`, `Guid ownerUserId`, `CancellationToken ct` | `Task<bool>` | Löscht Favoriten |

## `IApiClient` (Client-Vertrag, relevante Methoden)
Verwendet in `ReportDashboardViewModel` und `HomeKpiGrid.razor`:

| Methode | Zweck |
|---|---|
| `Reports_QueryAggregatesAsync(ReportAggregatesQueryRequest, CancellationToken)` | Aggregatabfrage; `request.Filters` (`ReportAggregatesFiltersRequest`) trägt `BankPostingTypes` |
| `Reports_QueryPostingsAsync(ReportPostingsQueryRequest, CancellationToken)` | Drilldown; Filter-Payload enthält `BankPostingTypes` |
| `Reports_CreateFavoriteAsync(ReportFavoriteCreateApiRequest, CancellationToken)` | Favorit anlegen; `request.Filters` (`ReportFavoriteFiltersDto`) kann `BankPostingTypes` tragen — wird im ViewModel aktuell nicht befüllt |
| `Reports_UpdateFavoriteAsync(Guid, ReportFavoriteUpdateApiRequest, CancellationToken)` | Favorit aktualisieren; gleiche Stelle |
| `Reports_GetFavoriteAsync(Guid)` / `Reports_ListFavoritesAsync` / `Reports_DeleteFavoriteAsync` | Favoriten lesen/löschen; `ReportFavoriteDto.Filters.BankPostingTypes` kommt serverseitig korrekt zurück |
