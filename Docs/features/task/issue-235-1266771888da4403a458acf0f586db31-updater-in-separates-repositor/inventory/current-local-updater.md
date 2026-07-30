# Aktueller lokaler Updater

## Projektstatus

| Projekt | Pfad | Zweck | Zielzustand laut Anforderung |
|---------|------|-------|------------------------------|
| `SoftwareSchmiede.AutoUpdate` | `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj` | Lokale, DI-kompatible Self-Update-Bibliothek | Entfernen |
| `SoftwareSchmiede.AutoUpdate.Tests` | `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.csproj` | Unit-Tests der lokalen Bibliothek | Entfernen |

Die Anforderung spricht von `FinanceManager.AutoUpdater`. Im Repository existiert kein Projekt mit diesem Namen. Die historisch passende Komponente ist `SoftwareSchmiede.AutoUpdate`, siehe README und CHANGELOG.

## Bibliotheksmetadaten

`SoftwareSchmiede.AutoUpdate.csproj`:

| Feld | Wert |
|------|------|
| TargetFramework | `net10.0` |
| PackageId | `SoftwareSchmiede.AutoUpdate` |
| Version | `0.1.0` |
| License | `MIT` |
| RepositoryUrl | `https://github.com/martin-stromberg/FinanceManager` |
| Documentation | XML-Dokumentation aktiv |

Die lokale Bibliothek hat 64 C#-Dateien. Das Testprojekt hat 22 C#-Dateien plus Testsupport.

## Öffentliche Einstiegspunkte

| Typ | Rolle |
|-----|------|
| `AutoUpdateHostBuilderExtensions.UseAutoUpdate(...)` | Zentrale Registrierung auf `IHostApplicationBuilder` |
| `AutoUpdateBuilder` | Fluent-Konfiguration |
| `AutoUpdateOptions` | Runtime-mutable Singleton-Konfiguration |
| `IAutoUpdateSource` | Abstraktion für Update-Quellen |
| `AutoUpdateGithubSource` | GitHub-Releases-Quelle |
| `AutoUpdateLocalFolderSource` | Lokale Ordnerquelle |
| `IAutoUpdateOrchestrator` / `AutoUpdateOrchestrator` | Workflow-Koordination |
| `IAutoUpdateCommandHandler` / `AutoUpdateCommandService` | Manuelle Check/Download/Install-Kommandos |
| `IAutoUpdateStatusProvider` / `AutoUpdateStatusService` | Status-Snapshot |

## Registrierung der Bibliothek

`UseAutoUpdate` registriert u. a.:

- `AutoUpdateOptions` als Singleton.
- `HttpClient`.
- `IAutoUpdateEnvironment`, Package-/State-Stores und Validatoren.
- `IAutoUpdatePlatformResolver`, Service-Probe, Service-Resolver, Script-Generator, Process-Runner.
- `IAutoUpdateInstaller`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`.
- Hosted Services `AutoUpdateCheckerService` und `AutoUpdateSchedulerService`, sofern `HostedServicesEnabled` aktiv ist.

## Plattform- und Laufzeitannahmen

- Windows: Installation über Windows Service oder ausführbare Datei.
- Linux: Installation über systemd Unit.
- macOS: laut README nicht unterstützt.
- `AutoUpdateOptions.UpdateUnitName` wird von der konsumierenden App auf einen eindeutigen Namen gesetzt, aktuell `FinanceManagerUpdate`.

## Entfernungspunkte

Bei der Umsetzung müssen mindestens folgende Spuren entfernt werden:

- Solution-Einträge in `FinanceManager.sln`.
- Projektverweis von `SoftwareSchmiede.AutoUpdate.Tests` auf `SoftwareSchmiede.AutoUpdate`.
- Projektverweis von `FinanceManager.Web` auf `SoftwareSchmiede.AutoUpdate`.
- Verzeichnisse `SoftwareSchmiede.AutoUpdate/` und `SoftwareSchmiede.AutoUpdate.Tests/`.
- README-/CHANGELOG-Verweise, sofern sie die aktuelle Projektstruktur beschreiben.
