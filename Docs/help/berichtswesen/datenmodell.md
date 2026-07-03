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
| `...IdsCsv` | `string?` | Persistierte Filterlisten |

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
