← [Zurück zur Übersicht](index.md)

# Kontakte — Datenmodell

## Entitäten

### `Contact`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kontakt-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Anzeigename |
| `Type` | `ContactType` | Kontakttyp |
| `CategoryId` | `Guid?` | Kategorie |
| `Description` | `string?` | Beschreibung |
| `IsPaymentIntermediary` | `bool` | Zahlungsvermittler-Flag |
| `SymbolAttachmentId` | `Guid?` | Optionales Symbol |

### `ContactCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Kategoriename |

### `AliasName`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Alias-ID |
| `ContactId` | `Guid` | Referenz auf Kontakt |
| `Name` | `string` | Alternativer Erkennungsname |

## Beziehungen

- Eine `ContactCategory` kann viele `Contact`-Einträge enthalten.
- Ein `Contact` kann viele `AliasName`-Einträge besitzen.
