# Bestandsaufnahme Interfaces

Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

## `IUpdateOrchestrator`
Zentrale Orchestrierungs-API für Update-Management.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetStatusAsync` | `CancellationToken ct = default` | `Task<UpdateStatusDto>` | Liest aktuellen Update-Status mit Runtime-State |
| `GetSettingsAsync` | `CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Liest aktuelle Update-Einstellungen |
| `SaveSettingsAsync` | `UpdateSettingsUpdateRequest request, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Speichert geänderte Einstellungen |
| `ScheduleAsync` | `TimeOnly? scheduledInstallTime, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Setzt geplante Installationszeit |
| `CheckAsync` | `CancellationToken ct = default` | `Task<UpdateCheckResultDto>` | Prüft auf Updates: lädt Manifest, validiert, lädt Asset falls neuer |
| `StartInstallAsync` | `bool confirmDowntime, CancellationToken ct = default` | `Task<UpdateStatusDto>` | Startet Installation nach Downtime-Bestätigung |
| `ResetLockAsync` | `string? reason, CancellationToken ct = default` | `Task` | Setzt verwaistes Lock zurück |

---

## `IUpdateExecutor`
Executor für Installation: Lock, Skript, Prozess-Start, Host-Termination.

| Methode/Property | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `StartAsync` | `UpdateSettingsDto settings, UpdateStatusDto status, CancellationToken ct = default` | `Task<UpdateStatusDto>` | Startet Installation: Lock erstellen, Skript generieren, Prozess starten, Host terminator rufen |
| `IsInstallRunning` | (property) | `bool` (get/set) | Indikator, dass Installation läuft; wird manuell gesetzt, nie automatisch zurückgesetzt |

---

## `IUpdateSettingsStore`
Persistierung von Update-Einstellungen.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Liest Settings aus Speicher (JSON) |
| `SaveAsync` | `UpdateSettingsUpdateRequest request, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Speichert Settings nach Normalisierung |
| `SaveScheduleAsync` | `TimeOnly? scheduledInstallTime, CancellationToken ct = default` | `Task<UpdateSettingsDto>` | Speichert nur geplante Installationszeit |

---

## `IInstalledReleaseMetadataProvider`
Bestimmt installierte Version/Release-Metadaten.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync` | `CancellationToken ct = default` | `Task<InstalledReleaseMetadataDto>` | Liest Metadaten der installierten Release (Version, Datum, etc.) |

---

## `IUpdateManifestClient`
Lädt Manifest und Assets von GitHub-Releases.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetManifestAsync` | `UpdateSettingsDto settings, CancellationToken ct = default` | `Task<UpdateMetadataDto>` | Lädt Update-Manifest (update.json) von GitHub Release |
| `DownloadAssetAsync` | `UpdateAssetDto asset, string targetPath, long maxBytes, CancellationToken ct = default` | `Task` | Lädt Asset (ZIP) von GitHub-Release-URL |

---

## `IUpdatePlatformResolver`
Bestimmt aktuelle Plattform und wählt Assets.

| Methode/Property | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `CurrentRuntimeIdentifier` | (property) | `string` | Gibt aktuellen Runtime-Identifier zurück (z.B. "win-x64", "linux-x64") |
| `CurrentPlatform` | (property) | `string` | Gibt aktuelle Plattform zurück (z.B. "windows", "linux") |
| `SelectAsset` | `UpdateMetadataDto manifest` | `UpdateAssetDto?` | Wählt Asset aus Manifest mit Plattform- und Runtime-Match |

---

## `IUpdateFileStore`
Verzeichnis- und Datei-Verwaltung für Update-Artefakte.

| Methode/Property | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `RootDirectory` | (property) | `string` | Gibt Root-Verzeichnis für Update-Dateien zurück |
| `PendingDirectory` | (property) | `string` | Gibt Verzeichnis für heruntergeladene Assets zurück |
| `StagingDirectory` | (property) | `string` | Gibt Verzeichnis für entpackte Assets zurück |
| `SettingsPath` | (property) | `string` | Gibt Pfad zu settings.json zurück |
| `StatusPath` | (property) | `string` | Gibt Pfad zu status.json zurück |
| `LockPath` | (property) | `string` | Gibt Pfad zu update.lock Datei zurück |
| `ScriptPath` | `string extension` | `string` | Gibt Pfad zu Skript mit Erweiterung zurück (z.B. "ps1", "sh") |
| `PendingAssetPath` | `string assetName` | `string` | Gibt sicheren Pfad für Asset zurück (verhindert Path-Traversal) |
| `UseWorkingDirectory` | `string workingDirectory` | void | Setzt Custom Working Directory |
| `EnsureAsync` | `CancellationToken ct = default` | `Task` | Erstellt alle erforderlichen Verzeichnisse |
| `ReadStatusAsync` | `CancellationToken ct = default` | `Task<UpdateStatusDto?>` | Liest status.json (gibt null zurück wenn nicht vorhanden) |
| `WriteStatusAsync` | `UpdateStatusDto status, CancellationToken ct = default` | `Task` | Schreibt status.json atomar |
| `GetLockCreatedAtAsync` | `CancellationToken ct = default` | `Task<DateTimeOffset?>` | Liest Erstellungszeit der Lock-Datei (gibt null zurück wenn nicht existiert) |
| `TryCreateLockAsync` | `CancellationToken ct = default` | `Task<bool>` | Erstellt Lock-Datei; gibt false zurück bei Konflikt |
| `DeleteLockAsync` | `CancellationToken ct = default` | `Task<bool>` | Löscht Lock-Datei; gibt false zurück wenn nicht vorhanden |

---

## `IUpdateValidator`
Validiert Manifest und heruntergeladene Assets.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsNewerVersion` | `string? installedVersion, string availableVersion` | `bool` | Vergleicht Versionsnummern; gibt false zurück wenn installiert unknown |
| `ValidateManifest` | `UpdateMetadataDto manifest, UpdateSettingsDto settings, string currentPlatform` | void | Validiert Manifest-Struktur, Repository-Match, Asset für aktuelle Plattform |
| `ValidateDownloadedAssetAsync` | `UpdateAssetDto asset, string path, long maxBytes, CancellationToken ct = default` | `Task` | Validiert heruntergeladene ZIP: Existenz, Größe, SHA-256, sichere Entry-Pfade |

---

## `IUpdateScriptGenerator`
Generiert platformspezifische Shell-Skripte für Installation und Neustart.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GenerateAsync` | `UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct = default` | `Task<string>` | Generiert Skript (PowerShell oder Bash) und gibt Pfad zurück |

---

## `IUpdateServiceResolver`
Findet oder validiert Service-Name/Executable-Pfad für Installation.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Resolve` | `UpdateSettingsDto settings` | `UpdateInstallationTarget` | Wählt Service-Namen oder Executable-Pfad basierend auf Konfiguration oder Auto-Detektion |

---

## `IUpdateServiceProbe`
Auto-detektiert Service-Namen basierend auf aktueller Prozess-ID.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `FindWindowsServicesForCurrentProcess` | (keine) | `IReadOnlyList<string>` | Findet Windows-Services mit aktueller PID |
| `FindLinuxServicesForCurrentProcess` | (keine) | `IReadOnlyList<string>` | Findet systemd-Services mit aktueller PID |

---

## `IUpdateProcessRunner`
Startet Update-Skript als Prozess.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StartScript` | `string scriptPath` | void | Startet Skript (PowerShell oder Bash) als Prozess |

---

## `IUpdateHostTerminator`
Terminiert Host-Anwendung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `StopApplication` | (keine) | void | Stoppt Host-Anwendung |

---

## `UpdateInstallationTarget` (sealed record)
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Zielort für Installation.

| Property | Typ | Beschreibung |
|----------|-----|-------------|
| `Platform` | `string` | Zielplattform (z.B. "windows", "linux") |
| `ServiceName` | `string?` | Systemd-Service-Name (Linux) oder Windows-Service-Name |
| `ExecutablePath` | `string?` | Executable-Pfad (nur Windows, wenn Service nicht vorhanden) |
