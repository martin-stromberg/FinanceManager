# Interfaces und Externe Typen

## FinanceManager Interfaces

### `IUpdateOrchestrator`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Koordiniert Self-Update-Workflow für REST API und Setup UI. Implementiert von `UpdateOrchestratorAdapter`.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetStatusAsync(CancellationToken ct)` | CancellationToken | `Task<UpdateStatusDto>` | Aktueller Update-Status |
| `GetSettingsAsync(CancellationToken ct)` | CancellationToken | `Task<UpdateSettingsDto>` | Aktuelle Update-Settings |
| `SaveSettingsAsync(UpdateSettingsUpdateRequest request, CancellationToken ct)` | Request, CancellationToken | `Task<UpdateSettingsDto>` | Speichert Settings |
| `ScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct)` | TimeOnly?, CancellationToken | `Task<UpdateSettingsDto>` | Setzt/löscht Installations-Zeitplan |
| `CheckAsync(CancellationToken ct)` | CancellationToken | `Task<UpdateCheckResultDto>` | Manuelle Update-Prüfung |
| `StartInstallAsync(bool confirmDowntime, CancellationToken ct)` | bool (Downtime-Bestätigung), CancellationToken | `Task<UpdateStatusDto>` | Startet Installation |
| `ResetLockAsync(string? reason, CancellationToken ct)` | Grund (optional), CancellationToken | `Task` | Setzt stalen Lock zurück |

---

### `IUpdateSettingsStore`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Persistiert und lädt Update-Settings aus Konfiguration.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync(CancellationToken ct)` | CancellationToken | `Task<UpdateSettingsDto>` | Liest aktuelle Settings, applies Defaults on first access |
| `SaveAsync(UpdateSettingsUpdateRequest request, CancellationToken ct)` | Request, CancellationToken | `Task<UpdateSettingsDto>` | Speichert neue Settings |
| `SaveScheduleAsync(TimeOnly? scheduledInstallTime, CancellationToken ct)` | TimeOnly?, CancellationToken | `Task<UpdateSettingsDto>` | Speichert nur Zeitplan, andere Settings unverändert |
| `ApplyToOptions(UpdateSettingsDto settings)` | DTO | void | Transferiert Settings in Runtime-mutable AutoUpdateOptions |

---

### `IInstalledReleaseMetadataProvider`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Stellt Metadaten der aktuell installierten Release zur Verfügung.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetAsync(CancellationToken ct)` | CancellationToken | `Task<InstalledReleaseMetadataDto>` | Liest Metadaten der installierten Release |

---

### `IUpdateServiceCatalog`
Datei: `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

Katalog für Service-Namen-Autocomplete im Setup UI.

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `ListServiceNamesAsync(string? query, int take, CancellationToken ct)` | Filter-String, Max-Anzahl, CancellationToken | `Task<IReadOnlyList<string>>` | Liefert Kandidaten-Service-Namen |

---

## msTools.Updater Interfaces/Klassen (aus XML-Dokumentation)

### `IAutoUpdateOrchestrator`
Orchestrator aus Library (wird von `UpdateOrchestratorAdapter` benutzt).

| Methode | Zweck |
|---------|-------|
| `GetStatusAsync(CancellationToken ct)` | Liest aktuellen Status mit Restart-Reconciliation |
| `CheckForUpdateAsync(CancellationToken ct)` | Prüft auf verfügbare Updates |
| `DownloadAsync(CancellationToken ct)` | Lädt verfügbare Update herunter |
| `InstallAsync(bool confirmDowntime, CancellationToken ct)` | Installiert Download |
| `RunUpdateAsync(CancellationToken ct)` | Vollständiger Update-Ablauf |

Rückgabewert: `AutoUpdateResult` mit `Outcome` (Success/Failed) und `Error` (Exception oder null)

---

### `IAutoUpdatePackageStore`
Verwaltet Lock-Dateien und Status-Persistierung.

| Methode | Zweck |
|---------|-------|
| `GetLockCreatedAtAsync(CancellationToken ct)` | Live-Read: Liest Lock-Datei, gibt `DateTimeOffset?` zurück (null wenn nicht vorhanden) |
| `DeleteLockAsync(CancellationToken ct)` | Löscht Lock-Datei, gibt bool zurück |
| `IsLockStale(DateTimeOffset lockCreatedAt)` | Prüft, ob Lock alt genug ist (z.B. >2 Minuten) |
| `LockPath` (Property) | Pfad der Lock-Datei |

---

### `AutoUpdateStatusService`
Service aus msTools.Updater. Liest/schreibt Status-Snapshot aus/in Persistierung.

| Methode | Zweck |
|---------|-------|
| `GetSnapshot()` | Liest aktuellen Snapshot aus internem Cache |
| `UpdateAsync(Func<AutoUpdateStatusSnapshot, AutoUpdateStatusSnapshot> mutator, CancellationToken ct)` | Mutiert Snapshot (z.B. `s with { IsLocked = false }`) und persistiert |

**Hinweis:** `GetSnapshot()` ist synchron und gibt gecachten Wert zurück, nicht persisted State.

---

### `AutoUpdateStatusSnapshot`
Datenmodell aus msTools.Updater, repräsentiert aktuellen Update-Status.

Wichtige Eigenschaften:
- `State` (AutoUpdateState enum)
- `AvailableVersion` (string)
- `IsLocked` (bool) — Flag aus Cache
- `LockCreatedAt` (DateTimeOffset?) — Timestamp aus Cache
- `LastCheckedAt` (DateTimeOffset?)
- `LastDownloadResult` (object?)
- `LastCheckResult` (AutoUpdateCheckResult?)
- `LastError` (string?)

**Hinweis zu IsLocked/LockCreatedAt:** Diese Werte stammen aus internem Cache (`AutoUpdateStatusService`), nicht aus Live-Read der Lock-Datei.

---

### `AutoUpdateState` Enum
Zustände der Update-Maschine (aus XML):

| Wert | Bedeutung |
|------|-----------|
| `Idle` | Kein Update aktiv |
| `Checking` | Prüfung läuft |
| `UpdateAvailable` | Update gefunden, nicht heruntergeladen |
| `Downloading` | Download läuft |
| `ReadyToInstall` | Download abgeschlossen, Installation bereit |
| `Installing` | Installation läuft |
| `Success` | Installation erfolgreich |
| `Failed` | Operation fehlgeschlagen |
| `Disabled` | Auto-Update deaktiviert |

---

### `AutoUpdateResult`
Ergebnis einer Library-Operation.

| Eigenschaft | Typ | Bedeutung |
|-------------|-----|-----------|
| `Outcome` | `AutoUpdateOutcome` (Success/Failed) | Operation erfolgreich oder fehlgeschlagen |
| `State` | `AutoUpdateState` | Resultat-Status |
| `Message` | string | Ergebnis-Meldung |
| `Error` | Exception? | Exception (bei Failed), null bei Success |

---

### `AutoUpdateOutcome` Enum
Ergebnis einer Operation.

| Wert | Bedeutung |
|------|-----------|
| `Success` | Operation erfolgreich |
| `Failed` | Operation fehlgeschlagen |

---

### `AutoUpdateCheckResult`
Ergebnis einer Update-Prüfung (in `LastCheckResult`).

| Eigenschaft | Typ | Bedeutung |
|-------------|-----|-----------|
| `AvailableVersion` | string | Verfügbare Version |
| `Package` | `AutoUpdatePackageDescriptor` | Paket-Info |
| `ReleaseNotes` | string? | Release Notes URL |
| `PublishedAt` | DateTimeOffset? | Veröffentlichungs-Datum |

---

### `AutoUpdatePackageDescriptor`
Beschreibung eines Download-Pakets.

| Eigenschaft | Typ | Bedeutung |
|-------------|-----|-----------|
| `Version` | string | Paket-Version |
| `Platform` | string | Plattform (z.B. "windows") |
| `RuntimeIdentifier` | string | Runtime-ID (z.B. "win-x64") |
| `FileName` | string | Dateiname (z.B. "app.zip") |
| `Uri` | Uri | Download-URL |
| `Sha256` | string | SHA256-Hash |
| `SizeBytes` | long | Größe in Bytes |
