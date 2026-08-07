# Umsetzungsplan: Update-Lock-Status-Synchronisierung mit dem Dateisystem

## Übersicht

Der `UpdateOrchestratorAdapter` wird um eine automatische Cache-Reconciliation erweitert, die bei jedem Status-Abruf (`GetStatusAsync()`, `CheckAsync()`, `StartInstallAsync()`) den prozessinternen Cache gegen den Live-Status der Lock-Datei auf dem Dateisystem abgleicht. Dies behebt Inkonsistenzen, die entstehen, wenn externe Prozesse die Lock-Datei löschen, ohne dass der Cache aktualisiert wird. Die Änderung ist rein intern und beeinträchtigt keine öffentlichen APIs oder Datenmodelle.

---

## Designentscheidungen

Keine — folgt bestehenden Mustern. Die neue Methode `ReconcileLockStatusAsync()` folgt den Exception-Handling-Konventionen bereits existierender Methoden wie `ValidateLockCleanupAsync()` (Zeilen 210–231) und `DeleteLockOrThrowAsync()` (Zeilen 181–200): Try-Catch um `_packageStore`-Aufrufe, `OperationCanceledException` wird durchgereicht, andere Exceptions werden geloggt statt zu propagieren.

---

## Programmabläufe

### Cache-Reconciliation bei Status-Abruf

1. `ReconcileLockStatusAsync()` wird aufgerufen (vor Status-Mapping)
2. Liest Live-Lock-Zeitstempel via `_packageStore.GetLockCreatedAtAsync()` (gibt `DateTimeOffset?` zurück)
3. Liest gecachten Status via `_statusService.GetSnapshot()` (schnell, aus Speicher)
4. **Fall A:** Cache sagt `IsLocked=true`, Live-Read liefert `null` (keine Lock-Datei)
   - Ruft `_statusService.UpdateAsync(s => s with { IsLocked = false, LockCreatedAt = null }, ct)` auf
   - Loggt auf Debug-Level: "Lock-Status wurde durch Reconciliation bereinigt"
5. **Fall B:** Cache sagt `IsLocked=true`, Live-Read liefert `DateTimeOffset` (Lock existiert)
   - Keine Aktion, Cache ist konsistent
6. **Fehlerfall:** `GetLockCreatedAtAsync()` wirft `IOException` oder `UnauthorizedAccessException`
   - Loggt auf Debug-Level den Fehler
   - Gibt defensiv zurück, verwendet Cache-Wert (Fallback)
   - `OperationCanceledException` wird durchgereicht (nicht abgefangen)

Beteiligte Klassen/Komponenten: `UpdateOrchestratorAdapter`, `IAutoUpdatePackageStore` (via `_packageStore`), `AutoUpdateStatusService` (via `_statusService`), `ILogger<UpdateOrchestratorAdapter>` (via `_logger`)

### Integration in GetStatusAsync()

**Abfolge:**
1. Ruft `ReconcileLockStatusAsync(ct)` auf (neu)
2. Ruft `_orchestrator.GetStatusAsync(ct)` auf (bestehend)
3. Mapped Snapshot zu DTO via `_statusMapper.MapAsync(snapshot, ct)` (bestehend)
4. Gibt DTO zurück

**Zweck der Änderung:** Stellt sicher, dass der Status-DTO immer den aktuellen Dateisystem-Zustand widerspiegelt.

### Integration in CheckAsync()

**Abfolge (vereinfacht):**
1. Try-Block
2. Ruft `ReconcileLockStatusAsync(ct)` auf (neu, vor Status-Abruf)
3. Ruft `_orchestrator.CheckForUpdateAsync(ct)` auf (bestehend)
4. Ruft `_statusService.GetSnapshot()` auf (bestehend; findet reconcilierten Status vor)
5. Mapped Status zu DTO (bestehend)
6. Behandelt GitHub-Rate-Limiting (bestehend)
7. Gibt `UpdateCheckResultDto` zurück

**Zweck der Änderung:** Reconciliation erfolgt VOR der Snapshot-Abfrage, damit Check-Ergebnis auf aktuellem Cache basiert.

### Integration in StartInstallAsync()

**Abfolge:**
1. Ruft `_orchestrator.InstallAsync(confirmDowntime, ct)` auf (bestehend)
2. Prüft auf Fehler und wirft Exceptions durch (bestehend)
3. Ruft `ValidateLockCleanupAsync(ct)` auf, wenn nicht fehlgeschlagen (bestehend)
4. Ruft `ReconcileLockStatusAsync(ct)` auf (neu, vor finaler Status-Abfrage)
5. Mapped finalen Status zu DTO (bestehend)
6. Gibt DTO zurück

**Zweck der Änderung:** Stellt sicher, dass der nach Installation zurückgegebene Status aktuell ist (falls externe Prozesse Lock gelöscht haben).

---

## Neue Klassen

Keine — die neue Funktionalität ist rein eine private Hilfsmethode in `UpdateOrchestratorAdapter`.

---

## Änderungen an bestehenden Klassen

### `UpdateOrchestratorAdapter` (Klasse)

- **Neue Methoden:**
  - `ReconcileLockStatusAsync(CancellationToken ct)` (private, async) — Synchronisiert Cache-Lock-Status mit Dateisystem-Zustand
    - Parameter: `CancellationToken ct` (wird weitergereicht)
    - Rückgabewert: `Task` (void-async)
    - Verhalten: Liest Live-Lock-Status, vergleicht mit Cache, aktualisiert Cache wenn inconsistent, loggt defensive Fehler

- **Geänderte Methoden:**
  - `GetStatusAsync(CancellationToken ct)` — Ruft `ReconcileLockStatusAsync(ct)` vor `_orchestrator.GetStatusAsync(ct)` auf
  - `CheckAsync(CancellationToken ct)` — Ruft `ReconcileLockStatusAsync(ct)` vor `_orchestrator.CheckForUpdateAsync(ct)` auf (innerhalb Try-Block, vor erste Status-Abfrage)
  - `StartInstallAsync(bool confirmDowntime, CancellationToken ct)` — Ruft `ReconcileLockStatusAsync(ct)` nach `ValidateLockCleanupAsync(ct)`, aber vor finaler `_statusMapper.MapAsync(...)` auf

---

## Datenbankmigrationen

Keine.

---

## Validierungsregeln

Keine.

---

## Konfigurationsänderungen

Keine.

---

## Seiteneffekte und Risiken

- **Performance:** Jeder Aufruf von `GetStatusAsync()`, `CheckAsync()` oder `StartInstallAsync()` führt einen zusätzlichen I/O-Zugriff durch (`_packageStore.GetLockCreatedAtAsync()`). Der Aufwand ist akzeptabel, da diese Methoden bereits asynchron sind und nicht im Hot-Path liegen. Die zusätzliche Latenz ist typischerweise <100ms pro Dateisystem-Zugriff.

- **Logging-Volumen:** Die neue Debug-Level-Logging-Ausgabe bei erfolgreicher Reconciliation wird sich nur bemerkbar machen, wenn Lock-Cleanup auf Dateisystem fehlschlägt (externen Prozess das Lock löscht). Im normalen Betriebszustand wird die Reconciliation keine Logs generieren.

- **Keine Änderungen an `ResetLockAsync()`:** Diese Methode bleibt unverändert; sie ist für explizite Nutzereingaben gedacht. `ReconcileLockStatusAsync()` ist ein automatischer Reparaturmechanismus für Inkonsistenzen.

- **Kein Seiteneffekt auf andere Klassen:** `UpdateStatusMapper`, `UpdateControllerIntegrationTests`, `UpdateOrchestratorAdapterTests` und `UpdateOrchestratorAdapterLockAndScheduleTests` sind von den Änderungen nicht betroffen, da sie keine öffentlichen APIs ändern.

---

## Umsetzungsreihenfolge

1. **Implementierung von `ReconcileLockStatusAsync()` in `UpdateOrchestratorAdapter`**
   - Voraussetzungen: Keine (alle benötigten Abhängigkeiten existieren bereits)
   - Beschreibung: Private Methode hinzufügen (nach `ValidateLockCleanupAsync()`, Zeile 231). Liest Lock-Status via `_packageStore.GetLockCreatedAtAsync()`, vergleicht mit `_statusService.GetSnapshot()`, und aktualisiert Cache via `_statusService.UpdateAsync()` wenn inconsistent. Exception-Handling nach Vorbild von `ValidateLockCleanupAsync()`: `OperationCanceledException` wird durchgereicht, andere Exceptions werden geloggt.

2. **Integration von `ReconcileLockStatusAsync()` in `GetStatusAsync()`**
   - Voraussetzungen: Schritt 1 (Methode existiert)
   - Beschreibung: In Zeile 50 (`public async Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct = default)`) vor `var snapshot = await _orchestrator.GetStatusAsync(ct);` einen Call zu `await ReconcileLockStatusAsync(ct);` einfügen.

3. **Integration von `ReconcileLockStatusAsync()` in `CheckAsync()`**
   - Voraussetzungen: Schritt 1 (Methode existiert)
   - Beschreibung: In Zeile 77 (`public async Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct = default)`) innerhalb des Try-Blocks (Zeile 79) VOR `var result = await _orchestrator.CheckForUpdateAsync(ct);` (Zeile 81) einen Call zu `await ReconcileLockStatusAsync(ct);` einfügen.

4. **Integration von `ReconcileLockStatusAsync()` in `StartInstallAsync()`**
   - Voraussetzungen: Schritt 1 (Methode existiert)
   - Beschreibung: In Zeile 103 (`public async Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct = default)`) nach `await ValidateLockCleanupAsync(ct);` (Zeile 113) einen Call zu `await ReconcileLockStatusAsync(ct);` einfügen, bevor `return await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);` (Zeile 116) aufgerufen wird.

5. **Unit-Tests für `ReconcileLockStatusAsync()` schreiben**
   - Voraussetzungen: Schritte 1–4 (Implementation vollständig)
   - Beschreibung: Neue Tests in `UpdateOrchestratorAdapterTests` hinzufügen:
     - `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedButFileIsAbsent_ClearsLock` — verifies Cache-Mutation
     - `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedAndFileExists_DoesNothing` — verifies no mutation
     - `Adapter_ReconcileLockStatusAsync_WhenGetLockThrowsIOException_LogsDebugAndContinues` — verifies defensive error handling
     - `Adapter_GetStatusAsync_ReconcilesCacheBeforeMapping` — integriert Reconciliation

6. **Integrationstests für `GetStatusAsync()`, `CheckAsync()`, `StartInstallAsync()` erweitern**
   - Voraussetzungen: Schritte 1–4 (Implementation vollständig), Schritt 5 (Unit-Tests existieren)
   - Beschreibung: Neue Szenarien in `UpdateControllerIntegrationTests` (oder separater Test-Klasse) hinzufügen, um zu verifizieren, dass HTTP-API mit bereinigtem Cache-Zustand antwortet:
     - `GetStatus_WhenCacheIsStale_ReturnsFreshCacheAfterReconciliation` — Cache wird bei Endpoint-Call synchronisiert
     - `CheckAsync_WhenCacheIsStale_ReturnsFreshStatusDuringCheck` — Reconciliation läuft VOR Check-Operation
     - `StartInstall_WhenCacheIsStale_ReturnsFreshStatusAfterInstall` — Reconciliation läuft VOR Status-Rückgabe

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedButFileIsAbsent_ClearsLock` | `UpdateOrchestratorAdapterTests` | Cache-Mutation: `_statusService.UpdateAsync()` wird mit `IsLocked=false, LockCreatedAt=null` aufgerufen |
| `Adapter_ReconcileLockStatusAsync_WhenCacheIsLockedAndFileExists_DoesNothing` | `UpdateOrchestratorAdapterTests` | Keine Mutation: `_statusService.UpdateAsync()` wird NICHT aufgerufen, wenn Live-Read nicht null |
| `Adapter_ReconcileLockStatusAsync_WhenGetLockThrowsIOException_LogsDebugAndContinues` | `UpdateOrchestratorAdapterTests` | Defensive Fehlerbehandlung: IOException wird geloggt (Debug-Level), Methode gibt zurück statt zu propagieren |
| `Adapter_ReconcileLockStatusAsync_WhenGetLockThrowsOperationCanceledException_Propagates` | `UpdateOrchestratorAdapterTests` | OperationCanceledException wird durchgereicht (nicht abgefangen) |
| `Adapter_GetStatusAsync_ReconcilesCacheBeforeMapping` | `UpdateOrchestratorAdapterTests` | Integrationstest: `ReconcileLockStatusAsync()` wird vor `_orchestrator.GetStatusAsync()` aufgerufen |
| `Adapter_CheckAsync_ReconcilesCacheBeforeCheck` | `UpdateOrchestratorAdapterTests` | Integrationstest: `ReconcileLockStatusAsync()` wird vor `CheckForUpdateAsync()` aufgerufen |
| `Adapter_StartInstallAsync_ReconcilesCacheAfterValidationBeforeReturn` | `UpdateOrchestratorAdapterTests` | Integrationstest: `ReconcileLockStatusAsync()` wird vor finaler Status-Rückgabe aufgerufen |
| HTTP-Integration: `GetStatus_WhenCacheIsStale_ReturnsFreshStatusAfterReconciliation` | `UpdateControllerIntegrationTests` | End-to-End: HTTP GET /api/updates/status gibt reconcilierten Status zurück |
| HTTP-Integration: `Check_WhenCacheIsStale_ReturnsFreshStatusDuringCheck` | `UpdateControllerIntegrationTests` | End-to-End: HTTP POST /api/updates/check gibt reconcilierten Status während Check zurück |
| HTTP-Integration: `StartInstall_WhenCacheIsStale_ReturnsFreshStatusAfterInstall` | `UpdateControllerIntegrationTests` | End-to-End: HTTP POST /api/updates/install gibt reconcilierten Status nach Installation zurück |

### Betroffene bestehende Tests

Keine. Die Änderungen sind rein intern (private Methode) und beeinflussen keine öffentlichen APIs, Signaturen oder externen Verhaltensweisen.

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Cache sagt `IsLocked=true`, Dateisystem hat keine Lock-Datei (externe Löschung) → GET /api/updates/status gibt `IsLocked=false` zurück | `UpdateControllerIntegrationTests` | AC1: Automatische Cache-Bereinigung bei Dateisystem-Abweichung |
| Nach Lock-Reset via DELETE /api/updates/lock, darauffolgender GET /api/updates/status gibt aktuellen Status ohne stale Lock | `UpdateControllerIntegrationTests` (bestehend: `ResetLock_Returns204_WhenStaleLockIsReleasedOnDisk`) | AC2: Reset-Operation wird von nachfolgenden Status-Abfragen nicht dupliziert |
| Während Check-Operation (POST /api/updates/check), wenn externe Prozesse Lock löschen → Check gibt `IsLocked=false` im finalen Status zurück | `UpdateControllerIntegrationTests` (neu) | AC3: Reconciliation läuft VOR externen Library-Aufrufen |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine. Die bestehenden Integrationstests (`UpdateControllerIntegrationTests`) sind nicht betroffen, da die Reconciliation transparent läuft und keine öffentliche API ändert.

---

## Offene Punkte

Keine. Alle in der Anforderung erwähnten offenen Punkte wurden durch Annahmen geklärt:

1. **Fehlerbehandlung:** Fehler beim Live-Read (`IOException`, `UnauthorizedAccessException`) werden auf Debug-Level geloggt; die Methode gibt defensiv zurück und verwendet den gecachten Wert (Fallback).

2. **Logging-Level:** Erfolgreiche Reconciliation (Cache bereinigt) wird auf Debug-Level geloggt, da dies unter normalen Betriebsbedingungen eine seltene Ereignis ist.

3. **Performance:** Der I/O-Aufwand ist akzeptabel, da diese Methoden bereits asynchron sind und nicht im Hot-Path liegen. Ein Reconciliation-Cache würde unnötige Komplexität einführen.

4. **ResetLockAsync() bleibt unverändert:** `ResetLockAsync()` ist für explizite Nutzereingaben gedacht. `ReconcileLockStatusAsync()` ist ein automatischer Reparaturmechanismus für Inkonsistenzen und läuft unabhängig davon.
