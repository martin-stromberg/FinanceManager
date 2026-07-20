# Umsetzungsplan: Automatisches Update reparieren

## Übersicht

Das bestehende Update-Subsystem (`UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `SetupUpdateTab`) wird an vier Stellen stabilisiert: (1) zuverlässige Lock-Alterserkennung und garantierte Lock-Freigabe bei Fehlern, (2) robuster Übergang von Installer-Ausführung zu Dienst-Neustart inklusive Post-Update-Versionsabgleich, (3) vollständige Lokalisierung der Fehler- und UI-Texte und (4) ein sichtbarer Wartestatus während der Installation. Es handelt sich ausschließlich um Änderungen an bestehenden Klassen und Ressourcen; keine neuen Datenbankstrukturen und keine neuen Konfigurationsparameter.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Lock-Altersbestimmung (`UpdateFileStore.GetLockCreatedAtAsync`) | Zeitstempel aus dem **Inhalt** der `update.lock`-Datei lesen (wird bereits in `TryCreateLockAsync` als ISO-8601 geschrieben) statt `File.GetCreationTimeUtc` | `CreationTime` ist auf Linux-Dateisystemen unzuverlässig; der Inhalt ist plattformunabhängig und bereits vorhanden. Kein neues Datenformat nötig. |
| Fortschrittsanzeige während Installation | UI-seitiger **Phasen-Indikator** (`InstallPhase` im `SetupUpdateViewModel`), gespeist aus dem bestehenden Health-Polling, statt neuer `UpdateStatusKind`-Enumwerte (`Restarting`/`ValidatingInstallation`) | Der Host-Prozess wird während der Installation terminiert und kann keine Zwischenstatus in `status.json` persistieren. Der einzige verlässliche Fortschritt entsteht clientseitig beim Health-Poll (Ausfall beobachtet → Wiedererreichbarkeit). Kein DTO-/Enum-Bruch. |
| Post-Update-Validierung | **Reconciliation** im `UpdateOrchestrator` beim Statuslesen: Ist der gespeicherte Status `Installing`, der Lock aber weg, wird `InstalledVersion` gegen die Ziel-`AvailableVersion` abgeglichen → `NoUpdate` (Erfolg) bzw. `Failed` (Versionsabweichung) | Transaction-Script-Muster: verifizierbar ohne Host-Termination, funktioniert nach Neustart, nutzt vorhandene `release-metadata.json`-Auslesung. Erfüllt „Bestätigung, dass neue Version tatsächlich lädt". |
| Lock-Freigabe bei Fehler nach Prozessstart (`UpdateExecutor.StartAsync`) | Catch-Zweig auch für den Pfad `processStarted == true`: `IsInstallRunning` zurücksetzen, Lock löschen, `Failed`-Status schreiben | Bei einer Ausnahme nach `StartScript()` terminiert der Host in der Regel nicht; ohne Bereinigung bleibt ein verwaister Lock und ein dauerhaft gesetztes In-Memory-Flag zurück. Das Installer-Skript räumt `update.lock` idempotent (`rm -f`) ebenfalls ab, sodass kein Race entsteht. |
| Fehlermeldungs-Lokalisierung | `Err_Update_*`-Schlüssel in `Pages.resx`/`.de`/`.en` ergänzen; API liefert weiterhin nur den Code, `BaseViewModel.SetError` löst über `IStringLocalizer<Pages>` auf | Folgt dem bestehenden Muster (`SetError(code, fallback)` → `Localizer[code]`). Keine Änderung an API-Vertrag oder `ApiErrorDto`. |

## Programmabläufe

### Installation starten (unverändert im Ablauf, gehärtet)

1. UI ruft `SetupUpdateViewModel.StartInstallAsync(confirmDowntime: true)` → `UpdateController.StartInstall` → `UpdateOrchestrator.StartInstallAsync`.
2. `StartInstallAsync` prüft `confirmDowntime`, liest Status via `GetStatusAsync`, verweigert bei `IsLocked`/`Installing` (`IOException`) bzw. nicht `Ready` (`FileNotFoundException`).
3. `UpdateExecutor.StartAsync` erstellt Lock (`TryCreateLockAsync`), setzt `IsInstallRunning = true`, revalidiert das ZIP, generiert das Skript (`IUpdateScriptGenerator.GenerateAsync`), schreibt `Installing`-Status, startet den Prozess (`IUpdateProcessRunner.StartScript`) und ruft `IUpdateHostTerminator.StopApplication`.
4. **Neu (Härtung):** Wirft ein Schritt nach `StartScript()` eine Ausnahme, werden im Catch `IsInstallRunning = false` gesetzt, der Lock gelöscht und `Failed`-Status geschrieben.

Beteiligte Klassen/Komponenten: `UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `UpdateScriptGenerator`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`.

### Lock-Reset (gehärtet)

1. UI ruft `SetupUpdateViewModel.ResetLockAsync` → `UpdateController.ResetLock` → `UpdateOrchestrator.ResetLockAsync`.
2. Verweigerung bei `_executor.IsInstallRunning` (`IOException` → `Err_Update_InstallRunning`).
3. `GetLockCreatedAtAsync` liefert jetzt den **aus dem Lock-Inhalt gelesenen** Zeitstempel; ohne Lock → `IOException` (`No update lock is active.`).
4. Staleness-Prüfung gegen `max(HealthTimeoutSeconds, 60s)`; ist der Lock alt genug, wird er gelöscht und der Grund im Status vermerkt.

Beteiligte Klassen/Komponenten: `UpdateOrchestrator`, `UpdateFileStore`.

### Post-Update-Reconciliation (neu)

1. Nach Neustart ruft die UI `GetStatusAsync`; der Installer-Skript hat `update.lock` entfernt, `status.json` steht noch auf `Installing` mit `AvailableVersion` = Zielversion.
2. `WithRuntimeStateAsync` erkennt: `Status == Installing` **und** kein aktiver Lock.
3. Abgleich `InstalledVersion` (aus `InstalledReleaseMetadataProvider`) gegen die zuvor gespeicherte Zielversion:
   - Gleich → neuer Status `NoUpdate`, `LastError = null`, `DownloadedAssetName = null` wird persistiert (Erfolg).
   - Ungleich → Status `Failed`, `LastError`-Marker `Err_Update_VersionMismatch` wird persistiert.
4. Solange der Lock noch aktiv ist (Skript läuft), bleibt der Status `Installing` und die UI zeigt den Wartestatus.

Beteiligte Klassen/Komponenten: `UpdateOrchestrator`, `UpdateFileStore`, `InstalledReleaseMetadataProvider`.

### Fortschritts-/Wartestatus-Anzeige (neu)

1. Nach erfolgreichem `StartInstallAsync` (`Installing == true`) startet `SetupUpdateTab.PollHealthAsync`.
2. Der Poll setzt über das ViewModel eine Phasen-Property (`InstallPhase`): zunächst „Installation läuft/Neustart", nach beobachtetem Ausfall „Warte auf Wiedererreichbarkeit".
3. Die Razor-Komponente rendert den lokalisierten Wartetext anhand von `InstallPhase`.
4. Bei Wiedererreichbarkeit → Reload; bei Timeout → `MarkHealthTimeout()` (lokalisierter `Err_Update_HealthTimeout`).

Beteiligte Klassen/Komponenten: `SetupUpdateTab.razor`, `SetupUpdateViewModel`.

### Lokalisierte Fehleranzeige (gehärtet)

1. API-Endpunkte werfen `ApiErrorDto.Create(Origin, "Err_Update_*", message)`.
2. `ApiClient` legt Code in `LastErrorCode` ab; `SetupUpdateViewModel.HandleException` ruft `SetError(LastErrorCode, LastError)`.
3. `BaseViewModel.SetError` löst `Localizer["Err_Update_*"]` gegen `Pages`-Ressourcen auf und zeigt den übersetzten Text.

Beteiligte Klassen/Komponenten: `UpdateController`, `SetupUpdateViewModel`, `BaseViewModel`, `Pages.resx`/`.de`/`.en`.

## Neue Klassen

Keine. Alle Änderungen erfolgen an bestehenden Klassen und Ressourcendateien.

## Änderungen an bestehenden Klassen

### `UpdateFileStore` (Service, `FinanceManager.Web/Services/Updates/UpdateFileStore.cs`)

- **Geänderte Methoden:** `GetLockCreatedAtAsync` — liest den ISO-8601-Zeitstempel aus der ersten Zeile der `update.lock`-Datei (via `DateTimeOffset.TryParse` mit `DateTimeStyles.RoundtripKind`); Fallback auf `File.GetLastWriteTimeUtc` nur, wenn der Inhalt nicht parsbar ist; unverändertes Verhalten (null) bei nicht existierender Datei.

### `UpdateExecutor` (Service, `FinanceManager.Web/Services/Updates/UpdateExecutor.cs`)

- **Geänderte Methoden:** `StartAsync` — der Catch-Zweig behandelt zusätzlich den Fall `processStarted == true`: `IsInstallRunning = false` setzen, `DeleteLockAsync` aufrufen und `Failed`-Status schreiben, bevor erneut geworfen wird. Bei erfolgreichem Durchlauf bleibt `IsInstallRunning = true` (Host terminiert).

### `UpdateOrchestrator` (Service, `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs`)

- **Neue Methoden:** `ReconcileInstallingAsync(UpdateStatusDto stored, InstalledReleaseMetadataDto installed, bool lockActive, CancellationToken ct)` (private) — führt den Post-Update-Versionsabgleich durch und persistiert den bereinigten Status; Rückgabe des bereinigten `UpdateStatusDto`.
- **Geänderte Methoden:** `WithRuntimeStateAsync` bzw. `GetStatusAsync` — ruft bei `Status == Installing` und inaktivem Lock `ReconcileInstallingAsync` auf.

### `SetupUpdateViewModel` (ViewModel, `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`)

- **Neue Eigenschaften:** `InstallPhase` (`UpdateInstallPhase`-Enum oder `string?`) — beschreibt die aktuelle Wartephase für die UI.
- **Neue Methoden:** `SetInstallPhase(...)` — setzt die Phase und ruft `RaiseStateChanged`.
- **Geänderte Methoden:** `MarkHealthTimeout` — nutzt weiterhin `SetError("Err_Update_HealthTimeout", fallback)`; der Fallback bleibt als letzte Rückfallebene, wird aber durch die neue Ressource überschrieben.

### `SetupUpdateTab.razor` (Blazor-Komponente, `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`)

- **Geänderte Bereiche:**
  - Zeile 26: `@Localizer["Msg_Loading"]` — Ressource wird bereitgestellt.
  - Zeile 30: hartcodierter Installationstext → lokalisierter Text basierend auf `InstallPhase` (`Msg_Update_Installing` / `Msg_Update_WaitingForRestart`).
  - `PollHealthAsync` setzt über das ViewModel die `InstallPhase` (Start = installing, nach `outageObserved` = waiting).
  - Optional im Rahmen dieser Anforderung: die für die Fehleranzeige relevante Überschrift/Confirm-Meldung (`StartInstallAsync`-Confirm, Zeile 160) lokalisieren (`Msg_Update_ConfirmDowntime`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine. Bestehende Normalisierung in `UpdateSettingsStore` (Clamping `CheckIntervalMinutes` [1,1440], `HealthTimeoutSeconds` [10,600]) bleibt unverändert.

## Konfigurationsänderungen

Keine neuen Einträge. `HealthTimeoutSeconds` (Default 120) wird lediglich als maßgeblicher Staleness-Schwellenwert im Code dokumentiert; keine Wertänderung.

## Seiteneffekte und Risiken

- **Bestehende Orchestrator-Tests:** `ResetLockAsync_WhenLockIsStale_DeletesLockAndWritesReason` und `ResetLockAsync_WhenLockIsFresh_RefusesReset` setzen das Lock-Alter über `File.SetCreationTimeUtc`. Da `GetLockCreatedAtAsync` künftig den Datei-**Inhalt** liest, müssen diese Tests den Zeitstempel stattdessen in die Lock-Datei schreiben.
- **Post-Update-Reconciliation:** Ein `Installing`-Status ohne aktiven Lock wird jetzt aktiv nach `NoUpdate`/`Failed` überführt. Falls ein Statusobjekt in Altbeständen `Installing` ohne `AvailableVersion` enthält, muss die Reconciliation defensiv (kein Abgleich → Status unverändert lassen oder `Failed` mit generischer Meldung) reagieren, um Fehlklassifikation zu vermeiden.
- **Lokalisierung:** Fehlt ein neuer Schlüssel in einer der drei Ressourcendateien, greift der Fallback-Text; kein Laufzeitfehler, aber inkonsistente Sprache. Alle drei Dateien konsistent pflegen.
- **UI-Polling:** `InstallPhase` wird während des Reloads verworfen; keine Persistenz nötig, da nach dem Reload die Reconciliation den finalen Status liefert.

## Umsetzungsreihenfolge

1. **Lokalisierungsressourcen ergänzen**
   - Voraussetzungen: Keine (Ressourcendateien vorhanden).
   - Beschreibung: In `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` die Schlüssel `Msg_Loading`, `Err_Update_Locked`, `Err_Update_InstallRunning`, `Err_Update_NotReady`, `Err_Update_InvalidState`, `Err_Update_InvalidRequest`, `Err_Update_HealthTimeout`, `Err_Update_VersionMismatch`, `Msg_Update_Installing`, `Msg_Update_WaitingForRestart`, `Msg_Update_ConfirmDowntime` anlegen.

2. **`UpdateFileStore.GetLockCreatedAtAsync` auf Inhaltszeitstempel umstellen**
   - Voraussetzungen: Keine (`TryCreateLockAsync` schreibt bereits ISO-8601-Zeitstempel).
   - Beschreibung: Ersten Zeileninhalt lesen und parsen; Fallback `File.GetLastWriteTimeUtc`.

3. **`UpdateExecutor.StartAsync` Fehlerbereinigung nach Prozessstart**
   - Voraussetzungen: Keine.
   - Beschreibung: Catch-Zweig auch für `processStarted == true` (Flag zurücksetzen, Lock löschen, `Failed`-Status).

4. **`UpdateOrchestrator` Post-Update-Reconciliation**
   - Voraussetzungen: Schritt 2 (verlässlicher Lock-Status), Ressource `Err_Update_VersionMismatch` (Schritt 1).
   - Beschreibung: `ReconcileInstallingAsync` implementieren und in `WithRuntimeStateAsync`/`GetStatusAsync` einbinden.

5. **`SetupUpdateViewModel` Phasen-Indikator**
   - Voraussetzungen: Keine.
   - Beschreibung: `InstallPhase`-Property + Setter; `MarkHealthTimeout` unverändert (nutzt lokalisierten Code).

6. **`SetupUpdateTab.razor` Lokalisierung und Wartestatus**
   - Voraussetzungen: Schritte 1 und 5.
   - Beschreibung: `Msg_Loading` bereits durch Ressource abgedeckt; Installationstext an `InstallPhase` binden; `PollHealthAsync` setzt Phasen.

7. **Tests anpassen und ergänzen**
   - Voraussetzungen: Schritte 2–6.
   - Beschreibung: Bestehende Staleness-Tests umstellen; neue Unit-/Integration-/Komponenten-Tests hinzufügen (siehe Abschnitt Tests).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetLockCreatedAtAsync_ReadsTimestampFromLockContent` | `UpdateFileStoreTests` (neu) bzw. `UpdateMetadataAndPlatformTests` | Zeitstempel wird aus Lock-Inhalt gelesen, unabhängig von `CreationTime` |
| `GetLockCreatedAtAsync_WhenContentUnparsable_FallsBackToLastWriteTime` | wie oben | Fallback auf `LastWriteTimeUtc` bei unlesbarem Inhalt |
| `StartAsync_WhenHostTerminationFails_ReleasesLockAndResetsFlag` | `UpdateExecutorTests` | Ausnahme nach `StartScript()`: Lock gelöscht, `IsInstallRunning == false`, `Failed`-Status |
| `GetStatusAsync_WhenInstallingAndVersionMatches_ReportsNoUpdate` | `UpdateOrchestratorTests` | Reconciliation → Erfolg |
| `GetStatusAsync_WhenInstallingAndVersionMismatch_ReportsFailed` | `UpdateOrchestratorTests` | Reconciliation → `Failed` mit `Err_Update_VersionMismatch` |
| `GetStatusAsync_WhenInstallingAndLockActive_KeepsInstalling` | `UpdateOrchestratorTests` | Während laufendem Skript kein vorzeitiges Reconcile |
| `ResetLockAsync_WhenLockIsStaleByContent_DeletesLock` | `UpdateOrchestratorTests` | Staleness anhand Inhaltszeitstempel |
| `PollHealthAsync_SetsInstallPhases` bzw. ViewModel-Phasentest | `SetupUpdateViewModelTests` | `InstallPhase`-Übergänge installing → waiting |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ResetLockAsync_WhenLockIsStale_DeletesLockAndWritesReason` (`UpdateOrchestratorTests`) | Lock-Alter jetzt über Datei-Inhalt statt `File.SetCreationTimeUtc` setzen |
| `ResetLockAsync_WhenLockIsFresh_RefusesReset` (`UpdateOrchestratorTests`) | Frischer Zeitstempel muss in Lock-Inhalt geschrieben werden |
| `SetupUpdateViewModelTests` (Install/Status-Tests) | Neue `InstallPhase`-Property; ggf. Status-Reconciliation im gemockten Ablauf berücksichtigen |
| `SetupUpdateTabTests` | Geänderte Render-Texte (`Msg_Loading`, Wartestatus über Localizer) |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Vollständiger Ablauf Check → Ready → StartInstall liefert `Installing`, lokalisierter Fehlercode bei aktivem Lock | `UpdateControllerIntegrationTests` | Lock-Handling stabil, lokalisierte Fehlerantwort |
| Lock-Reset über API nach verwaistem (staleness-fähigem) Lock erfolgreich | `UpdateControllerIntegrationTests` | Reset funktioniert plattformunabhängig |
| Reconciliation: `Installing` + gelöschter Lock + passende Version → `NoUpdate`; abweichende Version → `Failed` | `UpdateControllerIntegrationTests` | Post-Update-Versionsabgleich |
| UI zeigt Wartestatus während Installation und übersetzte Fehlermeldung | `SetupUpdateTabTests` (bUnit) | Wartestatus + Lokalisierung |

### Betroffene bestehende E2E-Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UpdateControllerIntegrationTests` (Status-/Install-Workflow) | Zusätzliche Reconciliation-Zustände und lokalisierte Fehlercodes im Response berücksichtigen |

## Offene Punkte

Keine. Beide zuvor offenen Punkte wurden mit dem Anwender geklärt:

1. **`HealthTimeoutSeconds`-Default:** Bleibt bei 120 s (konfigurierbar 10–600 s); keine Änderung des Standardwerts.
2. **Automatischer Rollback bei fehlgeschlagenem Update:** Wird nicht umgesetzt (außerhalb des Scopes dieser Anforderung). Stattdessen macht die neue Reconciliation die Versionsabweichung über `Err_Update_VersionMismatch` sichtbar; ein Rollback-Mechanismus wäre eine separate Anforderung.
