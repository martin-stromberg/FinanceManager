← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Datenmodell

## Entitäten

### `User`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Benutzer-ID |
| `UserName` | `string` | Loginname |
| `IsAdmin` | `bool` | Administratorstatus |
| `PreferredLanguage` | `string?` | UI-Sprache |
| `Active` | `bool` | Konto aktiv |
| `ImportSplitMode` | `ImportSplitMode` | Split-Strategie |
| `ImportMaxEntriesPerDraft` | `int` | Maximalgröße Importdraft |
| `MassImportDialogPolicy` | `MassImportDialogPolicy` | Dialogrichtlinie |

### `IpBlock`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Regel-ID |
| `AddressOrRange` | `string` | Einzel-IP oder Bereich |
| `IsBlocked` | `bool` | Aktiver Blockstatus |

### `Notification`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Benachrichtigungs-ID |
| `OwnerUserId` | `Guid?` | Zielbenutzer |
| `Title` | `string` | Titel |
| `Message` | `string` | Nachricht |
| `Target` | `NotificationTarget` | Zielbereich |
| `ScheduledDateUtc` | `DateTime` | Terminierung |
| `IsDismissed` | `bool` | Bereits geschlossen |

### `BackupRecord`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Id` | `Guid` | Backup-ID |
| `CreatedUtc` | `DateTime` | Erzeugungszeit |
| `FileName` | `string` | Dateiname |
| `SizeBytes` | `long` | Dateigröße |
