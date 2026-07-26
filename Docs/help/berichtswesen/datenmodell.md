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

### Report-Aggregation

| Modell | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `ReportAggregationQuery` | `CompareProjection` | `bool` | Fordert die Dividendenhochrechnung für gültige Wertpapierberichte an |
| `ReportAggregatesQueryRequest` | `CompareProjection` | `bool` | API-Eingabefeld für das Dashboard |
| `ReportAggregationResult` | `ComparedProjection` | `bool` | Meldet, ob die Hochrechnung im Ergebnis tatsächlich aktiv ist |
| `ReportAggregatePointDto` | `ProjectionAmount` | `decimal?` | Hochgerechneter Betrag direkt nach `Amount` |

### Favoriten-DTOs

| Modell | Eigenschaft | Typ | Beschreibung |
|--------|-------------|-----|--------------|
| `ReportFavoriteDto` | `CompareProjection` | `bool` | Gibt die gespeicherte Hochrechnungsoption zurück |
| `ReportFavoriteCreateRequest` / `ReportFavoriteUpdateRequest` | `CompareProjection` | `bool` | Persistiert die Option beim Anlegen oder Aktualisieren |
| `ReportFavoriteCreateApiRequest` / `ReportFavoriteUpdateApiRequest` | `CompareProjection` | `bool` | Transportiert die Option über die API |

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
