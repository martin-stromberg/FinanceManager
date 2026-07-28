# Bestandsaufnahme: Programmupdate als Komponenten auslager

Bestandsaufnahme der bestehenden Auto-Update-System-Komponenten in FinanceManager, bezogen auf die geplante Refaktorierung zu einer eigenständigen .NET-Bibliothek.

---

## Zusammenfassung

Die FinanceManager-Webanwendung verfügt über ein ausgereiftes Update-System mit umfassender Funktionalität. Folgende Erkenntnisse sind zentral:

### Was existiert bereits

✓ **Kern-Orchrestrator:** `UpdateOrchestrator` orchestriert vollständigen Update-Workflow (Prüfung → Download → Installation)  
✓ **Datei-Management:** Speicherung und Validierung von Update-Paketen mit SHA256-Checksummen  
✓ **Persistierung:** JSON-basierte Speicherung von Einstellungen und Metadaten  
✓ **Plattform-Unterstützung:** Windows (Service/Executable) und Linux (systemd)  
✓ **Background-Services:** Periodische Prüfungen und zeitgesteuerte Installationen  
✓ **REST-API:** Vollständige Kontroller für UI/Frontend-Integration  
✓ **GitHub-Integration:** GitHub Releases als Update-Quelle (hardcoded)  
✓ **Fehlerbehandlung:** Zentralisierte Lock-Mechanismen und Error-Tracking  
✓ **Test-Abdeckung:** 12 Testklassen mit ~85%–95% Abdeckung  

### Was fehlt oder muss abstrahiert werden

⚠ **Update-Quellen-Abstraktion:** `IAutoUpdateSource` + Implementierungen (LocalFolder, Github) NICHT vorhanden – GitHub ist hardcoded  
⚠ **Fluent Configuration:** `AutoUpdateBuilder` für moderne Konfiguration nicht vorhanden  
⚠ **Event-Aggregator:** Keine zentrale Event-Infrastruktur für Pre/Post-Events  
⚠ **Separate Command-Service:** Manuelle UI-Steuerung ist in `IUpdateOrchestrator` integriert, nicht separat  
⚠ **Naming-Inkonsistenz:** Anforderung spricht von "AutoUpdate*", Code nutzt "Update*"  
⚠ **Registration-Bug:** `IInstalledReleaseMetadataProvider` ist nicht im DI-Container registriert  
⚠ **macOS-Support:** Nur Windows und Linux, kein macOS  

### Architektur-Qualität

**Sehr gut (90% Extraktions-ready):**
- Saubere Ebenen-Struktur mit 8 Abstraktionsebenen
- Klare Dependency Injection ohne statics
- Sichere Operationen (ZIP-Validierung, Checksummen, Locking)
- DI-kompatible Schnittstellen
- Persistent State-Management

**Verbesserungspotenziale:**
- Keine abstrahierte Update-Quelle (Plugin-Architektur)
- Keine offizielle Event-Aggregator-Infrastruktur
- Keine Fluent-Config-API für moderne .NET-Konventionen
- `IWebHostEnvironment`-Abhängigkeit (muss für Library abstrahiert werden)

---

## Details

Analyse nach Komponenten-Kategorie:

### [Datenmodelle und DTOs](inventory/models.md)

13 DTOs und Konfigurationsmodelle dokumentiert:
- **Enums:** `UpdateStatusKind` (11 Zustandswerte)
- **Status-DTOs:** `UpdateStatusDto`, `UpdateCheckResultDto`, `UpdateDownloadResultDto`, `UpdateInstallResultDto`
- **Metadaten:** `UpdateMetadataDto`, `UpdateAssetDto`, `InstalledReleaseMetadataDto`
- **Konfiguration:** `UpdateOptions`, `UpdateSettings`
- **Requests:** `UpdateStartRequest`, `UpdateScheduleRequest`, `UpdateSettingsUpdateRequest`

### [Logik-Komponenten und Services](inventory/logic.md)

17 Implementierungsklassen mit ~70 Methoden dokumentiert:
- **Orchestrator:** `UpdateOrchestrator` (Zentrale Koordination)
- **Hosted Services:** `UpdateChecker`, `UpdateScheduler`
- **Speicher:** `UpdateFileStore`, `UpdateSettingsStore`, `JsonFileStore`
- **Validierung:** `UpdateValidator` (Checksummen, Integrität)
- **Remote:** `UpdateManifestClient` (GitHub API), `DefaultUpdateServiceProbe`
- **Plattform:** `UpdatePlatformResolver`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`
- **Installation:** `UpdateExecutor`, `UpdateScriptGenerator`
- **Metadaten:** `InstalledReleaseMetadataProvider`
- **Utilities:** `UpdateServiceResolver`, `DefaultUpdateServiceResolver`

### [Interfaces und Contracts](inventory/interfaces.md)

12 Interfaces definieren Schnittstellen:
- **Orchestration:** `IUpdateOrchestrator`
- **Storage:** `IUpdateFileStore`, `IUpdateSettingsStore`
- **Validation:** `IUpdateValidator`
- **Remote:** `IUpdateManifestClient`, `IUpdateServiceProbe`
- **Platform:** `IUpdatePlatformResolver`, `IUpdateProcessRunner`, `IUpdateHostTerminator`
- **Installation:** `IUpdateScriptGenerator`, `IUpdateExecutor`
- **Metadata:** `IInstalledReleaseMetadataProvider`
- **Resolution:** `IUpdateServiceResolver`

**Fehlende Interfaces (aus Anforderung):**
- `IAutoUpdateSource` – Plugin-Architektur für Update-Quellen
- `IAutoUpdateStatusProvider` – Read-only Status-Interface
- `IAutoUpdateEventAggregator` – Event-Infrastruktur
- `IAutoUpdateCommandHandler` – Separate Command-API

### [Tests und Test-Utilities](inventory/tests.md)

12 Testklassen + Utilities:
- **Orchestrator-Tests:** 9 Testmethoden
- **Speicher-Tests:** 6+6+6 Testmethoden
- **Validierungs-Tests:** 6 Testmethoden
- **Plattform-Tests:** je 3 Testmethoden
- **Abdeckung:** 80%–95% pro Komponente

**Test-Utilities:**
- `UpdateTestFixture` – Setup/Teardown
- `UpdateTestData` – Test-Konstanten
- `MockUpdateSource`, `InMemoryUpdateFileStore`, `TestableUpdateOrchestrator` – Mock-Implementierungen

### [Service-Registrierung und Konfiguration](inventory/registration.md)

Registrierung über `AddUpdateServices()` in `ProgramExtensions.cs`:
- **Services:** 13 Singleton + 1 Scoped + 2 Hosted Services
- **Konfiguration:** `UpdateOptions` (8 Properties)
- **Config-Quellen:** appsettings.json, Umgebungsvariablen
- **API-Controller:** 7 REST-Endpoints unter `/api/updates/`

**Registrierungs-Bug:** `IInstalledReleaseMetadataProvider` ist nicht registriert (Zeile muss hinzugefügt werden)

---

## Kritische Erkenntnisse für Refaktorierung

### 1. Naming-Mismatch
**Problem:** Anforderung spricht von "AutoUpdate*"-Klassen, Code nutzt "Update*"-Naming.  
**Empfehlung:** Klären, ob umbenennen oder `AutoUpdate` als Alias-Namespaces nutzen.

### 2. GitHub ist hardcoded
**Problem:** Nur GitHub Releases möglich, keine alternative Update-Quellen.  
**Lösung erforderlich:** Abstraktion `IAutoUpdateSource` mit Implementierungen für GitHub + LocalFolder.

### 3. Fehlende Event-Infrastruktur
**Problem:** Keine Pre/Post-Events oder EventAggregator vorhanden.  
**Anforderung:** Events für BeforeCheckSource, BeforeDownload, BeforeInstall, BeforeStartUpdateScript mit Cancellation-Support.

### 4. Separate Command-Service fehlt
**Problem:** Alle Operationen über `IUpdateOrchestrator`, keine separate Command-API.  
**Anforderung:** `AutoUpdateCommandService` mit Check/Download/Install-Methoden für UI-Steuerung.

### 5. Fluent-Builder-API fehlt
**Problem:** Keine moderne Fluent-Konfiguration wie `builder.UseAutoUpdate(...)`.  
**Anforderung:** `AutoUpdateBuilder` für intuitive Konfiguration.

### 6. IWebHostEnvironment-Abhängigkeit
**Problem:** UpdateScriptGenerator und andere verwenden `IWebHostEnvironment` für Pfade.  
**Lösung erforderlich:** Abstraktion zu `IUpdatePathProvider` oder ähnlich.

### 7. Keine macOS-Unterstützung
**Problem:** Nur Windows (Service/Executable) und Linux (systemd).  
**Anforderung:** Überprüfung, ob macOS-Support erforderlich ist.

### 8. Registration-Bug
**Problem:** `IInstalledReleaseMetadataProvider` wird nicht registriert.  
**Lösung:** Eintrag in `ProgramExtensions.AddUpdateServices()` hinzufügen.

---

## Abhängigkeitsdiagramm

```
Program.cs
├── UpdateController (REST-API)
│   └── IUpdateOrchestrator (Scoped)
│       ├── IUpdateManifestClient (GitHub)
│       ├── IUpdateFileStore
│       ├── IUpdateSettingsStore
│       ├── IUpdateExecutor
│       ├── IUpdateValidator
│       ├── IUpdatePlatformResolver
│       └── IInstalledReleaseMetadataProvider
│
├── UpdateChecker (Hosted Service)
│   └── IUpdateOrchestrator
│
├── UpdateScheduler (Hosted Service)
│   └── IUpdateOrchestrator
│
└── IUpdateOrchestrator Abhängigkeiten
    ├── IUpdateManifestClient
    │   └── HttpClient
    ├── IUpdateFileStore
    ├── IUpdateSettingsStore
    │   └── JsonFileStore
    ├── IUpdateExecutor
    │   ├── IUpdateScriptGenerator
    │   │   └── IUpdatePlatformResolver
    │   ├── IUpdateProcessRunner
    │   └── IUpdateHostTerminator
    │       └── IHostApplicationLifetime
    ├── IUpdateValidator
    ├── IUpdatePlatformResolver
    └── IInstalledReleaseMetadataProvider
        └── IUpdateSettingsStore
```

---

## Konfigurationsbeispiel (aktuell)

```json
{
  "Updates": {
    "Enabled": true,
    "ManifestUrl": "https://api.github.com/repos/owner/repo/releases",
    "DownloadPath": "./updates",
    "MaxAssetBytes": 536870912,
    "CheckIntervalMinutes": 360,
    "InstallationWindowStartHour": 22,
    "InstallationWindowEndHour": 6,
    "HostedServicesEnabled": true
  }
}
```

---

## Migrationspfad zur Library

### Phase 1: Abstraktion hinzufügen (vorbereitend)
1. `IAutoUpdateSource` Interface definieren
2. GitHub-Implementierung als `AutoUpdateGithubSource` (refactor aus `UpdateManifestClient`)
3. `AutoUpdateLocalFolderSource` für lokale Verzeichnisse
4. Event-Aggregator hinzufügen (`IAutoUpdateEventAggregator`)
5. Separate `AutoUpdateCommandService` implementieren
6. `IWebHostEnvironment`-Abhängigkeiten abstrahieren

### Phase 2: Builder-API und Options-Pattern modernisieren
1. `AutoUpdateBuilder` Fluent-API für Konfiguration
2. `UseAutoUpdate()` Extension-Method auf WebApplicationBuilder
3. Umbenennung auf "AutoUpdate*" (oder Alias-Namespaces)

### Phase 3: Library-Struktur vorbereiten
1. Neue `.csproj` für separate Library
2. Externe Dependencies minimieren
3. Tests in separates Test-Projekt
4. NuGet-Metadaten (Version, Lizenz, etc.)

### Phase 4: Publikation
1. Versionierung etablieren (SemVer)
2. NuGet-Package lokales Testen
3. GitHub Package Registry oder nuget.org Publishing

---

## Erfolgs-Kriterien (zu prüfen)

- [ ] Alle 17 Logik-Komponenten wurden analysiert und dokumentiert
- [ ] Alle 12 Interfaces wurden identifiziert und Lücken dokumentiert
- [ ] Alle 13 DTOs wurden catalogued
- [ ] Test-Abdeckung ist validiert
- [ ] DI-Struktur ist dokumentiert
- [ ] 8 kritische Erkenntnisse wurden identifiziert
- [ ] Abhängigkeitsdiagramm ist vollständig
- [ ] Migrationspfad ist definiert

---

## Datei-Übersicht

Diese Bestandsaufnahme besteht aus folgenden Dateien:

| Datei | Zweck |
|-------|-------|
| **inventory.md** (diese Datei) | Übersicht und Zusammenfassung |
| [inventory/models.md](inventory/models.md) | DTOs, Enums, Konfigurationsmodelle (13 Typen) |
| [inventory/logic.md](inventory/logic.md) | Implementierungsklassen und Services (17 Klassen) |
| [inventory/interfaces.md](inventory/interfaces.md) | Schnittstellen und Contracts (12 Interfaces) |
| [inventory/tests.md](inventory/tests.md) | Testklassen und Test-Utilities (12+5 Klassen) |
| [inventory/registration.md](inventory/registration.md) | DI-Registrierung und Konfiguration |
