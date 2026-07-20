# Bestandsaufnahme Service-Logik

## `UpdateOrchestrator`
Datei: `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs`

**Zweck:** Zentrale Orchestrierung des automatischen Update-Prozesses (Lock-State-Tracking, Manifest-Checks, Asset-Download, Installation).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetStatusAsync(CancellationToken ct)` | public | Liest aktuellen Update-Status, enriched mit Runtime-State (Lock-Status, Installed Version) |
| `GetSettingsAsync(CancellationToken ct)` | public | Liest aktuelle Update-Einstellungen |
| `SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct)` | public | Speichert geänderte Update-Einstellungen |
| `ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct)` | public | Setzt geplante Installationszeit |
| `CheckAsync(CancellationToken ct)` | public | Führt Update-Prüfung durch: Manifest laden, Asset validieren, ggf. herunterladen |
| `StartInstallAsync(bool confirmDowntime, CancellationToken ct)` | public | Startet Installation nach Downtime-Bestätigung; delegiert an `UpdateExecutor` |
| `ResetLockAsync(string? reason, CancellationToken ct)` | public | Setzt verwaistes Lock zurück, aber nur wenn Alter >= `MinimumStaleLockAge` oder `HealthTimeoutSeconds` |
| `EmptyStatus(InstalledReleaseMetadataDto installed)` | private | Erstellt Standard-Status mit aktuellen Installed-Versionsdaten |
| `WithRuntimeStateAsync(UpdateStatusDto status, InstalledReleaseMetadataDto installed, CancellationToken ct)` | private | Enriched Status mit aktuellen Runtime-Infos (Lock-Status, Scheduler-Info) |

**Dependencies:**
- `IUpdateSettingsStore` — Lädt/speichert Settings
- `IInstalledReleaseMetadataProvider` — Liest installierte Version
- `IUpdateManifestClient` — Downloadt Manifest und Assets
- `IUpdatePlatformResolver` — Bestimmt aktuelle Plattform
- `IUpdateFileStore` — Lock- und Status-Persistierung
- `IUpdateValidator` — Validiert Manifest und Assets
- `IUpdateExecutor` — Führt Installation aus

**Wichtige Konstanten:**
- `MinimumStaleLockAge = TimeSpan.FromMinutes(1)` — Minimales Alter eines Locks, bevor es als verwaist betrachtet wird (wird überschrieben durch `Math.Max(HealthTimeoutSeconds, 60)`)

**Fehlerbehandlung:**
- `StartInstallAsync`: Prüft `IsLocked` und `Status != Installing` vor Handover an Executor
- `ResetLockAsync`: Prüft `_executor.IsInstallRunning`, Lock-Existenz, und Staleness

---

## `UpdateExecutor`
Datei: `FinanceManager.Web/Services/Updates/UpdateExecutor.cs`

**Zweck:** Führt Installer-Prozess aus: Lock-Erstellung, Skript-Generierung, Prozess-Start, Host-Termination.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartAsync(UpdateSettingsDto settings, UpdateStatusDto status, CancellationToken ct)` | public | Startet Update-Installation: Lock erstellen, Skript generieren, Prozess starten, Host terminator aufrufen |
| `IsInstallRunning` (property) | public | Boolean-Flag, das während Installation true ist; **wird nach Prozessstart nicht automatisch zurückgesetzt** |

**Fehlerbehandlung:**
- Wenn `TryCreateLockAsync` fehlschlägt, wirft `IOException`
- Wenn Prozess-Start vor Script-Generation fehlschlägt, löscht Lock sofort und schreibt Failed-Status
- Nach erfolgreicher `ProcessRunner.StartScript()` wird `IsInstallRunning = true` gesetzt, aber **nie wieder zurückgesetzt** (das wird von außen erwartet)

**Probleme (Anforderung):**
1. `IsInstallRunning` wird nach Prozessstart auf `true` gesetzt, aber nie wieder zurückgesetzt → Lock kann nicht manuell resettet werden
2. Keine Finally-Klausel zur Lock-Freigabe bei Fehler nach Prozessstart

---

## `DefaultUpdateProcessRunner`
Datei: `FinanceManager.Web/Services/Updates/UpdateExecutor.cs`

**Zweck:** Startet Update-Skript als Prozess.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartScript(string scriptPath)` | public | Startet PowerShell (.ps1) oder Bash (.sh) Skript als Prozess ohne Shell-Fenster |

**Besonderheiten:**
- Windows: `powershell.exe -ExecutionPolicy Bypass -File "..."`
- Linux: `/usr/bin/env bash "..."`
- `CreateNoWindow = true`, `UseShellExecute = false`
- Wirft Exception, wenn `Process.Start()` null zurückgibt

---

## `DefaultUpdateHostTerminator`
Datei: `FinanceManager.Web/Services/Updates/UpdateExecutor.cs`

**Zweck:** Terminiert Host-Anwendung nach Prozessstart.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StopApplication()` | public | Ruft `IHostApplicationLifetime.StopApplication()` auf |

---

## `UpdateFileStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateFileStore.cs`

**Zweck:** Persistiert Lock-Dateien, Status-JSON und stellt Verzeichnisse bereit.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RootDirectory` (property) | public | Pfad zum Root-Verzeichnis für Updates |
| `PendingDirectory` (property) | public | Verzeichnis für heruntergeladene Assets |
| `StagingDirectory` (property) | public | Verzeichnis für entpackte Assets vor Installation |
| `SettingsPath` (property) | public | Pfad zur settings.json Datei |
| `StatusPath` (property) | public | Pfad zur status.json Datei |
| `LockPath` (property) | public | Pfad zur update.lock Datei |
| `ScriptPath(string extension)` | public | Gibt Pfad zum Skript mit Erweiterung (.ps1 oder .sh) |
| `PendingAssetPath(string assetName)` | public | Gibt sicheren Pfad für Asset (verhindert Path-Traversal) |
| `UseWorkingDirectory(string workingDirectory)` | public | Setzt Custom Working Directory |
| `EnsureAsync(CancellationToken ct)` | public | Erstellt alle erforderlichen Verzeichnisse |
| `ReadStatusAsync(CancellationToken ct)` | public | Liest status.json, gibt null zurück wenn nicht vorhanden |
| `WriteStatusAsync(UpdateStatusDto status, CancellationToken ct)` | public | Schreibt status.json atomar |
| `GetLockCreatedAtAsync(CancellationToken ct)` | public | Liest Erstellungszeit der Lock-Datei (oder null) |
| `TryCreateLockAsync(CancellationToken ct)` | public | Erstellt Lock-Datei mit CreateNew flag; gibt false zurück bei Konflikt |
| `DeleteLockAsync(CancellationToken ct)` | public | Löscht Lock-Datei; gibt false zurück wenn nicht vorhanden |
| `ResolveSafePath(string configuredPath)` | private | Konvertiert relative/absolute Pfade sicher |

**Probleme (Anforderung):**
- `GetLockCreatedAtAsync` nutzt `File.GetCreationTimeUtc()` — auf Linux unreliabel, da Creation-Time nicht gut unterstützt wird

---

## `UpdateSettingsStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`

**Zweck:** Lädt/speichert Settings aus JSON, unterstützt Legacy-Format Migration.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync(CancellationToken ct)` | public | Liest settings.json oder gibt Defaults zurück; setzt Working-Directory |
| `SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct)` | public | Speichert Settings nach Normalisierung |
| `SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct)` | public | Aktualisiert nur `ScheduledInstallTime` |
| `Defaults()` | private | Erstellt Default-SettingsDto aus `UpdateOptions` |
| `Normalize(UpdateSettingsUpdateRequest request)` | private | Normalisiert Request-Werte (Clamps, Trim, Validierung) |
| `ReadSettingsAsync(CancellationToken ct)` | private | Liest JSON; unterstützt Legacy-Format mit `windowsServiceName`/`linuxServiceName` |

**Normalisierungsregeln:**
- `CheckIntervalMinutes`: Clamped [1, 1440]
- `HealthTimeoutSeconds`: Clamped [10, 600]
- `WorkingDirectory`: Default "updates", validiert gegen ungültige Zeichen

---

## `UpdateScriptGenerator`
Datei: `FinanceManager.Web/Services/Updates/UpdateScriptGenerator.cs`

**Zweck:** Generiert platformspezifische Shell-Skripte für Update und Neustart.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GenerateAsync(UpdateAssetDto asset, string zipPath, UpdateSettingsDto settings, UpdateInstallationTarget target, CancellationToken ct)` | public | Generiert Skript basierend auf aktuellem OS |
| `GenerateWindowsAsync(string zipPath, UpdateInstallationTarget target, CancellationToken ct)` | private | Generiert PowerShell-Skript (.ps1) mit Service-Stop/Start oder EXE-Start |
| `GenerateLinuxAsync(string zipPath, UpdateInstallationTarget target, CancellationToken ct)` | private | Generiert Bash-Skript (.sh) mit systemctl stop/start |
| `Ps(string value)` | private static | Escaped PowerShell-String |
| `Sh(string value)` | private static | Escaped Bash-String |

**Script-Verhalten:**
- Windows: Stop Service → Clear Staging → Extract ZIP → Copy Files → Delete Lock → Start Service
- Linux: Stop systemd Service → Clear Staging → Extract ZIP → Copy Files → Delete Lock → Start systemd Service
- Beide: `sleep 3` vor Stop-Aktion
- Beide: Setzen Lock-Pfad auf Lese/Schreibebene

**Probleme (Anforderung):**
- Linux-Skript nach Service-Stop zur Überprüfung keine Validierung dass neue Version korrekt lädt

---

## `UpdateServiceResolver`
Datei: `FinanceManager.Web/Services/Updates/UpdateServiceResolver.cs`

**Zweck:** Findet oder validiert Service-Name/Executable-Pfad basierend auf aktueller Plattform.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Resolve(UpdateSettingsDto settings)` | public | Delegiert an `ResolveWindows` oder `ResolveLinux` |
| `ResolveWindows(UpdateSettingsDto settings)` | private | Wählt Service oder Executable; auto-detektiert wenn configured |
| `ResolveLinux(UpdateSettingsDto settings)` | private | Wählt Service; nur Service-Name unterstützt auf Linux |
| `ValidateServiceName(string value, string label)` | private static | Validiert gegen Pfad-Trennzeichen und ungültige Zeichen |
| `ValidateExecutablePath(string value)` | private | Validiert dass Pfad absolut, vorhanden und unter app root liegt |
| `Distinct(IReadOnlyList<string> names)` | private static | Dedupliziert und sortiert Service-Namen |

**Dependencies:**
- `IUpdateServiceProbe` — Detektiert Service-Namen wenn nicht configured

---

## `DefaultUpdateServiceProbe`
Datei: `FinanceManager.Web/Services/Updates/UpdateServiceResolver.cs`

**Zweck:** Auto-detektiert Service-Name basierend auf aktueller Prozess-ID.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `FindWindowsServicesForCurrentProcess()` | public | Nutzt `sc.exe queryex` um Services mit aktueller PID zu finden |
| `FindLinuxServicesForCurrentProcess()` | public | Nutzt `systemctl status` oder `/proc/self/cgroup` um systemd-Service zu finden |
| `TryReadSystemdServiceFromCgroup()` | private static | Parst `/proc/self/cgroup` nach systemd-Service-Name |
| `Run(string fileName, string arguments)` | private static | Startet Prozess und liest Stdout |
| `SystemdServiceRegex()` (generated) | private static | Regex: `[A-Za-z0-9_.@-]+\.service` |

---

## `UpdateValidator`
Datei: `FinanceManager.Web/Services/Updates/UpdateValidator.cs`

**Zweck:** Validiert Manifest und heruntergeladene Assets auf Integrität und Sicherheit.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IsNewerVersion(string? installedVersion, string availableVersion)` | public | Vergleicht Versionsnummern; gibt false zurück wenn installierte Version unknown ist |
| `ValidateManifest(UpdateMetadataDto manifest, UpdateSettingsDto settings, string currentPlatform)` | public | Validiert Manifest-Struktur, Repository-Match, Asset-Existenz für aktuelle Plattform |
| `ValidateDownloadedAssetAsync(UpdateAssetDto asset, string path, long maxBytes, CancellationToken ct)` | public | Validiert Dateiexistenz, Größe, SHA-256 Hash, ZIP-Struktur (sichere Entry-Pfade) |
| `ValidateManifestAsset(UpdateMetadataDto manifest, UpdateAssetDto asset)` | private static | Validiert einzelnes Asset: Plattform, URL, Sha256, Runtime-Identifier, Asset-Naming-Convention |
| `ValidateEntry(ZipArchiveEntry entry)` | private static | Validiert ZIP-Entry gegen Path-Traversal und spezielle Dateitypen |
| `ComputeSha256Async(string path, CancellationToken ct)` | private static | Berechnet SHA-256 Hash einer Datei |

**Validierungsregeln:**
- Manifest-Version muss parsbar sein
- Asset-Naming-Convention: `FinanceManager-v{version}-{runtime}.zip`
- Asset-URL muss HTTPS GitHub Release URL sein
- ZIP-Entry darf nicht mit `/`, `\`, rooted path oder `:` starten
- ZIP-Entry darf keine `..` oder `.` Segmente enthalten
- ZIP-Entry darf nur normale Datei oder Verzeichnis sein (kein Symlink, etc.)

---

## `InstalledReleaseMetadataProvider`
Datei: `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`

**Zweck:** Liest installierte Release-Versionsinformationen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAsync(CancellationToken ct)` | public | Liest `release-metadata.json` aus ContentRootPath; gibt Default zurück wenn nicht vorhanden |

**Bemerkung:**
- Datei-Pfad ist hartcodiert: `{ContentRootPath}/release-metadata.json`

---

## `UpdatePlatformResolver`
Datei: `FinanceManager.Web/Services/Updates/UpdatePlatformResolver.cs`

**Zweck:** Bestimmt aktuelle Plattform und wählt passenden Asset aus Manifest.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CurrentRuntimeIdentifier` (property) | public | Gibt "win-x64" oder "linux-x64" zurück |
| `CurrentPlatform` (property) | public | Gibt "windows", "linux" oder OSDescription zurück |
| `SelectAsset(UpdateMetadataDto manifest)` | public | Wählt Asset mit Plattform- und Runtime-Match |

---

## `UpdateChecker` (BackgroundService)
Datei: `FinanceManager.Web/Services/Updates/UpdateChecker.cs`

**Zweck:** Background-Service, der periodisch nach Updates prüft.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync(CancellationToken stoppingToken)` | protected | Endless-Loop: Liest Settings, führt Check aus, wartet `CheckIntervalMinutes` |

**Fehlerbehandlung:**
- Bei Fehler: Log Warning + 5-Minuten Wartezeit vor nächster Versuch

---

## Verwandte UI/ViewModel Klassen

### `SetupUpdateViewModel`
Datei: `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `LoadAsync(CancellationToken ct)` | public | Lädt Settings und Status via API |
| `SaveAsync(CancellationToken ct)` | public | Speichert geänderte Settings |
| `CheckAsync(CancellationToken ct)` | public | Startet Update-Prüfung |
| `StartInstallAsync(bool confirmDowntime, CancellationToken ct)` | public | Startet Installation |
| `ResetLockAsync(CancellationToken ct)` | public | Resettet Lock mit Grund "Reset from setup UI" |
| `UpdateSettings(UpdateSettingsDto settings)` | public | Updates lokale Settings-Referenz |
| `MarkHealthTimeout()` | public | Markiert Health-Timeout Fehler |

**Properties:**
- `Settings` (`UpdateSettingsDto?`), `Status` (`UpdateStatusDto?`)
- `Busy` (`bool`), `Installing` (`bool`)
- `LastError` (from BaseViewModel), `ErrorCode` (from BaseViewModel)

---

## Verwandte API-Klasse

### `UpdateController`
Datei: `FinanceManager.Web/Controllers/UpdateController.cs`

| Endpoint | Methode | Beschreibung |
|----------|---------|-------------|
| `GET /api/setup/update/status` | `Status(CancellationToken ct)` | Gibt aktuellen Update-Status zurück |
| `GET /api/setup/update/settings` | `Settings(CancellationToken ct)` | Gibt aktuelle Einstellungen zurück |
| `PUT /api/setup/update/settings` | `UpdateSettings(UpdateSettingsUpdateRequest, CancellationToken ct)` | Speichert Einstellungen |
| `POST /api/setup/update/check` | `Check(CancellationToken ct)` | Startet Update-Prüfung |
| `POST /api/setup/update/schedule` | `Schedule(UpdateScheduleRequest, CancellationToken ct)` | Setzt geplante Zeit |
| `POST /api/setup/update/install/start` | `StartInstall(UpdateStartRequest, CancellationToken ct)` | Startet Installation; Fehlerbehandlung: FileNotFoundException (404), IOException (409), ArgumentException/InvalidOperationException (400) |
| `POST /api/setup/update/lock/reset` | `ResetLock(UpdateLockResetRequest, CancellationToken ct)` | Resettet Lock; Fehlerbehandlung: IOException (409) |

**Authentifizierung:** JWT Bearer + Admin-Rolle erforderlich
