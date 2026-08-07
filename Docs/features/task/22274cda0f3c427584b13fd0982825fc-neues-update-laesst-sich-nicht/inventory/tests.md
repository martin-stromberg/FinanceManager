# Tests und Hilfsmethoden

## Testklassen

### `UpdateOrchestratorAdapterLockAndScheduleTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`

**Zweck:** Abdeckung von `ResetLockAsync()` und `ScheduleAsync()` — die beiden wichtigsten Lock-Methoden

#### Lock-Reset-Tests (zentral für die Anforderung)

- **`ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock`** — Verifiziert, dass `UpdateLockResetException` mit `NoLock` geworfen wird, wenn `GetLockCreatedAtAsync()` `null` zurückgibt
  - Setzt Mock: `packageStore.GetLockCreatedAtAsync()` → `null`
  - Erwartet: Exception mit `Kind.NoLock` und `FailureSource.FinanceManager`
  - DeleteLockAsync darf nicht aufgerufen werden

- **`ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale`** — Verifiziert Lock-Staleness-Prüfung
  - Setzt Mock: `packageStore.IsLockStale(lockCreatedAt)` → `false`
  - Erwartet: Exception mit `Kind.LockNotStale`

- **`ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed`** — Verifiziert Delete-Fehlerbehandlung
  - Setzt Mock: `packageStore.DeleteLockAsync()` → `false`
  - Erwartet: Exception mit `Kind.LockDeleteFailed` und `FailureSource.FinanceManager`

- **`ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed`** — I/O-Fehler beim Löschen
  - Setzt Mock: `packageStore.DeleteLockAsync()` → wirft `IOException`
  - Erwartet: Exception mit `Kind.LockDeleteFailed` und `FailureSource.Updater`

- **`ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed`** — I/O-Fehler beim Abrufen
  - Setzt Mock: `packageStore.GetLockCreatedAtAsync()` → wirft `IOException`
  - Erwartet: Exception mit `Kind.ResetFailed` und `FailureSource.Updater`

- **`ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus`** — Erfolgreicher Reset-Fall
  - Setzt Mock: Lock existiert, ist stale, Delete erfolgreich
  - Erwartet: `DeleteLockAsync` aufgerufen, Status aktualisiert mit `IsLocked = false`, `LockCreatedAt = null`

#### Schedule-Tests

- **`ScheduleAsync_SavesScheduleAndAppliesToAutoUpdateOptions`** — Verifiziert Schedule-Speicherung
  - Setzt Mock: `settingsStore.SaveScheduleAsync()` → gibt gespeicherte Settings zurück
  - Verifiziert: `ApplyToOptions()` wird aufgerufen

---

### `UpdateOrchestratorAdapterTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`

**Zweck:** Allgemeine Adapter-Tests für Status-Mapping und Exception-Handling

- **`Adapter_MapsSnapshotToUpdateStatusDto`** — Verifiziert Status-DTO-Mapping
  - Erstellt Mock-Snapshot mit Read-Status
  - Prüft alle DTO-Felder nach Mapping

- **`Adapter_MapsFailedResultToExpectedException`** — Exception-Mapping für Install-Fehler
  - Tests: FileNotFoundException, IOException, ArgumentException
  - Verifiziert, dass Library-Fehler durchgeworfen werden

- **`Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto`** — Check-Ergebnis-Mapping
  - Verifiziert `UpdateCheckResultDto` mit gefundenen Updates

- **`Adapter_SaveSettings_AppliesToAutoUpdateOptions`** — Settings-Anwendung
  - Verifiziert, dass `ApplyToOptions()` nach Speichern aufgerufen wird

---

### `SetupUpdateViewModelTests`
Datei: `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`

**Zweck:** ViewModel-Tests für UI-Interaktionen

- **`StartInstallAsync_WhenApiReportsNotReady_DoesNotSetInstalling`** — Install-Fehlerbehandlung
  - Mock wirft `HttpRequestException` mit "not ready"
  - Prüft: `vm.Installing = false`, Fehler gesetzt

- **`ResetLockAsync_WhenApiReportsSpecificError_SetsError`** — Lock-Reset Fehlerbehandlung
  - Mock wirft Fehler mit "reset failed"
  - Prüft: Error-Code "Err_Update_Reset_NoLock" gesetzt

---

### `UpdateOrchestratorAdapterTests` (weitere)
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs` (weiterführende Tests)

- Status-Mapping-Tests
- Exception-Re-Throw-Tests
- GitHub-RateLimit-Fehlerbehandlung

---

## Hilfsmethoden und Factories

### `UpdateOrchestratorAdapterTestFactory`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`

Factory für Test-Setup; vereinheitlicht Mock-Erstellung und Konfiguration.

- **`Create(...)`** — Erstellt `UpdateOrchestratorAdapter`-Instance mit konfigurierbaren Mocks
  - Parameter: `orchestrator`, `statusService`, `settingsStore`, `packageStore`, `statusMapper`, `installedProvider`, `platformResolver`
  - Gibt sinnvolle Defaults zurück, wenn Parameter nicht übergeben

- **`CreateStatusService()`** — Erstellt vorkonfigurierte `AutoUpdateStatusService` Mock

---

### `UpdateStatusTestData`
Datei: `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`

Test-Daten-Generator für `AutoUpdateStatusSnapshot`.

- **`ReadyToInstallSnapshot(version, package)`** — Erzeugt Snapshot mit `State = ReadyToInstall`
  - Includes: Lock-Info, verfügbare Version, heruntergeladenes Paket

---

## Test-Abdeckung für Lock-Problem

**Bestehende Tests prüfen:**
1. ✓ NoLock-Fehler (wenn `GetLockCreatedAtAsync()` null)
2. ✓ LockNotStale-Fehler (wenn nicht alt genug)
3. ✓ LockDeleteFailed-Fehler (wenn Löschen fehlschlägt)
4. ✓ IOException-Fehlerbehandlung
5. ✓ Erfolgreicher Reset (Status wird aktualisiert)

**Nicht direkt getestet:**
- ✗ Inkonsistenz zwischen `snapshot.IsLocked` (für UI) und `GetLockCreatedAtAsync()` (für Reset)
- ✗ Lock-Datei existiert, aber wird nicht von `GetLockCreatedAtAsync()` erkannt
- ✗ Status-Update schlägt fehl, obwohl Lock-Datei gelöscht wurde
- ✗ Race-Bedingungen beim Lock-Cleanup nach Installation
- ✗ Lock-Reconciliation nach Neustart

