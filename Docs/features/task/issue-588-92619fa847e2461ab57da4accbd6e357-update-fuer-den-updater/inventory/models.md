# Datenmodelle — Update-Subsystem

## `UpdateOptions`

Datei: `FinanceManager.Web/Services/Updates/UpdateOptions.cs` — Host-seitige Bindung des `Updates`-Abschnitts (nur FinanceManager-eigene Felder; runtime-mutable Werte bindet die Bibliothek selbst auf `AutoUpdateOptions`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SectionName` | `const string = "Updates"` | Konfigurationsabschnitt |
| `SourceCheckStartTime` | `TimeOnly` | Fensterbeginn automatischer Checks (Default 20:00) |
| `SourceCheckEndTime` | `TimeOnly` | Fensterende (Default 06:00) |
| `RepositoryOwner` | `string` | GitHub-Owner (Default `martin-stromberg`) |
| `RepositoryName` | `string` | GitHub-Repo (Default `FinanceManager`) |
| `ManifestAssetName` | `string` | Manifest-Dateiname (Default `update.json`) |
| `WorkingDirectory` | `string` | Wurzelverzeichnis für Pakete/Status/Lock (Default `updates`) |
| `SourceType` | `string` | `Github` oder `LocalFolder` (Default `Github`) |
| `LocalFolderPath` | `string?` | Pfad der lokalen Quelle bei `SourceType=LocalFolder` |

## DTOs (`FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`)

Unverändert im Scope; aufgeführt, da `UpdateStatusMapper`/`UpdateOrchestratorAdapter` sie erzeugen.

| Record | Felder |
|--------|--------|
| `UpdateMetadataDto` | `Version`, `ReleaseNotes`, `PublishedAt`, `RepositoryOwner`, `RepositoryName`, `Assets` |
| `UpdateAssetDto` | `Platform`, `RuntimeIdentifier`, `AssetName`, `AssetUrl`, `Sha256`, `SizeBytes` |
| `InstalledReleaseMetadataDto` | `Version`, `PublishedAt`, `CommitSha`, `Repository`, `RuntimeIdentifier` |
| `UpdateStatusDto` | `Status`, `InstalledVersion`, `InstalledReleasePublishedAt`, `AvailableVersion`, `CurrentPlatform`, `LastCheckedAt`, `LastError`, `DownloadedAssetName`, `IsLocked`, `LockCreatedAt`, `ScheduledInstallTime`, `AvailableUpdate` |
| `UpdateSettingsDto` | `Enabled`, `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `SourceCheckStartTime`, `SourceCheckEndTime`, `ScheduledInstallTime`, `ServiceName`, `ExecutablePath`, `WorkingDirectory`, `HealthTimeoutSeconds`, `IncludePrereleases` |
| `UpdateSettingsUpdateRequest` | wie `UpdateSettingsDto`, Referenzfelder nullable |
| `UpdateScheduleRequest` | `ScheduledInstallTime` |
| `UpdateStartRequest` | `ConfirmDowntime` |
| `UpdateLockResetRequest` | `Reason` |
| `UpdateCheckResultDto` | `UpdateAvailable`, `Status`, `Message` |

## `UpdateLockResetException`

Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs` — `sealed class : IOException`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `UpdateLockResetFailureKind` | klassifizierter Fehlschlaggrund |
| `FailureSource` | `UpdateLockResetFailureSource` | Erkennungsort (`FinanceManager`/`Updater`) |
| `LockCreatedAt` | `DateTimeOffset?` | Lock-Zeitstempel |
| `LockPath` | `string?` | Lock-Dateipfad (aus `IAutoUpdatePackageStore.LockPath`) |

## Persistenz-Artefakte des Update-Subsystems (Dateien, keine Klassen)

| Datei | Erzeuger | Inhalt |
|-------|----------|--------|
| `settings.json` | `UpdateSettingsStore` unter `packageStore.RootDirectory` | `PersistedUpdateSettingsDto` bzw. lesbar `LegacyUpdateSettingsDto` |
| `status.json` | `FileSystemAutoUpdateStateStore` (Bibliothek) | serialisierter `AutoUpdateStatusSnapshot`; Tests schreiben ihn via `WriteStatusAsync` vor |
| `update.lock` | `IAutoUpdatePackageStore.TryCreateLockAsync` (Bibliothek) | Lock-Zeitstempel; Integrationstests legen ihn manuell an |
