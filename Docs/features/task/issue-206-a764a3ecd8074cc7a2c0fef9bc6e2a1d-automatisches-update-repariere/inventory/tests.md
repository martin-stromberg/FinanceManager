# Bestandsaufnahme Tests

## Testklassen

### `UpdateOrchestratorTests`
Datei: `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs`

Testet zentrale Orchestrierungs-Logik für Lock-Management und Status-Übergänge.

| Testmethode | Was wird getestet |
|-------------|------------------|
| `ResetLockAsync_WhenInstallRuns_RefusesReset` | Lock-Reset wird abgelehnt wenn `executor.IsInstallRunning == true` |
| `ResetLockAsync_WhenNoLockExists_RefusesReset` | Lock-Reset wird abgelehnt wenn keine Lock-Datei existiert |
| `ResetLockAsync_WhenLockIsFresh_RefusesReset` | Lock-Reset wird abgelehnt wenn Lock jünger als Staleness-Threshold ist |
| `ResetLockAsync_WhenLockIsStale_DeletesLockAndWritesReason` | Lock wird gelöscht und Grund wird in Status geschrieben wenn Lock alt genug ist |
| `StartInstallAsync_WhenDowntimeIsNotConfirmed_ThrowsBadRequestCause` | Start schlägt fehl wenn `confirmDowntime == false` |
| `StartInstallAsync_WhenUpdateIsNotReady_ThrowsNotReadyCause` | Start schlägt fehl wenn Status nicht `Ready` |
| `StartInstallAsync_WhenReady_DelegatesToExecutorAndReturnsInstalling` | Start delegiert an Executor und gibt `Installing` Status zurück |
| `CheckAsync_WhenManifestHasNewerVersion_WritesCheckingThenReady` | Check-Workflow: Checking → Download → Ready Status |
| `CheckAsync_WhenManifestClientFails_WritesFailedStatus` | Fehler wird in Failed-Status geschrieben |

**TestContext (Helper):**
- `TestContext.Create()` — Erstellt Test-Setup mit Mocks (FileStore, Executor, ManifestClient, etc.)
- `TestData.ReadyStatus()` — Erstellt Status mit UpdateStatusKind.Ready
- `TestData.Manifest()` — Erstellt Test-Manifest mit konfigurierbarer Version

---

### `UpdateExecutorTests`
Datei: `FinanceManager.Tests/Updates/UpdateExecutorTests.cs`

Testet Executor-Logik: Lock-Erstellung, Skript-Generierung, Prozess-Start, Fehlerbehandlung.

| Testmethode | Was wird getestet |
|-------------|------------------|
| `StartAsync_WhenGeneratorFails_RemovesLockAndWritesFailedStatus` | Wenn Script-Generator wirft Exception: Lock gelöscht, Failed-Status geschrieben, `IsInstallRunning = false` |
| `StartAsync_WhenRunnerFails_RemovesLockAndWritesFailedStatus` | Wenn Script-Runner wirft Exception: Lock gelöscht, Failed-Status geschrieben, `IsInstallRunning = false` |
| `StartAsync_RevalidatesPendingZipBeforeGeneratingScript` | Heruntergeladenes ZIP wird vor Script-Generierung re-validiert |

**Test-Helfer (Klassen):**
- `TestEnvironment` — Mock IWebHostEnvironment
- `TestResolver` — Mock IUpdateServiceResolver (gibt fest "FinanceManager" Service zurück)
- `ThrowingGenerator` — Mock IUpdateScriptGenerator der Exception wirft ("script failed")
- `ThrowingRunner` — Mock IUpdateProcessRunner der Exception wirft ("runner failed")
- `TestRunner` — Mock IUpdateProcessRunner der erfolgreich ist
- `TestTerminator` — Mock IUpdateHostTerminator
- `TrackingGenerator` — Mock IUpdateScriptGenerator der erfolgreiche generiert und `WasCalled` trackt
- `ReadyStatusAsync(fileStore)` — Helper um UpdateStatusDto mit Ready-Status und gültigem ZIP zu erstellen
- `Settings()` — Helper um UpdateSettingsDto zu erstellen
- `Sha256Async(path)` — Helper um SHA-256 Hash zu berechnen

---

### `UpdateScriptGeneratorTests`
Datei: `FinanceManager.Tests/Updates/UpdateScriptGeneratorTests.cs`

Testet Skript-Generierung für Windows und Linux.

**Bekannte Tests (aus Dateiname):**
- Windows PowerShell Skript-Generierung
- Linux Bash Skript-Generierung
- Pfad-Escaping für Spezialzeichen

---

### `UpdateServiceResolverTests`
Datei: `FinanceManager.Tests/Updates/UpdateServiceResolverTests.cs`

Testet Service-Auflösung und Validierung.

---

### `UpdateSettingsStoreTests`
Datei: `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`

Testet Settings-Persistierung und Normalisierung.

| Testmethode (vermutlich) | Was wird getestet |
|-------------|------------------|
| (Norm- und Grenzwert-Tests) | CheckIntervalMinutes-Clamping, HealthTimeoutSeconds-Clamping |
| (Legacy-Format) | Migrationen von altem Format mit separaten `windowsServiceName`/`linuxServiceName` |
| (Defaults) | Defaults werden aus UpdateOptions angewendet |

---

### `UpdateValidatorTests`
Datei: `FinanceManager.Tests/Updates/UpdateValidatorTests.cs`

Testet Validierung von Manifest und Assets.

| Testmethode (vermutlich) | Was wird getestet |
|-------------|------------------|
| (Versionserkennung) | `IsNewerVersion` Logik für verschiedene Version-Formate |
| (Manifest-Struktur) | Validierung von Version, PublishedAt, ReleaseNotes, Assets |
| (Asset-Validierung) | ZIP-Struktur, Größe, SHA-256 Hash, Plattform-Match |
| (Sichere Entry-Pfade) | ZIP-Entries dürfen nicht mit `.`, `..`, absolute Pfade, Path-Traversal starten |

---

### `UpdateSchedulerTests`
Datei: `FinanceManager.Tests/Updates/UpdateSchedulerTests.cs`

Testet Scheduling-Logik für geplante Installationen.

---

### `UpdateMetadataAndPlatformTests`
Datei: `FinanceManager.Tests/Updates/UpdateMetadataAndPlatformTests.cs`

Testet Metadaten-Auslesen und Plattform-Bestimmung.

---

## UI/ViewModel Tests

### `SetupUpdateViewModelTests`
Datei: `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`

Testet SetupUpdateViewModel-Logik: Load, Save, Check, Install, ResetLock.

**Bekannte Tests (aus Dateiname):**
- Laden von Settings und Status
- Speichern von Einstellungen
- Start der Installation
- Lock-Reset

---

### `SetupUpdateTabTests`
Datei: `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`

Testet SetupUpdateTab.razor Komponente.

---

## Integration Tests

### `UpdateControllerIntegrationTests`
Datei: `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`

Integrationstests für UpdateController API-Endpunkte.

**Bekannte Tests (aus Dateiname):**
- Vollständiger Workflow: Check → Download → Install → Reload
- Lokalisierung: API gibt lokalisierte Error-Codes zurück
- API-Response-Formate und HTTP-Status-Codes

---

## Lokalisierung - Fehlende Einträge

### `Pages.resx`, `Pages.de.resx`, `Pages.en.resx`

**Analyseergebnis:**
- `Msg_Loading` existiert als `SetupProfile_Msg_Loading` aber **NICHT** als `Msg_Loading` (benötigt für Zeile 26 in SetupUpdateTab.razor)
- Folgende `Err_Update_*` Einträge **FEHLEN vollständig**:
  - `Err_Update_Locked` — "An update lock is active." / "Ein Update-Lock ist aktiv."
  - `Err_Update_InstallRunning` — "The current process still owns an update installation." / "Der aktuelle Prozess führt noch eine Update-Installation aus."
  - `Err_Update_NotReady` — "No ready update package is available." / "Kein Update-Paket bereit."
  - `Err_Update_InvalidState` — "Invalid update state." / "Ungültiger Update-Status."
  - `Err_Update_InvalidRequest` — "Invalid update request." / "Ungültige Update-Anfrage."
  - `Err_Update_HealthTimeout` — "Die Anwendung wurde nach dem Update nicht innerhalb des erwarteten Zeitfensters erreichbar." (exists in ViewModel hardcoded)

**Verweis-Lokationen (wo sie benötigt werden):**
- `SetupUpdateTab.razor` Zeile 26: `@Localizer["Msg_Loading"]`
- `UpdateController.cs` Zeilen 66, 70, 74, 78, 99: Error-Codes als Fehlerbehandlung
- `SetupUpdateViewModel.cs` Zeile 144: Hardcoded German text für `Err_Update_HealthTimeout`

---

## Test-Abdeckungs-Bemerkungen

**Vorhanden:**
- Unit-Tests für Orchestrator, Executor, Validator, ScriptGenerator
- Integration-Tests für Controller
- UI-Tests für ViewModel und Razor-Komponente

**Probleme/Lücken (aus Anforderung):**
- Keine expliziten Tests für Lock-Zustands-Desynchronisation (IsInstallRunning wird nach Prozessstart nicht automatisch zurückgesetzt)
- Keine Tests für Fehlerbehandlung nach erfolgreichen Prozessstart
- Keine Tests für Linux-spezifische Lock-Handling (File Creation Time Unreliabilität)
- Keine Tests für Service-Health-Check während Installation
- Keine Tests für Versionserkennung nach Update auf verschiedenen Plattformen
