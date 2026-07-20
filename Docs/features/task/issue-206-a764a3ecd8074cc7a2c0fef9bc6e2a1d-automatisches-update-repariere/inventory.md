# Bestandsaufnahme: Automatisches Update-System reparieren

Diese Bestandsaufnahme erfasst alle bestehenden Implementierungen des Update-Systems, die unter der Anforderung „Automatisches Update-System reparieren" (Task ID: a764a3ec-d807-4cc7-a2c0-fef9bc6e2a1d) analysiert wurden.

---

## Zusammenfassung

Das Update-System existiert als vollständig implementiertes Subsystem mit Orchestrator, Executor, Persistierung, Validierung, Script-Generierung und UI-Komponenten. Die Implementierung deckt Windows (PowerShell, Service/EXE) und Linux (Bash, systemd) ab.

**Hauptkomponenten vorhanden:**
- ✅ Service-Logik (`IUpdateOrchestrator`, `UpdateExecutor`, etc.)
- ✅ Persistierung (`UpdateFileStore`, `UpdateSettingsStore`)
- ✅ Validierung (`UpdateValidator`)
- ✅ Script-Generierung (`UpdateScriptGenerator`) für Windows und Linux
- ✅ Service-Auflösung (`UpdateServiceResolver`) mit Auto-Detektion
- ✅ API-Endpunkte (`UpdateController`)
- ✅ UI-Komponenten (`SetupUpdateTab.razor`, `SetupUpdateViewModel`)
- ✅ Background-Service für regelmäßige Prüfungen (`UpdateChecker`)
- ✅ Unit- und Integration-Tests

**Kritische Lücken/Probleme (aus Anforderung):**
- ❌ **Lokalisierung:** Fehlende Error-Message-Ressourcen (Err_Update_* und Msg_Loading)
- ❌ **Lock-Handling:** `IsInstallRunning` wird nach Prozessstart nicht automatisch zurückgesetzt; Lock-Desynchronisation möglich
- ❌ **Lock auf Linux:** `GetLockCreatedAtAsync` nutzt `File.GetCreationTimeUtc()` — unreliabel auf Linux
- ❌ **Fehlerbehandlung:** Keine Finally-Klausel zur Lock-Freigabe bei Fehler nach Prozessstart
- ❌ **UI-Status:** Keine Zwischen-Status während Installation (nur „Installing"); kein Live-Fortschritt
- ❌ **Post-Update-Validierung:** Keine Überprüfung, dass neue Version tatsächlich lädt

---

## Details

- [Datenmodelle](inventory/models.md) — DTOs, Records und Konfiguration
- [Service-Logik](inventory/logic.md) — Orchestrator, Executor und weitere Service-Klassen
- [Enums](inventory/enums.md) — UpdateStatusKind und Transitions
- [Interfaces](inventory/interfaces.md) — Contracts für alle Service-Klassen
- [Tests](inventory/tests.md) — Übersicht bestehender Unit-, Integration- und UI-Tests

---

## Kritische Befunde

### 1. Lock-State Desynchronisation

**Problem:**
- `UpdateExecutor.StartAsync()` setzt `IsInstallRunning = true` nach Prozessstart
- Das Flag wird **niemals automatisch zurückgesetzt**
- `ResetLockAsync()` prüft `_executor.IsInstallRunning` und weigert sich zu resetten, solange Flag true ist
- Falls Prozess crasht/hängt, kann Lock nicht manuell zurückgesetzt werden

**Auswirkung:**
- Update-Lock-Zustände können sich mit Dateisystem-Lock desynchronisieren
- Manuelle Lock-Reset-Aktion schlägt fehl, obwohl Installation nicht mehr läuft

**Code-Orte:**
- `UpdateExecutor.cs` Zeile 61, 83: `IsInstallRunning = true/false`
- `UpdateOrchestrator.cs` Zeile 121: Prüfung `if (_executor.IsInstallRunning)`

---

### 2. Fehlende Fehlerbehandlung nach Prozessstart

**Problem:**
- In `UpdateExecutor.StartAsync()` (Zeile 38-97):
  - Lock wird erstellt (Zeile 52)
  - Script generiert und gestartet (Zeile 74)
  - Host wird terminiert (Zeile 76)
- Es gibt eine Try-Catch (Zeile 79-96), aber diese **nur vor Prozessstart** aktiv
- Nach `_processRunner.StartScript()` gibt es **keine Finally-Klausel**
- Falls Exception nach `StartScript()` aber vor/während `_hostTerminator.StopApplication()` wirft, wird Lock nicht freigegeben

**Auswirkung:**
- Verwaiste Locks bei unerwarteten Fehlern nach Prozessstart

---

### 3. Lokalisierung - Kritische Lücken

**Problem:**
- `SetupUpdateTab.razor` Zeile 26 nutzt `@Localizer["Msg_Loading"]` — **dieser Eintrag existiert nicht**
- `UpdateController.cs` Zeile 66, 70, 74, 78, 99 nutzt Error-Codes, die **nicht in .resx vorhanden sind**:
  - `Err_Update_NotReady`
  - `Err_Update_Locked`
  - `Err_Update_InvalidState`
  - `Err_Update_InvalidRequest`
  - `Err_Update_InstallRunning`
- `SetupUpdateViewModel.cs` Zeile 144 nutzt **hardcoded Deutsch** für Health-Timeout

**Betroffene Dateien:**
- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`

**Auswirkung:**
- Englische Fehlermeldungen können nicht in UI angezeigt werden (Exception beim Localizer-Zugriff)
- Deutsche und englische Übersetzungen sind inkonsistent

---

### 4. Lock-Datei auf Linux unreliabel

**Problem:**
- `UpdateFileStore.GetLockCreatedAtAsync()` (Zeile 68-76) nutzt `File.GetCreationTimeUtc(lockFile)`
- Auf Linux-Dateisystemen ist `CreationTime` oft nicht vorhanden oder unreliabel
- Die Staleness-Berechnung in `ResetLockAsync()` basiert darauf (Zeile 132-136)

**Auswirkung:**
- Auf Linux kann nicht zuverlässig erkannt werden, wie alt ein Lock ist
- Manuelles Lock-Reset kann auf Linux fehlschlagen

---

### 5. Keine Zwischen-Status während Installation

**Problem:**
- `UpdateStatusKind` hat 7 Werte, aber während Installation wird nur `Installing` angezeigt
- Keine separaten Status für z.B. `Restarting`, `ValidatingInstallation`, etc.
- UI hat keine Möglichkeit zu erkennen, in welcher Phase der Installation sich die App befindet

**Auswirkung:**
- Benutzer sieht lange Zeit nur „Update wird installiert..." ohne weitere Fortschrittsanzeige

---

### 6. Keine Post-Update-Validierung

**Problem:**
- `UpdateScriptGenerator.GenerateLinuxAsync()` (Zeile 69-105) generiert Bash-Skript mit:
  - `systemctl stop`, `unzip`, `cp`, `rm -f lock`, `systemctl start`
- Nach Service-Start gibt es **keine Validierung**, dass neue Version tatsächlich lädt
- Die Versionserkennung nach Update wird von UI durchgeführt (Health-Check), aber nicht vom Installer selbst

**Auswirkung:**
- Service könnte nach Update nicht korrekt starten, aber Status wird nicht bemerkt

---

## Datei-Pfade (Vollständig)

### Service-Logik
- `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs`
- `FinanceManager.Web/Services/Updates/UpdateExecutor.cs`
- `FinanceManager.Web/Services/Updates/UpdateFileStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateScriptGenerator.cs`
- `FinanceManager.Web/Services/Updates/UpdateServiceResolver.cs`
- `FinanceManager.Web/Services/Updates/UpdateValidator.cs`
- `FinanceManager.Web/Services/Updates/UpdateChecker.cs`
- `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`
- `FinanceManager.Web/Services/Updates/UpdatePlatformResolver.cs`
- `FinanceManager.Web/Services/Updates/UpdateManifestClient.cs`
- `FinanceManager.Web/Services/Updates/UpdateScheduler.cs`
- `FinanceManager.Web/Services/Updates/JsonFileStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateOptions.cs`

### Contracts
- `FinanceManager.Web/Services/Updates/UpdateContracts.cs`

### DTOs
- `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`

### API & Controller
- `FinanceManager.Web/Controllers/UpdateController.cs`

### UI
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- `FinanceManager.Web/ViewModels/Setup/SetupUpdateViewModel.cs`

### Lokalisierung
- `FinanceManager.Web/Resources/Pages.resx`
- `FinanceManager.Web/Resources/Pages.de.resx`
- `FinanceManager.Web/Resources/Pages.en.resx`

### Tests
- `FinanceManager.Tests/Updates/UpdateOrchestratorTests.cs`
- `FinanceManager.Tests/Updates/UpdateExecutorTests.cs`
- `FinanceManager.Tests/Updates/UpdateScriptGeneratorTests.cs`
- `FinanceManager.Tests/Updates/UpdateServiceResolverTests.cs`
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
- `FinanceManager.Tests/Updates/UpdateValidatorTests.cs`
- `FinanceManager.Tests/Updates/UpdateSchedulerTests.cs`
- `FinanceManager.Tests/Updates/UpdateMetadataAndPlatformTests.cs`
- `FinanceManager.Tests/Web/SetupUpdateViewModelTests.cs`
- `FinanceManager.Tests/Components/SetupUpdateTabTests.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`

---

## Plattform-Unterstützung

### Windows
- **Service:** Windows Service (sc.exe Auto-Detektion)
- **Executable:** Optional direkter EXE-Start
- **Script:** PowerShell (.ps1)
- **Auto-Detektion:** `sc.exe queryex type= service state= all` zum Finden der aktuellen Service

### Linux
- **Service:** systemd Service (regex-Match aus cgroup oder systemctl status)
- **Executable:** Nicht unterstützt (nur Service-Name)
- **Script:** Bash (.sh) mit Unix-Dateirechten (0755)
- **Auto-Detektion:** `/proc/self/cgroup` oder `systemctl status` zum Finden der aktuellen Service

---

## Verzeichnis-Struktur

Alle Update-Dateien werden unter `{WorkingDirectory}` (Standard: `updates`) organisiert:

```
updates/
  ├─ settings.json            # Persistierte Einstellungen
  ├─ status.json              # Aktueller Status
  ├─ update.lock              # Lock-Datei (existiert während Installation)
  ├─ pending/
  │  ├─ release.zip           # Heruntergeladenes Asset
  │  ├─ update.ps1            # Windows Script
  │  └─ update.sh             # Linux Script
  └─ staging/
     └─ [extracted contents]  # Entpackte Dateien vor Installation
```

---

## Abhängigkeiten (Packages)

Aus Service-Klassen erkannt:
- `System.IO.Compression` — ZIP-Handling
- `System.Security.Cryptography` — SHA-256 Validierung
- `System.Diagnostics` — Process-Start
- `System.Text.Json` — Settings/Status Serialisierung
- `System.Runtime.InteropServices` — Platform-Detection
- `System.Text.RegularExpressions` — systemd-Service-Regex

---

## Konfiguration (appsettings.json)

```json
{
  "Updates": {
    "Enabled": true/false,
    "CheckIntervalMinutes": 360,
    "RepositoryOwner": "martin-stromberg",
    "RepositoryName": "FinanceManager",
    "ManifestAssetName": "update.json",
    "WorkingDirectory": "updates",
    "HealthTimeoutSeconds": 120,
    "MaxAssetBytes": 536870912,
    "HostedServicesEnabled": true,
    "ServiceName": null,
    "ExecutablePath": null
  }
}
```
