# Bestandsaufnahme: Update-Lock-Status-Synchronisierung mit dem Dateisystem

Diese Bestandsaufnahme dokumentiert die bestehende Architektur und Implementierung des Update-Status- und Lock-Management-Systems in FinanceManager, bezogen auf die Anforderung einer automatischen Cache-Reconciliation bei jedem Status-Abruf.

---

## Zusammenfassung

### Was existiert bereits

- **`UpdateOrchestratorAdapter`** implementiert `IUpdateOrchestrator` und orchestriert den Self-Update-Workflow durch Delegation an `msTools.Updater`-Library
- **`AutoUpdateStatusService`** (aus msTools.Updater) verwaltet einen prozessinternen Status-Cache mit Methoden `GetSnapshot()` (synchron, gecacht) und `UpdateAsync()` (persistiert Mutationen)
- **`IAutoUpdatePackageStore`** stellt Live-Read der Lock-Datei via `GetLockCreatedAtAsync()` bereit (gibt `DateTimeOffset?` zurück)
- **Lock-Reset-Logik** in `ResetLockAsync()`: Validiert Lock-Existenz, prüft Alter via `IsLockStale()`, löscht via `DeleteLockAsync()`, aktualisiert Cache via `_statusService.UpdateAsync()`
- **Lock-Cleanup-Validierung** in `StartInstallAsync()`: Ruft nach erfolgreicher Installation `ValidateLockCleanupAsync()` auf (loggt Warning bei Lock-Verbleib)
- **Umfangreiche Unit- und Integrationstests** für Lock-Handling, Error-Cases, Fehlerklassifizierung, HTTP-API
- **Test-Helpers**: Factory, Builder, Capturing-Logger für umfassende Testabdeckung

### Was fehlt oder ist inkonsistent

- **Keine automatische Reconciliation** bei Status-Abfragen: `GetStatusAsync()` und `CheckAsync()` lesen Status nur aus Cache, nicht von Dateisystem
- **Cache-Inkonsistenzen möglich**: Wenn externe Prozesse Lock-Datei löschen, bleibt Cache `IsLocked=true` bestehen bis `ResetLockAsync()` aufgerufen wird
- **Kein defensiver Abgleich**: Szenario "Cache sagt IsLocked=true, Live-Read sagt keine Lock-Datei" wird **nicht** behandelt
- **Status-Mapper** gibt Lock-Status direkt durch, ohne Validierung oder Reconciliation

### Anforderungsabstimmung

Die Anforderung fordert:
1. **Neue private Methode `ReconcileLockStatusAsync()`** — vergleicht Cache-Status mit Live-Read
2. **Integration in `GetStatusAsync()`** — Reconciliation vor Status-Abruf
3. **Integration in `CheckAsync()`** — Reconciliation vor Status-Abruf
4. **Integration in `StartInstallAsync()`** — Reconciliation vor finaler Status-Abruf
5. **Fehlerbehandlung** — Fehler bei Live-Read werden geloggt, Status wird gecacht zurückgegeben
6. **Debug-Logging** — Reconciliation-Aktionen auf Debug-Level

---

## Details

- [Logikklassen](inventory/logic.md) — `UpdateOrchestratorAdapter`, `UpdateStatusMapper`
- [Interfaces und Externe Typen](inventory/interfaces.md) — FinanceManager- und msTools.Updater-Interfaces, AutoUpdateStatusSnapshot, AutoUpdateStatusService
- [Tests und Testinfrastruktur](inventory/tests.md) — Unit-Tests, Integrationstests, Test-Helpers, aktuelle Abdeckung

---

## Kritische Schnittstellen für die Implementierung

### Datenfluss: Status-Abruf (aktuell)

```
GetStatusAsync()
  → _orchestrator.GetStatusAsync() [Library, synchron]
  → _statusMapper.MapAsync(snapshot) [Maps zu DTO, keine Reconciliation]
  → return UpdateStatusDto
```

**Problem:** Lock-Status im DTO basiert auf Cache, nicht auf Live-Dateisystem.

### Datenfluss: Lock-Reset (existierend)

```
ResetLockAsync(reason)
  → _packageStore.GetLockCreatedAtAsync() [Live-Read: null oder DateTimeOffset]
  → if (null) throw NoLock
  → if (!IsLockStale) throw LockNotStale
  → _packageStore.DeleteLockAsync() [Löscht Datei]
  → _statusService.UpdateAsync(s with { IsLocked = false }) [Aktualisiert Cache]
```

**Beobachtung:** Cache wird hier explizit synchronisiert, aber nur nach Löschen.

### Neue Methode: ReconcileLockStatusAsync() (zu implementieren)

```
ReconcileLockStatusAsync()
  → lockCreatedAt = _packageStore.GetLockCreatedAtAsync() [Live-Read]
  → cachedStatus = _statusService.GetSnapshot() oder _orchestrator.GetStatusAsync()
  → if (cachedStatus.IsLocked && lockCreatedAt == null)
      → _statusService.UpdateAsync(s with { IsLocked = false, LockCreatedAt = null })
      → _logger.LogDebug("Cache reconciliation: cleared stale lock")
  → catch (Exception ex)
      → _logger.LogDebug/Warning(ex, "...")
      → return (defensiv)
```

### Integration in GetStatusAsync() (zu implementieren)

```
public async Task<UpdateStatusDto> GetStatusAsync(CancellationToken ct)
{
    await ReconcileLockStatusAsync(ct); // [NEU]
    var snapshot = await _orchestrator.GetStatusAsync(ct);
    return await _statusMapper.MapAsync(snapshot, ct);
}
```

### Integration in CheckAsync() (zu implementieren)

```
public async Task<UpdateCheckResultDto> CheckAsync(CancellationToken ct)
{
    try
    {
        await ReconcileLockStatusAsync(ct); // [NEU] vor Status-Abruf
        var result = await _orchestrator.CheckForUpdateAsync(ct);
        var statusDto = await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
        // ...
    }
}
```

### Integration in StartInstallAsync() (zu implementieren)

```
public async Task<UpdateStatusDto> StartInstallAsync(bool confirmDowntime, CancellationToken ct)
{
    var result = await _orchestrator.InstallAsync(confirmDowntime, ct);
    if (result.Outcome == AutoUpdateOutcome.Failed && result.Error is not null)
        throw result.Error;

    if (result.Outcome != AutoUpdateOutcome.Failed)
        await ValidateLockCleanupAsync(ct);

    await ReconcileLockStatusAsync(ct); // [NEU] vor finaler Status-Abruf
    return await _statusMapper.MapAsync(_statusService.GetSnapshot(), ct);
}
```

---

## Abhängigkeiten: Was bereits vorhanden ist

| Komponente | Existiert | Nutzung |
|-----------|----------|--------|
| `_packageStore.GetLockCreatedAtAsync()` | ✓ | Live-Read der Lock-Datei |
| `_statusService.GetSnapshot()` | ✓ | Synchroner Zugriff auf gecachten Status |
| `_statusService.UpdateAsync()` | ✓ | Mutation und Persistierung des Status |
| `_orchestrator.GetStatusAsync()` | ✓ | Liest Status mit Restart-Reconciliation |
| `_logger` | ✓ | Logging |
| `ILogger<UpdateOrchestratorAdapter>` | ✓ | Injiziert |
| Error-Handling | ✓ | Existierende Try-Catch Patterns |

---

## Testing: Vorbereitung für neue Tests

Die bestehende Testinfrastruktur ermöglicht Tests für die neue Reconciliation:

| Test-Scenario | Mock-Setup | Assertion |
|---------------|-----------|-----------|
| Cache IsLocked=true, Live-Read null | `GetLockCreatedAtAsync()→null` | Status.IsLocked == false nach GetStatusAsync() |
| Cache IsLocked=true, Live-Read hat Wert | `GetLockCreatedAtAsync()→DateTimeOffset` | Status.IsLocked == true (unverändert) |
| Reconciliation wirft IOException | `GetLockCreatedAtAsync()→ThrowsAsync` | Status unverändert, Error geloggt |
| Debug-Logging bei Reconciliation | Capturing-Logger | Logger.Entries enthält Debug-Eintrag |

Die Factory `UpdateOrchestratorAdapterTestFactory` und der Builder `UpdateStatusTestData` sind bereits ausreichend für neue Tests.

---

## Annahmen aus der Anforderung

1. Fehler beim Live-Read werden geloggt, Status wird defensiv aus Cache zurückgegeben
2. Reconciliation auf Debug-Level geloggt
3. Nur der Fall "Cache sagt Lock, Dateisystem sagt kein Lock" wird behandelt
4. Der umgekehrte Fall wird nicht behandelt (kein Reparaturmechanismus in Library)
5. `ReconcileLockStatusAsync()` ist privat (interner Hilfsmechanismus)
6. Keine Konfigurierbarkeit erforderlich

---

## Nächste Schritte (außerhalb dieser Bestandsaufnahme)

1. Implementierung von `ReconcileLockStatusAsync()` in `UpdateOrchestratorAdapter`
2. Integration in `GetStatusAsync()`, `CheckAsync()`, `StartInstallAsync()`
3. Unit-Tests für alle Szenarien (Cache-Match, Cache-Mismatch, Fehler)
4. Integrationstests für HTTP-API mit Reconciliation
5. Verifikation Debug-Logging-Ausgabe
