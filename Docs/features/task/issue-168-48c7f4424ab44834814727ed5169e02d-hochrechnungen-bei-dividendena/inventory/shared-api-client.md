# Shared-DTOs und API-Client

## Relevante Dateien

- `FinanceManager.Shared/Dtos/Reports/ReportAggregationQuery.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportAggregatesQueryRequest.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportAggregationResult.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportAggregatePointDto.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteDtos.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteCreateRequest.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteUpdateRequest.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteCreateApiRequest.cs`
- `FinanceManager.Shared/Dtos/Reports/ReportFavoriteUpdateApiRequest.cs`
- `FinanceManager.Shared/ApiClient.Reports.cs`

## Aggregations-DTOs

`ReportAggregationQuery` transportiert serverinterne Aggregationsparameter inklusive `ComparePrevious`, `CompareYear`, optionalen `PostingKinds`, `AnalysisDate`, `UseValutaDate` und `Filters`.

`ReportAggregatesQueryRequest` ist der API-Request fuer `/api/report-aggregates` und enthaelt dieselben UI-seitigen Kernparameter. Das neue Hochrechnungsflag muss hier aufgenommen werden, damit Blazor/API-Client es senden kann.

`ReportAggregationResult` enthaelt nur `ComparedPrevious` und `ComparedYear`. Die UI sollte nicht nur vom lokalen Toggle abhaengen, sondern vom Serverergebnis erkennen koennen, ob Projektion wirklich berechnet wurde. Dafuer bietet sich ein `ComparedProjection`/`Projected`-Flag an.

`ReportAggregatePointDto` ist ein positional record mit `Amount`, `ParentGroupKey`, `PreviousAmount`, `YearAgoAmount`. Fuer die geforderte Ausgabereihenfolge sollte `ProjectionAmount` logisch direkt nach `Amount` eingefuegt werden. Das ist eine breaking change fuer alle positional constructor-Aufrufe im Code und in Tests.

## Favoriten-DTOs

`ReportFavoriteDto`, `ReportFavoriteCreateRequest`, `ReportFavoriteUpdateRequest`, `ReportFavoriteCreateApiRequest` und `ReportFavoriteUpdateApiRequest` enthalten die persistierten Reporteinstellungen. Das neue Flag muss in allen fuenf Typen ergaenzt werden.

Die Create/Update-Records haben Convenience-Konstruktoren mit Default-`Take`. Bei einer neuen bool-Option sollte ein Default `false` erhalten bleiben, damit bestehende Tests und Call-Sites moeglichst wenig angepasst werden muessen.

## API-Client

`ApiClient.Reports.cs` ist duenn und serialisiert die DTOs direkt via `PostAsJsonAsync`/`PutAsJsonAsync`. Nach DTO-Erweiterung braucht der API-Client selbst voraussichtlich keine eigene Logik, aber alle aufrufenden Payload-Erzeugungen muessen das neue Feld setzen.

## Kompatibilitaetsrisiken

- Positional records erzeugen viele Konstruktor-Call-Sites; `ReportAggregatePointDto` ist besonders breit genutzt.
- JSON-Deserialisierung sollte mit Default `false` fuer fehlende bool-Felder kompatibel bleiben.
- Nullable `decimal? ProjectionAmount` verhindert, dass nicht berechnete Projektionen als echte 0 interpretiert werden.
