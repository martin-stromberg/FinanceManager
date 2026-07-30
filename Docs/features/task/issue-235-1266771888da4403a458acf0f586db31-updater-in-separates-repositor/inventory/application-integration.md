# Anwendungsintegration

## Direkte Projektverweise

`FinanceManager.Web/FinanceManager.Web.csproj` enthält aktuell:

```xml
<ProjectReference Include="..\SoftwareSchmiede.AutoUpdate\SoftwareSchmiede.AutoUpdate.csproj" />
```

Weitere direkte Referenzen aus Anwendungsprojekten auf die lokale Updater-Bibliothek wurden nicht gefunden. Die übrigen Projekte hängen indirekt über `FinanceManager.Web` daran, z. B. Testprojekte mit `ProjectReference` auf `FinanceManager.Web`.

## Namespace-Nutzung

Produktiver Code importiert den lokalen Updater vor allem in:

- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`
- `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`
- `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`

Verwendeter Namespace: `SoftwareSchmiede.AutoUpdate`.

Falls die externe Bibliothek einen neuen Namespace hat, sind diese Dateien die primären Migrationspunkte.

## Adapter-Schicht

`UpdateOrchestratorAdapter` implementiert `IUpdateOrchestrator` auf Basis der Bibliothek:

| FinanceManager-Methode | Bibliotheksaufruf / Abhängigkeit |
|------------------------|-----------------------------------|
| `GetStatusAsync` | `IAutoUpdateOrchestrator.GetStatusAsync` |
| `GetSettingsAsync` | `IUpdateSettingsStore.GetAsync` |
| `SaveSettingsAsync` | `IUpdateSettingsStore.SaveAsync`, `ApplyToOptions` |
| `ScheduleAsync` | `IUpdateSettingsStore.SaveScheduleAsync`, `ApplyToOptions` |
| `CheckAsync` | `IAutoUpdateOrchestrator.CheckForUpdateAsync` |
| `StartInstallAsync` | `IAutoUpdateOrchestrator.InstallAsync` |
| `ResetLockAsync` | `IAutoUpdatePackageStore`, `AutoUpdateStatusService` |

Diese Adapter-Schicht ist die wichtigste Stabilisierung: REST-API, DTOs und UI müssen nicht zwingend geändert werden, wenn die externe Bibliothek funktional kompatible Typen bietet.

## Registrierung

In `ProgramExtensions.RegisterAppServices` wird der Updater so eingebunden:

1. `UpdateOptions` wird aus `Updates` gebunden.
2. `builder.UseAutoUpdate(cfg => cfg.SetInitialConfiguration(...))` registriert die Bibliothek.
3. FinanceManager-spezifische Adapter und Stores werden registriert:
   - `IUpdateOrchestrator -> UpdateOrchestratorAdapter`
   - `IUpdateSettingsStore -> UpdateSettingsStore`
   - `IInstalledReleaseMetadataProvider -> InstalledReleaseMetadataProvider`
   - `UpdateStatusMapper`
   - `IUpdateServiceCatalog -> DefaultUpdateServiceCatalog`

Die private Extension `SetInitialConfiguration` in `ProgramExtensions.cs` nutzt Bibliotheks-API:

- `BindConfiguration("Updates")`
- `WithUpdateUnitName("FinanceManagerUpdate")`
- `WithDownloadPath(...)`
- `WithSourceCheck(...)`
- `UseLocalFolderSource(...)`
- `UseGithubSource(...)`

Diese Methoden müssen im externen Artefakt vorhanden oder angepasst werden.

## Konfiguration

`FinanceManager.Web/appsettings.json` enthält den Abschnitt `Updates`:

| Key | Relevanz |
|-----|----------|
| `Enabled` | Aktiviert/deaktiviert die Bibliothek |
| `HostedServicesEnabled` | Steuert Hintergrunddienste |
| `CheckIntervalMinutes` und `SourceCheck.Interval` | Prüfintervall, inklusive Legacy-Alias |
| `RepositoryOwner`, `RepositoryName`, `ManifestAssetName` | GitHub-Quelle für App-Updates |
| `WorkingDirectory` | Download-/Status-/Lock-Verzeichnis |
| `SourceType`, `LocalFolderPath` | GitHub oder lokale Quelle |
| `EnableAutomaticDownload`, `EnableAutomaticInstallation` | Automatisierte Update-Schritte |
| `StopHostAfterScriptStart` | Host-Verhalten nach Scriptstart |
| `HealthTimeoutSeconds`, `MaxAssetBytes` | Sicherheits- und Größenlimits |

Die externe Bibliothek muss mit diesen Keys kompatibel bleiben, soweit `BindConfiguration("Updates")` weiter genutzt wird.

## REST-API bleibt intern stabil

`UpdateController` nutzt ausschließlich `IUpdateOrchestrator` und `IUpdateServiceCatalog`. Die API-Endpunkte unter `/api/setup/update` können unverändert bleiben, solange der Adapter weiterhin kompiliert:

- `GET status`
- `GET settings`
- `PUT settings`
- `GET services`
- `POST check`
- `POST schedule`
- `POST install/start`
- `POST lock/reset`

## Weitere Dokumentationsstellen

README und CHANGELOG beschreiben aktuell `SoftwareSchmiede.AutoUpdate` als lokales Projekt. Nach der Auslagerung sollten diese Stellen aktualisiert werden, damit die Projektstruktur nicht veraltet ist.
