# Tests und Testinfrastruktur

## Testklassen

### `UpdateOrchestratorAdapterTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`

Unit-Tests für `UpdateOrchestratorAdapter` allgemeine Funktionalität.

| Test | Zweck |
|------|-------|
| `Adapter_MapsSnapshotToUpdateStatusDto` | Prüft Mapping von Library-Snapshot zu DTO |
| `Adapter_MapsFailedResultToExpectedException` (Theory) | Wirft Library-Fehler durch (FileNotFoundException, IOException, ArgumentException) |
| `Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto` | Mappt erfolgreiche Check-Operation |
| `Adapter_SaveSettings_AppliesToAutoUpdateOptions` | Speichert Settings, ruft `ApplyToOptions()` auf |
| `Adapter_CheckAsync_WhenRateLimitedResult_ReturnsFriendlyMessage` | Behandelt GitHub-Rate-Limiting speziell |
| `Adapter_StartInstallAsync_WhenLockAbsentAfterInstall_DoesNotLog` | Loggt nicht, wenn Lock nach erfolgreicher Installation weg |
| `Adapter_StartInstallAsync_WhenSuccess_ValidatesLockCleanup` | Ruft `ValidateLockCleanupAsync()` auf |
| `Adapter_StartInstallAsync_WhenLockStillPresentAfterInstall_LogsWarning` | Loggt Warning, wenn Lock nach Installation noch existiert |
| `Adapter_StartInstallAsync_WhenLockCleanupCheckThrowsIOException_StillReturnsSuccessStatus` | Ignoriert I/O-Fehler defensiv |

**Abdeckung:**
- Status-Mapping
- Settings-Handling
- Check-Operation mit Rate-Limiting
- Installation mit Lock-Cleanup-Validierung
- Fehlerbehandlung

---

### `UpdateOrchestratorAdapterLockAndScheduleTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`

Unit-Tests für `ResetLockAsync()` und `ScheduleAsync()`, nicht in `UpdateOrchestratorAdapterTests` abgedeckt.

| Test | Zweck |
|------|-------|
| `ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock` | Wirft `UpdateLockResetException` mit Kind `NoLock` |
| `ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale` | Wirft Exception wenn Lock zu jung |
| `ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed` | Wirft Exception wenn Löschen fehlschlägt |
| `ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed` | Fängt I/O-Fehler beim Löschen, wirft typed Exception |
| `ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed` | Fängt I/O-Fehler beim Lesen, wirft typed Exception |
| `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus` | Löscht stalen Lock, aktualisiert Status (IsLocked=false) |
| `ScheduleAsync_SavesScheduleAndAppliesToAutoUpdateOptions` | Speichert Zeitplan, ruft `ApplyToOptions()` auf |

**Abdeckung:**
- Alle Error-Cases für Lock-Reset mit Fehlerklassifizierung
- Erfolgreicher Lock-Reset mit Status-Update
- Zeitplan-Speicherung

**Wichtig:** Kein Test für "Cache sagt IsLocked=true, Live-Read sagt keine Lock-Datei" → Das ist die **neue Anforderung** (Cache-Reconciliation).

---

### `UpdateControllerIntegrationTests`
Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`

Integrationstests für Update-API über HTTP.

| Test | Zweck |
|------|-------|
| `Health_IsAnonymous` | Health-Check ohne Auth |
| `UpdateStatus_RequiresAdmin` | Status-Endpoint benötigt Admin-Auth |
| `UpdateSettings_RoundTripsForAdmin` | Settings speichern/laden |
| `StartInstall_ReturnsConflict_WhenUpdateLockIsActive` | 409 bei aktiven Lock |
| `StartInstall_ReturnsNotFoundWithLocalizableCode_WhenNoReadyPackage` | 404 wenn kein Ready-Paket |
| `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk` | Erfolgreiches Reset mit staler Lock-Datei |
| `ResetLock_ReturnsSpecificErrorCode_WhenResetFailureIsClassified` (Theory) | Verschiedene Error-Codes für verschiedene Lock-Fehler |
| `ResetLock_ReturnsResetFailed_WhenResetThrowsUnclassifiedIOException` | Unbekannte I/O-Fehler generisch als Failed |
| `ResetLock_Returns409NoLock_WhenNoLockFileExists` | 409 wenn kein Lock vorhanden |
| `ResetLock_Returns409LockNotStale_WhenLockFileIsTooYoung` | 409 wenn Lock zu jung |
| `StartInstall_SucceedsAndLockRemains_WhenInstallerDoesNotCleanUpLock` | Erfolgreiche Installation wenn Lock-Cleanup fehlschlägt (mit Warning-Log) |
| `PersistedSettings_AreAppliedToAutoUpdateOptions_OnStartup_WithoutManualSave` | Settings-Persistierung über Restart |
| `Status_WhenInstallingAndVersionMatchesAfterRestart_ReportsNoUpdate` | Successful Install Detection nach Restart |
| `Status_WhenInstallingAndVersionMismatchAfterRestart_ReportsFailed` | Failed Install Detection nach Restart |
| `StartInstall_ReturnsBadRequest_WhenDowntimeIsNotConfirmed` | Validierung Downtime-Bestätigung |

**Abdeckung:**
- Auth-Anforderungen
- HTTP-Status-Codes
- Fehlerklassifizierung
- Lock-Handling auf Dateisystem-Ebene
- Settings-Persistierung
- Restart-Szenarien

**Test-Helpers:**
- `ThrowingUpdateOrchestrator` — Mock der Library für Exception-Tests
- `SucceedingAutoUpdateOrchestrator` — Mock der Library mit erfolgreicher Installation
- `FixedInstalledReleaseMetadataProvider` — Mock für Versions-Metadaten
- `FixedInstalledVersionProvider` — Mock für Installed-Version
- `SetDownloadPath()` — Hilfsmethode zum Redirect auf Temp-Verzeichnis
- `AuthenticateAdminAsync()` — Hilfsmethode für Admin-Login
- `WriteStatusAsync()` — Schreibt Test-Status-JSON in Temp-Verzeichnis

---

## Testinfrastruktur und Hilfsmethoden

### `UpdateOrchestratorAdapterTestFactory`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`

Factory für Adapter-Konstruktion in Unit-Tests.

| Methode | Zweck |
|---------|-------|
| `CreateStatusService()` | Erstellt `AutoUpdateStatusService` mit Mocks (stateStore, installedVersionProvider) |
| `Create(...)` | Factory-Methode mit optionalen Overrides: orchestrator, statusService, settingsStore, packageStore, installedProvider, platformResolver, logger |

**Hinweis:** Zentrale Konstruktion zur Vermeidung von Duplikation.

---

### `UpdateStatusTestData`
Datei: `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`

Builder für `AutoUpdateStatusSnapshot` Test-Fixtures.

| Methode | Zweck |
|---------|-------|
| `InstallingSnapshot(string availableVersion)` | Snapshot mit State=Installing, IsLocked=true, LockCreatedAt=UtcNow |
| `ReadyToInstallSnapshot(string availableVersion, AutoUpdatePackageDescriptor? package)` | Snapshot mit State=ReadyToInstall, IsLocked=false |

---

### `CapturingLogger<T>`
Testhelper (aus `FinanceManager.Tests.TestHelpers`).

Loggt alle Einträge in Liste `Entries`, ermöglicht Assertions über Log-Level und -Inhalte.

Beispiel:
```csharp
var logger = new CapturingLogger<UpdateOrchestratorAdapter>();
var adapter = CreateAdapter(logger: logger);
await adapter.StartInstallAsync(true);
logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
```

---

## Testabdeckung: Lock-Status-Reconciliation

**Aktuell NICHT abgedeckt:**
- Scenario: Cache sagt `IsLocked=true`, Live-Read via `GetLockCreatedAtAsync()` sagt `null` (Lock-Datei gelöscht) → Cache sollte bereinigt werden
- Automatische Reconciliation bei `GetStatusAsync()`, `CheckAsync()`, `StartInstallAsync()`

**Dies ist die zu implementierende Anforderung.**
