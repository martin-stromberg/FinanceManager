# Plan-Review: Update-Lock-Handling

## Ergebnis

**Status:** Vollständig umgesetzt

Alle geplanten Planelemente wurden vollständig in den Code implementiert. Die Lock-Inconsistency-Behebung ist abgeschlossen.

---

## Umgesetzte Planelemente

### `UpdateOrchestratorAdapter` — Logikänderungen

- [x] Neue private Methode `ValidateLockCleanupAsync(CancellationToken)` — vorhanden
  - Prüft nach erfolgreicher Installation, ob Lock gelöscht wurde
  - Loggt Warning, wenn Lock noch vorhanden ist
  - Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`, Zeilen 210–217

- [x] Geänderte Methode `StartInstallAsync(bool, CancellationToken)` — Lock-Cleanup-Validierung implementiert
  - Ruft nach erfolgreicher Installation `ValidateLockCleanupAsync()` auf
  - Fehler bei Installation werden weiterhin geworfen
  - Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`, Zeilen 103–117

- [x] Geänderte Methode `ResetLockAsync(string?, CancellationToken)` — vereinheitlichte Lock-Prüfung
  - Nutzt `_packageStore.GetLockCreatedAtAsync()` als einzige Wahrheitsquelle
  - Prüft Staleness mit `_packageStore.IsLockStale()`
  - Fehlerbehandlung für alle klassifizierten Fehlertypen vorhanden
  - Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`, Zeilen 120–179

### Unit-Tests — Neue Tests für Lock-Cleanup-Validierung

- [x] `ValidateLockCleanupAsync_WhenLockAbsent_DoesNothing` — vorhanden
  - Verifiziert, dass kein Log-Output erfolgt, wenn Lock abwesend ist
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`, Zeilen 132–145

- [x] `ValidateLockCleanupAsync_WhenLockPresent_LogsWarning` — vorhanden
  - Verifiziert, dass Warning geloggt wird, wenn Lock noch vorhanden ist
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`, Zeilen 147–161

- [x] `StartInstallAsync_WhenSuccess_ValidatesCleanup` — vorhanden
  - Verifiziert, dass `GetLockCreatedAtAsync()` nach erfolgreicher Installation aufgerufen wird
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`, Zeilen 163–176

- [x] `StartInstallAsync_WhenLockStillActive_LogsWarning` — vorhanden
  - Verifiziert, dass keine Exception geworfen wird, wenn Lock nach Installation aktiv bleibt
  - Warning wird geloggt
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`, Zeilen 178–193

### Unit-Tests — ResetLockAsync Tests

- [x] `ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock` — vorhanden
  - Verifiziert Exception mit `NoLock` Kind, wenn `GetLockCreatedAtAsync()` null zurückgibt
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`, Zeilen 16–29

- [x] `ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale` — vorhanden
  - Verifiziert Staleness-Prüfung mit Exception `LockNotStale`
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`, Zeilen 31–46

- [x] `ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed` — vorhanden
  - Verifiziert Exception mit `LockDeleteFailed`, wenn Delete fehlschlägt
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`, Zeilen 48–63

- [x] `ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed` — vorhanden
  - Verifiziert I/O-Fehlerbehandlung beim Löschen
  - Exception wird korrekt als `LockDeleteFailed` mit `FailureSource.Updater` klassifiziert
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`, Zeilen 65–82

- [x] `ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed` — vorhanden
  - Verifiziert I/O-Fehlerbehandlung beim Auslesen des Lock-Status
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`, Zeilen 84–98

- [x] `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus` — vorhanden
  - Verifiziert erfolgreichen Lock-Reset: Datei wird gelöscht, Status aktualisiert
  - `IsLocked` wird auf `false` gesetzt, `LockCreatedAt` auf `null`
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`, Zeilen 100–118

### Integration-Tests (E2E)

- [x] `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk` — Lock-Reset erfolgreich
  - Verifiziert erfolgreicher Reset nach ausreichender Zeit
  - Lock-Datei wird tatsächlich gelöscht
  - Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`, Zeilen 122–146

- [x] `ResetLock_ReturnsSpecificErrorCode_WhenResetFailureIsClassified` — Alle Fehlertypen
  - Verifiziert korrekte HTTP-Status-Codes und Error-Codes für alle Fehlertypen
  - `NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed`
  - Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`, Zeilen 148–177

- [x] `ResetLock_Returns409NoLock_WhenNoLockFileExists` — Kein Lock vorhanden
  - Verifiziert Error-Code `Err_Update_Reset_NoLock`
  - Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`, Zeilen 203–225

- [x] `ResetLock_Returns409LockNotStale_WhenLockFileIsTooYoung` — Lock zu jung
  - Verifiziert Error-Code `Err_Update_Reset_LockNotStale`
  - Lock-Datei bleibt erhalten
  - Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`, Zeilen 227–254

- [x] `StartInstall_SucceedsAndLockRemains_WhenInstallerDoesNotCleanUpLock` — Lock-Cleanup-Validierung
  - Verifiziert, dass Installation erfolgreich ist, obwohl Installer Lock nicht löscht
  - Keine Exception geworfen, Warning wird geloggt
  - Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`, Zeilen 256–286

### Test-Infrastruktur

- [x] `UpdateOrchestratorAdapterTestFactory.Create()` — erweitert um `logger` Parameter
  - Ermöglicht Test-Setup mit benutzerdefiniertem Logger für Logging-Assertions
  - Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`, Zeilen 26–48

### Datenmodelle und Exceptions

- [x] `UpdateLockResetException` — alle erforderlichen Fehlertypen vorhanden
  - `NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed`
  - Exception wird korrekt mit `FailureSource` klassifiziert

### Keine Änderungen erforderlich

- [x] `UpdateStatusMapper` — keine Änderung nötig
  - Mappt bereits `snapshot.IsLocked` und `snapshot.LockCreatedAt` direkt
  - Datei: `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

- [x] `UpdateController` — keine Änderung nötig
  - Delegiert zu `UpdateOrchestratorAdapter`
  - Exception-Mapping funktioniert bereits korrekt

- [x] `SetupUpdateViewModel` — keine Änderung nötig
  - Button-Bedingung bleibt: `Busy || Status is null || !Status.IsLocked`

- [x] Datenbankmigrationen — keine erforderlich
  - System arbeitet dateibasiert (JSON)

---

## Offene Aufgaben

Keine — alle Planelemente vollständig umgesetzt.

---

## Hinweise für Nachimplementierung

### 1. `GetHealthTimeoutSecondsAsync()` — bewusst nicht implementiert

Der Plan erwähnt eine neue Methode `GetHealthTimeoutSecondsAsync()`, aber die Umsetzungsreihenfolge (Punkt 2) klärt, dass diese nicht nötig ist:

> **Hinweis:** Bisherige Logik ist bereits korrekt! Nur prüfen, dass `IsLockStale()` mit Library-Defaults arbeitet (keine `UpdateOptions.HealthTimeoutSeconds` nötig, da Library intern Schwelle berechnet).

Die Implementierung nutzt `_packageStore.IsLockStale()` direkt, eine Library-Methode, die selbst die Staleness-Schwelle bestimmt. Die Schwelle wird durch `_settingsStore.ApplyToOptions()` an die Library übertragen. Dies ist korrekt und erfordert keine zusätzliche `GetHealthTimeoutSecondsAsync()`-Methode.

### 2. Post-Restart-Reconciliation — nicht implementiert, aber nicht erforderlich

Der Plan erwähnt Post-Restart-Reconciliation als optional:

> **Vorbedingung:** Erst nach Klarstellung prüfen, ob notwendig.

Die Library-Methode `IAutoUpdateOrchestrator.GetStatusAsync()` reconciliert laut Dokumentation bereits selbst nach Neustart. Keine zusätzliche Reconciliation erforderlich.

### 3. Lock-Cleanup bei Installation fehlgeschlagen — beabsichtiges Verhalten

Falls die Installation fehlschlägt, wird der Lock **nicht** gelöscht. Dies ist konsistent mit dem Plan:

> Lock wird bei Fehler nicht gelöscht; nur Validierung + Warning-Log. Konsistent mit Anforderung: Lock als verwaist behandeln; Benutzer kann später manuell via Reset-Button aufräumen.

Test `UpdateOrchestratorAdapterTests.Adapter_MapsFailedResultToExpectedException()` bestätigt, dass Installation-Fehler durchgeworfen werden.

### 4. Desynchronisation als vermindertes Risiko

Nach dieser Behebung sind beide Pfade (Status-Display und Reset-Logik) auf die gleiche Quelle ausgerichtet: `GetLockCreatedAtAsync()`. Dies reduziert das Desynchronisations-Risiko deutlich, eliminiert es aber nicht völlig. Das ist akzeptabel und dokumentiert im Plan:

> Desynchronisation bleibt ein Risiko: Auch nach dieser Behebung könnte theoretisch zwischen Status-Abfrage und Reset-Aufruf der Lock gelöscht werden (z. B. von außen).

### 5. E2E-Tests validieren alle Szenarien

Die Integration-Tests decken alle geplanten Szenarien ab:
- ✓ Erfolgreicher Reset nach ausreichender Zeit
- ✓ Reset schlägt fehl, Lock zu jung
- ✓ Reset schlägt fehl, kein Lock vorhanden
- ✓ Installation mit Cleanup-Validierung (Lock bleibt, wenn Installer es nicht löscht)

---

## Zusammenfassung

Die Implementierung ist vollständig und folgt dem Umsetzungsplan konsistent. Alle Planelemente sind vorhanden oder bewusst nicht implementiert (weil nicht erforderlich). Die Testabdeckung ist umfassend und deckt alle Fehlerszenarien und Happy-Path-Fälle ab.
