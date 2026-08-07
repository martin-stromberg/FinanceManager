# Datenmodelle

## `UpdateStatusDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

Sealed Record mit folgenden Eigenschaften:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Status` | `UpdateStatusKind` | Aktueller Zustand des Update-Systems (NoUpdate, Checking, Available, Downloading, Ready, Installing, Failed) |
| `InstalledVersion` | `string?` | Versionsnummer der installierten Release |
| `InstalledReleasePublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum der installierten Release |
| `AvailableVersion` | `string?` | Versionsnummer der verfügbaren Update |
| `CurrentPlatform` | `string` | Runtime-Identifier der aktuellen Plattform (z.B. "win-x64") |
| `LastCheckedAt` | `DateTimeOffset?` | Zeitstempel der letzten Update-Prüfung |
| `LastError` | `string?` | Fehlermeldung aus der letzten fehlgeschlagenen Operation |
| `DownloadedAssetName` | `string?` | Dateiname des heruntergeladenen Update-Pakets |
| `IsLocked` | `bool` | Gibt an, ob derzeit ein aktiver Update-Lock vorhanden ist |
| `LockCreatedAt` | `DateTimeOffset?` | Zeitstempel der Lock-Erstellung (Quelle: msTools.Updater Library) |
| `ScheduledInstallTime` | `TimeOnly?` | Geplante Installationszeit |
| `AvailableUpdate` | `UpdateMetadataDto?` | Metadaten der verfügbaren Update (Version, Release Notes, Assets) |

**Wichtig:** `IsLocked` und `LockCreatedAt` werden direkt aus `AutoUpdateStatusSnapshot` von der msTools.Updater Bibliothek übernommen (via `UpdateStatusMapper.MapAsync`).

---

## `UpdateSettingsDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

Sealed Record mit folgenden Eigenschaften:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Enabled` | `bool` | Update-System aktiviert/deaktiviert |
| `RepositoryOwner` | `string` | GitHub-Repository-Besitzer |
| `RepositoryName` | `string` | Name des GitHub-Repositories |
| `ManifestAssetName` | `string` | Dateiname des Update-Manifests im Release |
| `SourceCheckStartTime` | `TimeOnly` | Startzeit des täglichen Update-Check-Fensters |
| `SourceCheckEndTime` | `TimeOnly` | Endzeit des täglichen Update-Check-Fensters |
| `ScheduledInstallTime` | `TimeOnly?` | Geplante Installationszeit |
| `ServiceName` | `string?` | Name des Hoststänger (Windows Service / systemd) |
| `ExecutablePath` | `string?` | Pfad zur ausführbaren Anwendungsdatei |
| `WorkingDirectory` | `string` | Verzeichnis für Updates, Status und Lock-Dateien |
| `HealthTimeoutSeconds` | `int` | Timeout in Sekunden für Health-Checks nach Installation |
| `IncludePrereleases` | `bool` | Prä-Releases in Update-Suche einbeziehen |

**Persisted:** Diese Settings werden in `{WorkingDirectory}/settings.json` persistiert.

---

## `UpdateStatusKind` (Enum)
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

| Wert | Bedeutung |
|------|-----------|
| `NoUpdate` | Keine Update verfügbar oder System im Idle-Zustand |
| `Checking` | Update-Prüfung läuft |
| `Available` | Update verfügbar und bereit zum Download |
| `Downloading` | Update wird heruntergeladen |
| `Ready` | Update heruntergeladen und bereit zur Installation |
| `Installing` | Update-Installation läuft |
| `Failed` | Update-Vorgang fehlgeschlagen |

---

## `UpdateMetadataDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

Sealed Record mit:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Version` | `string` | Versionsnummer der Update |
| `ReleaseNotes` | `string?` | Freigabe-Notizen |
| `PublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum |
| `RepositoryOwner` | `string` | Repository-Besitzer |
| `RepositoryName` | `string` | Repository-Name |
| `Assets` | `IReadOnlyList<UpdateAssetDto>` | Verfügbare Dateien der Release |

---

## `InstalledReleaseMetadataDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

Sealed Record mit Metadaten der aktuell installierten Version:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Version` | `string?` | Versionsnummer |
| `PublishedAt` | `DateTimeOffset?` | Veröffentlichungsdatum |
| `CommitSha` | `string?` | Git Commit SHA |
| `Repository` | `string?` | Repository-URL |
| `RuntimeIdentifier` | `string?` | Runtime-Identifier (z.B. "win-x64") |

---

## `UpdateAssetDto`
Datei: `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

Sealed Record für einzelne Dateien einer Release:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Platform` | `string` | Plattform (z.B. "windows") |
| `RuntimeIdentifier` | `string` | Runtime-Identifier |
| `AssetName` | `string` | Dateiname |
| `AssetUrl` | `string` | Download-URL |
| `Sha256` | `string` | SHA256-Checksumme für Integrität |
| `SizeBytes` | `long` | Dateigröße in Bytes |
