# Logikklassen

## `UpdateOrchestratorAdapter`
Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`

Implementiert `IUpdateOrchestrator` als Adapter über die `msTools.Updater`-Bibliothek. Koordiniert den Self-Update-Workflow durch Mapping zwischen Library-Typen und DTO-Typen.

### Abhängigkeiten
- `IAutoUpdateOrchestrator` — Orchestrator aus msTools.Updater
- `AutoUpdateStatusService` — Cache für Update-Status
- `IUpdateSettingsStore` — Host-spezifisches Settings-Store
- `IAutoUpdatePackageStore` — Library-Interface für Lock-Dateiverwaltung
- `UpdateStatusMapper` — Mapper für Status-DTO
- `ILogger<UpdateOrchestratorAdapter>` — Logging

### Methoden

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|--------------|
| `GetStatusAsync(CancellationToken ct)` | public | Ruft `_orchestrator.GetStatusAsync()` auf und mapped das Ergebnis via `_statusMapper.MapAsync()` |
| `GetSettingsAsync(CancellationToken ct)` | public | Delegiert zu `_settingsStore.GetAsync()` |
| `SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct)` | public | Speichert Settings, ruft `ApplyToOptions()` auf |
| `ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct)` | public | Speichert Installationszeitplan, ruft `ApplyToOptions()` auf |
| `CheckAsync(CancellationToken ct)` | public | Ruft `_orchestrator.CheckForUpdateAsync()` auf, mapped Status, behandelt GitHub-Rate-Limiting |
| `StartInstallAsync(bool confirmDowntime, CancellationToken ct)` | public | Ruft `_orchestrator.InstallAsync()` auf, validiert Lock-Cleanup via `ValidateLockCleanupAsync()` bei Erfolg, wirft Exceptions bei Fehler |
| `ResetLockAsync(string? reason, CancellationToken ct)` | public | Liest Lock via `_packageStore.GetLockCreatedAtAsync()`, validiert Alter via `IsLockStale()`, löscht Lock via `DeleteLockAsync()`, aktualisiert Status via `_statusService.UpdateAsync()` |
| `DeleteLockOrThrowAsync(DateTimeOffset lockCreatedAt, CancellationToken ct)` | private | Delegiert zu `_packageStore.DeleteLockAsync()`, behandelt `IOException` und `UnauthorizedAccessException` |
| `CreateResetException(...)` | private | Factory-Methode für `UpdateLockResetException` |
| `ValidateLockCleanupAsync(CancellationToken ct)` | private | Prüft nach Installation, ob Lock noch existiert; loggt Warning auf Debug-Level wenn vorhanden, ignoriert I/O-Fehler defensiv |

### Wichtige Verhalten

**GetStatusAsync():**
- Ruft Library-Status ab
- Mapped zu DTO ohne Lock-Status-Abgleich

**CheckAsync():**
- Behandelt GitHub-Rate-Limiting speziell
- Mappt Status nach Library-Check
- Setzt LastError bei Rate-Limit-Fehler

**StartInstallAsync():**
- Wirft Library-Fehler durch
- Ruft `ValidateLockCleanupAsync()` auf, wenn Installation erfolgreich
- Returned Status nach Installation

**ResetLockAsync():**
- Liest Live-Lock-Status von Dateisystem
- Validiert Lock existiert und ist alt genug
- Löscht Lock-Datei
- Aktualisiert Cache via `_statusService.UpdateAsync()` mit `IsLocked = false, LockCreatedAt = null`
- Klassifiziert und typisiert Fehler in `UpdateLockResetException`

**ValidateLockCleanupAsync():**
- Warnt, wenn Lock nach erfolgreicher Installation noch existiert
- Ignoriert I/O-Fehler und loggt sie auf Warning-Level

---

## `UpdateStatusMapper`
Datei: `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

Mapped `AutoUpdateStatusSnapshot` (aus msTools.Updater) auf `UpdateStatusDto` (FinanceManager DTO).

### Abhängigkeiten
- `IInstalledReleaseMetadataProvider` — Installed-Release-Metadaten
- `IAutoUpdatePlatformResolver` — Platform-Auflösung
- `IUpdateSettingsStore` — Update-Settings

### Methoden

| Methode | Sichtbarkeit | Beschreibung |
|---------|-------------|--------------|
| `MapAsync(AutoUpdateStatusSnapshot snapshot, CancellationToken ct)` | public | Mapped Snapshot zu DTO; aggregiert InstallVersion, CurrentPlatform, Settings, AvailableUpdate |

### Mapping-Details

| DTO-Feld | Quelle |
|----------|--------|
| `Status` (UpdateStatusKind) | `snapshot.State` via Switch-Expression (Idle→NoUpdate, Checking→Checking, etc.) |
| `InstalledVersion` | `installed.Version` |
| `InstalledPublishedAt` | `installed.PublishedAt` |
| `AvailableVersion` | `snapshot.AvailableVersion` |
| `CurrentPlatform` | `platformResolver.CurrentRuntimeIdentifier` |
| `LastCheckedAt` | `snapshot.LastCheckedAt` |
| `LastError` | `snapshot.LastError` via `UpdateErrorMessageMapper.Map()` |
| `DownloadedAssetName` | `Path.GetFileName(snapshot.LastDownloadResult.LocalPath)` oder null |
| `IsLocked` | `snapshot.IsLocked` (nicht mapped/verarbeitet) |
| `LockCreatedAt` | `snapshot.LockCreatedAt` (nicht mapped/verarbeitet) |
| `ScheduledInstallTime` | `settings.ScheduledInstallTime` |
| `AvailableUpdate` | Konstruiert aus `snapshot.LastCheckResult?.Package` wenn vorhanden |

**Hinweis zu Lock-Status:** Der Mapper gibt Lock-Status direkt aus Snapshot durch, ohne Reconciliation oder Validierung gegen Dateisystem.
