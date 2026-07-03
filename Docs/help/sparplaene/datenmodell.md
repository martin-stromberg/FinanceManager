← [Zurück zur Übersicht](index.md)

# Sparpläne — Datenmodell

## Entitäten

### `SavingsPlan`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Sparplan-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
| `Type` | `SavingsPlanType` | Einmalig oder wiederkehrend |
| `TargetAmount` | `decimal?` | Optionales Ziel |
| `TargetDate` | `DateTime?` | Optionales Zieldatum |
| `Interval` | `SavingsPlanInterval?` | Wiederholintervall |
| `CategoryId` | `Guid?` | Kategorie |
| `IsActive` | `bool` | Aktivstatus |
| `ArchivedUtc` | `DateTime?` | Archivzeit |

### `SavingsPlanCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Name |
