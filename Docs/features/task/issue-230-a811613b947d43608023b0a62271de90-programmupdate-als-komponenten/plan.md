# Umsetzungsplan: Programmupdate als Komponentenpaket auslagern

## Übersicht

Die bisher fest in `FinanceManager.Web` verdrahtete Selbstaktualisierung (`FinanceManager.Web/Services/Updates/*`) wird in ein eigenständiges, hosting-unabhängiges und NuGet-fähiges Bibliotheksprojekt `SoftwareSchmiede.AutoUpdate` überführt. Die Bibliothek erhält eine Fluent-Konfiguration (`UseAutoUpdate()` + `AutoUpdateBuilder`), eine austauschbare Quellen-Abstraktion (`IAutoUpdateSource` mit `AutoUpdateLocalFolderSource` und `AutoUpdateGithubSource`), einen Event-Aggregator mit abbrechbaren Vor-Ereignissen, einen thread-sicheren Status-Service, einen Command-Service für manuelle Steuerung sowie zwei Hintergrunddienste (Prüfung, geplante Installation).

`FinanceManager.Web` konsumiert die Bibliothek künftig ausschließlich über `builder.UseAutoUpdate(...)`. Damit REST-API (`/api/setup/update/*`), `ApiClient`, `SetupUpdateViewModel` und `SetupUpdateTab.razor` unverändert bleiben, wird im Web-Projekt eine Adapterschicht (`UpdateOrchestratorAdapter`) ergänzt, die zwischen den Bibliothekstypen und den bestehenden DTOs in `FinanceManager.Shared/Dtos/Update` vermittelt.

> **Hinweis zur Bestandsaufnahme:** Die Detaildokumente unter `inventory/` beschreiben Dateipfade (`src/FinanceManager.Web/...`), Klassennamen und Methodensignaturen, die so im Repository nicht existieren. Maßgeblich für diesen Plan ist der tatsächliche Code-Stand: flache Ablage unter `FinanceManager.Web/Services/Updates/`, Interfaces gesammelt in `UpdateContracts.cs`, DTOs als `record`-Typen in `FinanceManager.Shared/Dtos/Update/UpdateDtos.cs`, Tests unter `FinanceManager.Tests/Updates/`.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Projektstruktur | Neues Projekt `SoftwareSchmiede.AutoUpdate` (`Microsoft.NET.Sdk`, net10.0), zusätzlich Testprojekt `SoftwareSchmiede.AutoUpdate.Tests`; beide in `FinanceManager.sln` aufgenommen | NuGet-Fähigkeit setzt voraus, dass die Bibliothek ohne Referenz auf FinanceManager-Projekte baut und eigenständig testbar ist |
| Hosting-Abstraktion | Erweiterungsmethode auf `IHostApplicationBuilder` statt auf `WebApplicationBuilder`; keine `FrameworkReference` auf `Microsoft.AspNetCore.App` | `WebApplicationBuilder` implementiert `IHostApplicationBuilder`; damit funktioniert dieselbe API für Web-, Worker- und Konsolen-Hosts (Anforderung „Unabhängigkeit vom Hosting-Modell") |
| Pfad-/Umgebungszugriff | Neues `IAutoUpdateEnvironment` (`ApplicationDirectory`), Standardimplementierung `HostAutoUpdateEnvironment` liest `IHostEnvironment.ContentRootPath` | Löst die in der Bestandsaufnahme genannte `IWebHostEnvironment`-Abhängigkeit von `UpdateFileStore`, `UpdateScriptGenerator`, `InstalledReleaseMetadataProvider` auf, ohne ASP.NET-Referenz |
| `IAutoUpdateSource` | Zustandsloses **Gateway**: `CheckAsync`/`DownloadAsync` liefern Ergebnisobjekte; keine veränderlichen `CurrentVersion`/`AvailableVersion`-Properties | Anforderung fordert Thread-Safety, deterministisches Verhalten und dass die Quelle den Status-Service nicht schreibt; Instanz-State auf einem Singleton widerspräche dem. Versionsinformationen fließen über `AutoUpdateCheckResult` |
| Event-Signaturen | `EventHandler<T>` mit von `CancelEventArgs` abgeleiteten Argumentklassen statt `ref bool cancel` | C#-Events erlauben keine `ref`-Parameter; `CancelEventArgs` ist das idiomatische .NET-Äquivalent und erfüllt dieselbe Anforderung |
| Fehler in Event-Handlern | Ausnahme wird gefangen, über `ErrorOccurred` gemeldet, im Status-Service als `LastError` abgelegt; der Ablauf läuft weiter, die Abbruch-Stimme des fehlgeschlagenen Handlers zählt nicht | Ein einzelner defekter Abonnent darf das Update weder unbemerkt abbrechen noch die Bibliothek destabilisieren; Anforderung verlangt ausdrücklich nur Meldung und Speicherung |
| Lebensdauer `AutoUpdateOrchestrator` | Singleton | Anforderung Abschnitt 5 („Er wird als Singleton registriert"). Alle Abhängigkeiten des Orchestrators sind zustandslos oder selbst thread-sicher |
| Serialisierung paralleler Aufrufe | `AutoUpdateOrchestrator` serialisiert Check/Download/Install über ein internes `SemaphoreSlim`; `AutoUpdateCommandService` reicht nur durch | Thread-Safety-Anforderung; nur eine Stelle hält den kritischen Abschnitt, damit Hintergrunddienst und UI dieselbe Sperre teilen |
| Status-Speicherung | `AutoUpdateStatusService` hält einen unveränderlichen `AutoUpdateStatusSnapshot` hinter einem `lock`; Aktualisierung durch Austausch des Snapshots (**Value Object**) | Einfacher und zuverlässiger als feldweise Sperren; Leser bekommen immer einen konsistenten Gesamtzustand |
| Statuspersistenz | `IAutoUpdateStateStore` schreibt den Snapshot atomar nach `<DownloadPath>/status.json`; unlesbare oder fremdformatige Dateien führen zu einem frischen `Idle`-Zustand statt zu einer Ausnahme | Der Status muss einen Prozessneustart durch das Update-Skript überleben; bestehende Installationen enthalten eine `status.json` im alten Schema |
| Verzeichnislayout | `DownloadPath` bleibt das Wurzelverzeichnis mit den bestehenden Unterordnern `pending`, `staging` und den Dateien `status.json`, `update.lock`, `update.log`; Standardwert `updates` | Bestehende Installationen behalten ihr Layout; keine Migration von Verzeichnissen nötig |
| Konfigurationsbindung | Bibliothek bindet standardmäßig Sektion `AutoUpdate`, `AutoUpdateBuilder.BindConfiguration(section)` erlaubt abweichende Sektionen; FinanceManager bindet weiterhin `Updates` | Vermeidet eine erzwungene Änderung bestehender `appsettings.json`/Umgebungsvariablen in ausgelieferten Installationen |
| Persistenz der Laufzeiteinstellungen | Bleibt im Host: `UpdateSettingsStore` (inkl. Legacy-Migration) verbleibt in `FinanceManager.Web` und schreibt geänderte Werte in die Singleton-`AutoUpdateOptions` | Anforderung fordert nur laufzeitveränderliche Options als Singleton, keine Persistenz. Die JSON-Persistenz ist FinanceManager-spezifisch (Repository-Owner, Dienstname) und gehört nicht in ein allgemeines NuGet-Paket |
| Rückwärtskompatibilität der API | `IUpdateOrchestrator` und alle DTOs in `FinanceManager.Shared/Dtos/Update` bleiben bestehen; neue Implementierung `UpdateOrchestratorAdapter` mappt auf die Bibliothek (**Adapter**) | Controller, `ApiClient`, ViewModel, Razor-Komponente, Integrations- und bUnit-Tests bleiben unverändert; der Umbau bleibt auf die Serviceschicht begrenzt |
| Host-Beendigung nach Skriptstart | Bibliothek stellt `IAutoUpdateHostTerminator` bereit, ruft ihn aber nur, wenn `AutoUpdateOptions.StopHostAfterScriptStart` gesetzt ist (Standard `false`) | Der heutige `UpdateExecutor` injiziert `IUpdateHostTerminator`, ruft ihn nie auf — die Dienststeuerung übernimmt das Skript. Standardverhalten bleibt damit unverändert, die Option macht den bisher toten Pfad nutzbar |
| Prüf-Zeitfenster | Eigene reine Klasse `SourceCheckWindowEvaluator` mit `IsWithinWindow(DateTimeOffset)`; leere `TimeRanges` bedeuten „immer erlaubt" | Testbar ohne Hintergrunddienst und ohne Zeitmanipulation; entspricht dem Muster **Specification** |
| Zeitquelle | Durchgängig `TimeProvider` (bereits als Singleton registriert) | Bestehende Konvention im Repo (`UpdateScheduler`), erlaubt deterministische Tests der Hintergrunddienste |

---

## Programmabläufe

### Registrierung beim Start

1. `Program.cs`/`ProgramExtensions` ruft `builder.UseAutoUpdate(cfg => ...)`.
2. `UseAutoUpdate` erzeugt einen `AutoUpdateBuilder`, führt die übergebene Konfigurationsaktion aus und erhält daraus eine `AutoUpdateOptions`-Instanz.
3. Wurde keine Quelle gesetzt, setzt der Builder `AutoUpdateLocalFolderSource` als Standardquelle.
4. `UseAutoUpdate` registriert `AutoUpdateOptions` als Singleton-Instanz, alle Standardimplementierungen (Status-, Command-, Orchestrator-, Storage-, Validierungs-, Installations- und Plattformdienste) als Singleton sowie `IValidateOptions<AutoUpdateOptions>`.
5. Ist `AutoUpdateOptions.HostedServicesEnabled` gesetzt, werden `AutoUpdateCheckerService` und `AutoUpdateSchedulerService` als Hosted Services registriert.
6. `AutoUpdateStatusService` lädt beim ersten Zugriff über `IAutoUpdateStateStore.ReadAsync` den zuletzt persistierten Snapshot; schlägt das fehl, startet er mit `AutoUpdateState.Idle`.

Beteiligte Klassen/Komponenten: `AutoUpdateHostBuilderExtensions`, `AutoUpdateBuilder`, `AutoUpdateOptions`, `AutoUpdateOptionsValidator`, `AutoUpdateStatusService`, `IAutoUpdateStateStore`

### Vollständiger Update-Workflow (`RunUpdateAsync`)

1. `AutoUpdateOrchestrator.RunUpdateAsync` betritt den internen kritischen Abschnitt.
2. Ist `AutoUpdateOptions.Enabled` `false`, setzt der Orchestrator `AutoUpdateState.Disabled` und liefert `AutoUpdateResult` mit `AutoUpdateOutcome.Skipped`.
3. `IAutoUpdateEventAggregator.RaiseBeforeCheckSource` wird ausgelöst. Bei Abbruch: Zustand zurück auf `Idle`, Ergebnis `Canceled`.
4. Zustand `Checking`; `IAutoUpdateSource.CheckAsync` liefert `AutoUpdateCheckResult` mit `AvailableVersion` und `AutoUpdatePackageDescriptor`.
5. `IAutoUpdatePackageValidator.IsNewerVersion` vergleicht mit `IInstalledVersionProvider.GetAsync`. Keine neuere Version: Zustand `Idle`, Ergebnis `NoUpdate`; `LastCheckResult` wird im Status-Service abgelegt.
6. Neuere Version vorhanden: Zustand `UpdateAvailable`. Ist `EnableAutomaticDownload` `false`, Ergebnis `Skipped`.
7. `RaiseBeforeDownload` mit der Quell-`Uri` des Pakets. Bei Abbruch: Ergebnis `Canceled`, Zustand `UpdateAvailable`.
8. Zustand `Downloading`; `IAutoUpdatePackageStore.GetPendingPath` liefert das Ziel, `IAutoUpdateSource.DownloadAsync` lädt herunter, `IAutoUpdatePackageValidator.ValidateDownloadedPackageAsync` prüft Größe, SHA256 und ZIP-Integrität.
9. Zustand `ReadyToInstall`; `LastDownloadResult` wird gespeichert. Ist `EnableAutomaticInstallation` `false`, Ergebnis `Skipped`.
10. `RaiseBeforeInstall` mit dem `FileInfo` des heruntergeladenen Pakets. Bei Abbruch: Ergebnis `Canceled`, Zustand `ReadyToInstall`.
11. Weiter mit dem Ablauf „Installation und Skriptstart".
12. Jede Ausnahme in den Schritten 4–11 wird gefangen, über `RaiseErrorOccurred` gemeldet, im Status-Service als `LastError` mit Zustand `Failed` abgelegt und als `AutoUpdateResult` mit `Outcome.Failed` zurückgegeben — der Aufrufer erhält keine Ausnahme.

Beteiligte Klassen/Komponenten: `AutoUpdateOrchestrator`, `IAutoUpdateSource`, `AutoUpdateEvents`, `AutoUpdateStatusService`, `IAutoUpdatePackageStore`, `IAutoUpdatePackageValidator`, `IInstalledVersionProvider`

### Installation und Skriptstart

1. `AutoUpdateOrchestrator.InstallAsync` prüft, dass ein Paket im Zustand `ReadyToInstall` vorliegt.
2. `IAutoUpdatePackageStore.TryCreateLockAsync` legt die Sperrdatei an; scheitert das, Ergebnis `Failed` mit Hinweis auf die aktive Sperre.
3. `IAutoUpdateInstaller.PrepareAsync` validiert das Paket erneut, ermittelt über `IAutoUpdateServiceResolver.Resolve` das Installationsziel und lässt `IAutoUpdateScriptGenerator.GenerateAsync` das plattformspezifische Skript schreiben.
4. `RaiseBeforeStartUpdateScript` mit dem `FileInfo` des Skripts. Bei Abbruch: Sperre wird gelöscht, Zustand zurück auf `ReadyToInstall`, Ergebnis `Canceled`.
5. Zustand `Installing` wird gesetzt und persistiert, **bevor** das Skript startet, damit der Zustand einen Prozessabbruch übersteht.
6. `IAutoUpdateProcessRunner.StartPrepareEnvironment` und `StartScript` starten das Skript.
7. `RaiseAfterStartUpdateScript` wird ausgelöst. Ist `StopHostAfterScriptStart` gesetzt, ruft der Orchestrator anschließend `IAutoUpdateHostTerminator.StopApplication`.
8. Ergebnis `AutoUpdateResult` mit `Outcome.Success` und Zustand `Installing`; `LastInstallResult` wird gespeichert.
9. Schlägt Schritt 3, 6 oder 7 fehl: Sperre löschen, Zustand `Failed`, `ErrorOccurred` melden.

Beteiligte Klassen/Komponenten: `AutoUpdateOrchestrator`, `AutoUpdateInstaller`, `AutoUpdateScriptGenerator`, `AutoUpdateServiceResolver`, `DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateHostTerminator`, `AutoUpdateEvents`, `AutoUpdateStatusService`

### Zustandsabgleich nach Neustart

1. Beim ersten `GetStatusAsync` nach einem Prozessstart liest `AutoUpdateStatusService` den persistierten Snapshot.
2. Steht dort `Installing` und ist keine Sperrdatei mehr vorhanden, vergleicht der Orchestrator `IInstalledVersionProvider`-Version mit `AvailableVersion` des Snapshots.
3. Bei Gleichheit: Zustand `Success`, Paket- und Versionsfelder werden geleert.
4. Bei Abweichung: Zustand `Failed` mit erklärender Meldung.

Beteiligte Klassen/Komponenten: `AutoUpdateOrchestrator`, `AutoUpdateStatusService`, `IAutoUpdatePackageStore`, `IInstalledVersionProvider`

### Periodische Quellprüfung (Hintergrunddienst)

1. `AutoUpdateCheckerService.ExecuteAsync` liest bei jedem Durchlauf die aktuellen `AutoUpdateOptions` (laufzeitveränderlich).
2. `SourceCheckWindowEvaluator.IsWithinWindow` prüft anhand von `SourceCheck.TimeRanges` und `TimeProvider.GetLocalNow`, ob geprüft werden darf.
3. Innerhalb des Fensters und bei `Enabled` ruft der Dienst `IAutoUpdateOrchestrator.RunUpdateAsync` auf; Download und Installation finden dadurch nur statt, wenn `AutoUpdateOptions.EnableAutomaticDownload` bzw. `EnableAutomaticInstallation` gesetzt sind — der Dienst selbst enthält keine eigene Download-/Installationslogik, sondern delegiert vollständig an den Orchestrator (`RunUpdateAsync` bricht ohne Nebeneffekte ab, sobald einer der beiden Schalter deaktiviert ist).
4. Der Dienst wartet `SourceCheck.Interval` bis zum nächsten Durchlauf; bei einer Ausnahme wird geloggt und nach einer festen Rückfallwartezeit (mit eigener Abbruchbehandlung, analog `AutoUpdateSchedulerService`) erneut versucht.

Beteiligte Klassen/Komponenten: `AutoUpdateCheckerService`, `SourceCheckWindowEvaluator`, `AutoUpdateOptions`, `IAutoUpdateOrchestrator`

### Geplante Installation (Hintergrunddienst)

1. `AutoUpdateSchedulerService` prüft minütlich `AutoUpdateOptions.ScheduledInstallTime` und den Status-Snapshot.
2. Zustand `ReadyToInstall`, keine aktive Sperre und erreichte Uhrzeit lösen `IAutoUpdateCommandHandler.InstallAsync(confirmDowntime: true)` aus.
3. Datum und Uhrzeit des letzten Versuchs werden gemerkt, damit derselbe Termin nicht mehrfach ausgelöst wird.

Beteiligte Klassen/Komponenten: `AutoUpdateSchedulerService`, `AutoUpdateCommandService`, `AutoUpdateStatusService`

### Manuelle Steuerung aus der UI

1. `SetupUpdateTab.razor` → `SetupUpdateViewModel` → `ApiClient.Updates_CheckAsync` → `POST /api/setup/update/check`.
2. `UpdateController` ruft unverändert `IUpdateOrchestrator.CheckAsync`; die Implementierung ist jetzt `UpdateOrchestratorAdapter`.
3. Der Adapter delegiert an `IAutoUpdateCommandHandler.CheckAsync`, liest anschließend `IAutoUpdateStatusProvider.GetSnapshot` und mappt Snapshot und Ergebnis auf `UpdateCheckResultDto`/`UpdateStatusDto`.
4. Analog für `POST install/start` (→ `InstallAsync`), `GET status`, `GET/PUT settings` und `POST schedule`.
5. `PUT settings` schreibt über `UpdateSettingsStore` in die JSON-Persistenz und überträgt die Werte anschließend in die Singleton-`AutoUpdateOptions`.

Beteiligte Klassen/Komponenten: `UpdateController`, `UpdateOrchestratorAdapter`, `AutoUpdateCommandService`, `AutoUpdateStatusService`, `UpdateSettingsStore`, `AutoUpdateOptions`

---

## Neue Klassen

Alle Typen liegen im neuen Projekt `SoftwareSchmiede.AutoUpdate` (Root-Namespace `SoftwareSchmiede.AutoUpdate`), sofern nicht anders vermerkt.

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AutoUpdateHostBuilderExtensions` | Statische Klasse | Erweiterungsmethode `UseAutoUpdate(this IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` als einziger Registrierungspunkt |
| `AutoUpdateBuilder` | Klasse | Fluent-API: `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `UseSource`, `UseGithubSource`, `UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices` |
| `AutoUpdateOptions` | Konfigurationsklasse | Laufzeitveränderliche Singleton-Optionen (Enabled, EnableAutomaticDownload, DownloadPath, EnableAutomaticInstallation, Source, SourceCheck, MaxAssetBytes, HostedServicesEnabled, ScheduledInstallTime, ServiceName, ExecutablePath, StopHostAfterScriptStart, HealthTimeoutSeconds) |
| `SourceCheckOptions` | Konfigurationsklasse | `Interval` (Minuten) und `TimeRanges` |
| `SourceCheckTimeRange` | Datenmodellklasse | Zeitfenster aus `DayOfWeek`, `TimeOnly StartTime`, `TimeOnly EndTime` |
| `AutoUpdateOptionsValidator` | Klasse (`IValidateOptions<AutoUpdateOptions>`) | Startvalidierung der Konfiguration |
| `IAutoUpdateSource` | Interface | `CheckAsync`, `DownloadAsync` — Abstraktion der Update-Quelle |
| `AutoUpdateLocalFolderSource` | Klasse | Standardquelle: liest Manifest und Paket aus einem lokalen Verzeichnis |
| `AutoUpdateGithubSource` | Klasse | GitHub-Releases-Quelle, erzeugt über `Create(repositoryName, repositoryOwner)` |
| `IAutoUpdateOrchestrator` | Interface | `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync`, `GetStatusAsync` |
| `AutoUpdateOrchestrator` | Klasse | Zentrale Ablaufsteuerung inkl. Ereignisauslösung und Fehlerbehandlung |
| `IAutoUpdateCommandHandler` | Interface | `CheckAsync`, `DownloadAsync`, `InstallAsync(bool confirmDowntime, …)` für manuelle Auslösung |
| `AutoUpdateCommandService` | Klasse | UI-unabhängige Fassade ohne eigene Update-Logik; delegiert an den Orchestrator |
| `IAutoUpdateStatusProvider` | Interface | Lesender Zugriff auf `AutoUpdateStatusSnapshot` |
| `AutoUpdateStatusService` | Klasse | Thread-sichere Zustandsverwaltung inkl. Persistenz |
| `AutoUpdateStatusSnapshot` | Datenmodellklasse (record) | Unveränderlicher Gesamtzustand: State, InstalledVersion, AvailableVersion, LastCheckedAt, LastCheckResult, LastDownloadResult, LastInstallResult, LastError, IsLocked, LockCreatedAt |
| `AutoUpdateState` | Enum | Idle, Checking, UpdateAvailable, Downloading, ReadyToInstall, Installing, Success, Failed, Disabled |
| `AutoUpdateOutcome` | Enum | Success, NoUpdate, Skipped, Canceled, Failed |
| `AutoUpdateResult` | Datenmodellklasse (record) | Ergebnis einer Update-Operation: Outcome, State, Message, Error |
| `AutoUpdateCheckResult` | Datenmodellklasse (record) | Ergebnis der Quellprüfung inkl. `AvailableVersion` und `AutoUpdatePackageDescriptor` |
| `AutoUpdateDownloadResult` | Datenmodellklasse (record) | Ergebnis eines Downloads: lokaler Pfad, Größe, Prüfsummenstatus |
| `AutoUpdateInstallResult` | Datenmodellklasse (record) | Ergebnis einer Installation: Version, Skriptpfad, Startzeitpunkt |
| `AutoUpdatePackageDescriptor` | Datenmodellklasse (record) | Beschreibung eines Update-Pakets: Version, Plattform, RuntimeIdentifier, Dateiname, `Uri`, SHA256, Größe |
| `AutoUpdateReleaseInfo` | Datenmodellklasse (record) | Manifest einer Version: Version, ReleaseNotes, PublishedAt, Paketliste |
| `InstalledReleaseInfo` | Datenmodellklasse (record) | Installierte Version: Version, PublishedAt, CommitSha, Repository, RuntimeIdentifier |
| `IAutoUpdateEventAggregator` | Interface | Registrierung und Auslösung aller Update-Ereignisse |
| `AutoUpdateEvents` | Klasse | Thread-sichere Implementierung des Event-Aggregators |
| `AutoUpdateCancelEventArgs` | Datenmodellklasse | Basis-Argument für abbrechbare Ereignisse (`BeforeCheckSource`) |
| `BeforeDownloadEventArgs` | Datenmodellklasse | Abbrechbares Ereignisargument mit `Uri SourceUri` |
| `BeforeInstallEventArgs` | Datenmodellklasse | Abbrechbares Ereignisargument mit `FileInfo PackageFile` |
| `BeforeStartUpdateScriptEventArgs` | Datenmodellklasse | Abbrechbares Ereignisargument mit `FileInfo ScriptFile` |
| `AutoUpdateErrorEventArgs` | Datenmodellklasse | Ereignisargument mit `Exception Error` und Ablaufphase |
| `IAutoUpdateEnvironment` | Interface | `ApplicationDirectory` — Hosting-unabhängiger Pfadzugriff |
| `HostAutoUpdateEnvironment` | Klasse | Standardimplementierung über `IHostEnvironment.ContentRootPath` |
| `IInstalledVersionProvider` | Interface | Ermittlung der installierten Version |
| `ReleaseMetadataInstalledVersionProvider` | Klasse | Liest `release-metadata.json` aus dem Anwendungsverzeichnis |
| `IAutoUpdatePackageStore` | Interface | Verzeichnisse, Pfade und Sperrdatei-Verwaltung |
| `FileSystemAutoUpdatePackageStore` | Klasse | Portierung von `UpdateFileStore` ohne `IWebHostEnvironment` |
| `IAutoUpdateStateStore` | Interface | Lesen/Schreiben des persistierten Status-Snapshots |
| `FileSystemAutoUpdateStateStore` | Klasse | Atomare JSON-Persistenz von `AutoUpdateStatusSnapshot` |
| `JsonFileStore` | Interne statische Klasse | Atomares Lesen/Schreiben von JSON (Portierung) |
| `IAutoUpdatePackageValidator` | Interface | `IsNewerVersion`, `ValidateDownloadedPackageAsync` (kein separates `ValidateReleaseAsync`: keiner der Programmabläufe validiert das Manifest getrennt vom heruntergeladenen Paket, ein ungenutztes öffentliches Interface-Mitglied würde in einem NuGet-Paket dauerhaft mitgeschleppt) |
| `AutoUpdatePackageValidator` | Klasse | Portierung von `UpdateValidator` (SemVer-Vergleich, SHA256, ZIP-Integrität, Größenlimit) |
| `IAutoUpdateInstaller` | Interface | `PrepareAsync` (Ziel auflösen, Skript erzeugen), `StartAsync` (Skript starten) |
| `AutoUpdateInstaller` | Klasse | Portierung der Kernlogik von `UpdateExecutor` ohne Ereignisauslösung |
| `IAutoUpdateScriptGenerator` / `AutoUpdateScriptGenerator` | Interface + Klasse | Portierung von `UpdateScriptGenerator` (Windows `.ps1`, Linux `.sh`) |
| `IAutoUpdatePlatformResolver` / `AutoUpdatePlatformResolver` | Interface + Klasse | Portierung von `UpdatePlatformResolver`, Paketauswahl nach RuntimeIdentifier |
| `IAutoUpdateServiceResolver` / `AutoUpdateServiceResolver` | Interface + Klasse | Portierung von `UpdateServiceResolver` |
| `IAutoUpdateServiceProbe` / `DefaultAutoUpdateServiceProbe` | Interface + Klasse | Portierung von `DefaultUpdateServiceProbe` |
| `IAutoUpdateProcessRunner` / `DefaultAutoUpdateProcessRunner` | Interface + Klasse | Portierung von `DefaultUpdateProcessRunner` |
| `IAutoUpdateHostTerminator` / `DefaultAutoUpdateHostTerminator` | Interface + Klasse | Portierung von `DefaultUpdateHostTerminator` (`IHostApplicationLifetime`) |
| `AutoUpdateInstallationTarget` | Datenmodellklasse (record) | Portierung von `UpdateInstallationTarget` |
| `SourceCheckWindowEvaluator` | Klasse | Prüft, ob ein Zeitpunkt innerhalb der konfigurierten Zeitfenster liegt |
| `AutoUpdateCheckerService` | Hosted Service | Periodische Quellprüfung unter Beachtung von Intervall und Zeitfenstern |
| `AutoUpdateSchedulerService` | Hosted Service | Zeitgesteuerte Installation über den Command-Service |
| `UpdateOrchestratorAdapter` | Klasse (in `FinanceManager.Web/Services/Updates/`) | Implementiert `IUpdateOrchestrator` auf Basis der Bibliothek und mappt auf die bestehenden DTOs |
| `AutoUpdateOptionsMapper` | Statische Klasse (in `FinanceManager.Web/Services/Updates/`) | Überträgt `UpdateSettingsDto` in die Singleton-`AutoUpdateOptions` und zurück |

---

## Änderungen an bestehenden Klassen

### `ProgramExtensions` (statische Erweiterungsklasse, `FinanceManager.Web`)

- **Geänderte Methoden:** Der Block „Self-update services" (Zeilen ~157–182) entfällt vollständig. Die 13 `AddSingleton`/`AddScoped`-Aufrufe, die `AddHttpClient<IUpdateManifestClient, …>`-Registrierung und die beiden `AddHostedService`-Aufrufe werden durch einen einzigen `builder.UseAutoUpdate(cfg => …)`-Aufruf ersetzt. Die Konfiguration bindet die bestehende Sektion `Updates`, wählt anhand von `Updates:SourceType` zwischen `UseGithubSource` und `UseLocalFolderSource` und übernimmt `HostedServicesEnabled`.
- **Neue Registrierungen:** `IUpdateOrchestrator` → `UpdateOrchestratorAdapter` (Scoped, wie bisher), `IUpdateSettingsStore` → `UpdateSettingsStore` (Singleton, bleibt), `IInstalledReleaseMetadataProvider` → `InstalledReleaseMetadataProvider` (Singleton, bleibt).
- **Entfallende Registrierung:** `builder.Services.AddSingleton(TimeProvider.System)` bleibt bestehen, da die Bibliothek den `TimeProvider` konsumiert und ihn nur registriert, wenn er noch nicht vorhanden ist.

### `UpdateContracts.cs` (Interface-Sammlung, `FinanceManager.Web`)

- **Entfallende Interfaces:** `IUpdateManifestClient`, `IUpdatePlatformResolver`, `IUpdateFileStore`, `IUpdateValidator`, `IUpdateScriptGenerator`, `IUpdateServiceResolver`, `IUpdateServiceProbe`, `IUpdateProcessRunner`, `IUpdateHostTerminator`, `IUpdateExecutor` sowie `record UpdateInstallationTarget` — alle in die Bibliothek überführt.
- **Verbleibende Interfaces:** `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator` (unverändert in Signatur, damit Controller und Tests unberührt bleiben).

### `UpdateSettingsStore` (Klasse, `FinanceManager.Web`)

- **Geänderte Methoden:** `GetAsync`, `SaveAsync`, `SaveScheduleAsync` nutzen statt `IUpdateFileStore`/`IOptions<UpdateOptions>` künftig `IAutoUpdatePackageStore` (für den Pfad der `settings.json`) und `AutoUpdateOptions` (für die Standardwerte).
- **Neue Methoden:** `ApplyToOptionsAsync` — überträgt die geladenen bzw. gespeicherten Einstellungen über `AutoUpdateOptionsMapper` in die Singleton-`AutoUpdateOptions`, damit Änderungen aus der Setup-UI sofort wirken.
- Die Legacy-Migration (`windowsServiceName`/`linuxServiceName` → `ServiceName`) bleibt unverändert erhalten.

### `InstalledReleaseMetadataProvider` (Klasse, `FinanceManager.Web`)

- **Geänderte Methoden:** `GetAsync` liest nicht mehr selbst `release-metadata.json`, sondern delegiert an `IInstalledVersionProvider` der Bibliothek und mappt `InstalledReleaseInfo` auf `InstalledReleaseMetadataDto`.
- **Entfallende Abhängigkeit:** `IWebHostEnvironment`; stattdessen `IInstalledVersionProvider`.
- Die Verwendung durch `LoginStatus.razor` (Versionsanzeige) bleibt dadurch unverändert.

### `UpdateOptions` (Konfigurationsklasse, `FinanceManager.Web`)

- **Neue Eigenschaften:** `SourceType` (`string`, „Github" | „LocalFolder") — steuert die im `AutoUpdateBuilder` gewählte Quelle; `LocalFolderPath` (`string?`) — Verzeichnis für `AutoUpdateLocalFolderSource`; `EnableAutomaticDownload` (`bool`) und `EnableAutomaticInstallation` (`bool`) — bilden die neuen Anforderungs-Schalter ab.
- **Geänderte Bedeutung:** `WorkingDirectory` wird beim Aufbau auf `AutoUpdateOptions.DownloadPath` abgebildet; `CheckIntervalMinutes` auf `SourceCheck.Interval`.
- Diese Klasse bleibt reine Bindungsklasse des Hosts und wandert bewusst **nicht** in die Bibliothek.

### `UpdateController` (Controller, `FinanceManager.Web`)

- Keine Signaturänderung. Die injizierte `IUpdateOrchestrator`-Instanz ist künftig der Adapter; das Ausnahme-Mapping (`FileNotFoundException` → 404, `IOException` → 409, `ArgumentException`/`InvalidOperationException` → 400) muss vom Adapter weiterhin bedient werden, da die Bibliothek Fehler als Ergebnisobjekte statt als Ausnahmen liefert.

### `SetupUpdateViewModel` / `SetupUpdateTab.razor` (`FinanceManager.Web`)

- Keine Änderung. Beide arbeiten ausschließlich über `IApiClient` und die DTOs aus `FinanceManager.Shared`, die unverändert bleiben.

---

## Datenbankmigrationen

Keine. Das Update-System persistiert ausschließlich in Dateien (`settings.json`, `status.json`, `update.lock`) und berührt keine Entity-Framework-Entitäten.

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AutoUpdateOptions.DownloadPath` | Nicht leer, keine ungültigen Pfadzeichen; Verzeichnis wird bei Bedarf angelegt | `OptionsValidationException` beim Start |
| `AutoUpdateOptions.Source` | Nach Auswertung des Builders nicht `null` (Fallback `AutoUpdateLocalFolderSource`) | `OptionsValidationException` beim Start |
| `AutoUpdateOptions.MaxAssetBytes` | `> 0` | `OptionsValidationException` beim Start |
| `AutoUpdateOptions.HealthTimeoutSeconds` | Zwischen 10 und 600 (wird geklemmt) | Wert wird auf den Bereich begrenzt |
| `SourceCheckOptions.Interval` | `>= 1` Minute | `OptionsValidationException` beim Start |
| `SourceCheckTimeRange` | `StartTime < EndTime` | `OptionsValidationException` beim Start |
| `AutoUpdateGithubSource.Create` | `repositoryOwner` und `repositoryName` nicht leer | `ArgumentException` bei Erzeugung |
| `AutoUpdateLocalFolderSource` | Quellverzeichnis nicht leer; fehlendes Verzeichnis bei `CheckAsync` liefert `AutoUpdateCheckResult` ohne Version statt einer Ausnahme | `AutoUpdateOutcome.NoUpdate` mit Meldung |
| `AutoUpdatePackageDescriptor.FileName` | Darf keine Pfadsegmente enthalten (`Path.GetFileName`-Gleichheit) | `InvalidOperationException`, gemeldet über `ErrorOccurred` |
| Heruntergeladenes Paket | Größe `<= MaxAssetBytes`, SHA256 stimmt mit dem Deskriptor überein, gültiges ZIP-Archiv | Download-Ergebnis `Failed`, Zustand `Failed` |
| Versionsvergleich | Semantische Versionierung; ist die installierte Version unbekannt, gilt kein Update als neuer | Ergebnis `NoUpdate` mit erklärender Meldung |
| `InstallAsync(confirmDowntime)` | Muss `true` sein | `AutoUpdateResult` mit `Outcome.Failed`; Adapter wirft daraus `ArgumentException` → HTTP 400 |
| Installationsziel (Windows) | `ServiceName` oder `ExecutablePath` gesetzt | `InvalidOperationException`, Zustand `Failed` |
| Installationsziel (Linux) | `ServiceName` (systemd) gesetzt | `InvalidOperationException`, Zustand `Failed` |

---

## Konfigurationsänderungen

Sektion `Updates` in `FinanceManager.Web/appsettings.json` (Bindung an `UpdateOptions`, Weitergabe an `AutoUpdateOptions`):

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Updates:SourceType` | string | `Github` | Auswahl der Update-Quelle (`Github` oder `LocalFolder`) |
| `Updates:LocalFolderPath` | string? | `null` | Verzeichnis für `AutoUpdateLocalFolderSource` |
| `Updates:EnableAutomaticDownload` | bool | `true` | Automatischer Download nach erfolgreicher Prüfung |
| `Updates:EnableAutomaticInstallation` | bool | `false` | Automatische Installation nach erfolgreichem Download |
| `Updates:SourceCheck:Interval` | int | `360` | Prüfintervall in Minuten (ersetzt `CheckIntervalMinutes`, dieses bleibt als Alias erhalten) |
| `Updates:SourceCheck:TimeRanges` | Array | `[]` | Zeitfenster mit `DayOfWeek`, `StartTime`, `EndTime`; leer bedeutet „jederzeit" |
| `Updates:StopHostAfterScriptStart` | bool | `false` | Beendet den Host nach erfolgreichem Skriptstart |

Unverändert weiterverwendet: `Updates:Enabled`, `Updates:RepositoryOwner`, `Updates:RepositoryName`, `Updates:ManifestAssetName`, `Updates:WorkingDirectory`, `Updates:MaxAssetBytes`, `Updates:HostedServicesEnabled`, `Updates:HealthTimeoutSeconds`, `Updates:ServiceName`, `Updates:ExecutablePath`.

Zusätzlich in `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs` als Umgebungsvariablen für den Testserver: `Updates__SourceType=LocalFolder`, `Updates__LocalFolderPath=<Temp-Verzeichnis>`, `Updates__EnableAutomaticInstallation=false`, `Updates__HostedServicesEnabled=false`, `Updates__WorkingDirectory=<Temp-Verzeichnis>`.

---

## Seiteneffekte und Risiken

- **Statusdatei-Schema:** Bestehende Installationen enthalten eine `updates/status.json` im Format von `UpdateStatusDto`. `FileSystemAutoUpdateStateStore` liest ein anderes Schema. Ohne toleranten Fallback würde der Start scheitern — deshalb ist „unlesbar → frischer `Idle`-Zustand" verpflichtender Bestandteil der Implementierung. Der Verlust der letzten Prüfhistorie beim ersten Start nach dem Update ist akzeptiert.
- **Laufende Installation über den Umbau hinweg:** Startet ein altes Release die Installation und das neue Release liest danach den Status, greift der Abgleich aus „Zustandsabgleich nach Neustart" nicht (Schema unbekannt). Folge: Der Zustand wird auf `Idle` gesetzt, die Sperrdatei bleibt bis zum manuellen Reset bestehen. Der bestehende Endpunkt `POST /api/setup/update/lock/reset` deckt diesen Fall ab.
- **Versionsanzeige im Menü:** `LoginStatus.razor` nutzt `IInstalledReleaseMetadataProvider`. Wird die Umstellung auf `IInstalledVersionProvider` falsch verdrahtet, verschwindet die Versionsanzeige. Abgedeckt durch die bestehenden `LoginStatusTests` und den E2E-Test `VersionDisplayPlaywrightTests`.
- **Lebensdauer-Wechsel:** Der Orchestrator wird von Scoped auf Singleton umgestellt. Der `UpdateOrchestratorAdapter` bleibt Scoped, darf aber keinen Zustand über Requests hinweg halten. Alle Bibliotheksdienste müssen Singleton-tauglich (thread-sicher) sein.
- **`UpdateChecker`/`UpdateScheduler`-Neuverdrahtung:** Beide Dienste laufen heute über `IServiceScopeFactory`. In der Bibliothek entfällt das (Singleton-Orchestrator). Fehler in dieser Umstellung führen zu stillstehenden Hintergrunddiensten — daher explizite Tests mit `FakeTimeProvider`.
- **Testprojekt-Aufteilung:** `FinanceManager.Tests/Updates/*` referenziert `FinanceManager.Web.Services.Updates`. Nach dem Umzug brechen dort alle Testklassen bis auf `UpdateSettingsStoreTests` und `InstalledReleaseMetadataProviderTests`. Der Umzug in `SoftwareSchmiede.AutoUpdate.Tests` muss im selben Schritt erfolgen, sonst ist die Projektmappe nicht baubar.
- **`FinanceManager.Tests.Integration/UpdateControllerIntegrationTests`:** ersetzt heute `IUpdateOrchestrator` im Testcontainer. Da der Adapter jetzt diese Rolle übernimmt, muss geprüft werden, dass die Ersetzung weiterhin greift und der Testserver keine Netzwerkanfragen gegen GitHub auslöst.
- **Dokumentationspflicht (`CS1591` als Fehler):** Sowohl `FinanceManager.Web` als auch `FinanceManager.Shared` behandeln fehlende XML-Dokumentation als Fehler. Die bestehenden Update-Dateien umgehen das über `#pragma warning disable CS1591`. Für ein NuGet-Paket ist das unzureichend: `SoftwareSchmiede.AutoUpdate` erhält `GenerateDocumentationFile` mit derselben `WarningsAsErrors`-Einstellung und vollständige XML-Kommentare auf allen öffentlichen Typen — das ist ein spürbarer Mehraufwand pro Klasse.
- **Keine macOS-Unterstützung:** `AutoUpdateScriptGenerator` wirft auf anderen Plattformen weiterhin `InvalidOperationException`. Damit ist die Bibliothek zwar hosting-, aber nicht plattformunabhängig — für ein öffentliches NuGet-Paket eine bewusste Einschränkung, die dokumentiert werden muss.
- **Doppelte `TimeProvider`-Registrierung:** `ProgramExtensions` registriert bereits `TimeProvider.System`. `UseAutoUpdate` darf nur per `TryAddSingleton` registrieren, sonst gewinnt je nach Reihenfolge die falsche Instanz und die Scheduler-Tests werden unzuverlässig.

---

## Umsetzungsreihenfolge

1. **Bibliotheksprojekt anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj` (net10.0, `Nullable`, `ImplicitUsings`, `GenerateDocumentationFile`, `WarningsAsErrors` inkl. `CS1591`, NuGet-Metadaten: `PackageId`, `Description`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl`) anlegen. Paketreferenzen: `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Configuration.Binder`. Projekt in `FinanceManager.sln` eintragen.

2. **Modelle und Zustandsmodell**
   - Voraussetzungen: Schritt 1
   - Beschreibung: `AutoUpdateState`, `AutoUpdateOutcome`, `AutoUpdateResult`, `AutoUpdateCheckResult`, `AutoUpdateDownloadResult`, `AutoUpdateInstallResult`, `AutoUpdateStatusSnapshot`, `AutoUpdatePackageDescriptor`, `AutoUpdateReleaseInfo`, `InstalledReleaseInfo`, `AutoUpdateInstallationTarget` anlegen. Enums müssen vor den Datenmodellklassen existieren.

3. **Konfigurationsmodell und Zeitfenster**
   - Voraussetzungen: Schritt 1
   - Beschreibung: `SourceCheckTimeRange`, `SourceCheckOptions`, `AutoUpdateOptions` und `SourceCheckWindowEvaluator` anlegen. `AutoUpdateOptions.Source` bleibt zunächst untypisiert dokumentiert und wird in Schritt 4 mit `IAutoUpdateSource` verbunden.

4. **Kern-Schnittstellen definieren**
   - Voraussetzungen: Schritte 2 und 3
   - Beschreibung: `IAutoUpdateSource`, `IAutoUpdateEnvironment`, `IInstalledVersionProvider`, `IAutoUpdatePackageStore`, `IAutoUpdateStateStore`, `IAutoUpdatePackageValidator`, `IAutoUpdateScriptGenerator`, `IAutoUpdatePlatformResolver`, `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe`, `IAutoUpdateProcessRunner`, `IAutoUpdateHostTerminator`, `IAutoUpdateInstaller`, `IAutoUpdateStatusProvider`, `IAutoUpdateEventAggregator`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler` anlegen.

5. **Event-Infrastruktur**
   - Voraussetzungen: Schritt 4
   - Beschreibung: `AutoUpdateCancelEventArgs`, `BeforeDownloadEventArgs`, `BeforeInstallEventArgs`, `BeforeStartUpdateScriptEventArgs`, `AutoUpdateErrorEventArgs` sowie `AutoUpdateEvents` mit thread-sicherer Abonnentenverwaltung und Raise-Methoden inkl. Handler-Ausnahmebehandlung implementieren.

6. **Umgebung, Persistenz und Validierung portieren**
   - Voraussetzungen: Schritte 3 und 4
   - Beschreibung: `HostAutoUpdateEnvironment`, `JsonFileStore`, `FileSystemAutoUpdatePackageStore` (Portierung von `UpdateFileStore`, `IWebHostEnvironment` → `IAutoUpdateEnvironment`), `FileSystemAutoUpdateStateStore` (inkl. tolerantem Lesefehler-Fallback), `AutoUpdatePackageValidator` (Portierung von `UpdateValidator`), `ReleaseMetadataInstalledVersionProvider` anlegen.

7. **Plattform- und Installationsdienste portieren**
   - Voraussetzungen: Schritte 4 und 6
   - Beschreibung: `AutoUpdatePlatformResolver`, `DefaultAutoUpdateServiceProbe`, `AutoUpdateServiceResolver`, `AutoUpdateScriptGenerator` (Windows-`.ps1`/Linux-`.sh`, Pfade über `IAutoUpdateEnvironment` und `IAutoUpdatePackageStore`), `DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateHostTerminator`, `AutoUpdateInstaller` aus den bestehenden Web-Klassen portieren.

8. **Status-Service**
   - Voraussetzungen: Schritte 2, 4 und 6
   - Beschreibung: `AutoUpdateStatusService` implementieren: Snapshot hinter `lock`, Mutationsmethoden für Zustandsübergänge und Teilergebnisse, Persistenz über `IAutoUpdateStateStore`, verzögertes Laden beim ersten Zugriff.

9. **Quellen-Implementierungen**
   - Voraussetzungen: Schritte 2 und 4
   - Beschreibung: `AutoUpdateLocalFolderSource` (Manifest und Paket aus lokalem Verzeichnis) und `AutoUpdateGithubSource` inkl. statischer Factory `Create(repositoryName, repositoryOwner)` implementieren; Letztere übernimmt Manifest-Abruf und Asset-Download aus `UpdateManifestClient` und dokumentiert die externen Abhängigkeiten (Netzwerk, GitHub-Release-URLs).

10. **Orchestrator**
    - Voraussetzungen: Schritte 5, 7, 8 und 9
    - Beschreibung: `AutoUpdateOrchestrator` mit `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync`, `GetStatusAsync` implementieren; Ereignisreihenfolge, Abbruchbehandlung, Sperrverwaltung, Zustandsabgleich nach Neustart und die zentrale Fehlerbehandlung gemäß den Programmabläufen.

11. **Command-Service**
    - Voraussetzungen: Schritt 10
    - Beschreibung: `AutoUpdateCommandService` als dünne, thread-sichere Fassade über `IAutoUpdateOrchestrator` ohne eigene Update-Logik implementieren.

12. **Hintergrunddienste**
    - Voraussetzungen: Schritte 3, 10 und 11
    - Beschreibung: `AutoUpdateCheckerService` (Intervall + `SourceCheckWindowEvaluator`, ruft ausschließlich `CheckForUpdateAsync`) und `AutoUpdateSchedulerService` (geplante Installation über den Command-Service) implementieren, beide mit `TimeProvider`.

13. **Builder, Validator und Registrierung**
    - Voraussetzungen: Schritte 3, 9, 11 und 12
    - Beschreibung: `AutoUpdateBuilder`, `AutoUpdateOptionsValidator` und `AutoUpdateHostBuilderExtensions.UseAutoUpdate` implementieren; Standardquelle setzen, alle Dienste per `TryAddSingleton` registrieren, Hosted Services optional registrieren, `TimeProvider` nur bei Bedarf ergänzen.

14. **Testprojekt der Bibliothek anlegen**
    - Voraussetzungen: Schritt 13
    - Beschreibung: `SoftwareSchmiede.AutoUpdate.Tests` (xunit.v3, FluentAssertions, Moq, `Microsoft.Extensions.TimeProvider`-Testhilfen, `Microsoft.NET.Test.Sdk`, coverlet — Versionen analog `FinanceManager.Tests`) anlegen, in `FinanceManager.sln` eintragen, Referenz auf die Bibliothek setzen. Testhilfen `FakeAutoUpdateSource`, `TestAutoUpdateEnvironment`, `AutoUpdateTestContext` bereitstellen.

15. **Bibliothekstests schreiben**
    - Voraussetzungen: Schritt 14
    - Beschreibung: Unit-Tests gemäß Abschnitt „Neue Tests" implementieren; Testklassen bleiben je Thema klein und getrennt (Orchestrator nach Check/Download/Install/Ereignisabbruch aufgeteilt).

16. **Alte Update-Klassen aus `FinanceManager.Web` entfernen**
    - Voraussetzungen: Schritt 13
    - Beschreibung: `UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `UpdateValidator`, `UpdateScriptGenerator`, `UpdatePlatformResolver`, `UpdateServiceResolver`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`, `UpdateManifestClient`, `UpdateChecker`, `UpdateScheduler`, `JsonFileStore` löschen; `UpdateContracts.cs` auf `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator` reduzieren.

17. **Adapterschicht im Web-Projekt**
    - Voraussetzungen: Schritte 13 und 16
    - Beschreibung: `AutoUpdateOptionsMapper` und `UpdateOrchestratorAdapter` anlegen; Adapter mappt Snapshot und Ergebnisobjekte auf `UpdateStatusDto`/`UpdateCheckResultDto` und übersetzt Fehlerergebnisse in die vom `UpdateController` erwarteten Ausnahmetypen. `UpdateSettingsStore` und `InstalledReleaseMetadataProvider` auf die neuen Bibliotheks-Abstraktionen umstellen.

18. **Registrierung in `ProgramExtensions` umstellen**
    - Voraussetzungen: Schritt 17
    - Beschreibung: Self-update-Block durch `builder.UseAutoUpdate(...)` ersetzen, `UpdateOptions` um `SourceType`, `LocalFolderPath`, `EnableAutomaticDownload`, `EnableAutomaticInstallation` erweitern, Adapter und verbleibende Web-Dienste registrieren.

19. **Konfiguration aktualisieren**
    - Voraussetzungen: Schritt 18
    - Beschreibung: Sektion `Updates` in `appsettings.json` um die neuen Einträge ergänzen; Standardwerte so wählen, dass sich das Verhalten bestehender Installationen nicht ändert.

20. **Bestehende Tests migrieren**
    - Voraussetzungen: Schritte 15 und 18
    - Beschreibung: `FinanceManager.Tests/Updates/*` bereinigen — verschobene Testklassen entfernen (Inhalte sind in Schritt 15 abgedeckt), `UpdateSettingsStoreTests` und `InstalledReleaseMetadataProviderTests` an die neuen Abhängigkeiten anpassen, `TestWebHostEnvironment` und `UpdateStatusTestData` an die verbliebene Nutzung angleichen, neue `UpdateOrchestratorAdapterTests` ergänzen.

21. **Integrationstests anpassen**
    - Voraussetzungen: Schritt 20
    - Beschreibung: `UpdateControllerIntegrationTests` so anpassen, dass die Testumgebung eine lokale Ordnerquelle verwendet und die Hintergrunddienste deaktiviert sind; die Statuscode-Erwartungen der Endpunkte bleiben unverändert.

22. **E2E-Test ergänzen**
    - Voraussetzungen: Schritt 21
    - Beschreibung: `PlaywrightWebAppFixture` um die Update-Umgebungsvariablen erweitern, Test-Gateway für den Setup-Tab anlegen und `UpdateSetupPlaywrightTests` mit dem Happy Path „Admin prüft manuell auf Updates und erhält ein Ergebnis" implementieren.

23. **Dokumentation**
    - Voraussetzungen: Schritt 22
    - Beschreibung: `README.md` der Bibliothek (Verwendung von `UseAutoUpdate`, Fluent-Optionen, Ereignisse, eigene Quellen, unterstützte Plattformen), `CHANGELOG.md`-Eintrag und Aktualisierung der betroffenen Abschnitte in `Docs/help`.

---

## Tests

### Neue Tests

Alle Testklassen ohne abweichenden Vermerk liegen in `SoftwareSchmiede.AutoUpdate.Tests`.

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `FakeAutoUpdateSource` | `TestSupport/FakeAutoUpdateSource` | Steuerbare Quelle: verfügbare Version setzen, Download scheitern lassen, Aufrufzähler |
| `TestAutoUpdateEnvironment` | `TestSupport/TestAutoUpdateEnvironment` | `IAutoUpdateEnvironment` auf ein temporäres Verzeichnis |
| `AutoUpdateTestContext` | `TestSupport/AutoUpdateTestContext` | Baut Orchestrator, Status-Service, Events, Store und Validator über ein temporäres Verzeichnis auf; `IDisposable`-Aufräumen |
| `UseAutoUpdate_RegistersAllServices` | `UseAutoUpdateRegistrationTests` | Alle Schnittstellen sind nach einem einzigen Aufruf auflösbar |
| `UseAutoUpdate_WithoutSource_UsesLocalFolderSource` | `UseAutoUpdateRegistrationTests` | Standardquelle wird gesetzt |
| `UseAutoUpdate_WhenHostedServicesDisabled_RegistersNoHostedService` | `UseAutoUpdateRegistrationTests` | `DisableHostedServices` wirkt |
| `UseAutoUpdate_DoesNotOverrideExistingTimeProvider` | `UseAutoUpdateRegistrationTests` | Keine Doppelregistrierung des `TimeProvider` |
| `Builder_FluentChain_SetsAllOptions` | `AutoUpdateBuilderTests` | Fluent-Kette schreibt Download-, Installations-, Quellen- und Prüfoptionen |
| `Builder_UseGithubSource_CreatesGithubSource` | `AutoUpdateBuilderTests` | Factory-Aufruf über den Builder |
| `Builder_BindConfiguration_ReadsSection` | `AutoUpdateBuilderTests` | Bindung einer abweichenden Konfigurationssektion |
| `Validate_WithInvalidInterval_Fails` | `AutoUpdateOptionsValidationTests` | Intervall `< 1` |
| `Validate_WithInvertedTimeRange_Fails` | `AutoUpdateOptionsValidationTests` | `StartTime >= EndTime` |
| `Validate_WithEmptyDownloadPath_Fails` | `AutoUpdateOptionsValidationTests` | Leerer Download-Pfad |
| `Validate_WithNonPositiveMaxAssetBytes_Fails` | `AutoUpdateOptionsValidationTests` | `MaxAssetBytes <= 0` |
| `Raise_BeforeCheckSource_HonorsCancel` | `AutoUpdateEventsTests` | Abbruchstimme eines Abonnenten |
| `Raise_WhenHandlerThrows_ReportsErrorAndContinues` | `AutoUpdateEventsTests` | Handler-Ausnahme → `ErrorOccurred`, kein Abbruch |
| `Raise_AfterStartUpdateScript_HasNoCancelSemantics` | `AutoUpdateEventsTests` | Ereignis ohne Abbruchmöglichkeit |
| `Subscribe_FromMultipleThreads_IsSafe` | `AutoUpdateEventsTests` | Thread-Sicherheit der Abonnentenverwaltung |
| `GetSnapshot_ReturnsConsistentState` | `AutoUpdateStatusServiceTests` | Snapshot ist in sich konsistent |
| `Update_FromParallelThreads_KeepsLastWriteVisible` | `AutoUpdateStatusServiceTests` | Thread-Sicherheit bei parallelen Zustandswechseln |
| `Snapshot_IsPersistedAndReloaded` | `AutoUpdateStatusServiceTests` | Persistenz über einen Neustart hinweg |
| `Load_WithUnreadableStateFile_FallsBackToIdle` | `AutoUpdateStatusServiceTests` | Toleranter Umgang mit fremdem/defektem `status.json` |
| `Check_WhenNewerVersionAvailable_SetsUpdateAvailable` | `AutoUpdateOrchestratorCheckTests` | Zustandsübergang und `LastCheckResult` |
| `Check_WhenNoNewerVersion_ReturnsNoUpdate` | `AutoUpdateOrchestratorCheckTests` | Kein Zustandswechsel auf `UpdateAvailable` |
| `Check_WhenDisabled_ReturnsSkippedAndDisabledState` | `AutoUpdateOrchestratorCheckTests` | `Enabled = false` |
| `Check_WhenSourceThrows_ReportsErrorAndFails` | `AutoUpdateOrchestratorCheckTests` | Ausnahme → `ErrorOccurred`, Zustand `Failed`, keine Weitergabe der Ausnahme |
| `Run_WhenAutomaticDownloadDisabled_StopsAfterCheck` | `AutoUpdateOrchestratorDownloadTests` | `Outcome.Skipped` |
| `Run_DownloadsAndValidatesPackage` | `AutoUpdateOrchestratorDownloadTests` | Download, Prüfsumme, Zustand `ReadyToInstall` |
| `Run_WhenChecksumMismatch_Fails` | `AutoUpdateOrchestratorDownloadTests` | Fehlerhafte Prüfsumme |
| `Run_WhenPackageExceedsMaxBytes_Fails` | `AutoUpdateOrchestratorDownloadTests` | Größenlimit |
| `Install_WhenAutomaticInstallationDisabled_StopsAfterDownload` | `AutoUpdateOrchestratorInstallTests` | `Outcome.Skipped` |
| `Install_GeneratesScriptAndStartsIt` | `AutoUpdateOrchestratorInstallTests` | Skripterzeugung und -start, Zustand `Installing` |
| `Install_WhenLockActive_Fails` | `AutoUpdateOrchestratorInstallTests` | Aktive Sperre |
| `Install_WithoutConfirmDowntime_Fails` | `AutoUpdateOrchestratorInstallTests` | Bestätigungspflicht |
| `Install_WhenStopHostAfterScriptStart_TerminatesHost` | `AutoUpdateOrchestratorInstallTests` | Optionaler Host-Stopp |
| `Install_PersistsInstallingStateBeforeScriptStart` | `AutoUpdateOrchestratorInstallTests` | Zustand übersteht Prozessabbruch |
| `Reconcile_AfterRestart_WhenVersionMatches_SetsSuccess` | `AutoUpdateOrchestratorInstallTests` | Zustandsabgleich nach Neustart |
| `Reconcile_AfterRestart_WhenVersionDiffers_SetsFailed` | `AutoUpdateOrchestratorInstallTests` | Fehlgeschlagene Installation erkennen |
| `Run_WhenBeforeCheckSourceCanceled_StopsImmediately` | `AutoUpdateOrchestratorEventTests` | Abbruch vor der Prüfung |
| `Run_WhenBeforeDownloadCanceled_DoesNotDownload` | `AutoUpdateOrchestratorEventTests` | Abbruch vor dem Download |
| `Run_WhenBeforeInstallCanceled_DoesNotInstall` | `AutoUpdateOrchestratorEventTests` | Abbruch vor der Installation |
| `Run_WhenBeforeStartUpdateScriptCanceled_ReleasesLock` | `AutoUpdateOrchestratorEventTests` | Abbruch vor dem Skriptstart gibt die Sperre frei |
| `Run_RaisesEventsInDocumentedOrder` | `AutoUpdateOrchestratorEventTests` | Reihenfolge aller Ereignisse im Vollworkflow |
| `Commands_DelegateToOrchestrator` | `AutoUpdateCommandServiceTests` | Keine eigene Logik im Command-Service |
| `Commands_UpdateStatusService` | `AutoUpdateCommandServiceTests` | Statusaktualisierung nach jedem Befehl |
| `Commands_ParallelCalls_AreSerialized` | `AutoUpdateCommandServiceTests` | Thread-Sicherheit |
| `Check_ReadsManifestFromFolder` | `AutoUpdateLocalFolderSourceTests` | Manifest-Auswertung |
| `Check_WhenFolderMissing_ReturnsNoUpdate` | `AutoUpdateLocalFolderSourceTests` | Fehlendes Verzeichnis ohne Ausnahme |
| `Download_CopiesPackageToTarget` | `AutoUpdateLocalFolderSourceTests` | Kopiervorgang |
| `Create_WithEmptyOwner_Throws` | `AutoUpdateGithubSourceTests` | Argumentprüfung der Factory |
| `Check_ParsesManifestResponse` | `AutoUpdateGithubSourceTests` | Manifest-Deserialisierung über einen Stub-`HttpMessageHandler` |
| `Download_WhenResponseExceedsLimit_Throws` | `AutoUpdateGithubSourceTests` | Größenlimit während des Streamens |
| `Download_WhenHttpFails_Throws` | `AutoUpdateGithubSourceTests` | Fehlerhafte HTTP-Antwort |
| `IsWithinWindow_WithoutRanges_AlwaysTrue` | `SourceCheckWindowEvaluatorTests` | Leere Zeitfensterliste |
| `IsWithinWindow_InsideRange_ReturnsTrue` | `SourceCheckWindowEvaluatorTests` | Treffer innerhalb eines Fensters |
| `IsWithinWindow_WrongDayOfWeek_ReturnsFalse` | `SourceCheckWindowEvaluatorTests` | Wochentagsprüfung |
| `IsWithinWindow_OutsideRange_ReturnsFalse` | `SourceCheckWindowEvaluatorTests` | Zeit außerhalb des Fensters |
| `Execute_TriggersCheckOnlyWithinWindow` | `AutoUpdateCheckerServiceTests` | Zeitfenster werden respektiert |
| `Execute_NeverTriggersDownloadOrInstall` | `AutoUpdateCheckerServiceTests` | Hintergrunddienst löst nur die Prüfung aus |
| `Execute_WhenCheckThrows_ContinuesLoop` | `AutoUpdateCheckerServiceTests` | Resilienz |
| `Execute_RespectsConfiguredInterval` | `AutoUpdateCheckerServiceTests` | Intervall über `FakeTimeProvider` |
| `Execute_AtScheduledTime_TriggersInstall` | `AutoUpdateSchedulerServiceTests` | Geplante Installation |
| `Execute_WhenNotReady_DoesNotInstall` | `AutoUpdateSchedulerServiceTests` | Vorbedingung `ReadyToInstall` |
| `Execute_SameScheduleTwice_InstallsOnce` | `AutoUpdateSchedulerServiceTests` | Keine Mehrfachauslösung |
| `IsNewerVersion_*` | `AutoUpdatePackageValidatorTests` | SemVer-Vergleich inkl. unbekannter installierter Version |
| `ValidateDownloadedPackageAsync_*` | `AutoUpdatePackageValidatorTests` | Prüfsumme, Größe, ZIP-Integrität |
| `Generate_OnWindows_WritesPowerShellScript` | `AutoUpdateScriptGeneratorTests` | Windows-Skript |
| `Generate_OnLinux_WritesShellScriptWithUnixLineEndings` | `AutoUpdateScriptGeneratorTests` | Linux-Skript |
| `Generate_WithoutTarget_Throws` | `AutoUpdateScriptGeneratorTests` | Fehlendes Installationsziel |
| `SelectPackage_MatchesRuntimeIdentifier` | `AutoUpdatePlatformResolverTests` | Paketauswahl |
| `Resolve_*` | `AutoUpdateServiceResolverTests` | Auflösung von Dienstname/Ausführbarer Datei |
| `Lock_*`, `PendingPath_*` | `FileSystemAutoUpdatePackageStoreTests` | Sperrdatei, Pfadsicherheit, Verzeichnisanlage |
| `Read/Write_RoundTrips` | `FileSystemAutoUpdateStateStoreTests` | Atomare Persistenz |
| `Adapter_MapsSnapshotToUpdateStatusDto` | `UpdateOrchestratorAdapterTests` (`FinanceManager.Tests/Updates/`) | Vollständiges Mapping aller Statusfelder |
| `Adapter_MapsFailedResultToExpectedException` | `UpdateOrchestratorAdapterTests` (`FinanceManager.Tests/Updates/`) | Fehlerergebnis → `FileNotFoundException`/`IOException`/`ArgumentException` für die Controller-Statuscodes |
| `Adapter_SaveSettings_AppliesToAutoUpdateOptions` | `UpdateOrchestratorAdapterTests` (`FinanceManager.Tests/Updates/`) | Setup-UI-Änderungen wirken zur Laufzeit |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `FinanceManager.Tests/Updates/UpdateOrchestratorTests` | Zielklasse entfällt; Inhalte gehen in die Orchestrator-Testklassen der Bibliothek über |
| `FinanceManager.Tests/Updates/UpdateExecutorTests` | `UpdateExecutor` entfällt; ersetzt durch `AutoUpdateOrchestratorInstallTests` |
| `FinanceManager.Tests/Updates/UpdateFileStoreTests` | `UpdateFileStore` entfällt; ersetzt durch `FileSystemAutoUpdatePackageStoreTests` |
| `FinanceManager.Tests/Updates/UpdateValidatorTests` | `UpdateValidator` entfällt; ersetzt durch `AutoUpdatePackageValidatorTests` |
| `FinanceManager.Tests/Updates/UpdateScriptGeneratorTests` | `UpdateScriptGenerator` entfällt; ersetzt durch `AutoUpdateScriptGeneratorTests` |
| `FinanceManager.Tests/Updates/UpdatePlatformResolverTests` | Zielklasse entfällt; ersetzt durch `AutoUpdatePlatformResolverTests` |
| `FinanceManager.Tests/Updates/UpdateServiceResolverTests` | Zielklasse entfällt; ersetzt durch `AutoUpdateServiceResolverTests` |
| `FinanceManager.Tests/Updates/UpdateSchedulerTests` | `UpdateScheduler` entfällt; ersetzt durch `AutoUpdateSchedulerServiceTests` |
| `FinanceManager.Tests/Updates/UpdateSettingsStoreTests` | `UpdateSettingsStore` nutzt statt `IUpdateFileStore`/`IOptions<UpdateOptions>` künftig `IAutoUpdatePackageStore` und `AutoUpdateOptions` |
| `FinanceManager.Tests/Updates/InstalledReleaseMetadataProviderTests` | Provider delegiert an `IInstalledVersionProvider` statt selbst zu lesen |
| `FinanceManager.Tests/Updates/TestWebHostEnvironment` | Wird nur noch von den verbliebenen Web-Tests benötigt; Rest wandert als `TestAutoUpdateEnvironment` in die Bibliothekstests |
| `FinanceManager.Tests/Updates/UpdateStatusTestData` | Muss zusätzlich Snapshots der Bibliothek für die Adaptertests bereitstellen |
| `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests` | Ersetzt heute `IUpdateOrchestrator` im Testcontainer; Testumgebung muss auf lokale Quelle und deaktivierte Hintergrunddienste umgestellt werden |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Admin öffnet Setup → Update-Tab und sieht Status und Einstellungen aus der neuen Bibliothek | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` | Abschnitt 7: Status ist jederzeit über den Status-Service abfragbar |
| Admin löst die Prüfung manuell aus und erhält ein Ergebnis (lokale Ordnerquelle mit bereitgestellter neuer Version) | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` | Abschnitte 8/9: manuelle Auslösung über den Command-Service; Abschnitt 2.3: Standard-/Ordnerquelle |
| Admin speichert geänderte Update-Einstellungen und sieht sie nach dem Neuladen | `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` | Abschnitt 3: Optionen zur Laufzeit änderbar |

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture` | Muss die neuen `Updates__*`-Umgebungsvariablen setzen, damit der Testserver eine deterministische lokale Quelle nutzt und keine GitHub-Anfragen stellt |
| `FinanceManager.Tests.E2E/Tests/Version/VersionDisplayPlaywrightTests` | Keine Codeänderung, aber Regressionsnachweis für die umgestellte Ermittlung der installierten Version — muss weiterhin grün sein |

---

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Paketname und Root-Namespace der Bibliothek sind nicht festgelegt (`AutoUpdate` vs. `ProgramUpdate`, Herstellerpräfix) | `SoftwareSchmiede.AutoUpdate` als Projekt-, Assembly-, Paket- und Namespace-Name verwenden — passt zum in den Anforderungen durchgängig genutzten `AutoUpdate*`-Vokabular und ist als Paket-Id eindeutig belegbar. Die Entscheidung sollte vor Schritt 1 fallen, danach ist die Umbenennung teuer |
| 2 | Veröffentlichungsziel und Zeitpunkt für das NuGet-Paket sind offen (nuget.org vs. GitHub Packages, ab welchem Reifegrad) | In diesem Vorhaben nur die Paketierung vorbereiten (`.csproj`-Metadaten, `dotnet pack` lauffähig, lokaler Paketordner als Testziel). Veröffentlichung als eigene Aufgabe nach der ersten produktiven Nutzung durch FinanceManager |
| 3 | Versionierung der Bibliothek (eigener SemVer-Strang oder Mitführen der FinanceManager-Version) | Eigener SemVer-Strang für die Bibliothek, beginnend bei `0.1.0`, gepflegt über die `.csproj`-Eigenschaft `Version`; die bestehende `release.config.js`-Automatik bleibt der FinanceManager-Anwendung vorbehalten |
| 4 | macOS-Unterstützung ist weder vorhanden noch beauftragt | Nicht umsetzen. Windows und Linux beibehalten, die Einschränkung im README der Bibliothek dokumentieren und `AutoUpdateScriptGenerator` weiterhin mit einer klaren Ausnahme abbrechen lassen |
| 5 | Skriptformate: heute Windows `.ps1` und Linux `.sh`; ob zusätzlich `.bat` unterstützt werden soll, ist offen | Kein `.bat`. Das bestehende PowerShell-Skript deckt Dienst- und Prozessstart ab; ein zweites Windows-Format verdoppelt die Testmatrix ohne fachlichen Gewinn |
| 6 | Manifestformat der `AutoUpdateLocalFolderSource` ist nicht spezifiziert | Dasselbe JSON-Schema wie bei der GitHub-Quelle verwenden (`update.json` mit Version, Release-Notes, Paketliste). Damit lässt sich ein GitHub-Release unverändert in ein lokales Verzeichnis kopieren und als Quelle nutzen — und der E2E-Test wird trivial aufsetzbar |
| 7 | Umgang mit einer `status.json` aus einer Vorgängerversion beim ersten Start der neuen Fassung | Wie im Plan beschrieben tolerant auf `Idle` zurückfallen. Zusätzlich beim Erkennen eines fremden Schemas eine Warnung loggen, damit der Fall im Betrieb nachvollziehbar bleibt |

