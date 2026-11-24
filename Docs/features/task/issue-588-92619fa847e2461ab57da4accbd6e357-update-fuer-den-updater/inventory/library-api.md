# Konsumierte API der vendored Bibliothek `msTools.Updater` 0.10.1

Ermittelt aus `external/msTools.Updater/msTools.Updater.0.10.1.nupkg` (`lib/net10.0/msTools.Updater.xml`, 456 dokumentierte Member) und den Verwendungsstellen im Code. Aufgeführt ist nur, was FinanceManager tatsächlich nutzt.

## Von FinanceManager verwendete Typen

### Konstruktor-/Fabrikaufrufe (positionsabhängig — Bruchgefahr bei Signaturänderungen)

| Typ | Aktuelle Signatur (0.10.1) | Verwendet in |
|-----|----------------------------|--------------|
| `AutoUpdateStatusSnapshot` | `#ctor(AutoUpdateState, string, string, DateTimeOffset?, AutoUpdateCheckResult, AutoUpdateDownloadResult, AutoUpdateInstallResult, string, AutoUpdateErrorCode?, bool, DateTimeOffset?)` — 11 Parameter | `FinanceManager.Tests/Updates/UpdateStatusTestData.cs` (3× target-typed `new(...)`: Zeilen 18–29, 38–49, 57–69), `FinanceManager.Tests/Updates/UpdateStatusMapperTests.cs` (2× explizit: Zeilen 37–48, 75–86) |
| `AutoUpdateCheckResult` | `#ctor(string, AutoUpdatePackageDescriptor, string, DateTimeOffset, bool)` (5. Parameter optional bool) | `UpdateStatusTestData.cs` Z. 43, 63; `UpdateStatusMapperTests.cs` Z. 36, 74 |
| `AutoUpdateDownloadResult` | `#ctor(string, long, bool)` | `UpdateStatusTestData.cs` Z. 64 |
| `AutoUpdatePackageDescriptor` | `#ctor(string, string, string, string, Uri, string, long)` | `UpdateStatusMapperTests.cs` Z. 28, 66; `UpdateOrchestratorAdapterTests.cs` Z. 31, 381 |
| `AutoUpdateResult` | `#ctor(AutoUpdateOutcome, AutoUpdateState, string, Exception)` | `UpdateOrchestratorAdapterTests.cs` (7 Aufrufe), `UpdateControllerIntegrationTests.cs` Z. 594 |
| `InstalledReleaseInfo` | `#ctor(string, DateTimeOffset?, string, string, string)` | `UpdateOrchestratorAdapterTestFactory.cs` Z. 22; `InstalledReleaseMetadataProviderTests.cs` Z. 23; `UpdateControllerIntegrationTests.cs` Z. 621 |
| `AutoUpdateInstallResult` | `#ctor(string, string, DateTimeOffset)` (Version, ScriptPath, StartedAt) | **keine** direkte Konstruktion im Repository |
| `SourceCheckTimeRange` | Objekt-Initializer (`DayOfWeek`, `StartTime`, `EndTime`) | `AutoUpdateOptionsMapper.cs` Z. 116–127 |
| `AutoUpdateOptions` | parameterloser Ctor + Objekt-Initializer | `UpdateOrchestratorAdapterTestFactory.cs` Z. 48; `UpdateOrchestratorAdapterTests.cs` Z. 163; `AutoUpdateOptionsMapperTests.cs`; `UpdateSettingsStoreTests.cs` Z. 239 |
| `AutoUpdateGithubSource.Create(...)` | statische Fabrik | `AutoUpdateOptionsMapper.cs` Z. 55; `AutoUpdateOptionsMapperTests.cs` Z. 124 |
| `AutoUpdateLocalFolderSource` | `#ctor(string)` | `AutoUpdateOptionsMapperTests.cs` Z. 148 |
| `FileSystemAutoUpdatePackageStore` | `#ctor(IAutoUpdateEnvironment, AutoUpdateOptions, TimeProvider)` | `UpdateSettingsStoreTests.cs` Z. 240 |
| `AutoUpdateStatusService` | `#ctor(IAutoUpdateStateStore, IInstalledVersionProvider)` | `UpdateOrchestratorAdapterTestFactory.cs` Z. 23 |

### Gelesene Eigenschaften/Methoden

| Mitglied | Typ | Verwendet von |
|----------|-----|---------------|
| `AutoUpdateStatusSnapshot.State/.AvailableVersion/.LastCheckResult/.LastDownloadResult/.LastCheckedAt/.LastError/.IsLocked/.LockCreatedAt` | Properties | `UpdateStatusMapper.MapAsync`, `UpdateOrchestratorAdapter.StartInstallAsync`; Integration-Tests serialisieren Snapshots nach `status.json` (`WriteStatusAsync`, Z. 501–506) |
| `AutoUpdateStatusSnapshot.LastInstallResult` | Property | nicht gelesen (nur Konstruktor-Slot `null`) |
| `AutoUpdateStatusSnapshot.LastErrorCode` | `AutoUpdateErrorCode?` | **nirgends** ausgewertet — `UpdateErrorMessageMapper` arbeitet nur mit Nachrichtentexten |
| `AutoUpdateResult.Outcome/.Message/.Error` | Properties | `UpdateOrchestratorAdapter` Z. 119–134, 155–169 |
| `AutoUpdateError.Exception/.Message` | Properties | `UpdateOrchestratorAdapter.CreateUpdateException` Z. 268; `UpdateErrorMessageMapper.Map` Z. 23–24 |
| `AutoUpdateCheckResult.Package/.AvailableVersion/.ReleaseNotes/.PublishedAt` | Properties | `UpdateStatusMapper` Z. 47–57 |
| `AutoUpdatePackageDescriptor.Version/.Platform/.RuntimeIdentifier/.FileName/.Uri/.Sha256/.SizeBytes` | Properties | `UpdateStatusMapper` Z. 57 |
| `AutoUpdateDownloadResult.LocalPath` | Property | `UpdateStatusMapper` Z. 72 |
| `AutoUpdateOptions.Enabled/.SourceCheck(.Interval/.TimeRanges)/.ServiceName/.ExecutablePath/.DownloadPath/.HealthTimeoutSeconds/.ScheduledInstallTime/.AllowPrereleaseUpdates/.EnableAutomaticInstallation/.Source` | Properties (read/write) | `AutoUpdateOptionsMapper.ApplySettings`/`ToSettingsDto`; `UpdateOrchestratorAdapter.RunWithManualOverrideAsync` (setzt `Enabled` temporär); `TestWebApplicationFactory` (setzt `DownloadPath`); `UpdateControllerIntegrationTests.SetDownloadPath` |
| `AutoUpdateOptions.MinHealthTimeoutSeconds/.MaxHealthTimeoutSeconds` | statische Felder | `UpdateSettingsStore.Build` Z. 104 |
| `IAutoUpdateOrchestrator.GetStatusAsync/.CheckForUpdateAsync/.DownloadAsync/.InstallAsync(confirmDowntime, force, ct)` | Methoden | `UpdateOrchestratorAdapter` Z. 87, 118, 120, 154, 162 |
| `AutoUpdateStatusService.GetSnapshot()/.UpdateAsync(Func<...>, ct)` | Methoden | `UpdateOrchestratorAdapter` Z. 124, 139, 151, 174, 213; `UpdateOrchestratorAdapterTests`/`...LockAndScheduleTests`; `InstalledReleaseMetadataProvider`-Umgebung |
| `IAutoUpdatePackageStore.RootDirectory/.LockPath/.GetLockCreatedAtAsync/.IsLockStale/.DeleteLockAsync/.EnsureAsync` | Members | `UpdateOrchestratorAdapter` Z. 184–266; `UpdateSettingsStore` Z. 31, 36, 44 |
| `IAutoUpdatePlatformResolver.CurrentRuntimeIdentifier` | Property | `UpdateStatusMapper` Z. 69 |
| `IInstalledVersionProvider.GetAsync` | Methode | `InstalledReleaseMetadataProvider` Z. 31; `InstalledReleaseInfo`-Rückgabe (`.Version/.PublishedAt/.CommitSha/.Repository/.RuntimeIdentifier`) |
| `IAutoUpdateStateStore.ReadAsync` | Methode | `UpdateOrchestratorAdapterTestFactory` (Mock) |
| `IAutoUpdateEnvironment.ApplicationDirectory` | Property | `UpdateSettingsStoreTests.AutoUpdateEnvironmentAdapter` |
| `JsonFileStore.JsonOptions` / `JsonFileStore.WriteAtomicAsync` | statische Members | `UpdateSettingsStore` Z. 119, 141, 163 |
| `SourceCheckWindowEvaluator.IsWithinWindow(ranges, DateTimeOffset)` | Methode | `AutoUpdateOptionsMapperTests` Z. 173–178 |
| `AutoUpdateGithubSource` (Typvergleich + `Dispose`) | Klasse | `AutoUpdateOptionsMapper` Z. 53–60 |
| `AutoUpdateHostBuilderExtensions.UseAutoUpdate(builder, Action<AutoUpdateBuilder>)` | Extension | `ProgramExtensions.RegisterAppServices` Z. 264–267 |
| `AutoUpdateBuilder.BindConfiguration/.WithUpdateUnitName/.WithDownloadPath/.WithSourceCheck/.UseLocalFolderSource/.UseGithubSource` | Methoden | `ProgramExtensions.SetInitialConfiguration` Z. 570–595 |

### Bibliotheks-Hintergrunddienste (implizit durch `UseAutoUpdate` registriert)

`AutoUpdateCheckerService` (periodischer Quellcheck), `AutoUpdateSchedulerService` (geplante Installation — enthält den vom Issue geforderten Nachhol-Fix; interne Bibliotheksimplementierung, wird in FinanceManager nicht referenziert). In Integrationstests deaktiviert über `Updates:HostedServicesEnabled=false`.

## 0.10.1-API-Bestand der delta-relevanten Typen

### `AutoUpdateStatusSnapshot` (record)

Konstruktor mit 11 Positionsparametern (Reihenfolge wie oben): `State`, `InstalledVersion`, `AvailableVersion`, `LastCheckedAt`, `LastCheckResult`, `LastDownloadResult`, `LastInstallResult`, `LastError`, `LastErrorCode`, `IsLocked`, `LockCreatedAt`. Statische Fabrik `Idle(string)`. **Keine** `UpdateLogPath`/`ReadyToInstallAt`-Parameter in 0.10.1.

### `AutoUpdateInstallResult` (record)

Konstruktor `(Version, ScriptPath, StartedAt)` — 3 Parameter, Properties `Version`, `ScriptPath`, `StartedAt`. Kein `UpdateLogPath` in 0.10.1.

### `AutoUpdateErrorCode` (enum, 19 Member in 0.10.1)

`None`, `SourceUnavailable`, `ManifestInvalid`, `ManifestDownloadFailed`, `AssetNotFound`, `AssetSizeMismatch`, `DownloadFailed`, `HashMismatch`, `InstallationFailed`, `SourceNotConfigured`, `ConfigurationInvalid`, `UnsupportedPlatform`, `LockActive`, `NoPackageAvailable`, `PackagePersistenceFailed`, `PackageValidationFailed`, `WorkspaceRecoveryFailed`, `Canceled`, `Unknown`. `InstallationVerificationInconclusive` existiert in 0.10.1 noch nicht. Der Enum wird in FinanceManager **nicht** ausgewertet (kein Treffer für `AutoUpdateErrorCode`/`LastErrorCode` im Produktiv- oder Testcode); `EnumCoverageTests` erfasst nur Projekt-Enums, keine Bibliotheks-Enums.

### `IAutoUpdatePackageStore` (Interface, 0.10.1)

Properties: `RootDirectory`, `PendingDirectory`, `StagingDirectory`, `LockPath`, `LogPath`, `DescriptorPath`. Methoden: `ScriptPath(string)`, `PendingAssetPath(string)`, `EnsureAsync`, `GetLockCreatedAtAsync`, `TryCreateLockAsync`, `DeleteLockAsync`, `RecoverWorkspaceAsync`, `IsLockStale(DateTimeOffset)`, `PersistDescriptorAsync`, `LoadPersistedDescriptorAsync`, `DeletePersistedDescriptorAsync`. Kein `FailedMarkerFileName` in 0.10.1.

### `AutoUpdateOptions` (0.10.1)

`Enabled`, `EnableAutomaticDownload`, `DownloadPath`, `EnableAutomaticInstallation`, `AllowPrereleaseUpdates`, `Source`, `SourceCheck`, `MaxAssetBytes`, `HostedServicesEnabled`, `ScheduledInstallTime`, `ServiceName`, `ExecutablePath`, `AppPoolName`, `SiteName`, `WorkspaceUser`, `WorkspaceGroup`, `StopHostAfterScriptStart`, `HealthTimeoutSeconds`, `UpdateUnitName`; Konstanten `MinHealthTimeoutSeconds`, `MaxHealthTimeoutSeconds`, `DefaultDownloadPath`. Hinweis: `AppPoolName`/`SiteName`/`WorkspaceUser`/`WorkspaceGroup`/`StopHostAfterScriptStart`/`UpdateUnitName` und `AutoUpdateBuilder.WithIisApplicationPool` existieren bereits in 0.10.1 (die Anforderung listet sie als „neu in 0.11.0-rc.8") — sie sind in FinanceManager unbenutzt.

### `AutoUpdateBuilder` (0.10.1)

Methoden: `EnableAutomaticDownload(string)`, `EnableAutomaticInstallation`, `EnablePrereleaseUpdates`, `UseSource`, `UseGithubSource`, `UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices`, `WithUpdateUnitName`, `WithIisApplicationPool`, `WithDownloadPath`. `PreserveFile`/`MergeFile`/`ProtectFile` existieren in 0.10.1 nicht.

## Bekanntes API-Delta 0.10.1 → 0.11.0-rc.8 (laut `requirement.md`)

- `AutoUpdateStatusSnapshot`-Konstruktor erweitert um `UpdateLogPath` und `ReadyToInstallAt` → die 5 oben gelisteten Konstruktor-Aufrufstellen im Testcode sind die einzigen zu erwartenden Kompilierbrüche.
- `AutoUpdateInstallResult`-Konstruktor erweitert um `UpdateLogPath` → keine Verwendung im Repository, kein Bruch zu erwarten.
- `AutoUpdateErrorCode` erhält `InstallationVerificationInconclusive`; `IAutoUpdatePackageStore` erhält `FailedMarkerFileName` → beide werden in FinanceManager nicht ausgewertet.
- `AutoUpdateState`/`AutoUpdateOutcome` ohne neue Member → der erschöpfende `switch` in `UpdateStatusMapper.MapState` (Z. 79–91) bleibt vollständig.
