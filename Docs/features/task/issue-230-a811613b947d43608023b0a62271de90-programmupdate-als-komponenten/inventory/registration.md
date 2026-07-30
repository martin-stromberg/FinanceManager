# Service-Registrierung und Konfiguration

## Aktuelle Service-Registrierung

### Registrierungs-Einstiegspunkt
Datei: `src/FinanceManager.Web/ProgramExtensions.cs`

Alle Update-Services werden über die Methode `AddUpdateServices()` registriert:

```
services.AddUpdateServices(configuration)
```

### Registrierte Services (Singleton)

| Service-Interface | Implementierung | Scope | Beschreibung |
|-------------------|-----------------|-------|-------------|
| IUpdateOrchestrator | UpdateOrchestrator | Scoped | Zentrale Update-Logik |
| IUpdateManifestClient | UpdateManifestClient | Singleton | GitHub API-Client |
| IUpdateFileStore | UpdateFileStore | Singleton | Datei-Verwaltung |
| IUpdateSettingsStore | UpdateSettingsStore | Singleton | Einstellungs-Persistierung |
| IUpdateValidator | UpdateValidator | Singleton | Validierung |
| IUpdateScriptGenerator | UpdateScriptGenerator | Singleton | Skript-Generierung |
| IUpdateExecutor | UpdateExecutor | Singleton | Update-Ausführung |
| IUpdatePlatformResolver | UpdatePlatformResolver | Singleton | Plattform-Erkennung |
| IUpdateProcessRunner | DefaultUpdateProcessRunner | Singleton | Prozessausführung |
| IUpdateHostTerminator | DefaultUpdateHostTerminator | Singleton | Host-Beendigung |
| IUpdateServiceResolver | DefaultUpdateServiceResolver | Singleton | Service-Auflösung |
| IInstalledReleaseMetadataProvider | InstalledReleaseMetadataProvider | Singleton | Metadaten-Verwaltung |
| IUpdateServiceProbe | DefaultUpdateServiceProbe | Singleton | Verfügbarkeitsprüfung |

**Hinweis:** IInstalledReleaseMetadataProvider ist in der aktuellen Implementierung NICHT im DI-Container registriert (BUG).

### Registrierte Hosted Services

| Hosted Service | Typ | Beschreibung |
|---|---|---|
| UpdateChecker | BackgroundService | Periodische Versionsprüfung |
| UpdateScheduler | BackgroundService | Zeitgesteuerte Installation |

Diese sind optional konfigurierbar über `UpdateOptions.HostedServicesEnabled`.

## Konfigurationsmodell

### `UpdateOptions` (Konfigurationsklasse)
Datei: `src/FinanceManager.Web/Options/UpdateOptions.cs`

Eigenschaften:
- `Enabled` (bool) – Update-System aktiviert?
- `ManifestUrl` (string) – GitHub Releases API URL
- `DownloadPath` (string) – Lokales Download-Verzeichnis
- `MaxAssetBytes` (long) – Maximale Asset-Größe
- `CheckIntervalMinutes` (int) – Prüfungs-Intervall (Minuten)
- `InstallationWindowStartHour` (int) – Installation Fenster (Start-Stunde)
- `InstallationWindowEndHour` (int) – Installation Fenster (End-Stunde)
- `HostedServicesEnabled` (bool) – Background-Services aktivieren?

### Konfigurationsquellen

**appsettings.json:**
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

**Program.cs (Registrierung):**
```csharp
builder.Services.AddUpdateServices(configuration);
```

## DTOs und Datentypen

### Verwendete DTOs in Update-System

- `UpdateStatusKind` (Enum) – Zustandsrepräsentation
- `UpdateStatusDto` – Status-Snapshot
- `UpdateCheckResultDto` – Check-Ergebnis
- `UpdateDownloadResultDto` – Download-Ergebnis
- `UpdateInstallResultDto` – Installations-Ergebnis
- `UpdateMetadataDto` – Release-Metadaten
- `UpdateAssetDto` – Asset-Information
- `UpdateSettingsDto` – Einstellungs-DTO
- `UpdateSettingsUpdateRequest` – Einstellungs-Änderungs-Request
- `UpdateStartRequest` – Manueller Start-Request
- `UpdateScheduleRequest` – Scheduling-Request
- `UpdateLockResetRequest` – Lock-Reset-Request
- `InstalledReleaseMetadataDto` – Installierte Version-Metadaten

## API-Controller-Integration

### `UpdateController`
Datei: `src/FinanceManager.Web/Controllers/UpdateController.cs`

REST-API für Update-Management:

| Endpoint | Methode | Beschreibung |
|----------|---------|-------------|
| `/api/updates/status` | GET | Aktuellen Status abrufen |
| `/api/updates/check` | POST | Manuelle Versionsprüfung |
| `/api/updates/download` | POST | Manueller Download |
| `/api/updates/install` | POST | Manuelle Installation |
| `/api/updates/schedule` | POST | Installation planen |
| `/api/updates/settings` | GET | Einstellungen abrufen |
| `/api/updates/settings` | PUT | Einstellungen ändern |

## Abhängigkeitsbaum

```
UpdateOrchestrator (Scoped)
├── IUpdateManifestClient (Singleton)
│   └── HttpClient
├── IUpdateFileStore (Singleton)
├── IUpdateSettingsStore (Singleton)
│   └── JsonFileStore
├── IUpdateExecutor (Singleton)
│   ├── IUpdateScriptGenerator
│   │   └── IUpdatePlatformResolver
│   ├── IUpdateProcessRunner
│   └── IUpdateHostTerminator
├── IUpdateValidator (Singleton)
├── IUpdatePlatformResolver (Singleton)
└── IInstalledReleaseMetadataProvider (Singleton)
    └── IUpdateSettingsStore

UpdateChecker (Hosted Service)
├── IUpdateOrchestrator (Scoped)
├── IUpdateSettingsStore (Singleton)
└── ILogger<UpdateChecker>

UpdateScheduler (Hosted Service)
├── IUpdateOrchestrator (Scoped)
└── ILogger<UpdateScheduler>
```

## Fehlerbehandlung und Logging

### Log-Ebenen (aktuell)
- **Debug:** Detaillierte Operationsverfolgung
- **Information:** Erfolgreiche Operationen, wichtige Meilensteine
- **Warning:** Wiederherstellbare Fehler (z.B. temporäre Netzwerkfehler)
- **Error:** Kritische Fehler (z.B. beschädigte Pakete, Validierungsfehler)

### Fehler-Propagation

Fehler werden in verschiedenen Ebenen behandelt:
1. **UpdateOrchestrator:** Sammelt Fehler und speichert in `UpdateInstallResultDto.Error`
2. **Hosted Services:** Loggen Fehler, stopppen nicht (resilient)
3. **Controller:** Geben HTTP-Status und Fehlermeldung zurück
4. **Client (ApiClient.Update):** Wirft Exceptions bei HTTP-Fehler

## Konfigurationsvalidierung

### Validierte Eigenschaften

- `DownloadPath` – Muss existieren oder erstellt werden
- `ManifestUrl` – Muss valide HTTP(S)-URL sein
- `MaxAssetBytes` – Muss > 0 sein
- `CheckIntervalMinutes` – Muss >= 1 sein
- `InstallationWindowStartHour` – Muss 0–23 sein
- `InstallationWindowEndHour` – Muss 0–23 sein

### Validierungs-Ebenen

Aktuell: **Implizit** (bei Verwendung)
- Keine explizite `IValidateOptions<UpdateOptions>` Implementierung
- Fehler entstehen zur Laufzeit bei ungültigen Konfigurationen

## Konfigurationsebenen und Precedence

1. **appsettings.json** – Basis-Konfiguration
2. **appsettings.{Environment}.json** – Umgebungsspezifische Overrides
3. **Environment-Variablen** – Höchste Priorität (optional)

Keine Programmatic-Overrides nach Startup (aktuell).

## Integration mit Dependency Injection

### IServiceProvider-Auflösung

Die `IUpdateServiceResolver`-Schnittstelle ermöglicht programmatische Auflösung:

```csharp
var orchestrator = serviceResolver.ResolveOrchestrator(serviceProvider);
var validator = serviceResolver.ResolveValidator(serviceProvider);
```

### Scoping und Lifetime-Management

- **Singleton-Services** (UpdateManifestClient, UpdateFileStore, etc.): Thread-safe Implementierung erforderlich
- **Scoped Services** (UpdateOrchestrator): Ein Service pro Request/Scope
- **Transient Services** – Nicht vorhanden (aktuell)

## Hinweise zur aktuellen Implementierung

**Stärken:**
- Zentrale Registrierung in `ProgramExtensions`
- Klare Options-Pattern Implementierung
- Alle Services vollständig DI-auflösbar

**Verbesserungspotenziale:**
1. IInstalledReleaseMetadataProvider wird nicht registriert (BUG zu beheben)
2. Keine explizite `IValidateOptions<UpdateOptions>` implementiert
3. Keine Programmatic-Configuration nach Startup möglich
4. Keine Event-Aggregator Registrierung (fehlende Abstraktion)
5. Keine `IAutoUpdateSource`-Abstraktionen für alternative Update-Quellen

**Für NuGet-Bibliothek erforderlich:**
1. Extension-Methode `UseAutoUpdate()` auf `IServiceCollection` oder `WebApplicationBuilder`
2. Moderne `AutoUpdateBuilder` Fluent-API für Konfiguration
3. Abstraktion auf Update-Quellen (IAutoUpdateSource)
4. Separate Event-Aggregator-Infrastruktur
