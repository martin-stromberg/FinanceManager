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
| `CompareProjection` | `bool` | Speichert die Hochrechnungsoption |
| `...IdsCsv` | `string?` | Persistierte Filterlisten |
| `BankTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Bank-Buchungen |
| `ContactTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Kontakt-Buchungen |
| `SavingsPlanTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Sparplan-Buchungen |
| `SecurityTextFiltersJson` | `string?` | JSON-serialisierte Textfilter für Wertpapier-Buchungen |

### `ReportSuppressedExpectation`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Ausblendungs-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `ReportFavoriteId` | `Guid` | Geltungsbereich: Referenz auf `ReportFavorite` (Cascade-Delete) |
| `Kind` | `PostingKind` | Buchungsart der Entität |
| `EntityId` | `Guid` | Referenzierte Entität (Konto, Kontakt, Sparplan oder Wertpapier) |
| `PriorYearDate` | `DateTime` | Vorjahresdatum der ausgeblendeten Erwartung (Datum ohne Uhrzeit) |
| `SuppressedAtUtc` | `DateTime` | Zeitpunkt der Ausblendung (UTC) |

Einzigartig über (`OwnerUserId`, `ReportFavoriteId`, `Kind`, `EntityId`, `PriorYearDate`); der Betrag ist nicht Teil der Identität.

### Report-Aggregation

| Modell | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `ReportAggregationQuery` | `CompareProjection` | `bool` | Fordert die Hochrechnung für die ausgewählten Buchungsarten an (nicht für `AllHistory`) |
| `ReportAggregationQuery` | `ReportFavoriteId` | `Guid?` | Favoritenkontext: aktiviert die Berücksichtigung ausgeblendeter Erwartungen |
| `ReportAggregatesQueryRequest` | `CompareProjection` | `bool` | API-Eingabefeld für das Dashboard |
| `ReportAggregatesQueryRequest` | `ReportFavoriteId` | `Guid?` | Optionales API-Feld; ohne Angabe bleiben Ausblendungen wirkungslos |
| `ReportAggregatesFiltersRequest` / `ReportAggregationFilters` | `BankTextFilters`, `ContactTextFilters`, `SavingsPlanTextFilters`, `SecurityTextFilters` | `IReadOnlyCollection<string>?` | Optionale Freitext-Filterlisten je Buchungsart (`Subject`/`Description`) |
| `ReportAggregationResult` | `ComparedProjection` | `bool` | Meldet, ob die Hochrechnung im Ergebnis tatsächlich aktiv ist |
| `ReportAggregatePointDto` | `ProjectionAmount` | `decimal?` | Hochgerechneter Betrag direkt nach `Amount` |
| `ReportAggregatePointDto` | `ProjectionExpectedItems` | `IReadOnlyList<ReportProjectionExpectedItemDto>?` | Erwartete Ereignisse hinter dem Hochrechnungswert |
| `ReportProjectionExpectedItemDto` | `Kind`, `EntityId`, `EntityName`, `ExpectedDate`, `PriorYearDate`, `Amount` | `record` | Ein erwartetes Ereignis einer Entität (Buchungsart, Entität, erwarteter Termin, Vorjahrstermin, Betrag) |
| `ReportSuppressedExpectationRequest` | `Kind`, `EntityId`, `PriorYearDate` | `record` | Identität einer auszublendenden Erwartung (ohne Betrag) |
| `ReportSuppressedExpectationDto` | `Id`, `Kind`, `EntityId`, `EntityName`, `ExpectedDate`, `PriorYearDate`, `SuppressedAtUtc` | `record` | Ausgeblendete Erwartung eines Favoriten |

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
