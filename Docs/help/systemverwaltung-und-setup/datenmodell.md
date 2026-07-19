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
| `AlphaVantageApiKey` | `string?` | Geschuetzter AlphaVantage API Key mit `dp:v1:`-Praefix; Altwerte ohne Praefix werden beim naechsten erfolgreichen Lesen automatisch geschuetzt |
| `ShareAlphaVantageApiKey` | `bool` | Gibt an, ob ein Administrator seinen AlphaVantage API Key als Fallback fuer andere Benutzer freigibt |
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

## Self-Update-DTOs

Die Self-Update-Funktion persistiert ihre Betriebsdaten dateibasiert im
konfigurierten Update-Arbeitsverzeichnis und schreibt keine neuen
Datenbankentitaeten.

### `UpdateSettingsDto`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Enabled` | `bool` | Aktiviert die periodische Updatepruefung |
| `CheckIntervalMinutes` | `int` | Intervall des `UpdateChecker` in Minuten |
| `RepositoryOwner` / `RepositoryName` | `string` | GitHub-Repository der Updatequelle |
| `ManifestAssetName` | `string` | Name des Manifest-Assets, standardmaessig `update.json` |
| `ScheduledInstallTime` | `TimeOnly?` | Optionale lokale Uhrzeit fuer geplante Installation |
| `WindowsServiceName` / `LinuxServiceName` | `string?` | Optionale Service-Overrides fuer produktive Installationen |
| `ExecutablePath` | `string?` | Windows-EXE-Fallback ohne Service |
| `WorkingDirectory` | `string` | Verzeichnis fuer Status, Pending-Paket, Staging, Lock und Skripte |
| `HealthTimeoutSeconds` | `int` | Timeout der Warteseite beim Health-Polling |

### `UpdateStatusDto`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Status` | `UpdateStatusKind` | Statuswert `NoUpdate`, `Checking`, `Available`, `Downloading`, `Ready`, `Installing` oder `Failed` |
| `InstalledVersion` | `string?` | Version aus `release-metadata.json`; ohne Datei `null` |
| `InstalledReleasePublishedAt` | `DateTimeOffset?` | Veroeffentlichungszeitpunkt der installierten Version |
| `AvailableVersion` | `string?` | Neuere Version aus dem Manifest |
| `CurrentPlatform` | `string` | Aktuelle Runtime, z. B. `win-x64` oder `linux-x64` |
| `LastCheckedAt` | `DateTimeOffset?` | Zeitpunkt der letzten Pruefung |
| `LastError` | `string?` | Letzter fachlicher oder technischer Updatefehler |
| `DownloadedAssetName` | `string?` | Name des vorbereiteten ZIP-Pakets |
| `IsLocked` | `bool` | Gibt an, ob eine Lock-Datei existiert |
| `LockCreatedAt` | `DateTimeOffset?` | Erzeugungszeitpunkt der Lock-Datei |
| `ScheduledInstallTime` | `TimeOnly?` | Gespeicherte geplante Installationszeit |
| `AvailableUpdate` | `UpdateMetadataDto?` | Manifestdaten des verfuegbaren Updates |

### `UpdateMetadataDto` und `UpdateAssetDto`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Version` | `string` | Release-Version |
| `ReleaseNotes` | `string?` | Release Notes aus GitHub |
| `PublishedAt` | `DateTimeOffset?` | Veroeffentlichungszeitpunkt |
| `RepositoryOwner` / `RepositoryName` | `string` | Repository, zu dem das Manifest gehoert |
| `Assets` | `IReadOnlyList<UpdateAssetDto>` | Plattformpakete des Releases |
| `UpdateAssetDto.Platform` | `string` | Plattform, z. B. `windows` oder `linux` |
| `UpdateAssetDto.RuntimeIdentifier` | `string` | Runtime Identifier, z. B. `win-x64` oder `linux-x64` |
| `UpdateAssetDto.AssetName` | `string` | Erwarteter ZIP-Dateiname |
| `UpdateAssetDto.AssetUrl` | `string` | HTTPS-GitHub-Release-URL |
| `UpdateAssetDto.Sha256` | `string` | SHA-256 des ZIP-Pakets |
| `UpdateAssetDto.SizeBytes` | `long` | ZIP-Groesse in Bytes |
