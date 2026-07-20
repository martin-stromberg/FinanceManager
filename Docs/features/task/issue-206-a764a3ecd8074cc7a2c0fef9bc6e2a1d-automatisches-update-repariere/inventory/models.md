# Bestandsaufnahme Datenmodelle

## `UpdateStatusDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Status` | `UpdateStatusKind` | Aktueller Update-Status (NoUpdate, Checking, Available, Downloading, Ready, Installing, Failed) |
| `InstalledVersion` | `string?` | Versionsnummer der aktuell installierten Anwendung |
| `InstalledReleasePublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum der installierten Version |
| `AvailableVersion` | `string?` | Versionsnummer des verfügbaren Updates |
| `CurrentPlatform` | `string` | Aktuelle Plattform (z.B. "windows", "linux") |
| `LastCheckedAt` | `DateTimeOffset?` | Zeitpunkt der letzten Update-Prüfung |
| `LastError` | `string?` | Fehlermeldung bei fehlgeschlagener Operation |
| `DownloadedAssetName` | `string?` | Dateiname des heruntergeladenen Update-Pakets |
| `IsLocked` | `bool` | Indikator, ob ein Update-Lock aktiv ist |
| `LockCreatedAt` | `DateTimeOffset?` | Zeitpunkt der Lock-Erstellung |
| `ScheduledInstallTime` | `TimeOnly?` | Geplante Uhrzeit für automatische Installation |
| `AvailableUpdate` | `UpdateMetadataDto?` | Vollständige Metadaten des verfügbaren Updates |

## `UpdateSettingsDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Enabled` | `bool` | Aktivierung des automatischen Update-Systems |
| `CheckIntervalMinutes` | `int` | Interval für regelmäßige Update-Prüfungen (Minuten) |
| `RepositoryOwner` | `string` | GitHub Repository-Besitzer |
| `RepositoryName` | `string` | GitHub Repository-Name |
| `ManifestAssetName` | `string` | Name der Update-Manifest-Datei im Release (z.B. "update.json") |
| `ScheduledInstallTime` | `TimeOnly?` | Geplante Uhrzeit für automatische Installation |
| `ServiceName` | `string?` | Name des systemd-Service (Linux) oder Windows-Service |
| `ExecutablePath` | `string?` | Pfad zur Executable für direkten Start (Windows) |
| `WorkingDirectory` | `string` | Arbeitsverzeichnis für Update-Dateien und Lock |
| `HealthTimeoutSeconds` | `int` | Timeout in Sekunden für Health-Check nach Installation |

## `UpdateMetadataDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Version` | `string` | Versionsnummer des Updates |
| `ReleaseNotes` | `string?` | Release Notes des Updates |
| `PublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum des Updates |
| `RepositoryOwner` | `string` | GitHub Repository-Besitzer (für Validierung) |
| `RepositoryName` | `string` | GitHub Repository-Name (für Validierung) |
| `Assets` | `IReadOnlyList<UpdateAssetDto>` | Liste der verfügbaren Binärdateien für verschiedene Plattformen |

## `UpdateAssetDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Platform` | `string` | Plattform ("windows" oder "linux") |
| `RuntimeIdentifier` | `string` | Runtime-Identifier (z.B. "win-x64", "linux-x64") |
| `AssetName` | `string` | Dateiname des Assets (z.B. "FinanceManager-v1.2.3-win-x64.zip") |
| `AssetUrl` | `string` | HTTPS-URL zum GitHub Release Asset |
| `Sha256` | `string` | SHA-256 Hash des Assets für Integritätsprüfung |
| `SizeBytes` | `long` | Größe des Assets in Bytes |

## `InstalledReleaseMetadataDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Version` | `string?` | Versionsnummer der installierten Release |
| `PublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum der installierten Release |
| `CommitSha` | `string?` | Git-Commit-SHA (optional) |
| `Repository` | `string?` | Repository-URL (optional) |
| `RuntimeIdentifier` | `string?` | Runtime-Identifier (optional) |

## `UpdateSettingsUpdateRequest`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Enabled` | `bool` | Aktivierung des automatischen Update-Systems |
| `CheckIntervalMinutes` | `int` | Interval für regelmäßige Update-Prüfungen |
| `RepositoryOwner` | `string?` | GitHub Repository-Besitzer (nullable für Request) |
| `RepositoryName` | `string?` | GitHub Repository-Name (nullable für Request) |
| `ManifestAssetName` | `string?` | Name der Update-Manifest-Datei (nullable für Request) |
| `ScheduledInstallTime` | `TimeOnly?` | Geplante Uhrzeit für automatische Installation |
| `ServiceName` | `string?` | Name des systemd-Service oder Windows-Service |
| `ExecutablePath` | `string?` | Pfad zur Executable |
| `WorkingDirectory` | `string?` | Arbeitsverzeichnis (nullable für Request) |
| `HealthTimeoutSeconds` | `int` | Timeout für Health-Check nach Installation |

## `UpdateCheckResultDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `UpdateAvailable` | `bool` | Indikator, ob ein Update verfügbar ist |
| `Status` | `UpdateStatusDto` | Aktueller Update-Status |
| `Message` | `string?` | Informations- oder Fehlermeldung |

## Request DTOs

### `UpdateScheduleRequest`
- `ScheduledInstallTime` (`TimeOnly?`): Geplante Uhrzeit für automatische Installation

### `UpdateStartRequest`
- `ConfirmDowntime` (`bool`): Bestätigung, dass Downtime akzeptiert wird

### `UpdateLockResetRequest`
- `Reason` (`string?`): Grund für das manuelles Lock-Reset

## `UpdateOptions` (Konfigurationsklasse)
Datei: `FinanceManager.Web/Services/Updates/UpdateOptions.cs`

| Eigenschaft | Typ | Standardwert | Beschreibung |
|-------------|-----|-------------|-------------|
| `Enabled` | `bool` | (Konfiguration) | Aktivierung des Update-Systems |
| `CheckIntervalMinutes` | `int` | 360 | Interval für regelmäßige Prüfungen |
| `RepositoryOwner` | `string` | "martin-stromberg" | GitHub Repository-Besitzer |
| `RepositoryName` | `string` | "FinanceManager" | GitHub Repository-Name |
| `ManifestAssetName` | `string` | "update.json" | Name der Manifest-Datei |
| `WorkingDirectory` | `string` | "updates" | Arbeitsverzeichnis |
| `HealthTimeoutSeconds` | `int` | 120 | Timeout für Health-Check |
| `MaxAssetBytes` | `long` | 512 MB | Maximale Größe eines Download-Assets |
| `HostedServicesEnabled` | `bool` | true | Aktivierung von Background Services |
| `ServiceName` | `string?` | null | Name des Service (optional) |
| `ExecutablePath` | `string?` | null | Executable-Pfad (optional) |
