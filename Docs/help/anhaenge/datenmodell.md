← [Zurück zur Übersicht](index.md)

# Anhänge — Datenmodell

## Entitäten

### `Attachment`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Anhang-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `EntityKind` | `AttachmentEntityKind` | Zielentitätstyp |
| `EntityId` | `Guid` | Zielentität |
| `FileName` | `string` | Dateiname |
| `ContentType` | `string` | MIME-Type |
| `SizeBytes` | `long` | Dateigröße |
| `CategoryId` | `Guid?` | Kategorie |
| `Url` | `string?` | URL-Anhang |
| `Role` | `AttachmentRole` | Rolle (`Regular`, `Symbol`) |

### `AttachmentCategory`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Kategorie-ID |
| `OwnerUserId` | `Guid` | Eigentümer |
| `Name` | `string` | Kategoriename |
