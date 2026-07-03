← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Datenmodell

## Entitäten

### `Security`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Wertpapier-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
| `Identifier` | `string` | Kennung (z. B. ISIN/WKN) |
| `AlphaVantageCode` | `string?` | Externer Kurscode |
| `CurrencyCode` | `string` | Währung |
| `CategoryId` | `Guid?` | Kategorie |
| `HasPriceError` | `bool` | Preisfehler-Flag |
| `SymbolAttachmentId` | `Guid?` | Symbol |

### `SecurityPrice`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kurs-ID |
| `SecurityId` | `Guid` | Wertpapierreferenz |
| `Date` | `DateTime` | Kursdatum |
| `Close` | `decimal` | Schlusskurs |
| `CreatedUtc` | `DateTime` | Erzeugungszeit |

### `SecurityCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Kategoriename |

## Beziehungen

- Ein `Security` hat viele `SecurityPrice`-Einträge.
- Eine `SecurityCategory` kann vielen Wertpapieren zugeordnet sein.
