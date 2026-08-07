# Anforderung: Update-Lock-Status-Synchronisierung mit dem Dateisystem

## Fachliche Zusammenfassung

Der `UpdateOrchestratorAdapter` liest den Update-Lock-Status aus zwei unabhängigen Quellen, die nirgends automatisch synchronisiert werden: einem prozessinternen Cache (via `_orchestrator.GetStatusAsync()` oder `_statusService.GetSnapshot()`) und einem Live-Read der Lock-Datei (via `_packageStore.GetLockCreatedAtAsync()`). Wenn externe Prozesse die Lock-Datei löschen, ohne dass der Adapter den Cache aktualisiert, entstehen Inkonsistenzen: Die UI zeigt einen aktiven Lock an, während die Reset-Logik keinen Lock findet. Diese Anforderung zielt darauf ab, den Cache bei jedem Status-Abruf automatisch gegen den Dateisystem-Zustand abzugleichen und stale Cache-Einträge zu bereinigen.

## Betroffene Klassen und Komponenten

- `UpdateOrchestratorAdapter` (FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs)
  - Methode `GetStatusAsync(CancellationToken ct)`
  - Methode `CheckAsync(CancellationToken ct)`
  - Methode `StartInstallAsync(bool confirmDowntime, CancellationToken ct)`
  - Neue Hilfsmethode zur Cache-Reconciliation (privat)

- `AutoUpdateStatusService` (aus msTools.Updater)
  - Methode `UpdateAsync(...)` (bereits vorhanden; wird zur Cache-Korrektur verwendet)

- `IAutoUpdatePackageStore` (aus msTools.Updater)
  - Methode `GetLockCreatedAtAsync(CancellationToken ct)` (bereits vorhanden; wird zur Live-Wahrheitsprüfung verwendet)

- Tests
  - Unit-Tests für Cache-Reconciliation-Logik in `UpdateOrchestratorAdapter`
  - Tests für Szenario "Cache sagt IsLocked=true, Live-Read sagt keine Lock-Datei" → Cache wird bereinigt

## Implementierungsansatz

1. **Neue interne Hilfsmethode `ReconcileLockStatusAsync()`**
   - Liest den aktuellen Lock-Status aus `_packageStore.GetLockCreatedAtAsync()` (Live-Wahrheit)
   - Vergleicht mit dem gecachten Status aus `_orchestrator.GetStatusAsync()` oder `_statusService.GetSnapshot()`
   - Wenn Cache sagt `IsLocked=true`, aber Live-Read liefert `null` (keine Lock-Datei):
     - Ruft `_statusService.UpdateAsync(s => s with { IsLocked = false, LockCreatedAt = null }, ct)` auf
     - Loggt die Reconciliation auf Debug-Level

2. **Integration in bestehende Methoden**
   - `GetStatusAsync()`: Ruft `ReconcileLockStatusAsync()` vor der Status-Abfrage auf
   - `CheckAsync()`: Ruft `ReconcileLockStatusAsync()` vor der Status-Abfrage auf (vor `_statusService.GetSnapshot()`)
   - `StartInstallAsync()`: Ruft `ReconcileLockStatusAsync()` vor der finalen Status-Abfrage auf

3. **Nicht-Behandlung des umgekehrten Falls**
   - Der Fall "Live-Read sagt Lock vorhanden, Cache sagt kein Lock" wird **nicht** behandelt; dafür gibt es keinen Reparaturmechanismus in der Library und der Kunde berichtete diesen Fall nicht.

## Abhängigkeiten und Schnittstellen

- Die neue Logik nutzt ausschließlich bereits verfügbare öffentliche Methoden:
  - `_packageStore.GetLockCreatedAtAsync()` (Live-Read)
  - `_statusService.UpdateAsync(...)` (Cache-Mutation)
  - `_orchestrator.GetStatusAsync()` oder `_statusService.GetSnapshot()` (Cache-Abfrage)
- Keine neuen Abhängigkeiten erforderlich.

## Konfiguration

Diese Anforderung erfordert keine Konfiguration. Die Synchronisierung erfolgt automatisch bei jedem Aufruf der betroffenen Methoden.

## Offene Fragen

1. **Fehlerbehandlung bei `ReconcileLockStatusAsync()`**: Sollen Fehler beim Live-Read (z. B. Zugriffsrechte-Probleme) die Status-Abfrage fehlschlagen lassen oder stumm ignoriert werden (mit Logging)?
   - *Annahme*: Fehler werden geloggt, die Methode gibt `false` oder `null` zurück, und die Status-Abfrage verwendet den gecachten Wert, wenn der Live-Read fehlschlägt. Damit bleibt das Verhalten defensiv.

2. **Logging-Level**: Sollte die erfolgreiche Reconciliation (Cache bereinigt) auf `Debug`- oder `Information`-Level geloggt werden?
   - *Annahme*: `Debug`, da Reconciliation unter normalen Betriebsbedingungen stattfindet.

3. **Performance-Impakt**: Jeder Aufruf von `GetStatusAsync()`, `CheckAsync()` oder `StartInstallAsync()` führt jetzt einen zusätzlichen I/O-Zugriff durch (`GetLockCreatedAtAsync()`). Ist das akzeptabel, oder soll es konfigurierbar/cacheierbar sein?
   - *Annahme*: Der I/O-Aufwand ist akzeptabel, da diese Methoden ohnehin asynchron sind und nicht im Hot-Path liegen. Ein Reconciliation-Cache würde die Komplexität unnötig erhöhen.

4. **Wann soll `ResetLockAsync()` aufgerufen werden?**: Nach der Reconciliation in `ReconcileLockStatusAsync()`, wenn der Cache bereinigt wird, ist `ResetLockAsync()` überflüssig geworden. Sollte das berücksichtigt werden?
   - *Annahme*: Nein, `ResetLockAsync()` bleibt unverändert; es ist für explizite Nutzereingaben gedacht. `ReconcileLockStatusAsync()` ist ein automatischer Reparaturmechanismus für Inkonsistenzen.
