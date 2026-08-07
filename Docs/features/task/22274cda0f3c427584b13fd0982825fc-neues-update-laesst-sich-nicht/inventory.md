# Bestandsaufnahme: Update-Lock-Handling — Inkonsistenzen im Status und Reset

Diese Bestandsaufnahme analysiert den bestehenden Code für das Update-Lock-System, das einen Statusinkonsistenz-Fehler aufweist: Obwohl die UI einen aktiven Lock meldet, schlägt der Reset mit "kein aktiver Lock" fehl.

---

## Zusammenfassung der Befunde

### Systemdesign
Das Update-System besteht aus mehreren Schichten:
1. **msTools.Updater Library** — Externe Bibliothek für Auto-Update-Orchestrierung; verwaltet Lock-Dateien
2. **UpdateOrchestratorAdapter** — Adapter, der Library-Schnittstelle auf FinanceManager-DTOs mappt
3. **API-Controller und ViewModel** — REST API und Blazor-UI für Update-Verwaltung
4. **Persistierung** — Einstellungen in JSON, Status aus Library-Snapshots

### Kritische Komponenten für Lock-Problem
- **Status-Quelle für UI:** `UpdateStatusMapper.MapAsync()` liest `snapshot.IsLocked` aus Library-Snapshot
- **Reset-Quelle:** `UpdateOrchestratorAdapter.ResetLockAsync()` ruft `packageStore.GetLockCreatedAtAsync()` auf
- **Potenzielle Inkonsistenz:** Diese beiden prüfen möglicherweise unterschiedliche Quellen oder zu unterschiedlichen Zeiten

### Fehlerklassifizierung
Sechs Lock-Reset-Fehlerarten sind typisiert:
- `NoLock` — Kein Lock vorhanden (zugrunde liegende Ursache der Anforderung)
- `LockNotStale` — Lock existiert, ist aber zu jung
- `LockDeleteFailed` — Lock-Datei kann nicht gelöscht werden
- `ResetFailed` — Generischer technischer Fehler

### Testabdeckung
Bestehende Tests decken alle klassifizierten Fehlertypen ab, **aber nicht die Inkonsistenz selbst:**
- ✓ Getestet: Jeder einzelne Fehlertyp isoliert
- ✗ Nicht getestet: Race-Bedingung zwischen `GetStatusAsync()` und `ResetLockAsync()`

### Implementierungslücken
1. **Keine atomare Sicht auf Lock-Status** — Status und Reset nutzen möglicherweise unterschiedliche Lock-Quellen
2. **Kein Reconciliation nach Installation** — Lock-Cleanup wird nicht auf Konsistenz validiert
3. **Keine Staleness-Prüfung in UI-Button-Aktivierung** — Reset-Button wird aktiviert, sobald `IsLocked = true`, ohne `IsLockStale()` zu prüfen

---

## Details

### Datenmodelle und Enums
Die Datenmodelle bilden den Lock-Status ab:
- [Datenmodelle](inventory/models.md) — `UpdateStatusDto` mit `IsLocked` und `LockCreatedAt`, Enums
- [Enumerationen](inventory/enums.md) — `UpdateStatusKind`, `UpdateLockResetFailureKind`, `UpdateLockResetFailureSource`

### Logikschichten
Die Orchestrierung ist in mehreren Services aufgeteilt:
- [Logikklassen](inventory/logic.md) — `UpdateOrchestratorAdapter` (Lock-Reset), `UpdateStatusMapper`, `UpdateSettingsStore`, Service-Katalog
- [Exceptions](inventory/exceptions.md) — `UpdateLockResetException` mit Fehlerklassifizierung

### Schnittstellen und Contracts
- [Interfaces](inventory/interfaces.md) — `IUpdateOrchestrator`, `IUpdateSettingsStore`, `IAutoUpdatePackageStore` (Library-Interface)

### UI und API
- [UI-Komponenten](inventory/ui.md) — `UpdateController` (REST API), `SetupUpdateViewModel` (Blazor-ViewModel), Ribbon-Button-Aktivierung
  - **Kritisch:** Button "UpdateResetLock" aktiviert bei `Status.IsLocked = true`, ohne Staleness zu prüfen

### Tests
- [Tests](inventory/tests.md) — Testklassen und Test-Factories
  - Lock-Reset-Tests prüfen alle Fehlertypen einzeln
  - Keine Tests für Status-Abfrage ↔ Reset-Inkonsistenz

---

## Schlüsselfunde für Implementierung

### 1. Lock-Status hat zwei Quellen
- **Für UI (Status-Abfrage):** `AutoUpdateStatusSnapshot.IsLocked` → gelesen von `UpdateStatusMapper.MapAsync()`
- **Für Reset:** `IAutoUpdatePackageStore.GetLockCreatedAtAsync()` → direkt abgefragt in `ResetLockAsync()`
- **Problem:** Diese könnten desynchronisiert sein oder unterschiedliche Quelle nutzen

### 2. UI-Button-Aktivierung ist zu unkritisch
```csharp
disabled: Busy || Status is null || !Status.IsLocked
```
Der Button wird aktiviert, sobald `Status.IsLocked = true`, **ohne** zu prüfen:
- Ob `GetLockCreatedAtAsync()` tatsächlich einen Wert gibt
- Ob `IsLockStale()` true ist
- Ob andere Bedingungen erfüllt sind

### 3. Lock-Cleanup ist nicht validiert
Nach erfolgreicher Installation wird die Lock-Datei vom Installer-Skript gelöscht, aber:
- Keine Validierung, ob Löschen erfolgreich war
- Keine Retry-Logik für Race-Bedingungen
- Keine Post-Install-Reconciliation

### 4. Singletons und Mutable State
Mehrere Komponenten sind Singletons oder halten mutable State:
- `UpdateSettingsStore` — Singleton mit persistiertem State
- `AutoUpdateStatusService` — Singleton Snapshot
- Potenziel für Zustandsinkonsistenz über Zeit

### 5. Fehlertyp `NoLock` ist die Symptomatik
Die `NoLock` Exception ist **exakt die Symptomatik**, die in der Anforderung beschrieben wird:
- UI zeigt "Lock vorhanden"
- Reset antwortet "NoLock"
- Dies ist eine Rassenbedingung zwischen zwei Status-Quellen

---

## Abhängigkeitsbaum

```
UpdateController (API)
  └─ IUpdateOrchestrator (UpdateOrchestratorAdapter)
      ├─ IAutoUpdateOrchestrator (Library)
      ├─ AutoUpdateStatusService (Library)
      ├─ IUpdateSettingsStore (UpdateSettingsStore)
      ├─ IAutoUpdatePackageStore (Library) ← KRITISCH für Lock-Prüfung
      └─ UpdateStatusMapper
          ├─ IInstalledReleaseMetadataProvider
          ├─ IAutoUpdatePlatformResolver
          └─ IUpdateSettingsStore

SetupUpdateViewModel (ViewModel)
  └─ IApiClient (aufgerufen via ApiClient Wrapper)
      └─ UpdateController (Blazor → API)

SetupUpdateTab.razor (UI)
  └─ SetupUpdateViewModel
      └─ Ribbon-Button "UpdateResetLock"
          └─ Bedingung: Status.IsLocked && !Busy

IAutoUpdatePackageStore (Library)
  ├─ GetStatusAsync() → IsLocked (für UI)
  └─ GetLockCreatedAtAsync() → (für Reset)
  └─ DeleteLockAsync() → (für Reset)
  └─ IsLockStale() → (für Reset)
```

**Kritischer Punkt:** `IAutoUpdatePackageStore` ist die einzige Quelle der Lock-Wahrheit, aber wird von zwei Pfaden abgefragt:
1. **GetStatusAsync()** — gesamter Snapshot inkl. `IsLocked`
2. **ResetLockAsync()** — direkte Abfrage `GetLockCreatedAtAsync()`

Wenn diese beiden desynchronisiert werden, tritt das beschriebene Problem auf.

---

## Folgerungen für Implementierung

1. **Einheitliche Lock-Prüfung:** Beide Pfade sollten die gleiche Methode nutzen oder zur gleichen Zeit prüfen
2. **Validation nach Cleanup:** Nach erfolgreicher Installation Lock-Status validieren
3. **UI-Button kritischer:** Reset-Button nur aktivieren, wenn auch `GetLockCreatedAtAsync()` einen Wert liefert
4. **Post-Restart Reconciliation:** Nach Neustart Lock-Status mit Datei-Existenz abgleichen
