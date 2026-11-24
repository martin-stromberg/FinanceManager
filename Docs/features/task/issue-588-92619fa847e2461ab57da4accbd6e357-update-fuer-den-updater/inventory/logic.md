# Logik — Update-Subsystem (Adapter-Schicht auf `msTools.Updater`)

Alle Klassen unter `FinanceManager.Web/Services/Updates/` bilden die Adapter-/Mapping-Schicht zwischen der vendored Bibliothek und der Anwendung. `using msTools.Updater` haben: `UpdateOrchestratorAdapter`, `UpdateStatusMapper`, `UpdateSettingsStore`, `AutoUpdateOptionsMapper`, `InstalledReleaseMetadataProvider`, `UpdateErrorMessageMapper` sowie `ProgramExtensions` (Z. 31) und `UpdateContracts.cs` nur indirekt per XML-Doku-Verweis.

## `ProgramExtensions` (Aktivierung)

Datei: `FinanceManager.Web/ProgramExtensions.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RegisterAppServices` | `public static` | Z. 262–272: registriert `TimeProvider.System`, liest `UpdateOptions`, ruft `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(updateOptions))`, danach `IUpdateOrchestrator→UpdateOrchestratorAdapter` (scoped), `IUpdateSettingsStore→UpdateSettingsStore`, `IInstalledReleaseMetadataProvider→InstalledReleaseMetadataProvider`, `UpdateStatusMapper`, `IUpdateServiceCatalog→DefaultUpdateServiceCatalog` (je Singleton) |
| `SetInitialConfiguration` | `private static` (Extension auf `AutoUpdateBuilder`) | Z. 570–595: `BindConfiguration("Updates")`, `WithUpdateUnitName("FinanceManagerUpdate")`, optional `WithDownloadPath`, `WithSourceCheck(1440, timeRanges)`, je nach `SourceType` `UseLocalFolderSource` oder `UseGithubSource` |

Abonnierte/publizierte Events: keine — keine Nutzung von `IAutoUpdateEventAggregator`.

## `UpdateOrchestratorAdapter`

Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs` — implementiert `IUpdateOrchestrator`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| Ctor | `public` | injiziert `IAutoUpdateOrchestrator`, `AutoUpdateStatusService`, `IUpdateSettingsStore`, `IAutoUpdatePackageStore`, `UpdateStatusMapper`, `AutoUpdateOptions`, `ILogger` |
| `RunWithManualOverrideAsync<T>` | `private` | erzwingt temporär `AutoUpdateOptions.Enabled=true` für manuelle Check/Install-Aktionen, restauriert danach |
| `GetStatusAsync` | `public` | `_orchestrator.GetStatusAsync` → `_statusMapper.MapAsync` |
| `GetSettingsAsync` | `public` | delegiert an `_settingsStore.GetAsync` |
| `SaveSettingsAsync` | `public` | `SaveAsync` + `ApplyToOptions` |
| `ScheduleAsync` | `public` | `SaveScheduleAsync` + `ApplyToOptions` |
| `CheckAsync` | `public` | `CheckForUpdateAsync`; bei `Success` automatisch `DownloadAsync`; mappt Snapshot + Fehlermeldung (Rate-Limit-Sonderbehandlung) auf `UpdateCheckResultDto` |
| `StartInstallAsync` | `public` | bei `confirmDowntime` und `UpdateAvailable`-ohne-Download erst `DownloadAsync`, dann `InstallAsync(false→force:false)`; wirft Original-Exception aus `AutoUpdateError`; danach `ValidateLockCleanupAsync` |
| `ResetLockAsync` | `public` | Lock lesen → Stale-Prüfung → `DeleteLockAsync` → Status per `_statusService.UpdateAsync` entriegeln; klassifizierte `UpdateLockResetException` (`NoLock`/`LockNotStale`/`LockDeleteFailed`/`ResetFailed`, Quelle `FinanceManager`/`Updater`) |
| `DeleteLockOrThrowAsync` | `private` | `IOException`/`UnauthorizedAccessException` → `LockDeleteFailed` mit `FailureSource.Updater` |
| `CreateResetException` | `private` | baut `UpdateLockResetException` inkl. `_packageStore.LockPath` |
| `CreateUpdateException` | `private static` | `error.Exception ?? new InvalidOperationException(error.Message)` |
| `ValidateLockCleanupAsync` | `private` | warnt, wenn nach erfolgreichem Install noch ein Lock existiert |

## `UpdateStatusMapper`

Datei: `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MapAsync` | `public` | aggregiert Snapshot + `IInstalledReleaseMetadataProvider` + `IUpdateSettingsStore` + `IAutoUpdatePlatformResolver` zu `UpdateStatusDto` (12 Felder) |
| `MapState` | `private static` | erschöpfender `switch` über alle 9 `AutoUpdateState`-Werte → `UpdateStatusKind`; unbekannte Werte werfen `ArgumentOutOfRangeException` |

## `UpdateSettingsStore`

Datei: `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs` — implementiert `IUpdateSettingsStore`; persistiert `settings.json` unter `IAutoUpdatePackageStore.RootDirectory` via `JsonFileStore.WriteAtomicAsync`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync` | `public` | `EnsureAsync`, dann `ReadSettingsAsync` sonst `Defaults()` |
| `SaveAsync` | `public` | `Build` (Normalisierung/Clamping) + `WriteAtomicAsync` |
| `SaveScheduleAsync` | `public` | nur `ScheduledInstallTime` ändern |
| `ApplyToOptions` | `public` | `AutoUpdateOptionsMapper.ApplySettings` |
| `Defaults`/`Build`/`ReadSettingsAsync`/`WriteAtomicAsync`/`Normalize*`/`TrimToNull` | `private` | Normalisierung, Clamp `HealthTimeoutSeconds` auf `AutoUpdateOptions.Min/MaxHealthTimeoutSeconds`, Legacy-Migration (`windowsServiceName`/`linuxServiceName`) |
| `LegacyUpdateSettingsDto`/`PersistedUpdateSettingsDto` | `private sealed record` | Persistenz-Schemata (alt/neu) |

## `AutoUpdateOptionsMapper`

Datei: `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs` (statisch)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `DailySourceCheckIntervalMinutes` | `public const int` = 1440 | tägliches Check-Intervall |
| `DefaultSourceCheckStartTime`/`EndTime` | `public static TimeOnly` | 20:00 / 06:00 |
| `ApplySettings` | `public static` | schreibt 9 Felder auf `AutoUpdateOptions`; `EnableAutomaticInstallation = ScheduledInstallTime.HasValue`; ersetzt `AutoUpdateGithubSource` via `Create(...)` + `Dispose` der alten Instanz; Nicht-GitHub-Sources bleiben unangetastet |
| `ToSettingsDto` | `public static` | inverses Mapping inkl. lesen des Check-Fensters |
| `BuildSourceCheckTimeRanges` | `public static` | 7 Tage × ggf. 2 Ranges (Mitternacht-Split) → bis zu 14 `SourceCheckTimeRange` |
| `AddDailyRanges`/`ReadSourceCheckWindow` | `private static` | Fenster-Splitting bzw. Rücklesen |

## `InstalledReleaseMetadataProvider`

Datei: `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs` — dünner Adapter `IInstalledVersionProvider` → `InstalledReleaseMetadataDto`. Wird auch von `FinanceManager.Web/Components/LoginStatus.razor` (Z. 7) für die Versionsanzeige injiziert.

## `UpdateErrorMessageMapper`

Datei: `FinanceManager.Web/Services/Updates/UpdateErrorMessageMapper.cs` (intern, statisch): `GithubRateLimitMessage`-Konstante; `Map(string)`/`Map(Exception)`/`Map(AutoUpdateError)`; `IsGithubRateLimit` erkennt `403` + `rate limit`. Keine Auswertung von `AutoUpdateErrorCode`.

## `DefaultUpdateServiceCatalog`

Datei: `FinanceManager.Web/Services/Updates/UpdateServiceCatalog.cs` — OS-Service-Namen per `sc.exe`/`systemctl` (kein `msTools.Updater`-Bezug). Methoden: `ListServiceNamesAsync`, `ParseWindowsServiceNames`, `ParseLinuxServiceNames`.

## `UpdateController`

Datei: `FinanceManager.Web/Controllers/UpdateController.cs` — `[Route("api/setup/update")]`, JWT+Admin. Endpunkte: `GET status`, `GET settings`, `PUT settings`, `GET services`, `POST check`, `POST schedule`, `POST install/start`, `POST lock/reset`. Exception-Mapping: `FileNotFoundException`→404/`Err_Update_NotReady`, `IOException`→409/`Err_Update_Locked`, `ArgumentException`→400/`Err_Update_InvalidRequest`, `InvalidOperationException`→400/`Err_Update_InvalidState`; `UpdateLockResetException.Kind` → `Err_Update_Reset_*`; unkodierte `IOException` → 500/`Err_Update_Reset_Failed`. Kein direkter `msTools.Updater`-Bezug.

## Weitere Konsumenten (unverändert im Scope)

- `FinanceManager.Shared/ApiClient.Update.cs` — 8 `Updates_*`-Methoden für die REST-Routen (kein Bibliotheksbezug).
- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs` — nutzt `IApiClient` und DTOs; kein `msTools.Updater`-Bezug.
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor` — UI-Tab.
- `FinanceManager.Web/Components/LoginStatus.razor` — `IInstalledReleaseMetadataProvider` für Versionsanzeige.
