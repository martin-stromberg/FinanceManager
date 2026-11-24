# Interfaces — Update-Subsystem

## Projekt-Interfaces

### `IUpdateOrchestrator`

Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs` (Z. 72–129) — implementiert von `UpdateOrchestratorAdapter`; konsumiert von `UpdateController` und in Integrationstests gemockt (`ThrowingUpdateOrchestrator`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetStatusAsync` | `ct` | `Task<UpdateStatusDto>` | aktueller Update-Status |
| `GetSettingsAsync` | `ct` | `Task<UpdateSettingsDto>` | aktuelle Einstellungen |
| `SaveSettingsAsync` | `request`, `ct` | `Task<UpdateSettingsDto>` | Einstellungen speichern |
| `ScheduleAsync` | `scheduledInstallTime`, `ct` | `Task<UpdateSettingsDto>` | Installationszeitpunkt setzen/löschen |
| `CheckAsync` | `ct` | `Task<UpdateCheckResultDto>` | manueller Quellcheck |
| `StartInstallAsync` | `confirmDowntime`, `ct` | `Task<UpdateStatusDto>` | manuelle Installation |
| `ResetLockAsync` | `reason`, `ct` | `Task` | stale Lock zurücksetzen (wirft `UpdateLockResetException`) |

### `IUpdateSettingsStore`

Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs` (Z. 8–39) — implementiert von `UpdateSettingsStore`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `ct` | `Task<UpdateSettingsDto>` | Einstellungen lesen (Defaults bei Erstzugriff) |
| `SaveAsync` | `request`, `ct` | `Task<UpdateSettingsDto>` | Einstellungen speichern |
| `SaveScheduleAsync` | `scheduledInstallTime`, `ct` | `Task<UpdateSettingsDto>` | nur Zeitplan ändern |
| `ApplyToOptions` | `settings` | `void` | Settings auf `AutoUpdateOptions` anwenden |

### `IInstalledReleaseMetadataProvider`

Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs` (Z. 44–52) — implementiert von `InstalledReleaseMetadataProvider`; injiziert in `UpdateStatusMapper` und `LoginStatus.razor`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `ct` | `Task<InstalledReleaseMetadataDto>` | Metadaten der installierten Release |

### `IUpdateServiceCatalog`

Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs` (Z. 56–66) — implementiert von `DefaultUpdateServiceCatalog`; konsumiert von `UpdateController.Services`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListServiceNamesAsync` | `query`, `take`, `ct` | `Task<IReadOnlyList<string>>` | OS-Service-Namen für Autocomplete |

## Konsumierte Bibliotheks-Interfaces (`msTools.Updater` 0.10.1)

| Interface | Members im Einsatz | Verwendet von |
|-----------|--------------------|---------------|
| `IAutoUpdateOrchestrator` | `GetStatusAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync(bool,bool,ct)` (`RunUpdateAsync` nicht genutzt) | `UpdateOrchestratorAdapter`; in Integrationstests ersetzt (`SucceedingAutoUpdateOrchestrator`) |
| `IAutoUpdatePackageStore` | `RootDirectory`, `LockPath`, `EnsureAsync`, `GetLockCreatedAtAsync`, `IsLockStale`, `DeleteLockAsync` | `UpdateOrchestratorAdapter`, `UpdateSettingsStore`; in Unit-Tests gemockt |
| `IAutoUpdatePlatformResolver` | `CurrentRuntimeIdentifier` | `UpdateStatusMapper` |
| `IInstalledVersionProvider` | `GetAsync` → `InstalledReleaseInfo` | `InstalledReleaseMetadataProvider`; `UpdateOrchestratorAdapterTestFactory`, `UpdateControllerIntegrationTests` |
| `IAutoUpdateStateStore` | `ReadAsync` | `UpdateOrchestratorAdapterTestFactory` (Mock für `AutoUpdateStatusService`) |
| `IAutoUpdateEnvironment` | `ApplicationDirectory` | `UpdateSettingsStoreTests.AutoUpdateEnvironmentAdapter` |
| `IAutoUpdateSource` (implizit) | `AutoUpdateGithubSource`/`AutoUpdateLocalFolderSource` über `AutoUpdateOptions.Source` | `AutoUpdateOptionsMapper` (Typprüfung + Austausch) |
| `IAutoUpdateStatusProvider` | — (nur über konkrete Klasse `AutoUpdateStatusService` injiziert) | `UpdateOrchestratorAdapter` |
| `IAutoUpdateEventAggregator` | **nicht genutzt** | — |
