# Testbestand und Testluecken

## Bestehende Tests

### Adapter

`FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs` enthaelt bereits Reset-Tests:

- `ResetLockAsync_WhenNoLockActive_ThrowsIOException`
- `ResetLockAsync_WhenLockNotStale_ThrowsIOExceptionAndKeepsLock`
- `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus`

Diese Tests bestaetigen die aktuelle Grobklassifizierung im Adapter, pruefen aber nur `IOException`, nicht fachliche Fehlerarten.

### Controller-Integration

`FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs` enthaelt:

- `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk`
- Start-Install-Tests fuer `Err_Update_Locked`, `Err_Update_NotReady`, BadRequest bei fehlender Downtime-Bestaetigung

Es fehlen Reset-Fehlerfalltests fuer die neuen Fehlercodes.

### API-Client

`FinanceManager.Tests/Shared/ApiClientUpdateTests.cs` prueft:

- strukturierte Fehleruebernahme bei StartInstall
- Update-Endpunkte inklusive `POST /api/setup/update/lock/reset`

Der API-Client sollte fuer neue Reset-Fehlercodes vermutlich keinen neuen Pfad brauchen; ein Test fuer Fehlercode-/Message-Uebernahme beim Reset waere dennoch sinnvoll.

### ViewModel

`FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs` prueft viele Update-UI-Faelle, u. a.:

- Fehleruebernahme bei `StartInstallAsync`
- Ribbon-Zustaende
- Load/Save/Dirty-Verhalten
- Installationsstatus

Es gibt noch keinen Test fuer:

- Reset-Fehlercode wird in `LastErrorCode`/`LastError` sichtbar
- erfolgreicher Reset laedt Status neu

### Razor-Komponente

`FinanceManager.Tests/Components/SetupUpdateTabTests.cs` existiert und sollte fuer UI-nahe Anzeige oder Ribbon-Interaktion geprueft werden. Fuer die Anforderung duerfte ein ViewModel-Test meist zielgenauer sein, weil `SetupUpdateTab.razor` nur `_vm.LastError` rendert.

## Empfohlene neue Tests

### Adapter-Tests

- `ResetLockAsync_WhenNoLockActive_ThrowsTypedNoLock`
- `ResetLockAsync_WhenLockNotStale_ThrowsTypedLockNotStale`
- `ResetLockAsync_WhenDeleteReturnsFalse_ThrowsTypedLockDeleteFailed`
- `ResetLockAsync_WhenDeleteThrowsIOException_ThrowsTypedLockDeleteFailed`
- `ResetLockAsync_WhenGetLockCreatedAtThrowsIOException_ThrowsTypedResetFailed`
- `ResetLockAsync_WhenLockStale_DeletesLockAndUpdatesStatus`

### Controller-Tests

Mit Test-Orchestrator oder Integrationstest:

- `ResetLock_ReturnsConflictWithNoLockCode`
- `ResetLock_ReturnsConflictWithLockNotStaleCode`
- `ResetLock_ReturnsConflictWithDeleteFailedCode`
- `ResetLock_ReturnsConflictOrServerErrorWithResetFailedCode`
- `ResetLock_DoesNotReturnInstallRunningForResetIOException`

### API-Client-Tests

- `Updates_ResetLockAsync_WhenConflict_PreservesApiErrorCodeAndMessage`

### ViewModel-Tests

- `ResetLockAsync_WhenApiReportsSpecificError_SetsLocalizedError`
- `ResetLockAsync_WhenSuccessful_ReloadsStatus`

### Ressourcen-Tests

Falls es im Projekt bereits Ressourcenvollstaendigkeitspruefungen gibt, sollten die neuen Keys in `Pages.de.resx`, `Pages.en.resx` und `Pages.resx` enthalten sein. Falls nicht, reicht eine gezielte Assertion ueber `IStringLocalizer` oder ein einfacher Ressourcenschluesseltest.

## Regression gegen Pauschalisierung

Mindestens ein Test sollte explizit sicherstellen, dass der Reset-Pfad bei bekannter Reset-Fehlerart nicht `Err_Update_InstallRunning` ausgibt. Das ist der Kern der Anforderung und schuetzt gegen Rueckfall auf die bisherige Catch-All-Logik.

