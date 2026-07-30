# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Von 123 Planaufgaben sind 120 vollständig umgesetzt, 1 teilweise, 2 offen. Die Projektmappe baut fehlerfrei
(0 Fehler), `dotnet pack` erzeugt `SoftwareSchmiede.AutoUpdate.0.1.0.nupkg`, und alle betroffenen Testsuiten sind
grün: 79/79 Bibliothekstests, 29/29 Update-Tests in `FinanceManager.Tests`, 19/19 Update-Integrationstests.

> **Hinweis zur Ablage:** Die Projekte liegen im Repository-Wurzelverzeichnis, nicht unter `src/`. Alle
> Pfadangaben dieses Reviews beziehen sich auf die tatsächliche Struktur.

---

## Umgesetzte Planelemente

### Projektstruktur

- [x] `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj` — angelegt (net10.0, `Nullable`,
      `ImplicitUsings`, `GenerateDocumentationFile`, `WarningsAsErrors` inkl. `CS1591` in Debug und Release)
- [x] NuGet-Metadaten — `PackageId`, `Version` (0.1.0), `Description`, `Authors`, `PackageLicenseExpression` (MIT),
      `RepositoryUrl` vorhanden; `dotnet pack` läuft durch
- [x] Alle sechs geplanten Paketreferenzen (`Hosting.Abstractions`, `Options`, `Logging.Abstractions`, `Http`,
      `DependencyInjection.Abstractions`, `Configuration.Binder`) — vorhanden, keine `FrameworkReference` auf
      `Microsoft.AspNetCore.App`
- [x] `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.csproj` — angelegt
- [x] Beide Projekte in `FinanceManager.sln` eingetragen

### Datenmodell

- [x] `AutoUpdateState` (Enum) — alle 9 Werte: Idle, Checking, UpdateAvailable, Downloading, ReadyToInstall,
      Installing, Success, Failed, Disabled
- [x] `AutoUpdateOutcome` (Enum) — alle 5 Werte: Success, NoUpdate, Skipped, Canceled, Failed
- [x] `AutoUpdateResult` (record) — Outcome, State, Message, Error
- [x] `AutoUpdateCheckResult` (record) — AvailableVersion, Package, ReleaseNotes, PublishedAt
- [x] `AutoUpdateDownloadResult` (record) — LocalPath, SizeBytes, ChecksumValid
- [x] `AutoUpdateInstallResult` (record) — Version, ScriptPath, StartedAt
- [x] `AutoUpdateStatusSnapshot` (record) — alle 10 Planfelder: State, InstalledVersion, AvailableVersion,
      LastCheckedAt, LastCheckResult, LastDownloadResult, LastInstallResult, LastError, IsLocked, LockCreatedAt
- [x] `AutoUpdatePackageDescriptor` (record) — Version, Platform, RuntimeIdentifier, FileName, Uri, Sha256, SizeBytes
- [x] `AutoUpdateReleaseInfo` (record) — Version, ReleaseNotes, PublishedAt, Packages
- [x] `InstalledReleaseInfo` (record) — Version, PublishedAt, CommitSha, Repository, RuntimeIdentifier
- [x] `AutoUpdateInstallationTarget` (record) — Platform, ServiceName, ExecutablePath

### Konfiguration

- [x] `SourceCheckTimeRange` — DayOfWeek, StartTime, EndTime
- [x] `SourceCheckOptions` — Interval (Standard 360), TimeRanges
- [x] `AutoUpdateOptions` — alle 13 Planfelder vorhanden (Enabled, EnableAutomaticDownload, DownloadPath,
      EnableAutomaticInstallation, Source, SourceCheck, MaxAssetBytes, HostedServicesEnabled, ScheduledInstallTime,
      ServiceName, ExecutablePath, StopHostAfterScriptStart, HealthTimeoutSeconds); Standardwert `DownloadPath` = `updates`
- [x] `AutoUpdateBuilder` — alle 8 Fluent-Methoden: `EnableAutomaticDownload`, `EnableAutomaticInstallation`,
      `UseSource`, `UseGithubSource`, `UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`,
      `DisableHostedServices`
- [x] `AutoUpdateHostBuilderExtensions.UseAutoUpdate(this IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` —
      vorhanden, keine `WebApplicationBuilder`-Bindung
- [x] `UpdateOptions` (Web) erweitert um `SourceType` (Standard `Github`), `LocalFolderPath`,
      `EnableAutomaticDownload`, `EnableAutomaticInstallation`
- [x] Sektion `Updates` in `appsettings.json` — alle 7 neuen Einträge vorhanden (`SourceType`, `LocalFolderPath`,
      `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `SourceCheck:Interval`, `SourceCheck:TimeRanges`,
      `StopHostAfterScriptStart`); Bestandswerte unverändert

### Schnittstellen

Alle 17 geplanten Interfaces sind mit den geplanten Membern vorhanden:

- [x] `IAutoUpdateSource` — `CheckAsync`, `DownloadAsync` (zustandslos, keine `CurrentVersion`/`AvailableVersion`-Properties)
- [x] `IAutoUpdateEnvironment` — `ApplicationDirectory`
- [x] `IInstalledVersionProvider` — `GetAsync`
- [x] `IAutoUpdatePackageStore` — RootDirectory, PendingDirectory, StagingDirectory, LockPath, LogPath,
      `ScriptPath`, `PendingAssetPath`, `EnsureAsync`, `GetLockCreatedAtAsync`, `TryCreateLockAsync`, `DeleteLockAsync`
- [x] `IAutoUpdateStateStore` — `ReadAsync`, `WriteAsync`
- [x] `IAutoUpdatePackageValidator` — `IsNewerVersion`, `ValidateReleaseAsync`, `ValidateDownloadedPackageAsync`
- [x] `IAutoUpdateScriptGenerator`, `IAutoUpdateProcessRunner`, `IAutoUpdateHostTerminator`
- [x] `IAutoUpdatePlatformResolver`, `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe`
- [x] `IAutoUpdateInstaller` — `PrepareAsync`, `StartAsync`
- [x] `IAutoUpdateStatusProvider` — `GetSnapshot`
- [x] `IAutoUpdateEventAggregator` — 6 Events + 6 Raise-Methoden
- [x] `IAutoUpdateOrchestrator` — `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync`, `GetStatusAsync`
- [x] `IAutoUpdateCommandHandler` — `CheckAsync`, `DownloadAsync`, `InstallAsync(bool confirmDowntime, …)`

### Events

- [x] `AutoUpdateCancelEventArgs` — Basisklasse mit `Cancel`
- [x] `BeforeDownloadEventArgs` — `Uri SourceUri`
- [x] `BeforeInstallEventArgs` — `FileInfo PackageFile`
- [x] `BeforeStartUpdateScriptEventArgs` — `FileInfo ScriptFile`
- [x] `AutoUpdateErrorEventArgs` — `Exception Error`, `string Phase`
- [x] `AutoUpdateEvents` — thread-sichere Abonnentenverwaltung über `Lock`, add/remove gesperrt
- [x] Handler-Ausnahmebehandlung — Ausnahme wird gefangen, über `RaiseErrorOccured` gemeldet, Ablauf läuft weiter;
      `ErrorOccured`-Abonnentenfehler werden verschluckt
- [x] Events `BeforeCheckSource`, `BeforeDownload`, `BeforeInstall`, `BeforeStartUpdateScript`,
      `AfterStartUpdateScript` (ohne Abbruchsemantik), `ErrorOccured`

### Logik und Dienste

- [x] `HostAutoUpdateEnvironment` — über `IHostEnvironment.ContentRootPath`
- [x] `JsonFileStore` — portiert
- [x] `FileSystemAutoUpdatePackageStore` — portiert, ohne `IWebHostEnvironment`; Layout `pending`/`staging`/
      `status.json`/`update.lock`/`update.log` erhalten
- [x] `FileSystemAutoUpdateStateStore` — atomare JSON-Persistenz mit tolerantem Fallback auf `Idle`
- [x] `AutoUpdatePackageValidator` — SemVer-Vergleich, SHA256, ZIP-Integrität, Größenlimit
- [x] `ReleaseMetadataInstalledVersionProvider`
- [x] `AutoUpdatePlatformResolver`, `DefaultAutoUpdateServiceProbe`, `AutoUpdateServiceResolver`
- [x] `AutoUpdateScriptGenerator` — Windows `.ps1`, Linux `.sh`
- [x] `DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateHostTerminator`
- [x] `AutoUpdateInstaller` — `PrepareAsync`, `StartAsync`, ohne Ereignisauslösung
- [x] `AutoUpdateStatusService` — Snapshot hinter `lock`, Persistenz über `IAutoUpdateStateStore`, verzögertes Laden
      (`EnsureLoadedAsync`)
- [x] `SourceCheckWindowEvaluator.IsWithinWindow(DateTimeOffset)` — leere `TimeRanges` = immer erlaubt
- [x] `AutoUpdateLocalFolderSource` — Standardquelle
- [x] `AutoUpdateGithubSource` inkl. statischer Factory `Create(repositoryName, repositoryOwner)`
- [x] `AutoUpdateOrchestrator` — alle 5 Interface-Methoden; Ereignisreihenfolge, Abbruchbehandlung,
      Sperrverwaltung, Zustandsabgleich nach Neustart (`ReconcileAfterRestartAsync`)
- [x] Zentrale Fehlerbehandlung — `FailAsync` meldet über `RaiseErrorOccured`, setzt Zustand `Failed` und
      `LastError`, liefert `Outcome.Failed`; keine Ausnahme verlässt den Orchestrator
- [x] Serialisierung paralleler Aufrufe — internes `SemaphoreSlim(1,1)` in allen fünf öffentlichen Methoden
- [x] `AutoUpdateCommandService` — dünne Fassade ohne eigene Update-Logik
- [x] `AutoUpdateCheckerService` — Intervall + Zeitfensterprüfung, ruft ausschließlich `CheckForUpdateAsync`
- [x] `AutoUpdateSchedulerService` — geplante Installation über den Command-Service

### Registrierung

- [x] `UseAutoUpdate` registriert `AutoUpdateOptions` als Singleton-Instanz und `IValidateOptions<AutoUpdateOptions>`
- [x] Alle Standardimplementierungen per `TryAddSingleton` registriert
- [x] Standardquelle `AutoUpdateLocalFolderSource` wird gesetzt, wenn keine Quelle konfiguriert ist
- [x] Hosted Services nur bei `HostedServicesEnabled`
- [x] `TimeProvider` nur per `TryAddSingleton` — keine Doppelregistrierung gegenüber `ProgramExtensions`
- [x] `ProgramExtensions` — Self-update-Block durch einen einzigen `builder.UseAutoUpdate(cfg => …)`-Aufruf ersetzt;
      Sektion `Updates` gebunden, `SourceType` steuert Github- vs. LocalFolder-Quelle
- [x] `IUpdateOrchestrator` → `UpdateOrchestratorAdapter` (Scoped), `IUpdateSettingsStore` → `UpdateSettingsStore`
      (Singleton), `IInstalledReleaseMetadataProvider` → `InstalledReleaseMetadataProvider` (Singleton)

### Validierungsregeln

- [x] `AutoUpdateOptionsValidator` (`IValidateOptions<AutoUpdateOptions>`) — implementiert alle Planregeln:
      `DownloadPath` nicht leer/keine ungültigen Zeichen, `Source` nicht `null`, `MaxAssetBytes > 0`,
      `SourceCheck.Interval >= 1`, `StartTime < EndTime`, `HealthTimeoutSeconds` auf 10–600 geklemmt
- [x] Pfadsicherheit für `AutoUpdatePackageDescriptor.FileName` — in `FileSystemAutoUpdatePackageStore`
- [x] Bestätigungspflicht `confirmDowntime` — `InstallAsync` liefert ohne Bestätigung `Outcome.Failed` mit
      `ArgumentException` als `Error`
- [x] `AutoUpdateGithubSource.Create` prüft Owner und Name

### Adapterschicht im Web-Projekt

- [x] `UpdateOrchestratorAdapter` (`FinanceManager.Web/Services/Updates/`) — implementiert alle 7
      `IUpdateOrchestrator`-Methoden, mappt Snapshot auf `UpdateStatusDto`/`UpdateCheckResultDto`
- [x] Fehlerergebnis-Mapping — `result.Error` wird in `StartInstallAsync` erneut geworfen; der Orchestrator
      erzeugt gezielt `FileNotFoundException` (kein Paket bereit), `IOException` (Sperre aktiv) und
      `ArgumentException` (fehlende Bestätigung), sodass das Statuscode-Mapping des Controllers greift
- [x] `AutoUpdateOptionsMapper` (statische Klasse) — angelegt
- [x] `UpdateSettingsStore` — nutzt `IAutoUpdatePackageStore` und `AutoUpdateOptions`, neue Methode
      `ApplyToOptionsAsync`; Legacy-Migration (`windowsServiceName`/`linuxServiceName`) erhalten
- [x] `InstalledReleaseMetadataProvider` — delegiert an `IInstalledVersionProvider`, `IWebHostEnvironment` entfällt
- [x] `UpdateController`, `SetupUpdateViewModel`, `SetupUpdateTab.razor` — unverändert, wie geplant

### Aufräumen

- [x] Gelöscht: `UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `UpdateValidator`,
      `UpdateScriptGenerator`, `UpdatePlatformResolver`, `UpdateServiceResolver`, `DefaultUpdateProcessRunner`,
      `DefaultUpdateHostTerminator`, `UpdateManifestClient`, `UpdateChecker`, `UpdateScheduler`, `JsonFileStore`
- [x] `UpdateContracts.cs` auf `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator`
      reduziert; `record UpdateInstallationTarget` entfernt

### Tests

- [x] Testhilfen `FakeAutoUpdateSource`, `TestAutoUpdateEnvironment`, `AutoUpdateTestContext` — alle drei unter
      `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/`
- [x] Alle 21 geplanten Bibliotheks-Testklassen vorhanden, alle namentlich geplanten Testmethoden umgesetzt:
      `UseAutoUpdateRegistrationTests` (4), `AutoUpdateBuilderTests` (3), `AutoUpdateOptionsValidationTests` (4),
      `AutoUpdateEventsTests` (4), `AutoUpdateStatusServiceTests` (4), `AutoUpdateOrchestratorCheckTests` (4),
      `AutoUpdateOrchestratorDownloadTests` (4), `AutoUpdateOrchestratorInstallTests` (8),
      `AutoUpdateOrchestratorEventTests` (5), `AutoUpdateCommandServiceTests` (3),
      `AutoUpdateLocalFolderSourceTests` (3), `AutoUpdateGithubSourceTests` (4),
      `SourceCheckWindowEvaluatorTests` (4), `AutoUpdateCheckerServiceTests` (4),
      `AutoUpdateSchedulerServiceTests` (3), `AutoUpdatePackageValidatorTests` (4),
      `AutoUpdateScriptGeneratorTests` (3), `AutoUpdatePlatformResolverTests` (1),
      `AutoUpdateServiceResolverTests` (3), `FileSystemAutoUpdatePackageStoreTests` (3),
      `FileSystemAutoUpdateStateStoreTests` (1) — **79 Tests, alle grün**
- [x] `UpdateOrchestratorAdapterTests` in `FinanceManager.Tests/Updates/` — alle drei geplanten Testmethoden
- [x] Verschobene Testklassen aus `FinanceManager.Tests/Updates/` entfernt (8 Dateien)
- [x] `UpdateSettingsStoreTests`, `InstalledReleaseMetadataProviderTests`, `UpdateStatusTestData`,
      `TestWebHostEnvironment` angepasst — 29 Tests grün
- [x] `UpdateControllerIntegrationTests` und `TestWebApplicationFactory` auf lokale Ordnerquelle
      (`Updates:SourceType=LocalFolder`, `Updates:LocalFolderPath`) und `Updates:HostedServicesEnabled=false`
      umgestellt — 19 Tests grün, keine GitHub-Anfragen

### E2E-Tests

- [x] `PlaywrightWebAppFixture` — alle geplanten Umgebungsvariablen gesetzt (`Updates__SourceType=LocalFolder`,
      `Updates__LocalFolderPath`, `Updates__EnableAutomaticInstallation=false`,
      `Updates__HostedServicesEnabled=false`, `Updates__WorkingDirectory`; zusätzlich `Updates__Enabled=true`)
- [x] `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs` — Test-Gateway für den Setup-Update-Tab
- [x] `UpdateSetupPlaywrightTests` mit allen drei geplanten Szenarien: `Admin_OpensUpdateTab_ShowsStatus`,
      `Admin_TriggersCheck_ShowsAvailableUpdate`, `Admin_SavesSettings_PersistsAcrossReload`

### Dokumentation und Paketierung

- [x] `SoftwareSchmiede.AutoUpdate/README.md` — deckt alle geplanten Abschnitte ab: Getting started (`UseAutoUpdate`),
      Configuration, Update sources (eigene Quellen), Events, Status and manual control, Background services,
      Supported platforms (macOS-Einschränkung dokumentiert), Testing
- [x] `CHANGELOG.md` — Eintrag zur Auslagerung inkl. neuer Konfigurationseinträge und macOS-Hinweis
- [x] `dotnet pack` — erzeugt `SoftwareSchmiede.AutoUpdate.0.1.0.nupkg` fehlerfrei

---

## Offene Aufgaben

- [ ] **`Docs/help` aktualisieren (Plan Schritt 23, Aufgabe 122)** — fehlt vollständig: Keine Hilfeseite erwähnt
      die neue Bibliothek, `UseAutoUpdate` oder die neuen Konfigurationseinträge (`SourceType`, `LocalFolderPath`,
      `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `SourceCheck:*`, `StopHostAfterScriptStart`).
      Betroffen wären insbesondere `Docs/help/systemverwaltung-und-setup/beschreibung.md`, `business-rules.md`,
      `ablauf-technisch.md` und `api.md`. Die Änderung an
      `FinanceManager.Web/wwwroot/help/help-assets.sha256` ist **kein** Nachweis dafür: die Prüfsummen wurden nur
      an bereits zuvor committete Inhalte angeglichen (verifiziert — die vier geänderten Hashes entsprechen exakt
      dem unveränderten Dateiinhalt), die Markdown-Dateien selbst sind laut `git status` unverändert.

- [ ] **`AutoUpdateOptionsMapper` — Rückrichtung (Aufgabe 74)** — teilweise umgesetzt: Der Plan fordert
      „überträgt `UpdateSettingsDto` in die Singleton-`AutoUpdateOptions` **und zurück**". Implementiert ist nur
      `ApplySettings(AutoUpdateOptions, UpdateSettingsDto)` (DTO → Options). Eine Methode Options → DTO fehlt.
      Praktische Auswirkung derzeit gering, da `UpdateSettingsStore.GetAsync` die Werte aus der JSON-Persistenz
      und den `UpdateOptions`-Standardwerten aufbaut und die Rückrichtung nirgends aufgerufen wird.

- [ ] **`VersionDisplayPlaywrightTests` als Regressionsnachweis (Aufgabe 119)** — nicht nachgewiesen: Der Test
      existiert unverändert, wurde in diesem Review aber nicht ausgeführt (Playwright-Browserumgebung und
      laufender Testserver erforderlich). Der Nachweis ist mit dem regulären E2E-Lauf zu erbringen.

---

## Hinweise

1. **Implementierung ist noch nicht committet.** Der gesamte Umbau liegt ausschließlich als Arbeitsstand im
   Working Tree (`git status`: 21 geänderte, 21 gelöschte, 7 neue Pfade). Der letzte Commit auf dem Branch
   (`9959c9f plan: …`) enthält nur die Planungsdokumente. Auch die Aufgabendatei
   `Docs/features/task/issue-230-…-tasks.md` ist unversioniert.

2. **Abweichung `AutoUpdateCancelEventArgs`.** Die Designentscheidungen des Plans nennen „von `CancelEventArgs`
   abgeleitete Argumentklassen". Implementiert ist eine eigene Basisklasse `AutoUpdateCancelEventArgs : EventArgs`
   mit eigener `Cancel`-Eigenschaft statt `System.ComponentModel.CancelEventArgs`. Funktional gleichwertig; die
   Abbruchsemantik ist über `AutoUpdateEventsTests.Raise_BeforeCheckSource_HonorsCancel` abgesichert.

3. **Abweichung Standardquelle.** Der Plan setzt die Standardquelle im `AutoUpdateBuilder` (Ablauf „Registrierung
   beim Start", Schritt 3). Implementiert wird sie in `AutoUpdateHostBuilderExtensions.UseAutoUpdate`, nachdem die
   Konfiguration gebunden wurde. Das ist die robustere Reihenfolge (die Konfiguration kann die Quelle nicht
   überschreiben) und durch `UseAutoUpdate_WithoutSource_UsesLocalFolderSource` abgedeckt.

4. **`IAutoUpdateInstaller.StartAsync` ist synchron** (`void StartAsync(string scriptPath)`). Der Plan nennt nur
   „`StartAsync` (Skript starten)" ohne Rückgabetyp; die Benennung mit `Async`-Suffix bei synchroner Signatur ist
   irreführend und ein Kandidat für das Code-Review.

5. **Generierte Dokumentationsdatei im Projektverzeichnis.** `<DocumentationFile>SoftwareSchmiede.AutoUpdate.xml
   </DocumentationFile>` ist relativ gesetzt, wodurch `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.xml`
   im Projektwurzelverzeichnis landet. Die Datei ist nicht durch `.gitignore` abgedeckt und würde mit
   eingecheckt. Empfehlung: `DocumentationFile` entfernen (`GenerateDocumentationFile` genügt) oder die Datei
   ignorieren.

6. **Abbruchstimme bei fehlerhaftem Handler.** `AutoUpdateEvents.RaiseCancelable` verwendet eine gemeinsame
   `EventArgs`-Instanz für alle Abonnenten. Setzt ein Handler `Cancel = true` und wirft anschließend eine
   Ausnahme, bleibt sein Abbruchvotum wirksam — der Plan fordert, dass die Stimme eines fehlgeschlagenen
   Handlers nicht zählt. Randfall ohne Testabdeckung; Thema für das Code-Review, kein fehlendes Planelement.

7. **Reihenfolge der offenen Aufgaben.** Aufgabe 122 (`Docs/help`) ist im Lebenszyklus ohnehin als eigener
   Schritt 9 vorgesehen (siehe `todo.md`) und blockiert das Code-Review nicht. Aufgabe 74 (Mapper-Rückrichtung)
   ist unabhängig nachziehbar. Aufgabe 119 erfordert lediglich einen E2E-Testlauf.
