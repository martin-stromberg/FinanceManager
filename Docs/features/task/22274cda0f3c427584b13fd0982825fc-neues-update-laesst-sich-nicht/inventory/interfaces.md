# Interfaces

## `IUpdateOrchestrator`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Zentrale Schnittstelle für die Orchestrierung des Update-Workflows. Implementiert von `UpdateOrchestratorAdapter`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetStatusAsync` | `CancellationToken ct` | `Task<UpdateStatusDto>` | Liest aktuellen Update-Status (inkl. Lock-Info, verfügbare Version, Status) |
| `GetSettingsAsync` | `CancellationToken ct` | `Task<UpdateSettingsDto>` | Liest Benutzer-Einstellungen für Updates |
| `SaveSettingsAsync` | `UpdateSettingsUpdateRequest request, CancellationToken ct` | `Task<UpdateSettingsDto>` | Speichert geänderte Settings und wendet sie runtime-seitig an |
| `ScheduleAsync` | `TimeOnly? scheduledInstallTime, CancellationToken ct` | `Task<UpdateSettingsDto>` | Setzt/löscht die geplante Installationszeit |
| `CheckAsync` | `CancellationToken ct` | `Task<UpdateCheckResultDto>` | Triggert manuelle Update-Prüfung gegen GitHub-Quelle |
| `StartInstallAsync` | `bool confirmDowntime, CancellationToken ct` | `Task<UpdateStatusDto>` | Startet Installation des verfügbaren Updates; wirft Exception bei Fehler |
| `ResetLockAsync` | `string? reason, CancellationToken ct` | `Task` | **Lock-Reset-Methode** — wirft `UpdateLockResetException` bei Fehler |

**Exceptions:**
- `UpdateLockResetException` — Typierter Lock-Reset-Fehler mit `Kind` und `FailureSource`
- Andere I/O- und Argument-Exceptions von `StartInstallAsync`

---

## `IUpdateSettingsStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Persistierung und Verwaltung von Update-Settings. Implementiert von `UpdateSettingsStore`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct` | `Task<UpdateSettingsDto>` | Liest persistierte Settings mit Defaults bei Erstaufruf |
| `SaveAsync` | `UpdateSettingsUpdateRequest request, CancellationToken ct` | `Task<UpdateSettingsDto>` | Speichert Benutzer-Einstellungen (atomar) |
| `SaveScheduleAsync` | `TimeOnly? scheduledInstallTime, CancellationToken ct` | `Task<UpdateSettingsDto>` | Aktualisiert nur die geplante Installationszeit |
| `ApplyToOptions` | `UpdateSettingsDto settings` | `void` | Überträgt Einstellungen in Library-Options für Runtime-Effekt |

**Datei-Persistierung:**
- Ort: `{IAutoUpdatePackageStore.RootDirectory}/settings.json`
- Format: JSON mit `PersistedUpdateSettingsDto` Schema
- Atomares Schreiben durch `JsonFileStore.WriteAtomicAsync`

---

## `IInstalledReleaseMetadataProvider`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Liest Metadaten der aktuell installierten Anwendungsversion.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct` | `Task<InstalledReleaseMetadataDto>` | Liest installierte Version, Veröffentlichungsdatum, Git-SHA, Runtime-Identifier |

**Verwendung:** Von `UpdateStatusMapper.MapAsync()` aufgerufen, um die installierte Version in `UpdateStatusDto` einzutragen.

---

## `IUpdateServiceCatalog`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Service-Name-Suggestions für UI-Autocomplete. Implementiert von `DefaultUpdateServiceCatalog`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListServiceNamesAsync` | `string? query, int take, CancellationToken ct` | `Task<IReadOnlyList<string>>` | Listet verfügbare Windows Services / systemd-Units, optional gefiltert nach Query |

**Plattform-spezifisch:**
- Windows: Ruft `sc.exe query` auf
- Linux: Ruft `systemctl list-units` auf
- Andere Plattformen: Leer

**Constraints:**
- `take` wird auf 1..100 clamped
- Timeout: 3 Sekunden pro Prozessaufruf
- Best-Effort Fehlerbehandlung

---

## `IAutoUpdateOrchestrator` (von msTools.Updater)
Externe Library-Schnittstelle; wird von `UpdateOrchestratorAdapter` verwendet.

Relevante Methoden (aus Library-Dokumentation):
- `GetStatusAsync(CancellationToken)` → `AutoUpdateStatusSnapshot` (mit `IsLocked`, `LockCreatedAt`, `State`)
- `CheckForUpdateAsync(CancellationToken)` → `AutoUpdateResult`
- `InstallAsync(bool, CancellationToken)` → `AutoUpdateResult`

---

## `IAutoUpdatePackageStore` (von msTools.Updater)
Externe Library-Schnittstelle; wird von `UpdateOrchestratorAdapter` und `UpdateSettingsStore` verwendet.

Relevante Methoden:
- `GetLockCreatedAtAsync(CancellationToken)` → `DateTimeOffset?` — Abrufen der Lock-Erstellungszeit
- `IsLockStale(DateTimeOffset)` → `bool` — Prüfung auf Staleness
- `DeleteLockAsync(CancellationToken)` → `bool` — Löschen der Lock-Datei
- `EnsureAsync(CancellationToken)` → `Task` — Sicherstellen, dass Verzeichnis existiert
- Properties: `RootDirectory`, `LockPath`

**Kritisch für Lock-Problem:**
- Die `IsLocked` von `GetStatusAsync()` Snapshot wird in `UpdateStatusMapper` gelesen
- Die `GetLockCreatedAtAsync()` wird in `ResetLockAsync()` aufgerufen
- **Potenzielle Quelle der Inkonsistenz:** Diese könnten unterschiedliche Lock-Quellen nutzen
