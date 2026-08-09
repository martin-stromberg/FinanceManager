# Konfigurationsmodelle und Exception-Klassen

## `UpdateOptions`
Datei: `FinanceManager.Web/Services/Updates/UpdateOptions.cs`

FinanceManager-spezifische Binding für die Konfigurationssektion `Updates`. Hält die FinanceManager-spezifischen Felder (Repository, Manifest-Name, Source-Auswahl), die für die Erstellung der `AutoUpdateBuilder`-Konfiguration benötigt werden.

| Eigenschaft | Typ | Standardwert | Beschreibung |
|-------------|-----|---|--------|
| `SourceCheckStartTime` | `TimeOnly` | `20:00` | Inklusive Startzeit für tägliche automatische Checks |
| `SourceCheckEndTime` | `TimeOnly` | `06:00` | Exklusive Endzeit für tägliche automatische Checks |
| `RepositoryOwner` | `string` | `martin-stromberg` | GitHub-Repository-Besitzer (Benutzer oder Organisation) |
| `RepositoryName` | `string` | `FinanceManager` | Name des GitHub-Repository als Update-Quelle |
| `ManifestAssetName` | `string` | `update.json` | Dateiname des Release-Manifests |
| `WorkingDirectory` | `string` | `updates` | Root-Verzeichnis für Update-Pakete, Status und Sperr-Dateien |
| `SourceType` | `string` | `Github` | Typ der Update-Quelle: `Github` oder `LocalFolder` |
| `LocalFolderPath` | `string?` | `null` | Lokales Verzeichnis für LocalFolder-Source (wenn SourceType == LocalFolder) |

**Konfigurationssektion:** `UpdateOptions.SectionName = "Updates"`

**Bemerkung:** Nur die Repository und Manifest-spezifischen Felder sind hier. Runtime-änderbare Felder (Auto-Download/Installation, Timeouts, Byte-Limits, Services) sind auf `AutoUpdateOptions` aus der msTools.Updater-Bibliothek gebunden, nicht auf diese Klasse.

---

## `UpdateLockResetFailureKind` (Enum)
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Klassifiziert den Grund, warum ein Update-Lock-Reset fehlgeschlagen ist.

| Wert | Beschreibung |
|------|-----------|
| `NoLock` | Es existiert keine aktive Update-Sperr |
| `LockNotStale` | Die aktive Sperr ist nicht alt genug um als veraltet zu gelten |
| `LockDeleteFailed` | Die aktive Sperr konnte nicht gelöscht werden |
| `ResetFailed` | Das Reset ist aus einem anderen technischen Grund fehlgeschlagen |

---

## `UpdateLockResetFailureSource` (Enum)
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Gibt an, wo ein Reset-Fehler erkannt wurde.

| Wert | Beschreibung |
|------|-----------|
| `FinanceManager` | FinanceManager hat den Fehler aus lokalem Zustand oder Invarianten erkannt |
| `Updater` | Der Updater Package-Store oder eine andere Updater-Komponente hat den Fehler gemeldet |

---

## `UpdateLockResetException`
Datei: `FinanceManager.Web/Services/Updates/UpdateLockResetException.cs`

Exception-Klasse für klassifizierte Update-Lock-Reset-Fehler. Erbt von `IOException`.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-----------|
| `Kind` | `UpdateLockResetFailureKind` | Der klassifizierte Reset-Fehler-Typ |
| `FailureSource` | `UpdateLockResetFailureSource` | Wo der Fehler erkannt wurde |
| `LockCreatedAt` | `DateTimeOffset?` | Zeitstempel der Lock-Erstellung (falls verfügbar) |
| `LockPath` | `string?` | Pfad der Lock-Datei (falls verfügbar) |

**Konstruktor:**
```csharp
UpdateLockResetException(
    UpdateLockResetFailureKind kind,
    UpdateLockResetFailureSource failureSource,
    string message,
    DateTimeOffset? lockCreatedAt = null,
    string? lockPath = null,
    Exception? innerException = null)
```

**Verwendung:** Wird von `UpdateOrchestratorAdapter.ResetLockAsync` geworfen, wenn ein Lock-Reset fehlschlägt.

---

## Externe Konfigurationsquellen

### `AutoUpdateOptions`
Aus msTools.Updater v0.3.0

Die Laufzeit-änderbare Konfigurationsklasse der Bibliothek. Wird durch `AutoUpdateOptionsMapper` und `UpdateSettingsStore` aktualisiert und von `UpdateOrchestratorAdapter` verwendet.

**Relevant für FinanceManager:**
- `Enabled` — ob Auto-Updates aktiviert sind
- `SourceCheck` — Quellprüfungs-Konfiguration (Interval, TimeRanges)
- `ServiceName` — Name des zu aktualisierenden Windows/Linux-Service
- `ExecutablePath` — Pfad zur ausführbaren Datei
- `DownloadPath` — Verzeichnis für Download-Pakete (mapped zu `WorkingDirectory`)
- `HealthTimeoutSeconds` — Timeout für Health-Check nach Installation
- `ScheduledInstallTime` — Geplante Installationszeit
- `AllowPrereleaseUpdates` — ob Prerelease-Versionen erlaubt sind
- `Source` — Update-Quelle (üblicherweise `AutoUpdateGithubSource`)

---

## Legacy-Datenstrukturen

### `LegacyUpdateSettingsDto` (inneres Record)
Datei: `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`

Für Abwärtskompatibilität mit alten `settings.json`-Dateien, die `windowsServiceName` und `linuxServiceName` statt `serviceName` verwenden.

### `PersistedUpdateSettingsDto` (inneres Record)
Datei: `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`

Aktuelles Persistierungs-Format in `settings.json`.
