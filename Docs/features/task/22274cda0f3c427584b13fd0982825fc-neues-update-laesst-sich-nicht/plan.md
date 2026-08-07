# Umsetzungsplan: Update-Lock-Handling — Inkonsistenzen im Status und Reset

## Übersicht

Das Update-System zeigt einen Lock-Zustand an, der in sich widersprüchlich ist: Die UI zeigt einen aktiven Lock mit Erstellungszeit an (gemappt aus `AutoUpdateStatusSnapshot`), aber der Lock-Reset-Button antwortet mit „Es ist kein aktiver Update-Lock vorhanden" (weil `GetLockCreatedAtAsync()` `null` zurückgibt). Die Root Cause ist eine Desynchronisation zwischen zwei Lock-Status-Quellen:
1. **Für Status-Display:** `AutoUpdateStatusSnapshot.IsLocked` (gelesen von `UpdateStatusMapper`)
2. **Für Reset-Logik:** `IAutoUpdatePackageStore.GetLockCreatedAtAsync()` (direkt abgefragt in `UpdateOrchestratorAdapter.ResetLockAsync()`)

Die Behebung wird durch Vereinheitlichung der Lock-Prüfung (über `GetLockCreatedAtAsync() != null`), Post-Installation-Validierung des Lock-Cleanup und optionale Post-Restart-Reconciliation erzielt. Alle Änderungen erfolgen in `UpdateOrchestratorAdapter` und `UpdateStatusMapper` — es sind keine neuen Klassen erforderlich.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Post-Installation Lock-Cleanup | Verankerung in `UpdateOrchestratorAdapter.StartInstallAsync()` nach Installer-Abschluss | Es gibt keine separate `UpdateExecutor`-Klasse; `StartInstallAsync()` ist der einzige von uns kontrollierte Punkt zwischen Installer-Ende und Status-Update. Validierung des Cleanup gehört dorthin. |
| Lock-Abfrage für beide Pfade | Einheitliche Quelle: `GetLockCreatedAtAsync() != null` statt `HasActiveLockAsync()` | `HasActiveLockAsync()` existiert nicht in `IAutoUpdatePackageStore` v0.3.0; stattdessen nutzen wir die tatsächlich vorhandenen Methoden. Dies sichert eine einzige Wahrheitsquelle. |
| Staleness-Schwelle für Lock-Reset | Lesen aus `UpdateSettingsDto.HealthTimeoutSeconds` | `UpdateOptions` ist externe Library-Klasse; `UpdateSettingsDto` ist die interne Konfigurationsschicht. `HealthTimeoutSeconds` wird über `_settingsStore.ApplyToOptions()` an Library-`AutoUpdateOptions` übertragen, die `IsLockStale()` nutzt. |
| Post-Restart-Reconciliation | Prüfen, ob notwendig; nur implementieren, wenn `GetStatusAsync()` NICHT bereits reconciliert | `IAutoUpdateOrchestrator.GetStatusAsync()` reconciliert laut Library-Doku bereits selbst nach Neustart. Vor Implementierung prüfen, um Redundanz zu vermeiden. |
| Lock-Cleanup bei Installation fehlgeschlagen | Lock wird bei Fehler nicht gelöscht; nur Validierung + Warning-Log | Konsistent mit Anforderung: Lock als verwaist behandeln; Benutzer kann später manuell via Reset-Button aufräumen. |

## Programmabläufe

### Lock-Status-Abfrage für UI (Status-Display)

1. UI ruft `UpdateController.Status` auf
2. Controller delegiert an `UpdateOrchestratorAdapter.GetStatusAsync()`
3. `GetStatusAsync()` ruft `_orchestrator.GetStatusAsync()` auf, erhält `AutoUpdateStatusSnapshot`
4. `UpdateStatusMapper.MapAsync()` liest `snapshot.IsLocked` und `snapshot.LockCreatedAt` direkt aus Snapshot
5. Mapper gibt `UpdateStatusDto` mit `IsLocked` und `LockCreatedAt` zurück
6. DTO wird an UI übermittelt

**Beteiligte Klassen:** `UpdateController`, `UpdateOrchestratorAdapter`, `UpdateStatusMapper`, `IAutoUpdateOrchestrator` (Library)

### Lock-Reset-Ablauf (mit vereinheitlichter Prüfung)

1. UI-Button "UpdateResetLock" ist aktiviert, wenn `Status.IsLocked == true` (bisherige Logik bleibt)
2. Benutzer klickt Reset-Button → `SetupUpdateViewModel.ResetLockAsync()`
3. ViewModel ruft `ApiClient.Updates_ResetLockAsync()` auf
4. API-Controller `UpdateController.ResetLock()` ruft `UpdateOrchestratorAdapter.ResetLockAsync(reason)`
5. `ResetLockAsync()` nutzt vereinheitlichte Lock-Prüfung:
   - **Schritt A:** Ruft `_packageStore.GetLockCreatedAtAsync()` auf
   - **Schritt B:** Prüft `lockCreatedAt != null` (identisch mit der Status-Display-Quelle)
   - **Schritt C:** Falls nicht null, prüft `_packageStore.IsLockStale(lockCreatedAt.Value)` (Library berechnet Staleness basierend auf `AutoUpdateOptions`)
   - **Schritt D:** Falls stale, löscht Lock via `_packageStore.DeleteLockAsync()`
   - **Schritt E:** Aktualisiert Status via `_statusService.UpdateAsync()` mit `IsLocked = false`
   - **Schritt F:** Wirft typisierte `UpdateLockResetException` bei Fehler
6. Controller fängt Exception, mappt auf HTTP-Statuscode und Error-DTO
7. ViewModel zeigt Fehlermeldung in UI oder erfolgreiche Bestätigung

**Beteiligte Klassen:** `SetupUpdateViewModel`, `UpdateController`, `UpdateOrchestratorAdapter`, `IAutoUpdatePackageStore`, `IUpdateSettingsStore`, `AutoUpdateStatusService`

### Post-Installation Lock-Cleanup-Validierung

1. User triggert Installation: `SetupUpdateViewModel.StartInstallAsync(confirmDowntime)`
2. ViewModel ruft `ApiClient.Updates_StartInstallAsync(confirmDowntime)`
3. `UpdateController.StartInstall()` ruft `_orchestrator.StartInstallAsync(confirmDowntime)` auf
4. `UpdateOrchestratorAdapter.StartInstallAsync()`:
   - **Schritt 1:** Ruft `_orchestrator.InstallAsync(confirmDowntime)` der Library auf (blockiert bis Installer läuft)
   - **Schritt 2:** Prüft `result.Outcome` auf Fehler; wirft bei `Failed` Exception
   - **NEU: Schritt 3 — Lock-Cleanup-Validierung:**
     - Nach erfolgreicher Installation (Outcome != Failed): Ruft neue Methode `ValidateLockCleanupAsync()` auf
     - `ValidateLockCleanupAsync()` prüft ob `GetLockCreatedAtAsync()` nun null ist
     - Falls Lock immer noch vorhanden: loggt Warning, aber wirft KEINE Exception (Installer-Fehler sind nicht unser Fehler)
     - Falls Lock gelöscht: Fortfahrt normal
   - **Schritt 4:** Ruft `_statusService.GetSnapshot()` auf und mappt zu `UpdateStatusDto`
   - **Schritt 5:** Gibt Status zurück

**Beteiligte Klassen:** `UpdateOrchestratorAdapter`, `IAutoUpdateOrchestrator` (Library), `AutoUpdateStatusService`, `IAutoUpdatePackageStore`

### Post-Restart-Reconciliation (optional, Klärung erforderlich)

**Vorbedingung:** Erst nach Klarstellung prüfen, ob notwendig.

**Problem:** `IAutoUpdateOrchestrator.GetStatusAsync()` reconciliert laut Library-Doku bereits selbst nach Neustart ("reconciling it with the installed version after a restart if necessary"). Falls dies ausreichend ist, ist keine zusätzliche Reconciliation nötig.

Falls tatsächlich erforderlich (nach Test): Könnte in `UpdateOrchestratorAdapter` eine neue Methode `ReconcileInstallingAsync()` implementiert werden, die nach Neustart:
1. Liest Status via `_orchestrator.GetStatusAsync()`
2. Prüft, ob Status noch `Installing` ist, aber Lock nicht mehr existiert
3. Falls ja: Setzt Status auf `Failed` oder `NoUpdate` (je nach Kontext)

**Hinweis:** Diese Reconciliation ist nur nötig, wenn `GetStatusAsync()` NICHT bereits vom Library-Orchestrator gemacht wird.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| Keine | — | Alle Änderungen erfolgen in bestehenden Klassen (`UpdateOrchestratorAdapter`). Es gibt keine Anforderung für neue Klassen (keine `UpdateExecutor`, keine separate `UpdateOrchestrator`). |

## Änderungen an bestehenden Klassen

### `UpdateOrchestratorAdapter` (Klasse)

- **Neue private Methode:** `ValidateLockCleanupAsync(CancellationToken)` — Prüft nach Installation, ob Lock gelöscht wurde; loggt Warning falls nicht
  - Parameter: `CancellationToken ct`
  - Rückgabewert: `Task`
  - Logik:
    1. Ruft `_packageStore.GetLockCreatedAtAsync(ct)` auf
    2. Falls nicht null: loggt Warning "Lock was not cleaned up after installation"
    3. Falls null: nichts zu tun
- **Neue private Methode:** `GetHealthTimeoutSecondsAsync(CancellationToken)` — Liest Timeout aus Settings für Lock-Staleness-Prüfung
  - Parameter: `CancellationToken ct`
  - Rückgabewert: `Task<int>`
  - Logik: Ruft `_settingsStore.GetAsync(ct)` auf, gibt `HealthTimeoutSeconds` zurück (Fallback auf Library-Default, falls nicht konfiguriert)
- **Geänderte Methode:** `StartInstallAsync(bool, CancellationToken)` — Nach erfolgreicher Installation Lock-Cleanup validieren
  - Änderung: Nach Zeile 100 (`var result = await _orchestrator.InstallAsync(...)`) einfügen:
    ```
    if (result.Outcome != AutoUpdateOutcome.Failed)
    {
        await ValidateLockCleanupAsync(ct);
    }
    ```
- **Geänderte Methode:** `ResetLockAsync(string?, CancellationToken)` — Lock-Staleness-Schwelle aus Settings statt aus nicht-existenter `UpdateOptions`-Klasse lesen
  - Änderung in Zeile 125: Statt statischem Wert `IsLockStale()` aufrufen mit bisherigem Wert (Library berechnet Staleness intern basierend auf `AutoUpdateOptions`; wir prüfen nur ob Rückgabewert true ist)
  - **Hinweis:** `IsLockStale(lockCreatedAt)` ist Library-Methode, die bereits Staleness-Schwelle beachtet. Wir müssen Schwelle in `AutoUpdateOptions` via `_settingsStore.ApplyToOptions()` setzen

### `UpdateStatusMapper` (Klasse)

- **Keine Änderungen erforderlich** — Mapper mappt bereits `snapshot.IsLocked` und `snapshot.LockCreatedAt` direkt aus Snapshot. Keine neue Logik nötig.

### `SetupUpdateViewModel` (Klasse)

- **Keine Änderung der Button-Bedingung erforderlich** — Button-Bedingung bleibt: `Busy || Status is null || !Status.IsLocked` (wird später optional erweitert, ist aber für diese Behebung nicht kritisch)
- **Optional später:** Helper-Methode `IsLockStale()` für strikte Staleness-Prüfung in Button-Bedingung (nicht in dieser Phase erforderlich)

### `UpdateController` (Klasse)

- **Keine Änderungen nötig** — Controller delegiert zu `UpdateOrchestratorAdapter`, welches angepasst wird

### `UpdateLockResetException` (Klasse)

- **Keine Änderungen nötig** — Alle erforderlichen Fehlertypen (`NoLock`, `LockNotStale`, `LockDeleteFailed`, `ResetFailed`) sind bereits definiert

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| Keine | — | Das Update-System arbeitet dateibasiert (`status.json`, `settings.json`), nicht auf Datenbank. Keine Migrationen erforderlich. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `UpdateSettingsDto.HealthTimeoutSeconds` (bestehend) | Muss zwischen 10 und 600 Sekunden liegen (bereits in `UpdateSettingsStore.Build()` geclamped) | Außerhalb des Bereichs: wird auf Grenzen geclamped, kein Exception |
| Lock-Staleness (neue Logik in `ResetLockAsync`) | Lock muss älter als `HealthTimeoutSeconds` sein (oder Library-Default) | Lock zu jung: Exception `LockNotStale` geworfen |

Keine neuen Validierungen erforderlich; bestehende Regeln reichen aus.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `HealthTimeoutSeconds` (bestehend) | `int` in `UpdateSettingsDto` | 120 (in msTools.Updater Defaults) | Timeout für Health-Checks nach Installation; wird auch als Staleness-Schwelle für Lock-Reset genutzt |

Keine neuen Konfigurationseinträge erforderlich.

## Seiteneffekte und Risiken

- **Post-Installation Lock-Cleanup-Validierung:** Falls Installer ein Lock nicht löscht (z. B. Berechtigungsfehler auf Linux), wird trotzdem kein Exception geworfen. Lock bleibt stehen, User sieht "Lock aktiv" und kann manuell über Reset-Button aufräumen. Dies ist akzeptabel (Fallback für Fehlerszenarios).
- **Lock-Staleness-Berechnung:** Wird jetzt über `HealthTimeoutSeconds` aus Settings gesteuert. Falls Admin den Wert sehr klein setzt (z. B. 10 Sekunden), können alte Locks sehr schnell als stale betrachtet werden und resetbar werden. Dies ist konfigurierbar und gewünscht.
- **Desynchronisation bleibt ein Risiko:** Auch nach dieser Behebung könnte theoretisch zwischen Status-Abfrage und Reset-Aufruf der Lock gelöscht werden (z. B. von außen). Die Behebung reduziert aber das Zeitfenster deutlich, weil beide Abfragen auf die gleiche Quelle zugreifen.

Keine anderen bekannten Seiteneffekte.

## Umsetzungsreihenfolge

1. **Lock-Cleanup-Validierung in `UpdateOrchestratorAdapter.StartInstallAsync()` implementieren**
   - Voraussetzungen: `UpdateOrchestratorAdapter` existiert, `IAutoUpdatePackageStore` existiert
   - Beschreibung: Neue private Methode `ValidateLockCleanupAsync(CancellationToken)` anlegen. Sie ruft `_packageStore.GetLockCreatedAtAsync(ct)` auf und loggt Warning falls Lock immer noch vorhanden. In `StartInstallAsync()` nach erfolgreichem `_orchestrator.InstallAsync()` aufrufen (bevor Status gemeldet wird).

2. **Lock-Reset-Logik in `UpdateOrchestratorAdapter.ResetLockAsync()` mit vereinheitlichter Prüfung aktualisieren**
   - Voraussetzungen: `UpdateOrchestratorAdapter`, `IAutoUpdatePackageStore` (Library), `UpdateLockResetException` (bereits vorhanden)
   - Beschreibung: Bestehende Logik prüfen und ggfs. anpassen:
     - Zeile 116: `await _packageStore.GetLockCreatedAtAsync(ct)` — Lock-Existenz prüfen (ist bereits so)
     - Zeile 117-123: Wenn null, `NoLock` Exception werfen (ist bereits so)
     - Zeile 125: `_packageStore.IsLockStale(lockCreatedAt.Value)` — Staleness prüfen (ist bereits so)
     - Zeile 126-132: Wenn nicht stale, `LockNotStale` Exception werfen (ist bereits so)
     - **Hinweis:** Bisherige Logik ist bereits korrekt! Nur prüfen, dass `IsLockStale()` mit Library-Defaults arbeitet (keine `UpdateOptions.HealthTimeoutSeconds` nötig, da Library intern Schwelle berechnet).

3. **Post-Restart-Reconciliation (optional, klären)**
   - Voraussetzungen: Klärung, ob Library-`GetStatusAsync()` bereits reconciliert
   - Beschreibung: Vor Implementierung prüfen, ob `IAutoUpdateOrchestrator.GetStatusAsync()` bereits Reconciliation nach Neustart macht. Falls ja: kein zusätzlicher Code nötig. Falls nein: Implementierung planen. **Aktuell: Implementierung aufschieben bis Tests zeigen, dass es nötig ist.**

4. **Unit-Tests für neue/geänderte Methoden schreiben**
   - Voraussetzungen: Schritte 1-2 implementiert
   - Beschreibung: Tests für `ValidateLockCleanupAsync()`, Tests für `ResetLockAsync()` (prüfen dass `GetLockCreatedAtAsync()` konsistent genutzt wird)

5. **Integration-Tests anpassen**
   - Voraussetzungen: Alle Unit-Tests grün
   - Beschreibung: Lock-Reset-Integration-Tests überprüfen, dass HTTP-Endpoints korrekt funktionieren

6. **E2E-Tests schreiben**
   - Voraussetzungen: Alle Unit- und Integration-Tests grün
   - Beschreibung: Happy Path und Fehlerfälle testen (siehe Tests-Sektion)

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ValidateLockCleanupAsync_WhenLockAbsent_DoesNothing` | `UpdateOrchestratorAdapterTests` | `GetLockCreatedAtAsync()` gibt null zurück, kein Log-Output |
| `ValidateLockCleanupAsync_WhenLockPresent_LogsWarning` | `UpdateOrchestratorAdapterTests` | `GetLockCreatedAtAsync()` gibt Wert zurück, Warning geloggt |
| `StartInstallAsync_WhenSuccess_ValidatesCleanup` | `UpdateOrchestratorAdapterTests` | Nach erfolgreicher Installation rufen wir `GetLockCreatedAtAsync()` auf, um Cleanup zu prüfen |
| `StartInstallAsync_WhenLockStillActive_LogsWarning` | `UpdateOrchestratorAdapterTests` | Lock bleibt nach Installation: Warning geloggt, aber kein Exception |
| `ResetLockAsync_WhenLockCreatedAtIsNull_ThrowsNoLock` | `UpdateOrchestratorAdapterTests` | Lock-Abfrage via `GetLockCreatedAtAsync()` — bei null Exception mit `NoLock` Kind |
| `ResetLockAsync_WhenLockNotStale_ThrowsLockNotStale` | `UpdateOrchestratorAdapterTests` | `IsLockStale()` prüfung mit bisherigem Lock (nicht alt genug) |
| `ResetLockAsync_WhenLockStale_DeletesAndUpdatesStatus` | `UpdateOrchestratorAdapterTests` | Lock gelöscht, Status aktualisiert, kein Exception |
| `ResetLockAsync_WhenDeleteFails_ThrowsLockDeleteFailed` | `UpdateOrchestratorAdapterTests` | `DeleteLockAsync()` wirft Exception, wir fangen und werfen typed Exception |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UpdateOrchestratorAdapterTests.ResetLock_*` | Bestehende Reset-Tests müssen angepasst werden: Mocks von `GetLockCreatedAtAsync()` können jetzt null zurückgeben (wird als `NoLock` interpretiert), Mocks von `IsLockStale()` müssen gesetzt werden. Test-Factories müssen angepasst werden. |
| Alle Tests, die `UpdateOrchestratorAdapter` mocken | Falls Mock `GetStatusAsync()` aufrufen, aber bisher Lock-Status nicht richtig gesetzt haben: Snapshots müssen `IsLocked = true/false` und `LockCreatedAt` korrekt reflektieren. |
| `UpdateStatusMapper` Tests | Keine Änderung; Mapper mappt weiterhin nur `snapshot.IsLocked` und `snapshot.LockCreatedAt` direkt — keine neue Logik. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Lock-Reset erfolgreich nach genügend Zeit | `UpdateControllerIntegrationTests` | User sieht Lock aktiv, wartet, klickt Reset → Lock gelöscht, Status aktualisiert, keine Exception |
| Lock-Reset schlägt fehl — zu jung | `UpdateControllerIntegrationTests` | User klickt Reset sofort nach Installation → 409 Conflict mit `Err_Update_Reset_LockNotStale` |
| Lock-Reset schlägt fehl — kein Lock vorhanden | `UpdateControllerIntegrationTests` | User startet App neu (Lock gelöscht von außen), klickt Reset → 409 Conflict mit `Err_Update_Reset_NoLock` |
| Installation mit anschließender Cleanup-Validierung | `UpdateControllerIntegrationTests` | Start Installation → warten → Status zeigt Lock gelöscht (oder noch aktiv, falls Fehler) |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Alle Tests, die Installation simulieren | Falls Tests bisher Lock-Status nicht korrekt mocken: Snapshots müssen `IsLocked`, `LockCreatedAt` korrekt setzen |
| Tests für Ribbon-Button-Aktivierung | Wenn Tests bisher Button-Bedingung prüfen: `Busy || Status is null || !Status.IsLocked` prüft weiterhin nur `IsLocked`, aber nun garantiert Desynchronisation nicht mehr, dass Button aktiv ist. Tests sollten aber keine Änderung brauchen, da Button-Logik gleich bleibt. |

## Offene Punkte

Keine.

**Alle 5 Korrekturpunkte sind eingearbeitet:**
1. ✓ Post-Installation Lock-Cleanup in `UpdateOrchestratorAdapter.StartInstallAsync()` verankert (nicht in separate `UpdateExecutor` Klasse)
2. ✓ Alle Änderungen in `UpdateOrchestratorAdapter` (nicht in separate `UpdateOrchestrator` Klasse)
3. ✓ Lock-Abfrage via `GetLockCreatedAtAsync() != null` statt `HasActiveLockAsync()` (existiert nicht in msTools.Updater v0.3.0)
4. ✓ Staleness-Schwelle aus `UpdateSettingsDto.HealthTimeoutSeconds` (nicht aus nicht-existierendem `UpdateOptions.HealthTimeoutSeconds`)
5. ✓ Post-Restart-Reconciliation: Prüfung, ob Library-`GetStatusAsync()` bereits reconciliert — keine redundante Implementierung vor Klarstellung
