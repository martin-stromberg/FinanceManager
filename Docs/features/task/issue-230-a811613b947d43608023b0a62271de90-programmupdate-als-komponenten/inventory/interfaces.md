# Interfaces und Contracts

## Zentrale Orchestrator-Interface

### `IUpdateOrchestrator`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateOrchestrator.cs`

Zentrale Schnittstelle für den Update-Workflow.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| CheckForUpdatesAsync | CancellationToken | Task<UpdateCheckResultDto> | Prüfung auf neue Versionen |
| DownloadUpdateAsync | version: string, CancellationToken | Task<UpdateDownloadResultDto> | Download einer Version |
| InstallUpdateAsync | version: string, confirmDowntime: bool, CancellationToken | Task<UpdateInstallResultDto> | Installation einer Version |
| RunUpdateAsync | CancellationToken | Task<UpdateInstallResultDto> | Kompletter Workflow |
| GetCurrentStatusAsync | CancellationToken | Task<UpdateStatusDto> | Abfrage des aktuellen Status |
| ResetLockAsync | CancellationToken | Task | Entsperrt Operationen |

## Speicher und Persistierung

### `IUpdateFileStore`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateFileStore.cs`

Verwaltung heruntergeladener Update-Dateien.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| SaveUpdateFileAsync | version: string, stream: Stream, CancellationToken | Task | Speichert Update-Datei |
| GetUpdateFileAsync | version: string, CancellationToken | Task<Stream> | Liest Update-Datei |
| DeleteUpdateFileAsync | version: string, CancellationToken | Task | Löscht Update-Datei |
| GetStoredVersionsAsync | CancellationToken | Task<IEnumerable<string>> | Listet gespeicherte Versionen |
| ExistsAsync | version: string, CancellationToken | Task<bool> | Prüft Existenz |

### `IUpdateSettingsStore`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateSettingsStore.cs`

Persistierung von Update-Einstellungen und Status.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| LoadSettingsAsync | CancellationToken | Task<UpdateSettings> | Lädt Einstellungen |
| SaveSettingsAsync | settings: UpdateSettings, CancellationToken | Task | Speichert Einstellungen |
| GetLastCheckTimeAsync | CancellationToken | Task<DateTime?> | Gibt letzte Prüfungszeit |
| UpdateLastCheckTimeAsync | CancellationToken | Task | Aktualisiert Prüfungszeit |
| LockOperationAsync | operationId: string, CancellationToken | Task | Sperrt Operation |
| UnlockOperationAsync | operationId: string, CancellationToken | Task | Entsperrt Operation |
| IsLockedAsync | CancellationToken | Task<bool> | Prüft Lock-Status |

## Validierung und Sicherheit

### `IUpdateValidator`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateValidator.cs`

Validierung von Update-Paketen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| ValidateChecksum | filePath: string, expectedChecksum: string, CancellationToken | Task<bool> | SHA256-Validierung |
| ValidatePackageIntegrity | filePath: string, CancellationToken | Task<bool> | Integritätsprüfung |
| ValidateVersion | version: string | bool | Versionsformat-Validierung |

## Remote-Kommunikation

### `IUpdateManifestClient`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateManifestClient.cs`

Kommunikation mit Update-Quelle (GitHub Releases).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| GetLatestReleaseAsync | CancellationToken | Task<UpdateMetadataDto> | Neueste Version abrufen |
| GetReleaseByVersionAsync | version: string, CancellationToken | Task<UpdateMetadataDto> | Spezifische Version abrufen |
| DownloadAssetAsync | assetUrl: string, targetPath: string, progress: IProgress<long>, CancellationToken | Task | Asset herunterladen |

### `IUpdateServiceProbe`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateServiceProbe.cs`

Verfügbarkeitsprüfung der Update-Quelle.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| IsUpdateServiceAvailableAsync | CancellationToken | Task<bool> | Verbindungstest |

## Plattformspezifisches

### `IUpdatePlatformResolver`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdatePlatformResolver.cs`

Plattform-Erkennung und Auswahl.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| GetCurrentPlatform | - | OSPlatform | Aktuelle OS-Plattform |
| GetScriptExtension | - | string | Skript-Datei-Erweiterung |
| GetServiceName | - | string | Service-Name für Plattform |

### `IUpdateProcessRunner`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateProcessRunner.cs`

Ausführung von Prozessen und Skripten.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| RunProcessAsync | scriptPath: string, CancellationToken | Task<int> | Führt Prozess aus |
| IsProcessRunning | processName: string | bool | Prüft Prozess-Status |
| TerminateProcessAsync | processName: string, CancellationToken | Task | Beendet Prozess |

### `IUpdateHostTerminator`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateHostTerminator.cs`

Beendigung des Anwendungs-Host.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| TerminateHostAsync | CancellationToken | Task | Beendet Host |
| IsHostTerminated | - | bool | Prüft Terminierungs-Status |

## Installation und Skript-Generierung

### `IUpdateScriptGenerator`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateScriptGenerator.cs`

Generierung von Update-Skripten.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| GenerateScriptAsync | version: string, downloadPath: string, CancellationToken | Task<string> | Generiert Update-Skript |
| GetScriptExtensionForPlatform | - | string | Skript-Erweiterung |

### `IUpdateExecutor`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateExecutor.cs`

Ausführung des Update-Prozesses.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| ExecuteUpdateAsync | scriptPath: string, CancellationToken | Task | Führt Update aus |
| IsUpdateScriptAvailableAsync | version: string, CancellationToken | Task<bool> | Prüft Skript-Existenz |

## Metadata-Verwaltung

### `IInstalledReleaseMetadataProvider`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IInstalledReleaseMetadataProvider.cs`

Verwaltung installierter Versions-Metadaten.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| GetInstalledVersionAsync | CancellationToken | Task<string> | Aktuelle Version |
| UpdateInstalledMetadataAsync | version: string, CancellationToken | Task | Metadaten aktualisieren |
| GetInstalledMetadataAsync | CancellationToken | Task<InstalledReleaseMetadataDto> | Vollständige Metadaten |

## Utility und Resolution

### `IUpdateServiceResolver`
Datei: `src/FinanceManager.Web/Services/Updates/Interfaces/IUpdateServiceResolver.cs`

Service-Auflösung aus DI-Container.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| ResolveOrchestrator | provider: IServiceProvider | IUpdateOrchestrator | Orchestrator auflösen |
| ResolveValidator | provider: IServiceProvider | IUpdateValidator | Validator auflösen |
| ResolveFileStore | provider: IServiceProvider | IUpdateFileStore | FileStore auflösen |

---

## Notizen zu Interfaces

**Fehlende Interfaces aus Anforderung:**
- `IAutoUpdateSource` – Interface für plug-in-fähige Update-Quellen (aktuell: nur GitHub hardcoded)
- `IAutoUpdateStatusProvider` – Read-only Status-Interface (aktuell: alle Status-Abfragen über IUpdateOrchestrator)
- `IAutoUpdateEventAggregator` – Event-Aggregator-Interface (aktuell: keine zentrale Event-Implementierung)
- `IAutoUpdateCommandHandler` – Separate Command-Interface (aktuell: in IUpdateOrchestrator integriert)

**Hinweise:**
- Aktuelle Implementierung ist stark GitHub-fokussiert (keine abstrahierte Update-Quelle)
- Keine offizielle Event-Aggregator-Infrastruktur (Events könnten über Event-Aggregator oder Delegates erfolgen)
- Thread-Safety durch Locking in `IUpdateSettingsStore.LockOperationAsync()` implementiert
