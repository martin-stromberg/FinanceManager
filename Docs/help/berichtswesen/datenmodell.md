← [Zurück zur Übersicht](index.md)

# Berichtswesen — Datenmodell

## Entitäten

### `ReportFavorite`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Favorit-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name des Favoriten |
| `PostingKind` | `PostingKind` | Primäre Buchungsart |
| `Interval` | `ReportInterval` | Aggregationsintervall |
| `Take` | `int` | Anzahl Perioden |
| `UseValutaDate` | `bool` | Aggregation nach Valuta |
| `CompareProjection` | `bool` | Speichert die Hochrechnungsoption für Dividendenanalysen |
| `...IdsCsv` | `string?` | Persistierte Filterlisten |
| `BankTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Bank-Buchungen |
| `ContactTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Kontakt-Buchungen |
| `SavingsPlanTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Sparplan-Buchungen |
| `SecurityTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Wertpapier-Buchungen |

### Report-Aggregation

| Modell | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `ReportAggregationQuery` | `CompareProjection` | `bool` | Fordert die Dividendenhochrechnung für gültige Wertpapierberichte an |
| `ReportAggregatesQueryRequest` | `CompareProjection` | `bool` | API-Eingabefeld für das Dashboard |
| `ReportAggregatesFiltersRequest` / `ReportAggregationFilters` | `BankTextFilters`, `ContactTextFilters`, `SavingsPlanTextFilters`, `SecurityTextFilters` | `IReadOnlyCollection<string>?` | Optionale Freitext-Filterlisten je Buchungsart (`Subject`/`Description`) |
| `ReportAggregationResult` | `ComparedProjection` | `bool` | Meldet, ob die Hochrechnung im Ergebnis tatsächlich aktiv ist |
| `ReportAggregatePointDto` | `ProjectionAmount` | `decimal?` | Hochgerechneter Betrag direkt nach `Amount` |

### Posteneinsicht (Drill-down)

| Modell | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `ReportPostingsPeriod` | `Current` / `Previous` / `YearAgo` | `enum` | Wertespalte, auf die sich die Postenabfrage bezieht |
| `ReportPostingsQueryRequest` | `GroupKey`, `PeriodStart`, `Period`, `Take`, `CompareYear`, `Filters`, … | `record` | Zellkontext plus vollständiger Aggregationskontext; `Take`/`CompareYear` steuern das Ladefenster des Dividenden-Nettings |
| `ReportPostingsQuery` | `OwnerUserId` | `Guid` | Interne Query; Besitzer aus dem Auth-Kontext |
| `ReportPostingDto` | `BookingDate`, `ValutaDate`, `Amount`, `Subject`, `RecipientName`, `Description`, `EntityName`, `SecuritySubType`, Verknüpfungs-Ids | `record` | Zeile der Postenliste; `EntityName` = Name der referenzierten Entität |
| `ReportPostingsResult` | `Postings`, `TotalCount`, `TotalAmount`, `Truncated` | `record` | Ergebnis der Postenabfrage; `TotalAmount` ist die exakte Serversumme aller Treffer |

### Favoriten-DTOs

| Modell | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `ReportFavoriteDto` | `CompareProjection` | `bool` | Gibt die gespeicherte Hochrechnungsoption zurück |
| `ReportFavoriteCreateRequest` / `ReportFavoriteUpdateRequest` | `CompareProjection` | `bool` | Persistiert die Option beim Anlegen oder Aktualisieren |
| `ReportFavoriteCreateApiRequest` / `ReportFavoriteUpdateApiRequest` | `CompareProjection` | `bool` | Transportiert die Option über die API |
| `ReportFavoriteFiltersDto` | `BankTextFilters`, `ContactTextFilters`, `SavingsPlanTextFilters`, `SecurityTextFilters` | `IReadOnlyCollection<string>?` | Persistierte Textfilterlisten des Favoriten |
| `ReportFavoriteBackupDto` | `...TextFiltersJson` | `string?` | Backup-Felder der Textfilter (Defaults `null`, Alt-Backups bleiben ladbar) |
| `FavKpiData` (KPI-Cache) | `FavoriteModifiedUtc` | `DateTime` | Versionsmarke des Favoriten für die Cache-Invalidierung auf der Startseite |

### `HomeKpi`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | KPI-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Title` | `string` | Anzeigename |
| `Settings` | strukturiert | KPI-Konfiguration |

### `ReportCacheEntry`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Cache-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Key` | `string` | Cache-Schlüssel |
| `Payload` | `string` | Serialisierte Daten |
