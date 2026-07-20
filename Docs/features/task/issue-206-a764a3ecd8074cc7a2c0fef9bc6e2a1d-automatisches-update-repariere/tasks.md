# Tasks: Automatisches Update reparieren

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Lokalisierung | `Msg_Loading` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 2 | Lokalisierung | `Err_Update_Locked` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 3 | Lokalisierung | `Err_Update_InstallRunning` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 4 | Lokalisierung | `Err_Update_NotReady` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 5 | Lokalisierung | `Err_Update_InvalidState` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 6 | Lokalisierung | `Err_Update_InvalidRequest` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 7 | Lokalisierung | `Err_Update_HealthTimeout` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 8 | Lokalisierung | `Err_Update_VersionMismatch` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 9 | Lokalisierung | `Msg_Update_Installing` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 10 | Lokalisierung | `Msg_Update_WaitingForRestart` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 11 | Lokalisierung | `Msg_Update_ConfirmDowntime` in `Pages.resx`/`.de`/`.en` anlegen | Erledigt | Build + Testlauf grün |
| 12 | Logik (Lock) | `UpdateFileStore.GetLockCreatedAtAsync` auf Zeitstempel aus Lock-Inhalt umstellen (Fallback `LastWriteTimeUtc`) | Erledigt | `UpdateFileStoreTests`, `UpdateMetadataAndPlatformTests` |
| 13 | Logik (Executor) | `UpdateExecutor.StartAsync` Catch-Zweig für `processStarted == true`: Flag zurücksetzen, Lock löschen, `Failed`-Status | Erledigt | `UpdateExecutorTests` |
| 14 | Logik (Orchestrator) | `ReconcileInstallingAsync` in `UpdateOrchestrator` implementieren | Erledigt | `UpdateOrchestratorTests` |
| 15 | Logik (Orchestrator) | Reconciliation in `WithRuntimeStateAsync`/`GetStatusAsync` einbinden | Erledigt | `UpdateOrchestratorTests`, `UpdateControllerIntegrationTests` |
| 16 | UI (ViewModel) | `InstallPhase`-Property + Setter in `SetupUpdateViewModel` ergänzen | Erledigt | `SetupUpdateViewModelTests` |
| 17 | UI (Razor) | `SetupUpdateTab.razor`: Installationstext (Zeile 30) an `InstallPhase` binden und lokalisieren | Erledigt | `SetupUpdateTabTests` |
| 18 | UI (Razor) | `SetupUpdateTab.PollHealthAsync` setzt `InstallPhase` (installing → waiting) | Erledigt | `SetupUpdateTabTests` |
| 19 | UI (Razor) | Confirm-Meldung (`StartInstallAsync`, Zeile 160) über `Msg_Update_ConfirmDowntime` lokalisieren | Erledigt | Build erfolgreich (manuelle Prüfung, kein UI-Klick-Test) |
| 20 | Tests (Unit) | `UpdateFileStoreTests`: Zeitstempel aus Lock-Inhalt + Fallback | Erledigt | 2 neue Tests grün |
| 21 | Tests (Unit) | `UpdateExecutorTests`: Lock-Freigabe/Flag-Reset bei Fehler nach Prozessstart | Erledigt | `StartAsync_WhenHostTerminationFails_ReleasesLockAndResetsFlag` grün |
| 22 | Tests (Unit) | `UpdateOrchestratorTests`: Reconciliation Erfolg (`NoUpdate`) | Erledigt | `GetStatusAsync_WhenInstallingAndVersionMatches_ReportsNoUpdate` grün |
| 23 | Tests (Unit) | `UpdateOrchestratorTests`: Reconciliation Versionsabweichung (`Failed`) | Erledigt | `GetStatusAsync_WhenInstallingAndVersionMismatch_ReportsFailed` grün |
| 24 | Tests (Unit) | `UpdateOrchestratorTests`: `Installing` mit aktivem Lock bleibt `Installing` | Erledigt | `GetStatusAsync_WhenInstallingAndLockActive_KeepsInstalling` grün |
| 25 | Tests (Unit) | `UpdateOrchestratorTests`: Staleness-Tests auf Inhaltszeitstempel umstellen | Erledigt | `ResetLockAsync_WhenLockIsStale_DeletesLockAndWritesReason` umgestellt, `ResetLockAsync_WhenLockIsStaleByContent_DeletesLock` neu |
| 26 | Tests (UI) | `SetupUpdateViewModelTests`: `InstallPhase`-Übergänge | Erledigt | `SetInstallPhase_TransitionsFromInstallingToWaiting` grün |
| 27 | Tests (UI) | `SetupUpdateTabTests`: Render von `Msg_Loading` und Wartestatus | Erledigt | 2 neue bUnit-Tests grün |
| 28 | E2E | `UpdateControllerIntegrationTests`: lokalisierte Fehlercodes bei Lock/NotReady | Erledigt | `StartInstall_ReturnsConflict_WhenUpdateLockIsActive`, `StartInstall_ReturnsNotFoundWithLocalizableCode_WhenNoReadyPackage` grün |
| 29 | E2E | `UpdateControllerIntegrationTests`: Lock-Reset nach staleness-fähigem Lock | Erledigt | `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk` grün |
| 30 | E2E | `UpdateControllerIntegrationTests`: Reconciliation-Zustände nach Update | Erledigt | `Status_WhenInstallingAndVersionMatchesAfterRestart_ReportsNoUpdate`, `Status_WhenInstallingAndVersionMismatchAfterRestart_ReportsFailed` grün |
