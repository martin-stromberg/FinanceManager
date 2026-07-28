# Datenmodelle und DTOs

## DTOs (Data Transfer Objects)

### `UpdateStatusKind` (Enum)
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateStatusKind.cs`

| Wert | Bedeutung |
|------|-----------|
| NotStarted | Update-Prozess nicht gestartet |
| CheckingForUpdate | Prüfung auf neue Version läuft |
| UpdateAvailable | Neue Version verfügbar |
| UpdateNotAvailable | Keine neue Version vorhanden |
| Downloading | Download läuft |
| DownloadCompleted | Download abgeschlossen |
| Installing | Installation läuft |
| Success | Installation erfolgreich |
| Failed | Fehler während des Prozesses |
| Locked | Update durch Lock blockiert |
| Disabled | Update-System deaktiviert |

### `UpdateStatusDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateStatusDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| CurrentStatus | UpdateStatusKind | Aktueller Zustand des Update-Prozesses |
| InstalledVersion | string | Aktuell installierte Versionsnummer |
| AvailableVersion | string | Verfügbare neue Versionsnummer (null wenn keine) |
| LastCheckTime | DateTime? | Zeitpunkt der letzten Versionsprüfung |
| LastErrorMessage | string | Fehlermeldung aus dem letzten fehlgeschlagenen Versuch |
| IsUpdateAvailable | bool | Flag: Ist Update verfügbar? |
| DownloadProgress | int | Fortschritt des Downloads in Prozent (0–100) |
| InstallationProgress | int | Fortschritt der Installation in Prozent (0–100) |

### `UpdateCheckResultDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateCheckResultDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Success | bool | Prüfung erfolgreich? |
| CurrentVersion | string | Aktuell installierte Version |
| NewVersionAvailable | bool | Gibt es eine neue Version? |
| AvailableVersion | string | Verfügbare Versionsnummer (null wenn keine) |
| Metadata | UpdateMetadataDto | Metadaten der verfügbaren Version |
| Error | string | Fehlermeldung (falls Success = false) |

### `UpdateMetadataDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateMetadataDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Version | string | Versionsnummer |
| ReleaseDate | DateTime | Veröffentlichungsdatum |
| Changelog | string | Änderungsprotokoll für diese Version |
| Assets | List<UpdateAssetDto> | Liste der zum Download verfügbaren Assets |
| IsPreRelease | bool | Ist dies eine Vorabversion? |

### `UpdateAssetDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateAssetDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Name | string | Name des Assets (z.B. Paketdatei) |
| Url | string | Download-URL |
| Size | long | Größe in Bytes |
| ContentType | string | MIME-Type |
| Checksum | string | SHA256-Checksum zur Validierung |
| ChecksumAlgorithm | string | Checksummen-Algorithmus (z.B. "SHA256") |

### `UpdateDownloadResultDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateDownloadResultDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Success | bool | Download erfolgreich? |
| Version | string | Version, die heruntergeladen wurde |
| LocalPath | string | Lokaler Pfad der heruntergeladenen Datei |
| FileSize | long | Größe der heruntergeladenen Datei |
| ChecksumValid | bool | Checksumme validiert? |
| Error | string | Fehlermeldung (falls Success = false) |

### `UpdateInstallResultDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateInstallResultDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Success | bool | Installation erfolgreich? |
| Version | string | Version, die installiert wurde |
| InstalledAt | DateTime | Zeitpunkt der Installation |
| RequiresRestart | bool | Benötigt der Service/die App einen Neustart? |
| Error | string | Fehlermeldung (falls Success = false) |

### `UpdateSettingsDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateSettingsDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Enabled | bool | Update-System aktiviert? |
| AutomaticCheckEnabled | bool | Automatische Versionsprüfung aktivieren? |
| CheckInterval | int | Intervall für Versionsprüfungen (Minuten) |
| AutomaticDownloadEnabled | bool | Automatisches Herunterladen aktivieren? |
| AutomaticInstallEnabled | bool | Automatische Installation aktivieren? |
| MaxAssetSize | long | Maximale Größe von Update-Assets (Bytes) |

### `UpdateScheduleRequest`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateScheduleRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| ScheduledTime | DateTime | Geplante Zeit für Installation |
| ConfirmDowntime | bool | Bestätigung für Service-Ausfallzeit |

### `UpdateStartRequest`
Datei: `src/FinanceManager.Shared/Dtos/Update/UpdateStartRequest.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Version | string | Version zu installieren |
| ConfirmDowntime | bool | Bestätigung für Service-Ausfallzeit |
| ForceInstall | bool | Installation auch bei Locking erzwingen? |

### `InstalledReleaseMetadataDto`
Datei: `src/FinanceManager.Shared/Dtos/Update/InstalledReleaseMetadataDto.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Version | string | Installierte Versionsnummer |
| InstalledAt | DateTime | Installations-Datum und -Zeit |
| InstalledBy | string | Benutzer oder System, das installiert hat |

## Konfigurationsmodelle

### `UpdateOptions`
Datei: `src/FinanceManager.Web/Options/UpdateOptions.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Enabled | bool | Update-System aktiviert? |
| ManifestUrl | string | URL des Update-Manifests (GitHub Releases) |
| DownloadPath | string | Lokales Verzeichnis für Downloads |
| MaxAssetBytes | long | Maximale Größe für Update-Assets |
| CheckIntervalMinutes | int | Intervall für automatische Versionsprüfungen (Minuten) |
| InstallationWindowStartHour | int | Tageszeit für automatische Installation (Stunde) |
| InstallationWindowEndHour | int | Ende der Installation-Tageszeit (Stunde) |
| HostedServicesEnabled | bool | Background-Services aktiviert? |

### `UpdateSettings` (Persistiert)
Datei: `src/FinanceManager.Web/Services/Updates/Core/UpdateSettings.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Enabled | bool | Update-System aktiviert? |
| AutomaticCheckEnabled | bool | Automatische Prüfung aktiviert? |
| AutomaticDownloadEnabled | bool | Automatisches Herunterladen aktiviert? |
| AutomaticInstallEnabled | bool | Automatische Installation aktiviert? |

## Metadata und Status-Verwaltung

### `InstalledReleaseMetadata`
Datei: `src/FinanceManager.Web/Services/Updates/Metadata/InstalledReleaseMetadata.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Version | string | Versionsnummer der installierten Version |
| InstalledAt | DateTime | Zeitpunkt der Installation |

**Hinweis:** Diese Klasse wird verwendet, um aktuell installierte Versionsinformationen zu speichern und zu lesen.
