# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **Doppelter Code** — Die neue Methode `ReconcileLockStatusAsync` (Zeilen 236–259) dupliziert fast wörtlich das try/catch-Muster der bereits vorhandenen Methode `ValidateLockCleanupAsync` (Zeilen 213–234): beide rufen `_packageStore.GetLockCreatedAtAsync(ct)` auf, werfen `OperationCanceledException` unverändert weiter und loggen bei jeder anderen Exception nur mit unterschiedlichem Log-Level, um dann `return` auszuführen. Zusätzlich werden in `StartInstallAsync` (Zeilen 115 und 118) beide Methoden direkt hintereinander aufgerufen, wodurch `_packageStore.GetLockCreatedAtAsync` bei jedem erfolgreichen Installationsdurchlauf zweimal ausgeführt wird (bestätigt durch den geänderten Test `Adapter_StartInstallAsync_WhenSuccess_ValidatesLockCleanup`, der jetzt `Times.Exactly(2)` statt `Times.Once` erwartet).

  Empfehlung: Gemeinsame private Hilfsmethode extrahieren, z. B. `Task<(bool Succeeded, DateTimeOffset? LockCreatedAt)> TryGetLockCreatedAtAsync(CancellationToken ct, LogLevel failureLogLevel, string failureLogMessage)`, die von `ValidateLockCleanupAsync` und `ReconcileLockStatusAsync` genutzt wird. In `StartInstallAsync` den Lock-Zustand nur einmal abfragen und das Ergebnis sowohl für die Cleanup-Validierung als auch für die Cache-Reconciliation verwenden, statt den Package-Store zweimal anzusprechen.

### FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs (UpdateOrchestratorAdapterTests)

- **Doppelter Code** — Die neue private Factory-Methode `CreateAdapterForReconciliation` (Zeilen 282–302) dupliziert das Setup von `settingsStore` und `installedProvider` aus der bereits vorhandenen `CreateAdapterForInstall` (Zeilen 351–371) nahezu identisch (gleiches `UpdateSettingsDto`, gleiches `InstalledReleaseMetadataDto`, gleicher Aufruf von `UpdateOrchestratorAdapterTestFactory.Create` mit denselben Parametern). Das widerspricht dem im XML-Doc-Kommentar von `UpdateOrchestratorAdapterTestFactory` festgehaltenen Ziel, "a duplicated status-service factory and repeated adapter construction in every test" zu vermeiden.

  Empfehlung: Beide Hilfsmethoden zu einer einzigen zusammenführen (z. B. `CreateAdapterForInstall` um optionale Parameter in einheitlicher Reihenfolge erweitern und `CreateAdapterForReconciliation` entfernen, oder umgekehrt), sodass es nur eine private Factory-Methode für dieses Setup gibt.

- **Namenskonvention** — Die vier neuen Testmethoden `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedButFileIsAbsent_ClearsLock`, `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedAndFileExists_DoesNothing`, `Adapter_ReconcileLockStatusAsync_WhenGetLockThrowsIOException_LogsDebugAndContinues` und `Adapter_ReconcileLockStatusAsync_WhenGetLockThrowsOperationCanceledException_Propagates` (Zeilen 196–263) sind nach der privaten Methode `ReconcileLockStatusAsync` benannt, obwohl sie ausschließlich die öffentliche Methode `adapter.GetStatusAsync()` aufrufen und deren Verhalten prüfen. Das weicht von der in derselben Datei etablierten Konvention `Adapter_<öffentliche Methode>_<Szenario>` ab, die auch die im selben Change neu hinzugekommenen Tests `Adapter_GetStatusAsync_ReconcilesCacheBeforeMapping`, `Adapter_CheckAsync_ReconcilesCacheBeforeCheck` und `Adapter_StartInstallAsync_ReconcilesCacheAfterValidationBeforeReturn` befolgen.

  Empfehlung: Die vier Tests umbenennen, z. B. `Adapter_GetStatusAsync_WhenCacheIsLockedButFileIsAbsent_ClearsLock`, damit der Testname die tatsächlich aufgerufene öffentliche Methode widerspiegelt.

- **Doppelter Code (Testredundanz)** — `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedButFileIsAbsent_ClearsLock` (Zeilen 197–212) und `Adapter_GetStatusAsync_ReconcilesCacheBeforeMapping` (Zeilen 266–280) bauen einen nahezu identischen Testfall auf (gesperrter Cache-Status, kein Lock auf dem Dateisystem, Aufruf von `adapter.GetStatusAsync()`) mit copy-and-paste-gleichem Arrange-Block; sie unterscheiden sich nur darin, ob der Cache-Snapshot oder das zurückgegebene DTO/die Aufrufanzahl geprüft wird.

  Empfehlung: Beide Tests zu einem zusammenführen, der sowohl das zurückgegebene DTO als auch den persistierten Cache-Zustand (und optional die Aufrufanzahl) in einem Testfall prüft, um den doppelten Arrange-Block zu vermeiden.

## Geprüfte Dateien

- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
