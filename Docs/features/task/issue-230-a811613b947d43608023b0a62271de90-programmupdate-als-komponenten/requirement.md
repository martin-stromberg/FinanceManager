# Anforderungsanalyse: Programmupdate als Komponentenpaket auslagern

**Aufgaben-ID:** a811613b-947d-4360-8023-b0a62271de90  
**Branch:** task/issue-230-a811613b947d43608023b0a62271de90-programmupdate-als-komponenten  
**Analysedatum:** 2026-07-28

---

## Fachliche Zusammenfassung

Das bestehende Auto-Update-System der FinanceManager-Webanwendung soll in eine eigenständige, wiederverwendbare .NET-Bibliothek refaktoriert werden, mit dem Ziel, diese als NuGet-Paket zu veröffentlichen. Die Bibliothek muss alle Aspekte des automatisierten Updates abstrahieren – Prüfung auf neue Versionen, Downloads, Installation und Skriptausführung – und dabei vollständig vom Hosting-Modell unabhängig und DI-kompatibel sein. Die aktuelle monolithische Implementierung in `FinanceManager.Web.Services.Updates` wird in eine modulare, plug-and-play Lösung umgewandelt, die durch eine einzelne Erweiterungsmethode (`UseAutoUpdate()`) in `Program.cs` aktiviert wird.

---

## Betroffene Klassen und Komponenten

### Neue oder zu überarbeitende Kernklassen

| Klasse | Beschreibung | Scope |
|--------|-------------|-------|
| `AutoUpdateOptions` | Zentrale Konfigurationsklasse mit modernem .NET-Options-Pattern | Neu/Refactor |
| `AutoUpdateBuilder` | Fluent-API für Konfiguration und Service-Registrierung | Neu |
| `IAutoUpdateSource` | Interface für unterschiedliche Update-Quellen (abstrahiert) | Neu/Refactor |
| `AutoUpdateLocalFolderSource` | Implementierung für lokale Verzeichnisse als Update-Quelle | Neu |
| `AutoUpdateGithubSource` | Implementierung für GitHub-Releases als Update-Quelle | Refactor |
| `AutoUpdateOrchestrator` | Zentrale Koordinationsklasse für den Update-Workflow | Refactor |
| `AutoUpdateStatusService` | Singleton-Service für Zustandsverwaltung und Abfragen | Neu/Refactor |
| `AutoUpdateCommandService` | Singleton-Service für manuelle UI-Steuerung (Check/Download/Install) | Neu |
| `AutoUpdateEvents` | Event-Aggregator für Pre-/Post-Events im Update-Workflow | Neu/Refactor |
| `AutoUpdateState` | Enum für konsistente Zustandsrepräsentation | Neu/Refactor |
| `AutoUpdateResult` | DTO für Ergebnisse von Update-Operationen | Neu/Refactor |
| `IAutoUpdateSource.CheckAsync()` | Prüfung auf neue Versionen | Interface |
| `IAutoUpdateSource.DownloadAsync()` | Download einer neuen Version | Interface |
| `SourceCheckTimeRange` | Modell für Zeitfenster-Konfiguration | Neu |

### Hintergrundprozesse und Services

| Komponente | Beschreibung | Typ |
|------------|-------------|-----|
| `UpdateCheckerService` | Periodischer Hintergrunddienst für Source-Prüfung | Hosted Service |
| `UpdateSchedulerService` | Scheduler für zeitgesteuerte Installationen | Hosted Service |

### Schnittstellen (Interfaces)

| Interface | Zweck |
|-----------|-------|
| `IAutoUpdateSource` | Abstraktion für Versions-Check und Download |
| `IAutoUpdateStatusProvider` | Read-only Zugriff auf Update-Status |
| `IAutoUpdateEventAggregator` | Event-Registration und -Auslösung |
| `IAutoUpdateCommandHandler` | Manuelle Steuerbefehle (Check/Download/Install) |

### DTOs und Modelle

| Klasse | Zweck |
|--------|-------|
| `AutoUpdateStatusDto` | Snapshot des aktuellen Zustands |
| `AutoUpdateCheckResultDto` | Ergebnis einer Versions-Prüfung |
| `AutoUpdateDownloadResultDto` | Ergebnis eines Downloads |
| `AutoUpdateInstallResultDto` | Ergebnis einer Installation |
| `AutoUpdateSourceSettings` | Quell-spezifische Einstellungen |
| `CheckSourceTimeRange` | Zeitfenster für Prüfungen |

### Tests

| Testklasse | Fokus |
|-----------|-------|
| `AutoUpdateOptionsTests` | Konfigurationsvalidierung |
| `AutoUpdateOrchestratorTests` | Workflow-Koordination |
| `AutoUpdateStatusServiceTests` | Zustandsverwaltung und Thread-Safety |
| `AutoUpdateCommandServiceTests` | Manuelle Befehle |
| `AutoUpdateEventsTests` | Event-Auslösung und -Behandlung |
| `AutoUpdateGithubSourceTests` | GitHub-Source-Implementierung |
| `AutoUpdateLocalFolderSourceTests` | Lokale-Quelle-Implementierung |

---

## Implementierungsansatz

### Phase 1: Schnittstellen definieren (Contracts)

**Ziel:** Klare Abstraktion etablieren, unabhängig von bestehender Implementierung.

1. **`IAutoUpdateSource`** definieren:
   - `Task<CheckResult> CheckAsync(CancellationToken ct)` – Prüfung auf neue Versionen
   - `Task<DownloadResult> DownloadAsync(Uri uri, string targetPath, CancellationToken ct)` – Download
   - Properties: `CurrentVersion`, `AvailableVersion`

2. **Event-Aggregator** definieren:
   - `event EventHandler<BeforeCheckSourceEventArgs>? BeforeCheckSource`
   - `event EventHandler<BeforeDownloadEventArgs>? BeforeDownload`
   - `event EventHandler<BeforeInstallEventArgs>? BeforeInstall`
   - `event EventHandler<ErrorOccuredEventArgs>? ErrorOccured`
   - `event EventHandler<BeforeStartUpdateScriptEventArgs>? BeforeStartUpdateScript`
   - `event EventHandler? AfterStartUpdateScript`

3. **Status-Service** definieren:
   - Properties für: `CurrentState`, `InstalledVersion`, `LastCheckResult`, `LastDownloadResult`, `LastInstallResult`, `LastError`
   - Thread-safe Read-only Zugriff

4. **Command-Service** definieren:
   - `Task<AutoUpdateResult> CheckAsync(CancellationToken ct)`
   - `Task<AutoUpdateResult> DownloadAsync(CancellationToken ct)`
   - `Task<AutoUpdateResult> InstallAsync(bool confirmDowntime, CancellationToken ct)`

### Phase 2: Konfiguration und DI-Setup

**Ziel:** Moderne .NET-Extension-Methods und Options-Pattern.

1. **`AutoUpdateOptions`** strukturieren:
   ```csharp
   public class AutoUpdateOptions
   {
       public bool EnableAutomaticDownload { get; set; }
       public string DownloadPath { get; set; }
       public bool EnableAutomaticInstallation { get; set; }
       public IAutoUpdateSource? Source { get; set; } // Standard: LocalFolder
       public SourceCheckOptions SourceCheck { get; set; }
       public int MaxAssetBytes { get; set; }
   }
   
   public class SourceCheckOptions
   {
       public int Interval { get; set; } // Minuten
       public List<CheckSourceTimeRange> TimeRanges { get; set; }
   }
   ```

2. **`UseAutoUpdate()`** Extension-Method:
   ```csharp
   public static WebApplicationBuilder UseAutoUpdate(
       this WebApplicationBuilder builder,
       Action<AutoUpdateBuilder>? configureOptions = null)
   ```
   - Registriert alle Services als Singleton (außer Orchestrator: Scoped)
   - Setzt Standard-Implementierungen (`LocalFolderSource`, etc.)
   - Registriert Hosted Services (optional konfigurierbar)

3. **Fluent-Builder** für intuitive Konfiguration:
   ```csharp
   builder.UseAutoUpdate(options =>
   {
       options
           .EnableAutomaticDownload("./updates")
           .EnableAutomaticInstallation()
           .UseGithubSource("repository-name", "owner")
           .WithSourceCheck(interval: 360, timeRanges: [...]);
   });
   ```

### Phase 3: Orchestrator-Refactoring

**Ziel:** Zentrale Update-Logik unabhängig vom Kontext machen.

1. **`AutoUpdateOrchestrator`** aktualisieren:
   - Koordiniert alle Schritte des Update-Workflow
   - Nutzt `IAutoUpdateSource` für Abstraktionen
   - Triggert Events in korrekter Reihenfolge
   - Gibt `AutoUpdateResult` mit vollständigen Status zurück
   - Methods:
     - `Task<AutoUpdateResult> RunUpdateAsync()` – Kompletter Workflow
     - `Task<bool> CheckForUpdateAsync()` – Nur Prüfung
     - `Task<AutoUpdateResult> DownloadAsync()` – Download nach Prüfung
     - `Task<AutoUpdateResult> InstallAsync()` – Installation

2. **Fehlerbehandlung** zentralisieren:
   - Alle Fehler werden in `AutoUpdateResult.Error` gespeichert
   - Zusätzlich: `ErrorOccured`-Event triggern
   - `AutoUpdateStatusService` wird aktualisiert

### Phase 4: Status-Service und Command-Service

**Ziel:** Thread-safe Zustandsverwaltung und manuelle UI-Steuerung.

1. **`AutoUpdateStatusService`** (Singleton):
   - Speichert aktuellen State
   - Verwaltet History (letzter Check, Download, Install, Error)
   - Thread-safe (ReaderWriterLockSlim oder ähnlich)
   - Keine direkte Abhängigkeit vom Orchestrator (EventListener-Pattern)

2. **`AutoUpdateCommandService`** (Singleton):
   - Delegiert an Orchestrator
   - Delegiert Status-Updates an `AutoUpdateStatusService`
   - UI-agnostisch (wirkt mit Controller, Razor Pages, Blazor, API)
   - Methods: `CheckAsync()`, `DownloadAsync()`, `InstallAsync()`

### Phase 5: Event-System erweitern

**Ziel:** Vorhersehbare Event-Auslösung im kompletten Workflow.

Neue Events für Skript-Ausführung:
- `BeforeStartUpdateScript(ref bool cancel)` – Vor Skriptstart, kann abgebrochen werden
- `AfterStartUpdateScript()` – Nach erfolgreichem Start (Anwendung kann sich beenden)

Integrationspunkte:
- Orchestrator triggert beide Events im `InstallAsync()`-Workflow
- Command-Service triggert sie ebenfalls
- Fehler in Event-Handlern werden über `ErrorOccured` gemeldet
- Zustandsübergänge werden in `AutoUpdateStatusService` reflektiert

### Phase 6: Hintergrunddienste anpassen

**Ziel:** Entkopplung von Orchestrator-Logik.

1. **`UpdateCheckerService`** (Hosted Service):
   - Prüft in konfigurierten Intervallen (unter Beachtung von `TimeRanges`)
   - Triggert nur `CheckForUpdateAsync()` des Orchestrators
   - Download/Installation erfolgt ausschließlich via Orchestrator oder CommandService
   - Kann über Config deaktiviert werden

2. **`UpdateSchedulerService`** (Hosted Service):
   - Triggert manuelle Installation zu konfigurierten Zeiten
   - Nutzt `AutoUpdateCommandService` zur Auslösung

### Phase 7: Source-Implementierungen

**Ziel:** Austauschbare Quellen mit klarer Abstraktion.

1. **`AutoUpdateLocalFolderSource`** (Default):
   - Prüft lokales Verzeichnis auf neue Versionen
   - Kopiert Update-Paket beim Download
   - Deterministisch und offline-tauglich

2. **`AutoUpdateGithubSource.Create()`** (Factory-Method):
   - Prüft GitHub-Releases
   - Lädt Assets herunter
   - Validiert Asset-Namen und Checksummen
   - Dokumentiert externe Abhängigkeiten (Netzwerk, GitHub API)

---

## Konfiguration

### Konfigurationsebenen

**Anwendungseinstellungen (appsettings.json):**
```json
{
  "Updates": {
    "Enabled": true,
    "EnableAutomaticDownload": true,
    "DownloadPath": "./updates",
    "EnableAutomaticInstallation": false,
    "SourceCheck": {
      "Interval": 360,
      "TimeRanges": [
        {
          "DayOfWeek": "Monday",
          "StartTime": "08:00:00",
          "EndTime": "18:00:00"
        }
      ]
    },
    "HostedServicesEnabled": true,
    "MaxAssetBytes": 536870912
  }
}
```

**Programmatic (Program.cs):**
```csharp
builder.UseAutoUpdate(config =>
{
    config
        .EnableAutomaticDownload("./updates")
        .UseGithubSource("FinanceManager", "martin-stromberg")
        .WithSourceCheck(interval: 360)
        .EnableAutomaticInstallation(confirmDowntimeRequired: true);
});
```

### Optionen zur Laufzeit ändern

- `AutoUpdateOptions` als Singleton registriert
- Entwickler können über DI zugegriffen und bei Bedarf modifiziert werden
- Änderungen wirken sich auf zukünftige Prüfungen aus
- Vorsicht: Thread-Safety ist zu beachten

---

## Architekturmuster

### Dependency Injection

- Alle Services als Singleton (außer `AutoUpdateOrchestrator`: Scoped)
- Options folgen dem modernen IOptions<T>-Pattern
- Events über Service-Lokator oder Event-Aggregator
- Keine statics; vollständig DI-auflösbar

### Events und Asynchronität

- Asynchrone Task-basierte API
- Events synchron (Delegates), können aber async-Code aufrufen
- CancellationToken überall wo sinnvoll
- Timeout-Handling für lange laufende Operationen

### Thread-Safety

- `AutoUpdateStatusService`: Thread-safe (Lock oder concurrent collections)
- `AutoUpdateCommandService`: Thread-safe (Orchestrator-Aufrufe serialisieren)
- Event-Aggregator: Thread-safe
- Keine Annahmen über Aufrufer-Context (Web, Console, Desktop)

### Zustandsmodell

Definierte Zustände:
- **Idle** – Keine Aktivität
- **Checking** – Quellprüfung läuft
- **UpdateAvailable** – Neue Version erkannt
- **Downloading** – Download läuft
- **ReadyToInstall** – Download abgeschlossen, Installation vorbereitet
- **Installing** – Installation läuft
- **Success** – Installation erfolgreich
- **Failed** – Fehler in irgendeiner Phase
- **Disabled** – System deaktiviert oder blockiert

Zustandsübergänge erfolgen nur über Orchestrator und werden in `AutoUpdateStatusService` gespeichert.

---

## Offene Fragen und Annahmen

### Zu klärende Punkte

1. **Package-Name und Namespace:**
   - Soll die Bibliothek `AutoUpdate` oder `ProgramUpdate` heißen?
   - Welcher Root-Namespace? (`SoftwareSchmiede.AutoUpdate.*` oder ähnlich?)

2. **NuGet-Paketierung:**
   - Ab welchem Reifegradstand soll das Paket veröffentlicht werden?
   - Welche Min./Max.-Versions-Anforderungen für .NET, Dependencies?

3. **Skriptausführung und Plattformen:**
   - Welche Plattformen müssen unterstützt werden? (Windows, Linux, macOS)
   - Skriptformate: `.bat`, `.sh`, `.ps1`?
   - Wer generiert die Skripte? (bereits vorhanden: `UpdateScriptGenerator`)

4. **Versionsvergleich:**
   - Semantische Versionierung (`SemVer`)? Oder custom-Logic?
   - Wie werden Versionen aus Quellen extrahiert?

5. **Event-Fehlerbehandlung:**
   - Sollen fehlerhafte Event-Handler die Prüfung/Installation abbrechen?
   - Oder nur geloggt und gemeldet?

6. **Backward Compatibility:**
   - Muss die neue Bibliothek rückwärts-kompatibel mit dem alten System sein?
   - Migration-Path für bestehende Installationen?

7. **Externe Abhängigkeiten:**
   - Welche NuGet-Packages sind akzeptabel? (z.B. für GitHub-Kommunikation)
   - Ist Newtsonsoft.Json erlaubt, oder System.Text.Json preferred?

8. **Testbarkeit:**
   - Sollen Mock-Implementierungen von `IAutoUpdateSource` bereitgestellt werden?
   - Test-Container oder In-Memory-Source?

9. **Logging:**
   - Welche Log-Levels für verschiedene Ereignisse (Debug, Info, Warning, Error)?
   - Struktur des Loggings (ggf. structured logging mit Serilog)?

10. **Konfigurationsvalidierung:**
    - Ist `IValidateOptions<AutoUpdateOptions>` erforderlich?
    - Welche Validierungsregeln? (z.B. DownloadPath muss existieren?)

---

## Abhängigkeiten und Integrationspunkte

### Bestehende Komponenten (zu refaktorieren)

- `FinanceManager.Web.Services.Updates.*` – Kompletter Umzug in neue Bibliothek
- `FinanceManager.Shared.Dtos.Update.*` – DTOs müssen in neue Struktur angepasst werden
- `ProgramExtensions.cs` – Service-Registrierung wird durch `UseAutoUpdate()` ersetzt

### Externe Dependencies (zu evaluieren)

- **GitHub-API:** Für `AutoUpdateGithubSource`
- **HTTP-Client:** Für Manifest- und Asset-Downloads (bereits: HttpClient)
- **Logging:** `Microsoft.Extensions.Logging`
- **Options:** `Microsoft.Extensions.Options`
- **DI:** `Microsoft.Extensions.DependencyInjection`

### Integration in Testprojekte

- Unit-Tests für Options, Orchestrator, Status-Service
- Integration-Tests mit echtem Dateisystem
- E2E-Tests für GitHub-Source

---

## Erfolgskriterien

- [ ] `UseAutoUpdate()` ist die einzige Registrierungs-Point
- [ ] Alle Services sind DI-kompatibel und als Singleton/Scoped registriert
- [ ] Events können abgebrochen werden (BeforeCheckSource, BeforeDownload, BeforeInstall, BeforeStartUpdateScript)
- [ ] Status ist jederzeit über `AutoUpdateStatusService` abfragbar
- [ ] Manuelle Kontrolle über `AutoUpdateCommandService` möglich (Check, Download, Install)
- [ ] Hintergrunddienste respektieren Konfiguration (Intervalle, Zeitfenster)
- [ ] Source-Implementierungen sind austauschbar
- [ ] Zustandsmodell ist konsistent und vollständig
- [ ] Thread-Safety ist gewährleistet
- [ ] Fehler werden zentral über Events gemeldet
- [ ] NuGet-Paketierungsstruktur ist vorbereitet
