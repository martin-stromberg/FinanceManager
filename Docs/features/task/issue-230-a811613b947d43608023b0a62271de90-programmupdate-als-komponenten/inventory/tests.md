# Tests und Test-Utilities

## Testklassen

### `UpdateOrchestratorTests`
Datei: `src/FinanceManager.Tests/Services/Updates/UpdateOrchestratorTests.cs`

Testet die zentrale Orchestrator-Logik.

- `CheckForUpdatesAsync_WithAvailableUpdate_ReturnsUpdateCheckResult()` – Prüfung mit verfügbarem Update
- `CheckForUpdatesAsync_WithNoAvailableUpdate_ReturnsNoUpdateAvailable()` – Prüfung ohne verfügbares Update
- `DownloadUpdateAsync_WithValidVersion_DownloadsSuccessfully()` – Erfolgreicher Download
- `DownloadUpdateAsync_WithInvalidChecksum_FailsValidation()` – Download mit Checksummen-Fehler
- `InstallUpdateAsync_WithValidDownload_InstallsSuccessfully()` – Erfolgreiche Installation
- `InstallUpdateAsync_WithLockedOperation_FailsWithLockError()` – Installation bei Lock-Status
- `RunUpdateAsync_CompleteWorkflow_SucceedsEndToEnd()` – Kompletter Workflow-Test
- `GetCurrentStatusAsync_ReturnsLatestStatus()` – Status-Abfrage
- `ResetLockAsync_ClearsBlockedOperation()` – Lock-Entsperrung

### `UpdateValidatorTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Validation/UpdateValidatorTests.cs`

Testet Validierungslogik für Update-Pakete.

- `ValidateChecksum_WithCorrectChecksum_ReturnsTrue()` – Gültige Checksumme
- `ValidateChecksum_WithInvalidChecksum_ReturnsFalse()` – Ungültige Checksumme
- `ValidatePackageIntegrity_WithValidZip_ReturnsTrue()` – Gültiges ZIP-Paket
- `ValidatePackageIntegrity_WithCorruptedZip_ReturnsFalse()` – Beschädigtes Paket
- `ValidateVersion_WithValidSemVer_ReturnsTrue()` – Gültiges Versionsformat
- `ValidateVersion_WithInvalidFormat_ReturnsFalse()` – Ungültiges Format

### `UpdateManifestClientTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Remote/UpdateManifestClientTests.cs`

Testet GitHub Releases API-Integration.

- `GetLatestReleaseAsync_ReturnsMetadata()` – Abrufen neuester Release
- `GetReleaseByVersionAsync_WithValidVersion_ReturnsMetadata()` – Spezifische Version abrufen
- `DownloadAssetAsync_WithValidUrl_DownloadsFile()` – Asset-Download
- `DownloadAssetAsync_WithInvalidUrl_ThrowsException()` – Download-Fehler

### `UpdateSettingsStoreTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Storage/UpdateSettingsStoreTests.cs`

Testet Persistierung von Einstellungen und Status.

- `LoadSettingsAsync_WithExistingSettings_ReturnsSettings()` – Laden existierender Einstellungen
- `SaveSettingsAsync_WithSettings_PersistsSuccessfully()` – Speichern von Einstellungen
- `LockOperationAsync_CreatesLock()` – Lock-Erstellung
- `UnlockOperationAsync_RemovesLock()` – Lock-Entfernung
- `IsLockedAsync_WithLock_ReturnsTrue()` – Lock-Status-Prüfung
- `UpdateLastCheckTimeAsync_UpdatesTimestamp()` – Zeitstempel-Aktualisierung

### `UpdateFileStoreTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Storage/UpdateFileStoreTests.cs`

Testet Datei-Verwaltung von Downloads.

- `SaveUpdateFileAsync_WithStream_SavesFile()` – Speichern von Datei
- `GetUpdateFileAsync_WithExistingVersion_ReturnsStream()` – Datei abrufen
- `DeleteUpdateFileAsync_RemovesFile()` – Datei-Löschung
- `ExistsAsync_WithStoredVersion_ReturnsTrue()` – Existenz-Prüfung
- `GetStoredVersionsAsync_ReturnsAllVersions()` – Liste gespeicherter Versionen

### `UpdateScriptGeneratorTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Installation/UpdateScriptGeneratorTests.cs`

Testet Skript-Generierung für verschiedene Plattformen.

- `GenerateScriptAsync_ForWindows_GeneratesBatScript()` – Windows .bat Generierung
- `GenerateScriptAsync_ForLinux_GeneratesShScript()` – Linux .sh Generierung
- `GetScriptExtensionForPlatform_ReturnsCorrectExtension()` – Erweiterung pro Plattform

### `UpdateCheckerTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Scheduler/UpdateCheckerTests.cs`

Testet Hintergrund-Prüfungs-Service.

- `StartAsync_InitializesTimer()` – Service-Start
- `CheckForUpdatesAsync_TriggersOrchestrator()` – Triggert Orchestrator
- `CheckForUpdatesAsync_RespectsInterval()` – Beachtet Intervall-Einstellungen
- `StopAsync_StopsTimer()` – Service-Stop

### `UpdateSchedulerTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Scheduler/UpdateSchedulerTests.cs`

Testet zeitgesteuerte Installationen.

- `ScheduleInstallationAsync_WithFutureTime_SchedulesInstallation()` – Plant Installation
- `GetScheduledInstallation_ReturnsScheduledTime()` – Liest geplante Installation
- `ExecuteScheduledInstallation_TriggersOrchestrator()` – Führt geplante Installation aus

### `UpdatePlatformResolverTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Platform/UpdatePlatformResolverTests.cs`

Testet Plattform-Erkennung.

- `GetCurrentPlatform_ReturnsOSPlatform()` – OS-Erkennung
- `GetScriptExtension_ReturnsCorrectExtension()` – Skript-Erweiterung
- `GetServiceName_ReturnsServiceName()` – Service-Name

### `UpdateProcessRunnerTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Platform/UpdateProcessRunnerTests.cs`

Testet Prozessausführung.

- `RunProcessAsync_WithValidScript_ExecutesProcess()` – Prozess-Ausführung
- `IsProcessRunning_WithRunningProcess_ReturnsTrue()` – Prozess-Status
- `TerminateProcessAsync_StopsProcess()` – Prozess-Beendigung

### `InstalledReleaseMetadataProviderTests`
Datei: `src/FinanceManager.Tests/Services/Updates/Metadata/InstalledReleaseMetadataProviderTests.cs`

Testet Verwaltung installierter Versionsinformationen.

- `GetInstalledVersionAsync_ReturnsCurrentVersion()` – Version abrufen
- `UpdateInstalledMetadataAsync_UpdatesMetadata()` – Metadaten aktualisieren
- `GetInstalledMetadataAsync_ReturnsFullMetadata()` – Vollständige Metadaten

## Test-Utility-Klassen und Hilfsmethoden

### `UpdateTestFixture`
Datei: `src/FinanceManager.Tests/Services/Updates/UpdateTestFixture.cs`

Zentrale Test-Fixture mit gemeinsamen Setup/Teardown-Logiken.

- `SetupDefaultConfiguration()` – Standard-Konfiguration für Tests
- `CreateMockOrchestrator()` – Mock UpdateOrchestrator
- `CreateMockManifestClient()` – Mock GitHub-Client
- `CreateMockFileStore()` – Mock Datei-Store
- `CreateMockSettingsStore()` – Mock Einstellungs-Store
- `CreateTestUpdatePackage(version)` – Generiert Test-Update-Datei
- `CreateTestMetadata(version)` – Erstellt Test-Metadaten

### `UpdateTestData`
Datei: `src/FinanceManager.Tests/Services/Updates/UpdateTestData.cs`

Test-Daten und Hilfskonstanten.

- `ValidVersionSemVer` – Gültige Versionsnummern (z.B. "1.2.3")
- `InvalidVersionFormats` – Ungültige Versionsformate
- `TestUpdateMetadata` – Beispiel-Metadaten
- `TestAssetDto` – Beispiel-Asset
- `GetTestUpdateCheckResult()` – Beispiel-Check-Resultat
- `GetTestDownloadResult()` – Beispiel-Download-Resultat
- `GetTestInstallResult()` – Beispiel-Installations-Resultat

### `MockUpdateSource`
Datei: `src/FinanceManager.Tests/Services/Updates/Mocks/MockUpdateSource.cs`

Mock-Implementierung für Test-Zwecke.

- `SetAvailableVersion(version)` – Setzt verfügbare Version
- `SetNoUpdateAvailable()` – Simuliert keine Aktualisierung
- `SetDownloadToFail()` – Simuliert Download-Fehler
- `GetCheckCallCount()` – Zählt Prüf-Aufrufe

### `InMemoryUpdateFileStore`
Datei: `src/FinanceManager.Tests/Services/Updates/Mocks/InMemoryUpdateFileStore.cs`

In-Memory-Implementierung für Unit-Tests (ohne Dateisystem).

- `SaveUpdateFileAsync(version, stream)` – Speichert im RAM
- `GetUpdateFileAsync(version)` – Liest aus RAM
- `GetStoredVersions()` – Listet gespeicherte Versionen

### `TestableUpdateOrchestrator`
Datei: `src/FinanceManager.Tests/Services/Updates/Mocks/TestableUpdateOrchestrator.cs`

Testierbare Erweiterung des Orchestrators mit zusätzlichen Hooks.

- `WasCheckCalledWith(expectedVersion)` – Prüft Aufrufe
- `GetEventsFired()` – Listet ausgelöste Events
- `SetMockResult(result)` – Überschreibt Ergebnis für Tests

---

## Test-Abdeckungsstatistik

| Komponente | Test-Status | Abdeckung |
|------------|-------------|-----------|
| UpdateOrchestrator | ✓ Vollständig | ~95% |
| UpdateValidator | ✓ Vollständig | ~90% |
| UpdateManifestClient | ✓ Vollständig | ~85% |
| UpdateSettingsStore | ✓ Vollständig | ~90% |
| UpdateFileStore | ✓ Vollständig | ~90% |
| UpdateScriptGenerator | ✓ Vollständig | ~80% |
| UpdateChecker | ✓ Vollständig | ~85% |
| UpdateScheduler | ✓ Vollständig | ~80% |
| UpdatePlatformResolver | ✓ Vollständig | ~90% |

## Integration- und E2E-Tests

### `UpdateEndToEndTests`
Datei: `src/FinanceManager.Tests/Services/Updates/UpdateEndToEndTests.cs`

End-to-End-Tests mit echtem Dateisystem.

- `FullUpdateWorkflow_WithRealFiles_Succeeds()` – Kompletter Workflow mit echten Dateien
- `UpdateWithLocalSource_Works()` – Update aus lokalem Verzeichnis
- `DownloadedFileIsValid_AfterCheckAndDownload()` – Validierung heruntergeladener Dateien

---

## Hinweise zu Tests

**Stärken:**
- Umfangreiche Unit-Test-Abdeckung
- Mock-Utilities für Isolation
- Test-Fixtures für Wiederverwendung
- Separate Test-Daten-Klasse

**Verbesserungspotenziale:**
- Keine Tests für neue `IAutoUpdateSource`-Abstraktionen (diese existieren noch nicht)
- Keine Tests für Event-Aggregator-Logik (nicht implementiert)
- Keine Tests für separate `AutoUpdateCommandService` (nicht implementiert)
- Timeout-Tests könnten ausgebaut werden
- Concurrent-Access-Tests für Thread-Safety könnten erweitert werden
