# Umsetzungsplan: Update-Lock-Handling — Inkonsistenzen im Status und Reset

## Übersicht

Der Plan adressiert eine Statusinkonsistenz im Update-Lock-System: Die UI meldet einen aktiven Lock, aber der Reset-Button antwortet mit "kein aktiver Lock". Die Behebung erfolgt durch drei Maßnahmen: (1) Unified Lock-Abfrage — beide Status- und Reset-Pfade nutzen die gleiche Quelle, (2) Atomare Status-Updates — Lock-Datei-Existenz und Status-JSON bleiben konsistent, (3) Post-Install-Reconciliation — Lock-Status wird nach Installation und Neustart validiert. Betroffen sind `UpdateOrchestratorAdapter`, `UpdateStatusMapper`, `SetupUpdateViewModel`, `UpdateController` und die Tests für Lock-Reset-Logik sowie neue E2E-Tests für die Inkonsistenz-Szenarien.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Unified Lock-Abfrage | `IAutoUpdatePackageStore.GetLockCreatedAtAsync()` als Single Source of Truth für beide Pfade (Status-Abfrage und Reset) | Eliminiert Desynchronisationen; beide Pfade lesen aus der gleichen Library-Schnittstelle |
| Lock-Staleness-Validierung in UI | Button "UpdateResetLock" nur aktiviert, wenn `HasActiveLockAsync() && IsLockStale()` beide true sind | Verhindert Aktivierung bei zu jungem Lock; synchronisiert UI-Kriterium mit Reset-Logik |
| Post-Installation-Cleanup-Validierung | Explizite Validierungslogik in `UpdateExecutor` nach Installer-Abschluss, bevor Status auf `Completed` gesetzt wird | Sichert ab, dass Lock-Datei wirklich gelöscht wurde; bei Fehler bleibt Status `Installing` und Lock bestehen |
| Post-Restart-Reconciliation | Separate Methode `ReconcileLocksAfterRestartAsync()` in `UpdateOrchestrator`, aufgerufen während Startup | Räumt verwaiste Locks auf; prüft Lock-Datei-Existenz vs. Status-JSON und reconciliert Unterschiede |
| Exception-Klassifizierung | Bestehende `UpdateLockResetException` mit `Kind` (NoLock, LockNotStale, etc.) und `FailureSource` bleibt unverändert | Abdeckung aller bekannten Fehlertypen ist bereits vollständig; keine neuen Typen erforderlich |

## Programmabläufe

### 1. Status-Abfrage mit konsistenter Lock-Prüfung

1. `UpdateController.GetStatusAsync()` wird aufgerufen
2. `UpdateOrchestratorAdapter.GetStatusAsync()` delegiert an `IAutoUpdateOrchestrator` (Library)
3. Library gibt `AutoUpdateStatusSnapshot` zurück, enthält `IsLocked` basierend auf `GetLockCreatedAtAsync()`
4. `UpdateStatusMapper.MapAsync()` konvertiert zu `UpdateStatusDto` mit `IsLocked` und `LockCreatedAt`
5. UI empfängt `IsLocked = true` und `LockCreatedAt = [Zeitstempel]`

**Beteiligte Klassen/Komponenten:** `UpdateController`, `UpdateOrchestratorAdapter`, `IAutoUpdateOrchestrator` (Library), `UpdateStatusMapper`, `AutoUpdateStatusSnapshot`, `UpdateStatusDto`

### 2. Lock-Reset-Anfrage mit Staleness-Prüfung

1. `UpdateController.ResetLockAsync()` wird aufgerufen
2. `UpdateOrchestratorAdapter.ResetLockAsync()` prüft zuerst `HasActiveLockAsync()` (Library-Wrapper)
3. Falls kein Lock: Exception mit `Kind = NoLock` werfen
4. Falls Lock vorhanden: `IsLockStale()` prüfen (Lock-Alter >= Staleness-Schwelle)
5. Falls Lock nicht alt genug: Exception mit `Kind = LockNotStale` werfen
6. Falls Lock stale: `packageStore.DeleteLockAsync()` aufrufen
7. Bei erfolgreicher Löschung: Lock-Status in Status-JSON aktualisieren und `Completed` Status setzen
8. Bei Lösch-Fehler: Exception mit `Kind = LockDeleteFailed` werfen

**Beteiligte Klassen/Komponenten:** `UpdateController`, `UpdateOrchestratorAdapter`, `IAutoUpdatePackageStore` (Library), `UpdateExecutor`, `UpdateLockResetException`, Konfiguration `UpdateOptions.HealthTimeoutSeconds`

### 3. Post-Installation Lock-Cleanup-Validierung

1. `UpdateExecutor.ExecuteInstallerAsync()` ruft Installer-Skript auf
2. Installer-Skript führt Update aus und versucht, Lock-Datei zu löschen
3. Nach Installer-Abschluss (erfolgreich oder fehlgeschlagen): `ValidateLockCleanupAsync()` aufrufen
4. `ValidateLockCleanupAsync()` prüft: `HasActiveLockAsync()` == false?
5. Falls Lock noch vorhanden: Retry-Schleife (bis zu N Versuche mit exponential backoff) oder `DeleteLockAsync()` direkt aufrufen
6. Falls Lock nach Retry immer noch vorhanden: Status auf `Failed` mit Fehler-Details setzen, Exception loggen
7. Falls Lock erfolgreich gelöscht: Status auf `Completed` setzen

**Beteiligte Klassen/Komponenten:** `UpdateExecutor`, `IAutoUpdatePackageStore`, `UpdateFileStore`, Konfiguration für Retry-Logik

### 4. Post-Restart Reconciliation

1. `UpdateOrchestrator.InitializeAsync()` oder ähnliche Startup-Methode wird aufgerufen
2. `ReconcileLocksAfterRestartAsync()` wird aufgerufen
3. Prüfe Status-JSON: Was ist der aktuelle Zustand?
4. Prüfe Datei-System: Existiert Lock-Datei?
5. Bei Mismatch:
   - **Datei existiert, Status != `Installing`**: Lock-Datei löschen (verwaist); Status bleibt erhalten
   - **Datei existiert, Status == `Installing`**: Lasse Lock bestehen (Installation ist tatsächlich in Progress)
   - **Datei existiert nicht, Status == `Installing`**: Status auf `Failed` setzen mit Grund "Installation interrupted"
   - **Datei existiert nicht, Status == `Completed`**: Keine Aktion (alles ok)
6. Logging für alle Reconciliation-Aktionen

**Beteiligte Klassen/Komponenten:** `UpdateOrchestrator`, `IAutoUpdatePackageStore`, `UpdateFileStore`, Startup-Pipeline

### 5. UI-Button-Aktivierung mit Staleness-Kriterium

1. `SetupUpdateViewModel` ruft `UpdateController.GetStatusAsync()` auf (Polling)
2. ViewModel erhält `Status` mit `IsLocked` und `LockCreatedAt`
3. Für Button "UpdateResetLock" Aktivierung prüfen:
   - `Status.IsLocked == true`? UND
   - `IsLockStale()` == true? (basierend auf `LockCreatedAt` und Schwelle)
   - `!Busy`? (kein anderer Prozess läuft)
4. Button nur aktivieren, wenn alle drei Bedingungen erfüllt sind
5. Bei Klick: `UpdateController.ResetLockAsync()` aufrufen

**Beteiligte Klassen/Komponenten:** `SetupUpdateViewModel`, `UpdateController`, `SetupUpdateTab.razor`, Button-Binding-Logik, `IsLockStale()` Helper-Funktion

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| (keine neuen Klassen erforderlich) | — | —  |

**Hinweis:** Die Fehlerklassifizierung durch `UpdateLockResetException` ist bereits vorhanden. Keine neuen Klassen nötig, nur Verhaltensänderungen in bestehenden Klassen.

## Änderungen an bestehenden Klassen

### `UpdateOrchestratorAdapter` (Logikklasse / Service)

- **Geänderte Methoden:**
  - `GetStatusAsync()` — Sicherstellen, dass die Lock-Abfrage über die Library-Schnittstelle `IAutoUpdateOrchestrator.GetStatusAsync()` erfolgt und `IsLocked` korrekt aus `GetLockCreatedAtAsync()` abgeleitet wird (keine Alternative Lock-Quellen)
  - `ResetLockAsync()` — Logik erweitern:
    1. Zuerst `HasActiveLockAsync()` prüfen → `NoLock` Exception werfen, falls keine Lock
    2. Dann `IsLockStale()` prüfen → `LockNotStale` Exception werfen, falls zu jung
    3. Dann `DeleteLockAsync()` aufrufen und Erfolg validieren
    4. Bei Erfolg: Status-JSON aktualisieren (Lock-Status zurücksetzen)
- **Neue Methoden:**
  - `ReconcileLocksAfterRestartAsync()` — Post-Restart-Abgleich: Lock-Datei vs. Status-JSON reconcilieren (siehe Programmablauf 4)

### `UpdateStatusMapper` (Datenmodell-Mapper)

- **Geänderte Methoden:**
  - `MapAsync()` — Sicherstellen, dass `IsLocked` direkt aus `snapshot.IsLocked` (das von Library kommt) gelesen wird, ohne alternative Lock-Quellen zu prüfen. Ggfs. zusätzlich `LockCreatedAt` aus `snapshot.LockCreatedAt` mappen (falls nicht bereits vorhanden).

### `UpdateExecutor` (Ausführungs-Service)

- **Neue Methoden:**
  - `ValidateLockCleanupAsync()` — Nach Installer-Abschluss: Prüfe `HasActiveLockAsync()` == false. Falls noch Lock vorhanden: Retry-Schleife (bis zu 3 Versuche mit 500ms Backoff) oder `DeleteLockAsync()` direkt. Falls immer noch Lock: Status auf `Failed` setzen, Exception loggen
- **Geänderte Methoden:**
  - `ExecuteInstallerAsync()` — Nach Installer-Abschluss vor Status-Update: `ValidateLockCleanupAsync()` aufrufen. Nur wenn Cleanup erfolgreich (oder Lock bereits weg): Status auf `Completed` setzen. Falls Cleanup schlägt fehl: Status bleibt `Installing`, Exception wird geloggt (aber nicht propagiert — keine Exception an Caller)

### `UpdateOrchestrator` (Zentrale Orchestrierung)

- **Neue Methoden:**
  - `InitializeAsync()` oder `ReconcileAfterStartupAsync()` — Startup-Hook: Ruft `ReconcileLocksAfterRestartAsync()` auf (siehe auch `UpdateOrchestratorAdapter.ReconcileLocksAfterRestartAsync()`)
- **Registrierung in Startup-Pipeline:** Die Methode muss in der Anwendungs-Startup-Sequenz aufgerufen werden (z. B. in `Startup.cs` oder `Program.cs` nach DI-Setup)

### `SetupUpdateViewModel` (Blazor ViewModel)

- **Geänderte Methoden:**
  - `IsResetLockButtonDisabled` oder äquivalente Button-Binding-Logik — Änderung der Aktivierungsbedingung:
    - **Alt:** `Busy || Status is null || !Status.IsLocked`
    - **Neu:** `Busy || Status is null || !Status.IsLocked || !IsLockStale(Status.LockCreatedAt)`
  - Helper-Methode `IsLockStale(DateTime? lockCreatedAt)` hinzufügen — Prüft: `(DateTime.UtcNow - lockCreatedAt) >= StalenessSchwelle`. Schwelle ist konfigurierbar (siehe Konfigurationsänderungen).

### `SetupUpdateTab.razor` (Razor-Komponente)

- **Binding-Update:** Button "UpdateResetLock" nutzt neue Aktivierungsbedingung aus ViewModel (automatisch durch ViewModel-Änderung)

### `UpdateController` (API-Controller)

- **Keine direkten Änderungen nötig** — Methoden delegieren zu `UpdateOrchestratorAdapter`, welches angepasst wird

### `UpdateLockResetException` (Exception)

- **Keine Änderungen nötig** — Alle erforderlichen Fehlertypen (`NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed`) sind bereits definiert

## Datenbankmigrationen

Keine.

(Das Update-System persistiert Lock-Dateien im Dateisystem und Status in JSON, nicht in einer Datenbank. Keine Migrationen erforderlich.)

## Validierungsregeln

Keine neuen Validierungsregeln für Eingaben erforderlich.

(Lock-Validierung erfolgt durch Lock-Datei-Abfrage und Zeitstempel-Vergleich, nicht durch Eingabevalidation.)

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `UpdateOptions.HealthTimeoutSeconds` | int | 120 | Lock-Staleness-Schwelle: Lock gilt als stale, wenn älter als `max(HealthTimeoutSeconds, 60)` Sekunden |
| `UpdateOptions.LockCleanupRetryCount` | int | 3 | Anzahl der Retry-Versuche beim Cleanup nach Installation |
| `UpdateOptions.LockCleanupRetryDelayMs` | int | 500 | Verzögerung (ms) zwischen Retry-Versuchen (mit exponential backoff) |

**Anmerkung:** `HealthTimeoutSeconds` existiert bereits laut Anforderung. Die neuen Einträge sind nur erforderlich, falls Retry-Logik im Cleanup explizit konfigurierbar sein soll. Standardwerte können auch hardcoded sein.

## Seiteneffekte und Risiken

- **`UpdateFileStore` und `IAutoUpdatePackageStore`:** Lock-Cleanup-Logik wird in `UpdateExecutor` stärker genutzt (neue Retry-Logik). Bestehende Tests für `UpdateFileStore.DeleteLockAsync()` sollten Race-Conditions mit parallelen Aufrufen abdecken.
- **Startup-Pipeline:** Neue `ReconcileLocksAfterRestartAsync()` wird während Startup aufgerufen. Muss Thread-safe sein und darf nicht den Startup blockieren (ggfs. async Task ohne Await, falls Startup nicht asynchron ist).
- **UI-Button-Aktivierung:** Strikte Staleness-Prüfung könnte dazu führen, dass Benutzer länger warten müssen, bevor sie einen Lock rücksetzen können (Staleness-Schwelle ist z. T. 120 Sekunden). Dies ist **gewünscht** (verhindert versehentliche Resets bei zu jungen Locks).
- **Exception-Handlung:** `ValidateLockCleanupAsync()` in `UpdateExecutor` darf nicht an Caller propagiert werden (Fehler wird geloggt, Status wird auf `Failed` gesetzt). Caller sieht kein Exception, aber Status ändert sich.
- **Performance:** `ReconcileLocksAfterRestartAsync()` läuft beim Startup — sollte schnell sein (nur Datei-System-Abfragen, keine HTTP-Calls). Kein erkannter Performance-Risiko.
- **Keine bekannten Seiteneffekte auf andere Features** — Lock-System ist isoliert. Änderungen betreffen nur Update-Verwaltung.

## Umsetzungsreihenfolge

1. **Lock-Staleness-Konfiguration in `UpdateOptions` hinzufügen**
   - Voraussetzungen: `UpdateOptions` Klasse existiert
   - Beschreibung: Einträge `HealthTimeoutSeconds` (falls noch nicht vorhanden — laut Anforderung wahrscheinlich bereits vorhanden), `LockCleanupRetryCount`, `LockCleanupRetryDelayMs` hinzufügen. Standardwerte: 120, 3, 500.

2. **`ValidateLockCleanupAsync()` in `UpdateExecutor` implementieren**
   - Voraussetzungen: `UpdateExecutor` Klasse, `IAutoUpdatePackageStore` Interface (Library), `UpdateOptions` konfiguriert
   - Beschreibung: Neue private Methode, ruft `HasActiveLockAsync()` auf, bei Lock: Retry-Schleife (bis zu `LockCleanupRetryCount` Versuche mit `LockCleanupRetryDelayMs` Backoff). Bei Fehler: Log und return false (nicht Exception werfen).

3. **`ExecuteInstallerAsync()` in `UpdateExecutor` anpassen**
   - Voraussetzungen: `ValidateLockCleanupAsync()` implementiert
   - Beschreibung: Nach Installer-Abschluss vor Status-Update: `await ValidateLockCleanupAsync()` aufrufen. Nur wenn erfolgreich: Status auf `Completed` setzen. Sonst: Status bleibt `Installing`.

4. **`ResetLockAsync()` in `UpdateOrchestratorAdapter` anpassen**
   - Voraussetzungen: `UpdateLockResetException` Klassifizierung (bereits vorhanden), `IAutoUpdatePackageStore.IsLockStale()` existiert (Library)
   - Beschreibung: Logik ergänzen: (1) `HasActiveLockAsync()` prüfen → NoLock Exception, (2) `IsLockStale()` prüfen → LockNotStale Exception, (3) `DeleteLockAsync()` aufrufen, (4) Status-JSON aktualisieren bei Erfolg.

5. **`ReconcileLocksAfterRestartAsync()` in `UpdateOrchestratorAdapter` implementieren**
   - Voraussetzungen: `IAutoUpdatePackageStore` Interface (Library), Status-JSON Struktur bekannt
   - Beschreibung: Neue öffentliche Methode (oder private in `UpdateOrchestrator`): Startup-Abgleich zwischen Lock-Datei-Existenz und Status. Bei Mismatch reconcilieren (siehe Programmablauf 4).

6. **Startup-Hook registrieren für `ReconcileLocksAfterRestartAsync()`**
   - Voraussetzungen: `ReconcileLocksAfterRestartAsync()` implementiert, Startup-Konfiguration in `Startup.cs` / `Program.cs` bekannt
   - Beschreibung: Methode in Startup-Pipeline aufrufen (nach DI-Setup, vor Service-Verfügbarmachung).

7. **`IsLockStale()` Helper-Methode in `SetupUpdateViewModel` hinzufügen**
   - Voraussetzungen: `UpdateOptions.HealthTimeoutSeconds` konfiguriert, `SetupUpdateViewModel` Klasse
   - Beschreibung: Private Methode, prüft `(DateTime.UtcNow - lockCreatedAt) >= Schwelle`. Schwelle aus Konfiguration lesen.

8. **Button-Aktivierung in `SetupUpdateViewModel` anpassen**
   - Voraussetzungen: `IsLockStale()` Methode implementiert
   - Beschreibung: `IsResetLockButtonDisabled` oder Binding-Bedingung ändern: zusätzlich `!IsLockStale(Status.LockCreatedAt)` prüfen.

9. **Tests anpassen und neue Tests schreiben** (siehe Tests-Sektion)
   - Voraussetzungen: Alle obigen Klassen-Änderungen implementiert
   - Beschreibung: Unit-Tests für neue Methoden, Tests für bestehende Methoden anpassen, E2E-Tests schreiben.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ValidateLockCleanupAsync_LockDeleted_ReturnsTrue` | `UpdateExecutorTests` | Nach Cleanup: Lock-Datei weg → `ValidateLockCleanupAsync()` gibt true zurück |
| `ValidateLockCleanupAsync_LockStillExists_RetriesAndEventuallyDeletes` | `UpdateExecutorTests` | Beim 1. Versuch Lock noch vorhanden, 2. Versuch erfolgreich → Retry-Logik validieren |
| `ValidateLockCleanupAsync_LockDeleteFails_LogsErrorAndReturnsFalse` | `UpdateExecutorTests` | Lock-Löschung bleibt fehlerhaft → Error-Log, false zurück, Exception nicht propagiert |
| `ExecuteInstallerAsync_LockCleanupSucceeds_StatusSetToCompleted` | `UpdateExecutorTests` | Nach Cleanup erfolgreich → Status auf `Completed` |
| `ExecuteInstallerAsync_LockCleanupFails_StatusRemainsInstalling` | `UpdateExecutorTests` | Nach Cleanup Fehler → Status bleibt `Installing` |
| `ResetLockAsync_NoLock_ThrowsNoLockException` | `UpdateOrchestratorAdapterTests` | `HasActiveLockAsync()` == false → Exception mit `Kind = NoLock` |
| `ResetLockAsync_LockNotStale_ThrowsLockNotStaleException` | `UpdateOrchestratorAdapterTests` | Lock zu jung → Exception mit `Kind = LockNotStale` |
| `ResetLockAsync_LockStaleAndDelete_SucceedsAndUpdatesStatus` | `UpdateOrchestratorAdapterTests` | Lock stale + Löschung erfolgreich → Status aktualisiert, keine Exception |
| `ResetLockAsync_LockDeleteFails_ThrowsLockDeleteFailedException` | `UpdateOrchestratorAdapterTests` | `DeleteLockAsync()` schlägt fehl → Exception mit `Kind = LockDeleteFailed` |
| `ReconcileLocksAfterRestartAsync_FileExistsStatusNotInstalling_DeletesLock` | `UpdateOrchestratorAdapterTests` | Lock-Datei vorhanden, Status != Installing → Lock gelöscht |
| `ReconcileLocksAfterRestartAsync_FileNotExistsStatusInstalling_SetsStatusFailed` | `UpdateOrchestratorAdapterTests` | Lock-Datei weg, Status == Installing → Status auf Failed |
| `ReconcileLocksAfterRestartAsync_FileExistsStatusInstalling_LeavesLockIntact` | `UpdateOrchestratorAdapterTests` | Lock-Datei vorhanden, Status == Installing → Lock bleibt, keine Aktion |
| `IsLockStale_LockOlderThanThreshold_ReturnsTrue` | `SetupUpdateViewModelTests` | Lock älter als Schwelle → `IsLockStale()` gibt true zurück |
| `IsLockStale_LockYoungerThanThreshold_ReturnsFalse` | `SetupUpdateViewModelTests` | Lock jünger als Schwelle → `IsLockStale()` gibt false zurück |
| `IsResetLockButtonDisabled_LockStalenessNotMet_ReturnsTrue` | `SetupUpdateViewModelTests` | `IsLocked = true`, aber `!IsLockStale()` → Button disabled |
| `IsResetLockButtonDisabled_LockStaleAndActive_ReturnsFalse` | `SetupUpdateViewModelTests` | `IsLocked = true` und `IsLockStale() = true` → Button enabled |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UpdateOrchestratorAdapterTests.ResetLockAsync_*` | Logik erweitert: Staleness-Prüfung vor Löschung, Status-Update nach Erfolg. Bestehende Tests müssen angepasst werden oder neue Varianten für Staleness-Szenarien hinzugefügt werden. |
| `SetupUpdateViewModelTests.IsResetLockButtonDisabled_*` | Button-Aktivierungsbedingung geändert. Tests, die `IsLocked = true` mit erwartetem Button-State prüfen, müssen auch `IsLockStale()` berücksichtigen. |
| `UpdateExecutorTests.ExecuteInstallerAsync_*` | Nach Installer-Abschluss wird `ValidateLockCleanupAsync()` aufgerufen. Mocks für `IAutoUpdatePackageStore.HasActiveLockAsync()` müssen ggfs. angepasst werden. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| **Happy Path: Lock-Reset bei altem Lock** | `UpdateE2ETests.cs` (neu) | (1) UI zeigt Lock aktiv, (2) Lock ist älter als Staleness-Schwelle, (3) Reset-Button ist aktiviert, (4) Klick auf Reset löscht Lock, (5) UI aktualisiert und zeigt Lock weg |
| **Fehlerfall: Reset bei zu jungem Lock** | `UpdateE2ETests.cs` | (1) UI zeigt Lock aktiv, (2) Lock ist jünger als Staleness-Schwelle, (3) Reset-Button ist deaktiviert, (4) Versuch, Button zu klicken, schlägt fehl oder wird nicht erlaubt |
| **Fehlerfall: Reset bei keinem Lock** | `UpdateE2ETests.cs` | (1) UI zeigt kein Lock, (2) Reset-Button ist deaktiviert, (3) Versuch, Button zu klicken, zeigt Fehlermeldung "kein aktiver Lock" |
| **Installation mit Lock-Cleanup** | `UpdateE2ETests.cs` | (1) Update wird gestartet, Lock wird gesetzt, (2) Installation läuft, (3) Nach Installer-Abschluss: Lock wird validiert und gelöscht, (4) Status wechselt zu Completed, (5) UI zeigt keine Lock-Meldung mehr |
| **Post-Restart Reconciliation — verwaister Lock** | `UpdateE2ETests.cs` | (1) Lock-Datei existiert, Status != Installing, (2) Anwendung wird neu gestartet, (3) Startup reconciliert und löscht Lock, (4) UI zeigt danach kein Lock |
| **Post-Restart Reconciliation — abgebrochene Installation** | `UpdateE2ETests.cs` | (1) Lock-Datei weg, Status == Installing, (2) Anwendung wird neu gestartet, (3) Startup setzt Status auf Failed, (4) UI zeigt Fehler |

**Betroffene bestehende E2E-Tests:**
- Alle bestehenden Update-E2E-Tests müssen überprüft werden, ob sie durch die Staleness-Prüfung in UI-Button-Aktivierung beeinflusst werden. Tests, die sofort nach Lock-Setzung Reset aufrufen, müssen ggfs. mit Delays arbeiten, um Staleness-Schwelle zu erreichen.

## Offene Punkte

Keine.

(Alle ursprünglichen Fragen aus der Anforderung wurden durch die Bestandsaufnahme geklärt:
- Lock-Prüfung für UI-Buttons: Wird durch Staleness-Prüfung in `IsLockStale()` gelöst
- Timing nach Installation: Cleanup wird durch `ValidateLockCleanupAsync()` validiert
- Race-Condition nach Neustart: Wird durch `ReconcileLocksAfterRestartAsync()` adressiert
- Error-Szenarien: Status wird auf `Failed` gesetzt, Lock wird bereinigt oder als verwaist markiert
- Kundenerlebnis: Automatisches Recovery nach Neustart durch Reconciliation)
