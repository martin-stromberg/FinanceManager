# Logikklassen und Services

## `UpdateOrchestratorAdapter`
Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`

Adapter-Pattern-Implementierung von `IUpdateOrchestrator`. Delegiert an msTools.Updater Library und mappt DTOs.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStatusAsync(CancellationToken)` | public | Liest `AutoUpdateStatusSnapshot` von der Library und mappt auf `UpdateStatusDto` |
| `GetSettingsAsync(CancellationToken)` | public | Liest aktuelle Update-Settings |
| `SaveSettingsAsync(UpdateSettingsUpdateRequest, CancellationToken)` | public | Speichert Settings via `IUpdateSettingsStore` und wendet sie auf Library-Optionen an |
| `ScheduleAsync(TimeOnly?, CancellationToken)` | public | Speichert nur die geplante Installationszeit |
| `CheckAsync(CancellationToken)` | public | Triggert manuelle Update-Prüfung, mappt Ergebnis zu `UpdateCheckResultDto` |
| `StartInstallAsync(bool, CancellationToken)` | public | Triggert Installation via Library, wirft bei Fehler die Fehler-Exception |
| `ResetLockAsync(string?, CancellationToken)` | public | **Zentrale Lock-Reset-Methode** |
| `DeleteLockOrThrowAsync(DateTimeOffset, CancellationToken)` | private | Hilfsmethod für Lock-Löschung mit Fehlerbehandlung |
| `CreateResetException(...)` | private | Factory für `UpdateLockResetException` |

### `ResetLockAsync` — Detaillierte Logik
Die Methode ist das Zentrum des Lock-Inconsistency-Problems:

1. **Lock-Info abrufen:**
   - Ruft `_packageStore.GetLockCreatedAtAsync(ct)` auf
   - Wenn `null`, wirft Exception mit `NoLock`

2. **Staleness-Prüfung:**
   - Ruft `_packageStore.IsLockStale(lockCreatedAt.Value)` auf
   - Wenn nicht stale (zu jung), wirft Exception mit `LockNotStale`

3. **Lock-Datei löschen:**
   - Ruft `_packageStore.DeleteLockAsync(ct)` auf
   - Wenn false, wirft Exception mit `LockDeleteFailed`

4. **Status aktualisieren:**
   - Setzt `IsLocked = false` und `LockCreatedAt = null` in Status
   - Ergänzt optional `LastError` mit Reset-Grund

### Dependencies
- `IAutoUpdateOrchestrator` (von msTools.Updater)
- `AutoUpdateStatusService` (von msTools.Updater)
- `IUpdateSettingsStore`
- `IAutoUpdatePackageStore` (von msTools.Updater)
- `UpdateStatusMapper`

---

## `UpdateStatusMapper`
Datei: `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

Mappt Library-Status auf DTO. Liest auch `snapshot.IsLocked` und `snapshot.LockCreatedAt`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `MapAsync(AutoUpdateStatusSnapshot, CancellationToken)` | public | Mappt Library-Snapshot + Settings + Installed-Metadata zu `UpdateStatusDto` |
| `MapState(AutoUpdateState)` | private | Konvertiert Library-State zu `UpdateStatusKind` Enum |

**Wichtig:** Diese Klasse ist der Ursprung von `IsLocked` und `LockCreatedAt` in der DTO, die an die UI geht.

---

## `UpdateSettingsStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`

Persistiert Settings als JSON unter `{WorkingDirectory}/settings.json` und wendet sie auf Library-Optionen an.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync(CancellationToken)` | public | Liest Settings aus Datei oder gibt Defaults zurück |
| `SaveAsync(UpdateSettingsUpdateRequest, CancellationToken)` | public | Normalisiert Request und schreibt atomar zu settings.json |
| `SaveScheduleAsync(TimeOnly?, CancellationToken)` | public | Aktualisiert nur die geplante Installationszeit |
| `ApplyToOptions(UpdateSettingsDto)` | public | Delegiert an `AutoUpdateOptionsMapper.ApplySettings` |
| `ReadSettingsAsync(CancellationToken)` | private | Liest und deserialisiert JSON; unterstützt Legacy-Formate |
| `WriteAtomicAsync(UpdateSettingsDto, CancellationToken)` | private | Atomares Schreiben via `JsonFileStore.WriteAtomicAsync` |
| `Build(UpdateSettingsUpdateRequest)` | private | Normalisiert, validiert und clamped Settings |

### Datei-Operationen
- Ort: `{IAutoUpdatePackageStore.RootDirectory}/settings.json`
- Unterstützt Legacy-Formate (windowsServiceName, linuxServiceName)
- Atomares Schreiben mit `JsonFileStore.WriteAtomicAsync`

---

## `UpdateStatusMapper`
Datei: `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

(Bereits oben aufgelistet, aber hier Detailbeschreibung der Mapping-Logik)

Liest folgende Quellen:
1. `snapshot.IsLocked` → `UpdateStatusDto.IsLocked`
2. `snapshot.LockCreatedAt` → `UpdateStatusDto.LockCreatedAt`
3. `snapshot.State` → `UpdateStatusDto.Status` (State-Mapping)
4. `snapshot.LastDownloadResult?.LocalPath` → `UpdateStatusDto.DownloadedAssetName` (nur Dateiname)

---

## `AutoUpdateOptionsMapper`
Datei: `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`

Statische Utility-Klasse für bidirektionales Mapping zwischen DTO und Library-Options.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ApplySettings(AutoUpdateOptions, UpdateSettingsDto)` | public static | Überträgt DTO-Werte in mutable Library-Optionen; ersetzt GitHub-Source bei geändertem Repo |
| `ToSettingsDto(AutoUpdateOptions, repositoryOwner, repositoryName, manifestAssetName)` | public static | Konvertiert Library-Optionen zurück zu DTO |
| `BuildSourceCheckTimeRanges(TimeOnly, TimeOnly)` | public static | Erzeugt Liste von `SourceCheckTimeRange` für Update-Prüf-Fenster |
| `ReadSourceCheckWindow(...)` | private static | Rekonstruiert Start/End-Zeiten aus Time-Ranges |

### Konstanten
- `DefaultSourceCheckStartTime = TimeOnly(20, 0)` (20:00 Uhr)
- `DefaultSourceCheckEndTime = TimeOnly(6, 0)` (06:00 Uhr)
- `DailySourceCheckIntervalMinutes = 24 * 60 = 1440`

---

## `DefaultUpdateServiceCatalog`
Datei: `FinanceManager.Web/Services/Updates/UpdateServiceCatalog.cs`

Implementiert `IUpdateServiceCatalog` für Service-Name-Suggestions in der UI.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ListServiceNamesAsync(string?, int, CancellationToken)` | public | Listet Windows Services (sc.exe) oder systemd-Units auf, filtert und begrenzt |
| `ParseWindowsServiceNames(string)` | public static | Parst Ausgabe von `sc query` |
| `ParseLinuxServiceNames(string)` | public static | Parst Ausgabe von `systemctl list-units` |
| `Filter(IReadOnlyList<string>, string?, int)` | private static | Filtert nach Query-Substring und begrenzt auf `take` |
| `RunAsync(string, IReadOnlyList<string>, CancellationToken)` | private static | Startet Prozess, liest stdout mit 3-Sekunden-Timeout |
| `TryKill(Process)` | private static | Best-Effort Prozess-Beendigung |

---

## `UpdateErrorMessageMapper`
Datei: `FinanceManager.Web/Services/Updates/UpdateErrorMessageMapper.cs`

Statische Utility-Klasse für Error-Message-Mapping, besonders für GitHub-Rate-Limit-Fehler.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Map(string?)` | public static | Mappt Meldung; GitHub-RateLimit → deutsche Fehlermeldung |
| `Map(Exception)` | public static | Mappt Exception-Message |
| `IsGithubRateLimit(string)` | public static | Prüft auf "403" + "rate limit" in Text |

### Konstante
- `GithubRateLimitMessage` — Deutsche Fehlermeldung für Rate-Limit-Fehler
