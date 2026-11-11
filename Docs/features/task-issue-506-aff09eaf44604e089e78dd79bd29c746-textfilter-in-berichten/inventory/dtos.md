# DTOs der Report-Pipeline

Alle Records enthalten aktuell nur ID-/int-Listen und Flags — **keine Textfilter-Felder**. Die Filter werden auf drei Ebenen mit identischer Feldstruktur transportiert: API-Request (`ReportAggregatesFiltersRequest`), Service-Ebene (`ReportAggregationFilters`) und Favoriten-Persistenz (`ReportFavoriteFiltersDto`).

## `ReportAggregatesFiltersRequest`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregatesFiltersRequest.cs`

| Parameter | Typ | Zweck |
|-----------|-----|-------|
| `AccountIds` | `IReadOnlyCollection<Guid>?` | Konto-Filter |
| `ContactIds` | `IReadOnlyCollection<Guid>?` | Kontakt-Filter |
| `SavingsPlanIds` | `IReadOnlyCollection<Guid>?` | Sparplan-Filter |
| `SecurityIds` | `IReadOnlyCollection<Guid>?` | Wertpapier-Filter |
| `ContactCategoryIds` | `IReadOnlyCollection<Guid>?` | Kontakt-Kategorie-Filter |
| `SavingsPlanCategoryIds` | `IReadOnlyCollection<Guid>?` | Sparplan-Kategorie-Filter |
| `SecurityCategoryIds` | `IReadOnlyCollection<Guid>?` | Wertpapier-Kategorie-Filter |
| `SecuritySubTypes` | `IReadOnlyCollection<int>?` | Security-Subtyp-Filter (int) |
| `IncludeDividendRelated` | `bool?` | Dividenden-bezogene Buchungen einschließen |

Erzeugt in `ReportDashboardViewModel.BuildFiltersPayload()` bzw. `ReportDashboard.razor BuildFilters()` und `HomeKpiGrid.LoadFavoriteKpiDataAsync`; konsumiert in `ReportsController.QueryAsync` (Mapping auf `ReportAggregationFilters`, Z. 101–106).

## `ReportAggregatesQueryRequest`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregatesQueryRequest.cs`

Umschließt den Aggregations-Request: `PostingKind`, `Interval`, `Take`, `IncludeCategory`, `ComparePrevious`, `CompareYear`, `CompareProjection`, `UseValutaDate`, `PostingKinds`, `AnalysisDate`, **`Filters`** (`ReportAggregatesFiltersRequest?`), `SavingsPlanPostingSource`. Besitzt einen Kompatibilitäts-Konstruktor ohne `CompareProjection`/`SavingsPlanPostingSource`.

## `ReportAggregationFilters`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregationFilters.cs`

Identische 9 Felder wie `ReportAggregatesFiltersRequest`, alle mit `= null`-Defaults. Kommentar im Code: „All collections are treated as allow-lists; null or empty = no filtering". Transport in `ReportAggregationQuery.Filters` bis `ReportAggregationService.QueryAsync`.

## `ReportAggregationQuery`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregationQuery.cs`

Service-internes Query-Objekt: `OwnerUserId` (aus `_current.UserId`, nie vom Client), `PostingKind`, `Interval`, `Take`, `IncludeCategory`, `ComparePrevious`, `CompareYear`, `CompareProjection`, `PostingKinds`, `AnalysisDate`, `UseValutaDate`, `Filters` (`ReportAggregationFilters?`), `SavingsPlanPostingSource` (alle mit Defaults ab `CompareProjection`).

## `ReportFavoriteFiltersDto`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportFavoriteFiltersDto.cs`

Persistenz-DTO der Favoriten-Filter — identische 9 Felder (ohne Defaults, positional record). Wird erzeugt in `ReportFavoriteService.ToDtoFilters` (aus `ReportFavorite.GetFilters()`) und konsumiert in `ApplyFilters` (→ `SetFilters`); außerdem in `ReportsController.CreateFavoriteAsync`/`UpdateFavoriteAsync` und `ReportDashboardViewModel.SaveFavoriteAsync`/`UpdateFavoriteAsync` aufgebaut.

## `ReportFavoriteDto`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportFavoriteDtos.cs`

Favoriten-Abbild: `Id`, `Name`, `PostingKind`, `IncludeCategory`, `Interval`, `Take`, `ComparePrevious`, `CompareYear`, `CompareProjection`, `ShowChart`, `Expandable`, `CreatedUtc`, `ModifiedUtc`, `PostingKinds`, **`Filters`** (`ReportFavoriteFiltersDto?`), `UseValutaDate`, `SavingsPlanPostingSource` (Default `PlanPostings`). `ModifiedUtc` ist vorhanden und könnte für Cache-Invalidierung herangezogen werden.

## `ReportFavoriteCreateApiRequest` / `ReportFavoriteUpdateApiRequest`
Dateien: `FinanceManager.Shared/Dtos/Reports/ReportFavoriteCreateApiRequest.cs`, `ReportFavoriteUpdateApiRequest.cs`

Klassen mit `DataAnnotations` (`Required`, `MinLength(2)`, `MaxLength(120)`, `Range`); beide enthalten `Filters` (`ReportFavoriteFiltersDto?`) sowie `UseValutaDate` und `SavingsPlanPostingSource` — Filter-Felder müssten nur im `Filters`-Record erweitert werden.

## `ReportFavoriteCreateRequest` / `ReportFavoriteUpdateRequest`
Dateien: `FinanceManager.Shared/Dtos/Reports/ReportFavoriteCreateRequest.cs`, `ReportFavoriteUpdateRequest.cs`

Service-seitige Records (mit `JsonConstructor` und Kompatibilitäts-Konstruktoren), transportieren `Filters` (`ReportFavoriteFiltersDto?`) unverändert an `IReportFavoriteService.CreateAsync`/`UpdateAsync`.

## `ReportAggregatePointDto`
Datei: `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs`

Ergebnis-DTO: `PeriodStart`, `GroupKey` (`Account:`/`Contact:`/`SavingsPlan:`/`Security:`/`Category:`/`Type:`-Schema, Security-Leafs tragen Subtyp-Suffix `Security:{id}:{SubType}`), `GroupName`, `CategoryName`, `Amount`, `ProjectionAmount`, `ParentGroupKey`, `PreviousAmount`, `YearAgoAmount`, `ProjectionExpectedDividends`, `SecuritySubType`. Dazu `ReportProjectionExpectedDividendDto` (`SecurityId`, `SecurityName`, `ExpectedDate`, `PriorYearDate`, `Amount`).

## `HomeKpiDto`
Datei: `FinanceManager.Shared/Dtos/HomeKpi/HomeKpiDto.cs`

KPI-Kachel: `Id`, `Kind`, `ReportFavoriteId`, `ReportFavoriteName`, `Title`, `PredefinedType`, `DisplayMode`, `SortOrder`, `CreatedUtc`, `ModifiedUtc`. Keine Filterfelder — der Filterbezug läuft ausschließlich über den geladenen `ReportFavoriteDto` in `HomeKpiGrid`.

## `FavKpiData` / `SeriesPoint`
Datei: `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` (Z. 653–654)

Private Records der Komponente, die den KPI-Cache-Payload bilden: `FavKpiData(Amount, Prev, Year, Series, Kind)` — wird unter `home.kpi.favorite-{k.Id}` im `IKpiLocalStorageCache` abgelegt und enthält keine Versions-/Filter-Information.
