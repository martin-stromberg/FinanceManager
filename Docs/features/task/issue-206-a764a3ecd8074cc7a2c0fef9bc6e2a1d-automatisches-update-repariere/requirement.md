# Translated Requirement: Automatisches Update reparieren

**Task ID:** a764a3ec-d807-4cc7-a2c0-fef9bc6e2a1d  
**Branch:** task/issue-206-a764a3ecd8074cc7a2c0fef9bc6e2a1d-automatisches-update-repariere  
**Created:** 2026-07-20

---

## Fachliche Zusammenfassung

Das automatische Update-System (`IUpdateOrchestrator`, `UpdateExecutor`) weist kritische Fehler bei der Ausführung auf Linux-Produktionsumgebungen auf. Die primären Probleme sind: (1) persistente oder irrtürmliche Update-Lock-Zustände, die trotz Reset-Aktion erneut auftreten; (2) Dienst-Neustart ohne erfolgreiche Versionsaktualisierung; (3) fehlende oder inkonsistente Lokalisierung kritischer Fehlermeldungen und UI-Elemente in allen unterstützten Sprachen; (4) mangelnde Sichtbarkeit des Update-Fortschritts während der Installation. Die Implementierung muss das Lock-Handling stabilisieren, den Übergang zwischen Installer-Ausführung und Dienst-Neustart robust machen, die Lokalisierungs-Abdeckung vervollständigen und einen Wartestatus anzeigen.

---

## Betroffene Klassen und Komponenten

### Service-Klassen (Geschäftslogik)
- `IUpdateOrchestrator`, `UpdateOrchestrator` — Zentrale Orchestrierung: Lock-State-Tracking, Manifest-Checks, Asset-Download, Installation starten
- `IUpdateExecutor`, `UpdateExecutor` — Installer-Prozess: Lock-Erstellung, Skript-Generierung, Prozess-Start, Host-Termination
- `IUpdateFileStore`, `UpdateFileStore` — Persistierung: Lock-Dateien, Status-JSON
- `IUpdateSettingsStore`, `UpdateSettingsStore` — Konfiguration: Persistierte Update-Einstellungen
- `IUpdateServiceResolver` — Service/Executable-Auflösung (Windows vs. Linux)
- `IUpdateScriptGenerator` — Shell-Skript-Generierung (.ps1, .sh)
- `IUpdateProcessRunner`, `DefaultUpdateProcessRunner` — Prozess-Start (PowerShell oder Bash)
- `IUpdateHostTerminator`, `DefaultUpdateHostTerminator` — Host-Termination

### DTOs und Enums
- `UpdateStatusDto` — Status mit `IsLocked`, `LockCreatedAt`, `Status` (enum `UpdateStatusKind`)
- `UpdateStatusKind` (enum) — `NoUpdate`, `Checking`, `Available`, `Downloading`, `Ready`, `Installing`, `Failed`
- `UpdateLockResetRequest`, `UpdateStartRequest`, `UpdateCheckResultDto`

### UI-Komponenten (Blazor)
- `SetupUpdateTab.razor` — Update-Management-UI im Admin-Setup
- `SetupUpdateViewModel` — ViewState für Tab (Status, Settings, Fehlerbehandlung, Polling)

### Lokalisierung (Resource-Dateien)
- `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` — Zentrale UI-Strings
  - Fehlende/unvollständige Einträge: `Msg_Loading`, `Err_Update_Locked` (deutsche Übersetzung)

### API-Endpunkte
- `UpdateController` — REST-API für Status, Settings, Check, Install-Start, Lock-Reset
  - Fehlerbehandlung: `Err_Update_Locked` (409 Conflict), `Err_Update_NotReady` (404), `Err_Update_InvalidState` (400)

### Tests
- `UpdateOrchestratorTests` — Unit-Tests für Orchestrator-Logik
- `UpdateSettingsStoreTests` — Unit-Tests für Settings-Persistierung
- `UpdateControllerIntegrationTests` — Integration-Tests für API

---

## Implementierungsansatz

### 1. Lock-State Stabilisierung

**Problem:** Lock wird nicht zuverlässig freigegeben; `IsInstallRunning`-Flag und Dateisystem-Lock können desynchronisieren.

**Lösung:**
- Überprüfe in `UpdateOrchestrator.StartInstallAsync()` nicht nur `status.IsLocked`, sondern auch `_executor.IsInstallRunning`.
- Erweitere Lock-Validierung: Nach Prozessstart muss der Lock bei Fehler in einer `finally`-Klausel oder Task-Continuation freigegeben werden.
- Ändere `UpdateExecutor.StartAsync()`: Aktualisiere Lock-Metadata (Prozess-ID, Zeitstempel) atomarer. Falls Prozessstart fehlschlägt, lösche Lock sofort.
- Implementiere Garbage-Collection für verwaiste Locks: Rückgriff auf `MinimumStaleLockAge` (aktuell 1 Minute) ist zu schnell für stabile Erkennung. Erhöhe Schwellenwert oder nutze Prozess-Health-Checks.

**Betroffene Events/Hooks:**
- `IUpdateExecutor.StartAsync()` — Pre-Condition: Lock-Prüfung; Post-Condition: Lock-Erstellung garantiert
- `UpdateOrchestrator.ResetLockAsync()` — nur wenn kein Prozess mehr läuft

### 2. Dienst-Neustart und Versionsaktualisierung

**Problem:** Dienst wird neugestartet, aber alte Version bleibt aktiv; Versionserkennung schlägt fehl.

**Lösung:**
- In `UpdateScriptGenerator`: Stelle sicher, dass das generierte Shell-Skript nach dem Update den Dienst neu startet.
- In `UpdateExecutor.StartAsync()`: Nach Skriptstart müssen Polling-Logik und Health-Check robuster implementiert sein.
- Überprüfe nach Dienst-Recovery: `IInstalledReleaseMetadataProvider.GetAsync()` muss die neue Version korrekt auslesen (z. B. aus `CLAUDE.md`, Versionsdatei oder `AssemblyVersion`).
- Implementiere Validierung: Nach Health-Check OK muss `UpdateOrchestrator.GetStatusAsync()` bestätigen, dass `InstalledVersion` dem erwartetem Ziel entspricht.

**Betroffene Interfaces:**
- `IUpdateScriptGenerator` — Skript muss Neustart durchführen
- `IInstalledReleaseMetadataProvider` — Muss zuverlässig neue Version auslesen
- `UpdateExecutor` — Fehlerbehandlung nach Prozess-Termination

### 3. Lokalisierung

**Problem:** Fehlermeldungen ("An update lock is active.") sind nur auf Englisch; `Msg_Loading` hat keine Übersetzungen.

**Lösung:**
- Füge zu `Pages.resx`, `Pages.de.resx`, `Pages.en.resx` folgende Einträge hinzu:
  - `Msg_Loading` — "Wird geladen..." (de), "Loading..." (en) [nur Wert; es ist noch nicht definiert]
  - `Err_Update_Locked` — "Ein Update-Lock ist aktiv." (de), "An update lock is active." (en)
  - `Err_Update_InstallRunning` — "Der aktuelle Prozess führt noch eine Update-Installation aus." (de), "The current process still owns an update installation." (en)
  - `Err_Update_NotReady` — "Kein Update-Paket bereit." (de), "No ready update package is available." (en)
  - `Err_Update_InvalidState` — "Ungültiger Update-Status." (de), "Invalid update state." (en)
  - `Err_Update_InvalidRequest` — "Ungültige Update-Anfrage." (de), "Invalid update request." (en)
- In `SetupUpdateTab.razor`: Ändere hartcodierte Strings zu Localizer-Aufrufen (z. B. Zeile 30: `@Localizer["Msg_InstallingUpdate"]`).

**Betroffene Dateien:**
- `Pages.resx`, `Pages.de.resx`, `Pages.en.resx`
- `SetupUpdateTab.razor` — UI-Text ersetzen
- `UpdateController.cs` — Error-Message-Codes sind bereits lokalisierungsfähig; überprüfe Abfrage

### 4. Update-Status-Anzeige während Installation

**Problem:** Während Installation gibt es keine Fortschrittsanzeige; Benutzer sieht nur "Update wird installiert...".

**Lösung:**
- Erweitere `UpdateStatusKind` ggf. um Zwischen-States (z. B. `Restarting`, `ValidatingInstallation`).
- In `SetupUpdateTab.razor` (Zeile 28–30): Zeige aktualisierte Status-Meldung basierend auf `UpdateStatusKind` an.
- Implementiere Polling in `SetupUpdateViewModel`: Aktualisation `Status` alle 2–5 Sekunden während Installation.
- Optional: WebSocket oder SignalR für Live-Status-Updates.

**Betroffene Komponenten:**
- `UpdateStatusKind` — ggf. neue Enum-Werte
- `SetupUpdateTab.razor` — Status-Rendering verbessern
- `SetupUpdateViewModel` — Polling-Interval anpassen

---

## Konfiguration

Update-Verhalten ist über `UpdateSettingsDto` und Konfigurationsdatei konfigurierbar:
- **Aktivierung:** `Enabled` (bool)
- **Check-Intervall:** `CheckIntervalMinutes` (int)
- **Geplante Installation:** `ScheduledInstallTime` (TimeOnly?)
- **Service/Executable:** `ServiceName`, `ExecutablePath` (Pfade)
- **Repository:** `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`
- **Health-Timeout:** `HealthTimeoutSeconds` (int, 10–600 Sekunden)

Diese Werte werden von `IUpdateSettingsStore` (typischerweise JSON-Datei) geladen. **Keine neuen Konfigurationsparameter erforderlich**, aber `HealthTimeoutSeconds` sollte dokumentiert und Standard-Schwellenwert für Lock-Staleness überprüft werden.

---

## Offene Fragen / Annahmen

1. **Lock-Behandlung auf Linux:**
   - Wird das Lock über Dateisystem (Datei-Locks) oder prozessinternes Flag realisiert?
   - Kann auf Linux der Prozess-ID des Installers nach Daemonisierung noch abgefragt werden, um verwaiste Locks zu erkennen?

2. **Installer-Skript-Ausführung:**
   - Wird das generierte Shell-Skript (`.sh` auf Linux) als Daemon oder im Vordergrund gestartet?
   - Muss das Skript selbst den Lock freigeben, oder der Host nach Prozessende?

3. **Health-Check Robustheit:**
   - Beobachtet die Health-Poll-Logik in `SetupUpdateTab.razor` (Zeile 173–208) einen Neustart des Dienstes auf Linux zuverlässig?
   - Sollte `HealthTimeoutSeconds` Standardwert erhöht werden?

4. **Versionserkennung nach Update:**
   - Wie wird `InstalledVersion` ermittelt? Aus Dateiversion, Git-Commit, oder CLAUDE.md-Marker?
   - Muss diese Logik auf Linux getestet/angepasst werden?

5. **Rollout-Szenario:**
   - Ist die Lock-Reset-Aktion nur für Admin-Benutzer verfügbar (aktuell: Role "Admin")?
   - Soll es einen automatischen Rollback bei fehlgeschlagenem Update geben?

---

## Test-Plan

- **Unit-Tests:** Erweitere `UpdateOrchestratorTests`, `UpdateExecutorTests` um Szenarien:
  - Lock wird nach erfolgreichem Prozessstart gesetzt.
  - Lock wird nach Prozessabsturz freigegeben.
  - Reset schlägt fehl, wenn kein Lock existiert.
  - Stale-Lock-Erkennung funktioniert korrekt.
- **Integration-Tests:** `UpdateControllerIntegrationTests`
  - Vollständiger Workflow: Check → Download → Install → Reload.
  - Lokalisierung: API gibt lokalisierte Error-Codes zurück.
- **E2E-Tests:** `SetupUpdateTab.razor`
  - UI rendert korrekt während Installation.
  - Polling aktualisiert Status live.
  - Fehlermeldungen werden korrekt übersetzt angezeigt.

---

## Zusammenfassung der Implementierungsschritte

1. **Lokalisierung:** Fehlermeldungen und `Msg_Loading` zu `.resx`-Dateien hinzufügen.
2. **Lock-Stabilität:** `UpdateOrchestrator` und `UpdateExecutor` zur atomaren Lock-Verwaltung anpassen.
3. **Installer-Robustheit:** `UpdateScriptGenerator` überprüfen; Neustart und Validierung nach Update sicherstellen.
4. **UI-Verbesserung:** `SetupUpdateTab.razor` mit Status-Anzeige und Lokalisierung aktualisieren.
5. **Testing:** Unit-, Integration- und E2E-Tests für alle Szenarien erweitern.
