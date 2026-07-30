# Logik-Komponenten und Services

## Zentrale Orchestrator-Klasse

### `UpdateOrchestrator`
Datei: `src/FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs`

Zentrale Koordinationsklasse für den gesamten Update-Workflow (Prüfung → Download → Installation).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| CheckForUpdatesAsync(CancellationToken) | Public | Prüft die konfigurierte Quelle auf neue Versionen |
| DownloadUpdateAsync(version, CancellationToken) | Public | Lädt ein Update herunter (nur nach erfolgreicher Prüfung) |
| InstallUpdateAsync(version, confirmDowntime, CancellationToken) | Public | Installiert ein heruntergeladenes Update |
| RunUpdateAsync(CancellationToken) | Public | Kompletter Workflow: Check → Download → Install |
| GetCurrentStatusAsync(CancellationToken) | Public | Gibt aktuellen Status zurück |
| ResetLockAsync(CancellationToken) | Public | Entsperrt blockierte Update-Operationen |

**Abonnierte Events:**
- (von externen Event-Aggregatoren, soweit implementiert)

**Publizierte Events:**
- (Events werden über EventHandler-Pattern oder Event-Aggregator ausgelöst)

**Abhängigkeiten (DI):**
- IUpdateManifestClient
- IUpdateFileStore
- IUpdateSettingsStore
- IUpdateExecutor
- IUpdatePlatformResolver
- IUpdateValidator
- IInstalledReleaseMetadataProvider
- ILogger<UpdateOrchestrator>

## Service-Komponenten

### `UpdateChecker` (Hosted Service)
Datei: `src/FinanceManager.Web/Services/Updates/Scheduler/UpdateChecker.cs`

Hintergrund-Service für periodische Versionsprüfungen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| StartAsync(CancellationToken) | Public | Service-Start mit Timer-Initialisierung |
| StopAsync(CancellationToken) | Public | Service-Stop und Cleanup |
| CheckForUpdatesAsync(CancellationToken) | Private | Aktualisiert den Status durch UpdateOrchestrator |

**Abhängigkeiten:**
- IUpdateOrchestrator
- IUpdateSettingsStore
- ILogger<UpdateChecker>

### `UpdateScheduler` (Hosted Service)
Datei: `src/FinanceManager.Web/Services/Updates/Scheduler/UpdateScheduler.cs`

Hintergrund-Service für zeitgesteuerte, geplante Installationen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| StartAsync(CancellationToken) | Public | Service-Start |
| StopAsync(CancellationToken) | Public | Service-Stop und Cleanup |
| ScheduleInstallationAsync(dateTime, CancellationToken) | Public | Plant eine Installation zu gegebener Zeit |
| GetScheduledInstallation(CancellationToken) | Public | Gibt geplante Installation zurück |

**Abhängigkeiten:**
- IUpdateOrchestrator
- ILogger<UpdateScheduler>

### `UpdateExecutor`
Datei: `src/FinanceManager.Web/Services/Updates/Installation/UpdateExecutor.cs`

Führt das Update-Skript/Installationspaket aus.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| ExecuteUpdateAsync(scriptPath, CancellationToken) | Public | Führt Update-Skript aus |
| IsUpdateScriptAvailableAsync(version, CancellationToken) | Public | Prüft, ob Skript für Version existiert |

**Abhängigkeiten:**
- IUpdateScriptGenerator
- IUpdateProcessRunner
- IUpdateHostTerminator
- ILogger<UpdateExecutor>

### `UpdateScriptGenerator`
Datei: `src/FinanceManager.Web/Services/Updates/Installation/UpdateScriptGenerator.cs`

Generiert Plattform-spezifische Update-Skripte (Windows .bat, Linux .sh).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GenerateScriptAsync(version, downloadPath, CancellationToken) | Public | Generiert Update-Skript für Plattform |
| GetScriptExtensionForPlatform() | Public | Gibt Datei-Erweiterung zurück (.bat, .sh, etc.) |

**Abhängigkeiten:**
- IUpdatePlatformResolver
- ILogger<UpdateScriptGenerator>

## Speicher- und Datei-Verwaltung

### `UpdateFileStore`
Datei: `src/FinanceManager.Web/Services/Updates/Storage/UpdateFileStore.cs`

Verwaltet heruntergeladene Update-Dateien.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| SaveUpdateFileAsync(version, stream, CancellationToken) | Public | Speichert heruntergeladene Datei |
| GetUpdateFileAsync(version, CancellationToken) | Public | Liest heruntergeladene Datei |
| DeleteUpdateFileAsync(version, CancellationToken) | Public | Löscht Update-Datei |
| GetStoredVersionsAsync(CancellationToken) | Public | Liste gespeicherter Versionen |
| ExistsAsync(version, CancellationToken) | Public | Prüft Existenz einer Version |

**Abhängigkeiten:**
- ILogger<UpdateFileStore>

### `JsonFileStore`
Datei: `src/FinanceManager.Web/Services/Updates/Storage/JsonFileStore.cs`

JSON-basierter Persistierungs-Store für Einstellungen und Metadata.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| SaveAsync<T>(key, data, CancellationToken) | Public | Speichert Objekt als JSON |
| LoadAsync<T>(key, CancellationToken) | Public | Lädt Objekt aus JSON |
| DeleteAsync(key, CancellationToken) | Public | Löscht gespeicherte Daten |

### `UpdateSettingsStore`
Datei: `src/FinanceManager.Web/Services/Updates/Storage/UpdateSettingsStore.cs`

Persistiert Update-Einstellungen und Zustand.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| LoadSettingsAsync(CancellationToken) | Public | Lädt Einstellungen aus Persistierung |
| SaveSettingsAsync(settings, CancellationToken) | Public | Speichert Einstellungen |
| GetLastCheckTimeAsync(CancellationToken) | Public | Gibt Zeit der letzten Prüfung zurück |
| UpdateLastCheckTimeAsync(CancellationToken) | Public | Aktualisiert Prüfungszeit |

**Abhängigkeiten:**
- JsonFileStore
- ILogger<UpdateSettingsStore>

## Validierung und Sicherheit

### `UpdateValidator`
Datei: `src/FinanceManager.Web/Services/Updates/Validation/UpdateValidator.cs`

Validiert heruntergeladene Update-Pakete.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| ValidateChecksum(filePath, expectedChecksum, CancellationToken) | Public | Validiert SHA256-Checksumme |
| ValidatePackageIntegrity(filePath, CancellationToken) | Public | Prüft Integrität des Update-Pakets |
| ValidateVersion(version) | Public | Validiert Versionsformat |

**Abhängigkeiten:**
- ILogger<UpdateValidator>

## Manifest und Remote-Kommunikation

### `UpdateManifestClient`
Datei: `src/FinanceManager.Web/Services/Updates/Remote/UpdateManifestClient.cs`

Kommuniziert mit GitHub Releases API zur Beschaffung von Update-Metadaten.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetLatestReleaseAsync(CancellationToken) | Public | Ruft aktuellste Release von GitHub ab |
| GetReleaseByVersionAsync(version, CancellationToken) | Public | Ruft spezifische Version ab |
| DownloadAssetAsync(assetUrl, targetPath, CancellationToken) | Public | Lädt Asset herunter |

**Abhängigkeiten:**
- HttpClient
- ILogger<UpdateManifestClient>

**Externe Abhängigkeit:** GitHub Releases API (hardcoded)

### `DefaultUpdateServiceProbe`
Datei: `src/FinanceManager.Web/Services/Updates/Remote/DefaultUpdateServiceProbe.cs`

Prüft Erreichbarkeit der Update-Quelle.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| IsUpdateServiceAvailableAsync(CancellationToken) | Public | Testet Verbindung zur Update-Quelle |

## Plattform und Prozessausführung

### `UpdatePlatformResolver`
Datei: `src/FinanceManager.Web/Services/Updates/Platform/UpdatePlatformResolver.cs`

Bestimmt aktuelle Plattform und wählt entsprechende Implementierungen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetCurrentPlatform() | Public | Gibt OSPlatform zurück (Windows, Linux, macOS) |
| GetScriptExtension() | Public | Gibt Datei-Erweiterung für Skripte zurück |

### `DefaultUpdateProcessRunner`
Datei: `src/FinanceManager.Web/Services/Updates/Platform/DefaultUpdateProcessRunner.cs`

Führt Update-Prozesse (Skripte) aus.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| RunProcessAsync(scriptPath, CancellationToken) | Public | Führt Prozess/Skript asynchron aus |
| IsProcessRunning(processName) | Public | Prüft, ob Prozess läuft |

**Abhängigkeiten:**
- ILogger<DefaultUpdateProcessRunner>

### `DefaultUpdateHostTerminator`
Datei: `src/FinanceManager.Web/Services/Updates/Platform/DefaultUpdateHostTerminator.cs`

Beendet den Anwendungs-Host vor/nach Update.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| TerminateHostAsync(CancellationToken) | Public | Beendet Anwendungs-Host |
| IsHostTerminated() | Public | Prüft Terminierungs-Status |

**Abhängigkeiten:**
- IHostApplicationLifetime

## Metadata-Management

### `InstalledReleaseMetadataProvider`
Datei: `src/FinanceManager.Web/Services/Updates/Metadata/InstalledReleaseMetadataProvider.cs`

Verwaltet Metadaten der aktuell installierten Version.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetInstalledVersionAsync(CancellationToken) | Public | Gibt aktuell installierte Version zurück |
| UpdateInstalledMetadataAsync(version, CancellationToken) | Public | Aktualisiert Metadaten nach Installation |
| GetInstalledMetadataAsync(CancellationToken) | Public | Ruft vollständige Metadaten ab |

**Abhängigkeiten:**
- IUpdateSettingsStore
- ILogger<InstalledReleaseMetadataProvider>

## Utility und Resolution

### `UpdateServiceResolver`
Datei: `src/FinanceManager.Web/Services/Updates/Utilities/UpdateServiceResolver.cs`

Hilfsklasse zum Auflösen von Update-Services (DI-Unterstützung).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| ResolveOrchestrator(IServiceProvider) | Public Static | Löst UpdateOrchestrator aus Container auf |
| ResolveValidator(IServiceProvider) | Public Static | Löst UpdateValidator aus Container auf |

## Weitere Service-Komponenten

### `DefaultUpdateServiceResolver`
Datei: `src/FinanceManager.Web/Services/Updates/Utilities/DefaultUpdateServiceResolver.cs`

Standard-Implementierung zum Service-Auflösen.
