← [Zurück zur Übersicht](index.md)

# Berichtswesen — API

## Übersicht

Die Berichtsfunktionen liegen in `ReportsController` und `HomeKpisController`.

## Endpunkte / Methoden

### `POST /api/reports/report-aggregates`

**Beschreibung:** Liefert aggregierte Reportdaten. Das Request-Feld `CompareProjection` fordert eine Hochrechnung wiederkehrender Vorjahresposten an — für alle Buchungsarten und gemischte Auswahlen (nicht für `AllHistory`). Im Ergebnis zeigt `ComparedProjection`, ob die Hochrechnung tatsächlich aktiv ist; einzelne Berichtspunkte enthalten dann `ProjectionAmount` zusätzlich zu `Amount` sowie die erwarteten Ereignisse in `ProjectionExpectedItems` (je Eintrag `Kind`, `EntityId`, `EntityName`, `ExpectedDate`, `PriorYearDate`, `Amount`).

Das `Filters`-Objekt (`ReportAggregatesFiltersRequest`) akzeptiert optional vier Freitext-Filterlisten je Buchungsart:

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `BankTextFilters` | `string[]` | Nein | Textfilter für Bank-Buchungen |
| `ContactTextFilters` | `string[]` | Nein | Textfilter für Kontakt-Buchungen |
| `SavingsPlanTextFilters` | `string[]` | Nein | Textfilter für Sparplan-Buchungen |
| `SecurityTextFilters` | `string[]` | Nein | Textfilter für Wertpapier-Buchungen |

Jeder Eintrag wird getrimmt, ist maximal 500 Zeichen lang und matcht als Substring (Groß-/Kleinschreibung egal) auf `Subject` und `Description` der Buchungen; mehrere Einträge einer Liste sind ODER-verknüpft. Einträge über 500 Zeichen beim Favoriten-Speichern führen zu `400 BadRequest`.

Das optionale Request-Feld `ReportFavoriteId` (`Guid?`) versetzt die Abfrage in den Favoritenkontext: Mit gesetztem Wert werden die für diesen Favoriten ausgeblendeten Erwartungen in der Hochrechnung berücksichtigt. Ohne das Feld bleibt die Hochrechnung unverändert — Ad-hoc-Abfragen sind von Ausblendungen nicht betroffen.

### `GET /api/report-favorites/{id}/suppressed-expectations`

**Beschreibung:** Listet die für den Favoriten ausgeblendeten Erwartungen (`ReportSuppressedExpectationDto[]` mit `Id`, `Kind`, `EntityId`, `EntityName`, `ExpectedDate`, `PriorYearDate`, `SuppressedAtUtc`). Antwortet mit `404 NotFound`, wenn der Favorit nicht existiert oder dem Benutzer nicht gehört.

### `POST /api/report-favorites/{id}/suppressed-expectations`

**Beschreibung:** Blendet einen erwarteten Posten für den Favoriten aus. Der Body (`ReportSuppressedExpectationRequest`) enthält `Kind`, `EntityId` und `PriorYearDate`; die Wirkung gilt nur für Abfragen mit diesem Favoritenkontext. Idempotent: das wiederholte Ausblenden desselben Postens liefert den vorhandenen Satz. Antwortet mit `200 OK` und `ReportSuppressedExpectationDto`, `404 NotFound` bei fremdem/fehlendem Favoriten sowie `400 BadRequest` bei ungültigen Werten.

### `DELETE /api/report-favorites/{id}/suppressed-expectations/{suppressionId}`

**Beschreibung:** Hebt eine Ausblendung auf — der Posten wird wieder wie zuvor hochgerechnet. Antwortet mit `204 NoContent` bzw. `404 NotFound`, wenn die Ausblendung nicht zum Favoriten des Benutzers gehört.

### `POST /api/report-postings`

**Beschreibung:** Liefert die Einzelposten hinter einer Betragszelle des Report-Dashboards (Posteneinsicht). Der Request (`ReportPostingsQueryRequest`) trägt den Zellkontext — `GroupKey` (`null` für die Summenzeile), `PeriodStart` (Periodenanker der Zeile) und `Period` (`Current`/`Previous`/`YearAgo`) — sowie den vollständigen Aggregationskontext (`PostingKind`/`PostingKinds`, `Interval`, `IncludeCategory`, `CompareProjection`, `UseValutaDate`, `AnalysisDate`, `SavingsPlanPostingSource`, `Take`, `CompareYear` und `Filters` identisch zu `report-aggregates`), damit der Server Fenster und Postenmenge der Zelle exakt reproduzieren kann.

**Antwort** (`ReportPostingsResult`):

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `Postings` | `ReportPostingDto[]` | Posten der Zelle (Datum, `EntityName` als Herkunft, Beschreibung, Betrag, Verknüpfungs-Ids), nach effektivem Datum absteigend |
| `TotalCount` | `int` | Gesamtzahl der Treffer vor der Kürzung |
| `TotalAmount` | `decimal` | Exakte Summe aller Treffer — unabhängig von der Kürzung |
| `Truncated` | `bool` | `true`, wenn die Liste auf das serverseitige Limit (5000 Posten) gekürzt wurde |

**Fehler:** `400 BadRequest` bei ungültigem `GroupKey`, undefinierten Enum-Werten, `Take < 0` sowie `Interval == AllHistory` mit `Period != Current`.

### `GET /api/reports/report-favorites`

**Beschreibung:** Liefert gespeicherte Favoriten inklusive der Hochrechnungsoption `CompareProjection`. Das `Filters`-Objekt (`ReportFavoriteFiltersDto`) enthält die gespeicherten `*TextFilters`-Listen.

### `POST /api/reports/report-favorites`

**Beschreibung:** Legt Favorit an. `CompareProjection` speichert die Hochrechnungsoption zusammen mit den übrigen Dashboard-Einstellungen. Das `Filters`-Objekt nimmt die vier `*TextFilters`-Listen entgegen und persistiert sie am Favoriten.

### `PUT /api/reports/report-favorites/{id}`

**Beschreibung:** Aktualisiert Favorit. `CompareProjection` wird beim Aktualisieren eines Favoriten überschrieben. `Filters = null` leert alle Filter inklusive der Textfilter.

### MCP-Tools

Die Textfilter stehen auch über MCP zur Verfügung: `fm_reporting_aggregate` akzeptiert sie als Inline-Argumente `bankTextFilters`, `contactTextFilters`, `savingsPlanTextFilters`, `securityTextFilters` (String-Arrays, je max. 500 Zeichen) und wendet gespeicherte Favoriten-Filter automatisch an; `fm_reports_configuration_manage` pflegt sie im `filters`-Objekt beim Anlegen und Aktualisieren von Favoriten.

Bei `compareProjection = true` liefert `fm_reporting_aggregate` je Berichtspunkt zusätzlich `projectionAmount` und die erwarteten Einzelereignisse als `expectedItems` (`kind`, `entityId`, `untrusted_data.entityName`, `expectedDate`, `priorYearDate`, `amount`); das Flag `comparedProjection` meldet, ob die Hochrechnung aktiv war.

### `GET /api/home-kpis`

**Beschreibung:** Liefert Home-KPI-Konfigurationen.

### `POST /api/home-kpis`

**Beschreibung:** Legt Home-KPI an.
