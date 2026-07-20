# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Lokalisierungsressourcen (Schritt 1)

Alle 11 Schlüssel in `Pages.resx`, `Pages.de.resx` und `Pages.en.resx` vorhanden:

- [x] `Msg_Loading` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_Locked` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_InstallRunning` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_NotReady` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_InvalidState` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_InvalidRequest` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_HealthTimeout` — vorhanden (alle 3 Dateien)
- [x] `Err_Update_VersionMismatch` — vorhanden (alle 3 Dateien)
- [x] `Msg_Update_Installing` — vorhanden (alle 3 Dateien)
- [x] `Msg_Update_WaitingForRestart` — vorhanden (alle 3 Dateien)
- [x] `Msg_Update_ConfirmDowntime` — vorhanden (alle 3 Dateien)

### `UpdateFileStore` (Service)

- [x] Methode `GetLockCreatedAtAsync` — umgestellt: liest ISO-8601-Zeitstempel aus der ersten Zeile der Lock-Datei via `DateTimeOffset.TryParse` mit `DateTimeStyles.RoundtripKind` (Zeile 78–84); Fallback auf `File.GetLastWriteTimeUtc` (Zeile 90); Rückgabe `null` bei nicht existierender Datei (Zeile 71–74)

### `UpdateExecutor` (Service)

- [x] Methode `StartAsync` — Catch-Zweig behandelt den Pfad nach Prozessstart: `IsInstallRunning = false` (Zeile 79), `DeleteLockAsync` (Zeile 80), `Failed`-Status geschrieben (Zeile 81–88), anschließend Rethrow (Zeile 90)

### `UpdateOrchestrator` (Service)

- [x] Neue Methode `ReconcileInstallingAsync(UpdateStatusDto, InstalledReleaseMetadataDto, bool, CancellationToken)` (private) — vorhanden (Zeile 180–204); defensiver Guard bei fehlender `AvailableVersion` (Zeile 182), Versionsabgleich → `NoUpdate` bzw. `Failed` mit `Err_Update_VersionMismatch`, persistiert bereinigten Status
- [x] Methode `WithRuntimeStateAsync` — ruft bei `Status == Installing` und inaktivem Lock `ReconcileInstallingAsync` auf (Zeile 172–177)

### `SetupUpdateViewModel` (ViewModel)

- [x] Neue Eigenschaft `InstallPhase` (`string?`) — vorhanden (Zeile 21)
- [x] Neue Methode `SetInstallPhase(string?)` — setzt Phase und ruft `RaiseStateChanged` (Zeile 149–153)
- [x] Methode `MarkHealthTimeout` — nutzt `SetError("Err_Update_HealthTimeout", fallback)` (Zeile 143–147)

### `SetupUpdateTab.razor` (Blazor-Komponente)

- [x] Zeile 26: `@Localizer["Msg_Loading"]` — durch Ressource abgedeckt
- [x] Zeile 30: Installationstext an `InstallPhase` gebunden (`@Localizer[_vm.InstallPhase ?? "Msg_Update_Installing"]`)
- [x] `PollHealthAsync` setzt `InstallPhase` (Start `Msg_Update_Installing` Zeile 180, nach Ausfall `Msg_Update_WaitingForRestart` Zeile 197/203)
- [x] Confirm-Meldung lokalisiert über `Msg_Update_ConfirmDowntime` (Zeile 160)

### Tests — Neue Tests

- [x] `GetLockCreatedAtAsync_ReadsTimestampFromLockContent` — vorhanden
- [x] `GetLockCreatedAtAsync_WhenContentUnparsable_FallsBackToLastWriteTime` — vorhanden
- [x] `StartAsync_WhenHostTerminationFails_ReleasesLockAndResetsFlag` — vorhanden
- [x] `GetStatusAsync_WhenInstallingAndVersionMatches_ReportsNoUpdate` — vorhanden
- [x] `GetStatusAsync_WhenInstallingAndVersionMismatch_ReportsFailed` — vorhanden
- [x] `GetStatusAsync_WhenInstallingAndLockActive_KeepsInstalling` — vorhanden
- [x] `ResetLockAsync_WhenLockIsStaleByContent_DeletesLock` — vorhanden
- [x] `SetInstallPhase_TransitionsFromInstallingToWaiting` (ViewModel-Phasentest) — vorhanden

### Tests — Betroffene bestehende Tests

- [x] `ResetLockAsync_WhenLockIsStale_DeletesLockAndWritesReason` — vorhanden (auf Inhaltszeitstempel umgestellt)
- [x] `ResetLockAsync_WhenLockIsFresh_RefusesReset` — vorhanden
- [x] `SetupUpdateViewModelTests` (InstallPhase) — abgedeckt durch `SetInstallPhase_TransitionsFromInstallingToWaiting`
- [x] `SetupUpdateTabTests` — Render von `Msg_Loading` (Zeile 44) und `Msg_Update_WaitingForRestart` (Zeile 59–63)

### E2E-Tests (Pflicht)

- [x] `StartInstall_ReturnsConflict_WhenUpdateLockIsActive` — vorhanden
- [x] `StartInstall_ReturnsNotFoundWithLocalizableCode_WhenNoReadyPackage` — vorhanden
- [x] `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk` — vorhanden
- [x] `Status_WhenInstallingAndVersionMatchesAfterRestart_ReportsNoUpdate` — vorhanden
- [x] `Status_WhenInstallingAndVersionMismatchAfterRestart_ReportsFailed` — vorhanden

## Offene Aufgaben

Keine. Alle Planelemente sind vollständig im Code auffindbar.

## Hinweise

- Der Plan verweist auf einen Catch-Parameter `processStarted`; die Umsetzung verwendet einen einheitlichen Catch-Zweig ohne separates `processStarted`-Flag. Das erfüllt die geforderte Semantik (Flag-Reset + Lock-Löschung + `Failed`-Status bei jeder Ausnahme innerhalb `StartAsync`) und ist funktional deckungsgleich, da der Host bei Erfolg terminiert und den Catch-Pfad nicht erreicht.
- Der Plan nannte den Reconciliation-Erfolgsfall (`NoUpdate`) mit Persistierung von `LastError = null` und `DownloadedAssetName = null`; die Umsetzung setzt zusätzlich `AvailableVersion = null` und `AvailableUpdate = null` (Zeile 188–195). Dies ist eine konsistente Ergänzung im Rahmen des Plans (Erfolgs-Cleanup), keine Abweichung.
- Sanity-Check: `dotnet build FinanceManager.Web` läuft mit 0 Fehlern durch (nur bekannte NU1510/NU1903-Warnungen).
- Die Tasks-Datei (`tasks.md`) war bereits vollständig auf `Erledigt` mit Testnachweisen und deckt sich mit den Prüfergebnissen; keine Statusänderung erforderlich.
