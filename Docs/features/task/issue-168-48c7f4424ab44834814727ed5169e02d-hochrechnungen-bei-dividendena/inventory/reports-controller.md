# ReportsController

## Relevante Datei

- `FinanceManager.Web/Controllers/ReportsController.cs`

## Aggregationsendpoint

`POST /api/report-aggregates` nimmt `ReportAggregatesQueryRequest` an, validiert aktuell `Take`, liest bei Bedarf Query-String-Fallbacks fuer `postingKinds` und `analysisDate`, mappt Filter auf `ReportAggregationFilters` und erstellt `ReportAggregationQuery`.

Aktuell gibt es keine fachliche Validierung fuer Kombinationen aus Posting-Kinds und Vergleichsoptionen. Fuer die neue Hochrechnung muss der Controller mindestens das neue Request-Flag in `ReportAggregationQuery` weiterreichen. Optional kann er ungueltige Hochrechnung fuer Nicht-Security mit BadRequest ablehnen; alternativ kann der Service das Flag wirkungslos machen. Die Anforderung verlangt serverseitig, dass Hochrechnung nur bei ausschliesslich `PostingKind.Security` akzeptiert oder wirksam wird.

## Favoritenendpunkte

`POST /api/report-favorites` und `PUT /api/report-favorites/{id}` mappen API-Requests manuell auf `ReportFavoriteCreateRequest` und `ReportFavoriteUpdateRequest`. Das neue Flag muss in beiden Mapping-Ausdruecken mitgefuehrt werden.

## Fehlerbehandlung

Der Controller nutzt `ApiErrorFactory` fuer Argumentfehler und Konflikte. Wenn die Security-only-Regel als harte Validierung umgesetzt wird, sollte sie als `ArgumentException`/`ArgumentOutOfRangeException` oder explizites `BadRequest(ApiErrorDto...)` in dieses Muster passen.

## Testimplikation

Integrationstests fuer `ApiClientReportsTests` erzeugen `ReportAggregatesQueryRequest`, `ReportFavoriteCreateApiRequest` und `ReportFavoriteUpdateApiRequest`. Sie muessen je nach Default-/Property-Design angepasst oder um Assertions fuer das neue Flag erweitert werden.
