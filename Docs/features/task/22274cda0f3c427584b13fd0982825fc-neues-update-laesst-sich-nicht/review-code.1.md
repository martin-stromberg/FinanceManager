# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs (UpdateOrchestratorAdapterTests)

- **Doppelter Code** — Die neu eingefügte private Klasse `CapturingLogger<T>` samt `CapturedLogEntry`-Record (Zeilen 215–234) dupliziert nahezu 1:1 die bereits existierende private Klasse `CapturingLogger<T>` in `FinanceManager.Tests/Infrastructure/RequestLoggingMiddlewareTests.cs` (Zeilen 85–108). Beide implementieren `ILogger<T>` identisch (BeginScope gibt null zurück, IsEnabled immer true, Log sammelt Einträge in einer Liste); der einzige Unterschied ist, dass die bestehende Variante zusätzlich den State-Text erfasst.

  Empfehlung: Eine gemeinsame `CapturingLogger<T>`-Testklasse (z. B. in `FinanceManager.Tests/TestHelpers/`, analog zu `TestLoggerHelper.cs`) extrahieren und von beiden Testklassen (`UpdateOrchestratorAdapterTests` und `RequestLoggingMiddlewareTests`) wiederverwenden, statt sie pro Testdatei neu zu definieren.

- **Testqualität (redundante Tests)** — `ValidateLockCleanupAsync_WhenLockPresent_LogsWarning` (Zeilen 147–161) und `StartInstallAsync_WhenLockStillActive_LogsWarning` (Zeilen 178–193) haben identisches Arrange (Success-Outcome, Lock vorhanden) und identisches Act (`adapter.StartInstallAsync(true)`) und prüfen im Ergebnis denselben fachlichen Fall (genau ein Warning-Logeintrag). Die zweite Testmethode fügt lediglich eine überflüssige `act.Should().NotThrowAsync()`-Zwischenprüfung hinzu, die keinen zusätzlichen Erkenntniswert liefert, da ein direktes `await` bereits jede Exception hätte durchschlagen lassen. Beide Tests decken denselben Fall doppelt ab.

  Empfehlung: Einen der beiden Tests entfernen bzw. beide zu einem einzigen Test zusammenführen, der Arrange/Act einmal ausführt und den Warning-Logeintrag prüft.

- **Namenskonventionen** — Die vier neuen Testmethoden (Zeilen 131–193) folgen zwei unterschiedlichen, voneinander abweichenden Namensschemata (`ValidateLockCleanupAsync_*` für zwei Tests, `StartInstallAsync_*` für die anderen zwei), während alle bestehenden Tests in dieser Klasse einheitlich mit dem Präfix `Adapter_*` benannt sind (`Adapter_MapsSnapshotToUpdateStatusDto`, `Adapter_CheckAsync_MapsSuccessOutcomeToUpdateCheckResultDto`, `Adapter_SaveSettings_AppliesToAutoUpdateOptions`, `Adapter_CheckAsync_WhenRateLimitedResult_ReturnsFriendlyMessage`). Zusätzlich benennt `ValidateLockCleanupAsync_WhenLockAbsent_DoesNothing` und `ValidateLockCleanupAsync_WhenLockPresent_LogsWarning` den Test nach der privaten Implementierungsmethode `ValidateLockCleanupAsync` des Adapters statt nach dem tatsächlich aufgerufenen öffentlichen Verhalten (`StartInstallAsync`).

  Empfehlung: Alle vier neuen Tests konsistent mit dem in der Klasse etablierten Präfix benennen, z. B. `Adapter_StartInstallAsync_WhenLockAbsentAfterInstall_DoesNotLog` bzw. `Adapter_StartInstallAsync_WhenLockStillPresentAfterInstall_LogsWarning`, ohne Bezug auf den internen Methodennamen `ValidateLockCleanupAsync`.

## Geprüfte Dateien

- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
