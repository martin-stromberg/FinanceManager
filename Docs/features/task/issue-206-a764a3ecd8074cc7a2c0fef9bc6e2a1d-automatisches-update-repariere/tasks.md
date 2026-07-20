# Tasks: Automatisches Update reparieren

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Lokalisierung | `Msg_Loading` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 2 | Lokalisierung | `Err_Update_Locked` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 3 | Lokalisierung | `Err_Update_InstallRunning` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 4 | Lokalisierung | `Err_Update_NotReady` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 5 | Lokalisierung | `Err_Update_InvalidState` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 6 | Lokalisierung | `Err_Update_InvalidRequest` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 7 | Lokalisierung | `Err_Update_HealthTimeout` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 8 | Lokalisierung | `Err_Update_VersionMismatch` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 9 | Lokalisierung | `Msg_Update_Installing` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 10 | Lokalisierung | `Msg_Update_WaitingForRestart` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 11 | Lokalisierung | `Msg_Update_ConfirmDowntime` in `Pages.resx`/`.de`/`.en` anlegen | Offen | — |
| 12 | Logik (Lock) | `UpdateFileStore.GetLockCreatedAtAsync` auf Zeitstempel aus Lock-Inhalt umstellen (Fallback `LastWriteTimeUtc`) | Offen | — |
| 13 | Logik (Executor) | `UpdateExecutor.StartAsync` Catch-Zweig für `processStarted == true`: Flag zurücksetzen, Lock löschen, `Failed`-Status | Offen | — |
| 14 | Logik (Orchestrator) | `ReconcileInstallingAsync` in `UpdateOrchestrator` implementieren | Offen | — |
| 15 | Logik (Orchestrator) | Reconciliation in `WithRuntimeStateAsync`/`GetStatusAsync` einbinden | Offen | — |
| 16 | UI (ViewModel) | `InstallPhase`-Property + Setter in `SetupUpdateViewModel` ergänzen | Offen | — |
| 17 | UI (Razor) | `SetupUpdateTab.razor`: Installationstext (Zeile 30) an `InstallPhase` binden und lokalisieren | Offen | — |
| 18 | UI (Razor) | `SetupUpdateTab.PollHealthAsync` setzt `InstallPhase` (installing → waiting) | Offen | — |
| 19 | UI (Razor) | Confirm-Meldung (`StartInstallAsync`, Zeile 160) über `Msg_Update_ConfirmDowntime` lokalisieren | Offen | — |
| 20 | Tests (Unit) | `UpdateFileStoreTests`: Zeitstempel aus Lock-Inhalt + Fallback | Offen | — |
| 21 | Tests (Unit) | `UpdateExecutorTests`: Lock-Freigabe/Flag-Reset bei Fehler nach Prozessstart | Offen | — |
| 22 | Tests (Unit) | `UpdateOrchestratorTests`: Reconciliation Erfolg (`NoUpdate`) | Offen | — |
| 23 | Tests (Unit) | `UpdateOrchestratorTests`: Reconciliation Versionsabweichung (`Failed`) | Offen | — |
| 24 | Tests (Unit) | `UpdateOrchestratorTests`: `Installing` mit aktivem Lock bleibt `Installing` | Offen | — |
| 25 | Tests (Unit) | `UpdateOrchestratorTests`: Staleness-Tests auf Inhaltszeitstempel umstellen | Offen | — |
| 26 | Tests (UI) | `SetupUpdateViewModelTests`: `InstallPhase`-Übergänge | Offen | — |
| 27 | Tests (UI) | `SetupUpdateTabTests`: Render von `Msg_Loading` und Wartestatus | Offen | — |
| 28 | E2E | `UpdateControllerIntegrationTests`: lokalisierte Fehlercodes bei Lock/NotReady | Offen | — |
| 29 | E2E | `UpdateControllerIntegrationTests`: Lock-Reset nach staleness-fähigem Lock | Offen | — |
| 30 | E2E | `UpdateControllerIntegrationTests`: Reconciliation-Zustände nach Update | Offen | — |
