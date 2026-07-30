# Plan-Review

Geprüft am 2026-07-30 (fünfter Durchlauf) gegen den Arbeitsstand des Branches
`task/issue-230-...-programmupdate-als-komponenten` inklusive der noch nicht committeten Änderungen.

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Sanity-Check: `dotnet build FinanceManager.sln -c Debug` → 0 Fehler, 60 Warnungen (sämtlich Vorbestand).
`dotnet test SoftwareSchmiede.AutoUpdate.Tests` → 103 erfolgreich, 1 plattformbedingt übersprungen (Linux-Skript).
`dotnet test FinanceManager.Tests --filter Updates` → 39 erfolgreich.

123 von 126 Planelementen sind vollständig umgesetzt. Drei Punkte bleiben offen; alle drei sind
funktional folgenlos, weichen aber vom Wortlaut des Plans ab.

## Umgesetzte Planelemente

### Projekt- und Paketstruktur

- [x] `SoftwareSchmiede.AutoUpdate` (Projekt) — angelegt, net10.0, `Nullable`, `ImplicitUsings`,
      `GenerateDocumentationFile`, `WarningsAsErrors` inkl. `CS1591` in Debug und Release
- [x] NuGet-Metadaten — `PackageId`, `Version` 0.1.0, `Description`, `Authors`,
      `PackageLicenseExpression`, `RepositoryUrl`, `PackageReadmeFile` vorhanden
- [x] Alle sechs geplanten Paketreferenzen (`Hosting.Abstractions`, `Options`,
      `Logging.Abstractions`, `Http`, `DependencyInjection.Abstractions`, `Configuration.Binder`) — gesetzt
- [x] `SoftwareSchmiede.AutoUpdate` und `SoftwareSchmiede.AutoUpdate.Tests` in `FinanceManager.sln` — eingetragen

### Registrierung und Konfiguration

- [x] `AutoUpdateHostBuilderExtensions` (statische Klasse) mit `UseAutoUpdate(this IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` — vorhanden, keine ASP.NET-Referenz
- [x] `AutoUpdateBuilder` (Klasse) — alle acht geplanten Fluent-Methoden vorhanden
      (`EnableAutomaticDownload`, `EnableAutomaticInstallation`, `UseSource`, `UseGithubSource`,
      `UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices`)
- [x] `AutoUpdateOptions` (Konfigurationsklasse) — alle 13 geplanten Eigenschaften vorhanden
- [x] `SourceCheckOptions` (`Interval`, `TimeRanges`), `SourceCheckTimeRange` (`DayOfWeek`, `StartTime`, `EndTime`) — vorhanden
- [x] `AutoUpdateOptionsValidator` (`IValidateOptions<AutoUpdateOptions>`) — implementiert, deckt alle
      fünf geplanten Startregeln ab; `HealthTimeoutSeconds` wird wie geplant geklemmt statt abgewiesen
- [x] Standardquelle `AutoUpdateLocalFolderSource`, wenn keine Quelle gesetzt ist — vorhanden
- [x] Registrierung aller Dienste per `TryAddSingleton`, Hosted Services nur bei `HostedServicesEnabled` — vorhanden
- [x] `TimeProvider` nur per `TryAddSingleton` (keine Doppelregistrierung gegenüber `ProgramExtensions`) — vorhanden

### Datenmodell und Zustand

- [x] `AutoUpdateState` (Enum) — alle neun Werte
- [x] `AutoUpdateOutcome` (Enum) — alle fünf Werte
- [x] `AutoUpdateStatusSnapshot` (record) — alle zehn geplanten Felder
- [x] `AutoUpdateResult`, `AutoUpdateCheckResult`, `AutoUpdateDownloadResult`, `AutoUpdateInstallResult` (records) — vorhanden
- [x] `AutoUpdatePackageDescriptor` (record) — alle sieben geplanten Felder
- [x] `AutoUpdateReleaseInfo`, `InstalledReleaseInfo`, `AutoUpdateInstallationTarget` (records) — vorhanden

### Schnittstellen

- [x] Alle 17 geplanten Kern-Schnittstellen vorhanden: `IAutoUpdateSource`, `IAutoUpdateEnvironment`,
      `IInstalledVersionProvider`, `IAutoUpdatePackageStore`, `IAutoUpdateStateStore`,
      `IAutoUpdatePackageValidator`, `IAutoUpdateScriptGenerator`, `IAutoUpdatePlatformResolver`,
      `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe`, `IAutoUpdateProcessRunner`,
      `IAutoUpdateHostTerminator`, `IAutoUpdateInstaller`, `IAutoUpdateStatusProvider`,
      `IAutoUpdateEventAggregator`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`
- [x] `IAutoUpdatePackageValidator` ohne `ValidateReleaseAsync` — entspricht Plan Zeile 166
- [x] `IAutoUpdateOrchestrator` — alle fünf Methoden (`RunUpdateAsync`, `CheckForUpdateAsync`,
      `DownloadAsync`, `InstallAsync`, `GetStatusAsync`)

### Ereignisse

- [x] `AutoUpdateCancelEventArgs`, `BeforeDownloadEventArgs` (`Uri SourceUri`),
      `BeforeInstallEventArgs` (`FileInfo PackageFile`), `BeforeStartUpdateScriptEventArgs`
      (`FileInfo ScriptFile`), `AutoUpdateErrorEventArgs` (`Exception`, Phase) — vorhanden
- [x] `AutoUpdateEvents` (Klasse) — thread-sichere Abonnentenverwaltung hinter `Lock`, alle sechs Ereignisse
- [x] Handler-Ausnahmen brechen den Ablauf nicht ab und zählen nicht als Abbruchstimme; jeder Abonnent
      erhält eine eigene Argumentinstanz — vorhanden
- [x] `AfterStartUpdateScript` ohne Abbruchsemantik — vorhanden

### Ablauflogik

- [x] `AutoUpdateOrchestrator` (Klasse) — Singleton-tauglich, `SemaphoreSlim` serialisiert
      Check/Download/Install/Status
- [x] Vollworkflow `RunUpdateAsync` — Reihenfolge entspricht dem Programmablauf: `Disabled`/`Skipped` bei
      `Enabled = false`, `BeforeCheckSource` mit Abbruch → `Idle`/`Canceled`, `Checking`,
      Versionsvergleich über `IsNewerVersion`, `NoUpdate` ohne neuere Version, `UpdateAvailable`,
      Abbruch bei deaktiviertem Download, `BeforeDownload`, `Downloading`, Validierung,
      `ReadyToInstall`, Abbruch bei deaktivierter Installation, `BeforeInstall`
- [x] Installation und Skriptstart — Sperre über `TryCreateLockAsync`, `PrepareAsync`,
      `BeforeStartUpdateScript` mit Sperrfreigabe bei Abbruch, Zustand `Installing` wird **vor** dem
      Skriptstart persistiert, `AfterStartUpdateScript`, `StopHostAfterScriptStart` → `StopApplication`
- [x] Zustandsabgleich nach Neustart in `GetStatusAsync` — `Installing` ohne Sperrdatei,
      Versionsgleichheit → `Success` mit geleerten Feldern, sonst `Failed` mit erklärender Meldung
- [x] Zentrale Fehlerbehandlung — jede Ausnahme wird gefangen, über `RaiseErrorOccurred` gemeldet, als
      `LastError` mit Zustand `Failed` abgelegt und als `Outcome.Failed` zurückgegeben; keine Ausnahme
      erreicht den Aufrufer
- [x] `AutoUpdateCommandService` (Klasse) — dünne Fassade ohne eigene Update-Logik
- [x] `AutoUpdateStatusService` (Klasse) — unveränderlicher Snapshot hinter `Lock`, serialisierte
      Schreibvorgänge, verzögertes Laden beim ersten Zugriff über `EnsureLoadedAsync`

### Persistenz, Plattform- und Installationsdienste

- [x] `HostAutoUpdateEnvironment`, `JsonFileStore`, `ReleaseMetadataInstalledVersionProvider` — vorhanden
- [x] `FileSystemAutoUpdatePackageStore` — Verzeichnislayout `pending`/`staging` sowie `update.lock`,
      `update.log` unter dem `DownloadPath`-Wurzelverzeichnis unverändert; kein `IWebHostEnvironment`
- [x] Pfadsicherheit `PendingAssetPath` — `InvalidOperationException` bei Pfadsegmenten im Dateinamen
- [x] `FileSystemAutoUpdateStateStore` — atomares Schreiben nach `status.json`; unlesbare oder
      fremdformatige Dateien führen zu `null` und damit zu einem frischen `Idle`-Zustand, zusätzlich
      Warnung im Log (Offener Punkt 7 des Plans)
- [x] `AutoUpdatePackageValidator`, `AutoUpdatePlatformResolver`, `AutoUpdateServiceResolver`,
      `DefaultAutoUpdateServiceProbe`, `AutoUpdateScriptGenerator`, `DefaultAutoUpdateProcessRunner`,
      `DefaultAutoUpdateHostTerminator`, `AutoUpdateInstaller` — portiert
- [x] Installationsziel-Regeln — Windows `ServiceName` oder `ExecutablePath`, Linux `ServiceName`,
      sonst `InvalidOperationException`; andere Plattformen brechen mit „Unsupported platform for self update" ab

### Hintergrunddienste

- [x] `SourceCheckWindowEvaluator.IsWithinWindow` — leere `TimeRanges` bedeuten „immer erlaubt",
      Wochentag und Zeitfenster werden geprüft
- [x] `AutoUpdateCheckerService` (Hosted Service) — liest die Optionen bei jedem Durchlauf frisch,
      respektiert Zeitfenster und `Enabled`, wartet `SourceCheck.Interval`, fängt Ausnahmen ab und
      versucht es nach einer festen Rückfallwartezeit erneut; durchgängig `TimeProvider`
- [x] `AutoUpdateSchedulerService` (Hosted Service) — minütliche Prüfung, Auslösung nur bei
      `ReadyToInstall` ohne aktive Sperre und erreichter Uhrzeit, Merken des letzten Termins gegen
      Mehrfachauslösung, `InstallAsync(confirmDowntime: true)`

### Anbindung in `FinanceManager.Web`

- [x] `UpdateOrchestratorAdapter` (Klasse) — implementiert `IUpdateOrchestrator` unverändert und mappt
      Snapshot und Ergebnisobjekte auf `UpdateStatusDto`/`UpdateCheckResultDto`
- [x] Fehlerergebnis-Mapping — `result.Error` wird erneut geworfen, damit das bestehende
      Ausnahme-Mapping des `UpdateController` (404/409/400) greift
- [x] `AutoUpdateOptionsMapper` (statische Klasse) — überträgt `UpdateSettingsDto` in die
      Singleton-`AutoUpdateOptions` und zurück
- [x] `UpdateSettingsStore` — nutzt `IAutoUpdatePackageStore` für den Pfad der `settings.json` und
      `AutoUpdateOptions` für Standardwerte; `ApplyToOptions` ergänzt; Legacy-Migration
      (`windowsServiceName`/`linuxServiceName` → `ServiceName`) unverändert erhalten
- [x] `InstalledReleaseMetadataProvider` — delegiert an `IInstalledVersionProvider` und mappt auf
      `InstalledReleaseMetadataDto`; keine `IWebHostEnvironment`-Abhängigkeit mehr
- [x] `ProgramExtensions` — Self-update-Block durch einen einzigen `builder.UseAutoUpdate(...)`-Aufruf
      ersetzt, bindet Sektion `Updates`, wählt anhand von `SourceType` zwischen GitHub- und
      Ordnerquelle; `AddSingleton(TimeProvider.System)` bleibt bestehen
- [x] `UpdateContracts.cs` — auf genau `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`,
      `IUpdateOrchestrator` reduziert
- [x] Alle 13 geplanten Alt-Klassen (`UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`,
      `UpdateValidator`, `UpdateScriptGenerator`, `UpdatePlatformResolver`, `UpdateServiceResolver`,
      `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`, `UpdateManifestClient`,
      `UpdateChecker`, `UpdateScheduler`, `JsonFileStore`) — entfernt
- [x] `UpdateController`, `SetupUpdateViewModel`, `SetupUpdateTab.razor` — unverändert

### Konfiguration und Tests

- [x] Alle sieben neuen `Updates`-Einträge in `appsettings.json` — `SourceType`, `LocalFolderPath`,
      `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `SourceCheck:Interval`,
      `SourceCheck:TimeRanges`, `StopHostAfterScriptStart`
- [x] Alle fünf `Updates__*`-Umgebungsvariablen in `PlaywrightWebAppFixture` — gesetzt
- [x] Testhilfen `FakeAutoUpdateSource`, `TestAutoUpdateEnvironment`, `AutoUpdateTestContext` — vorhanden
- [x] Alle im Plan aufgeführten Testmethoden — vorhanden (vier davon unter abweichendem Namen, siehe Hinweise)
- [x] Drei E2E-Pflichtszenarien in `UpdateSetupPlaywrightTests` sowie `VersionDisplayPlaywrightTests` als
      Regressionsnachweis — vorhanden
- [x] `README.md` der Bibliothek, `CHANGELOG.md`-Eintrag, aktualisierte Abschnitte in `Docs/help` — vorhanden

## Offene Aufgaben

- [ ] **`UpdateOptions.EnableAutomaticDownload` / `UpdateOptions.EnableAutomaticInstallation`
      (Plan Zeile 212)** — fehlt vollständig: Die Web-Bindungsklasse
      `FinanceManager.Web/Services/Updates/UpdateOptions.cs` enthält von den vier geplanten neuen
      Eigenschaften nur `SourceType` und `LocalFolderPath`. Die beiden booleschen Schalter sind nicht
      vorhanden. Auswirkung: keine — beide Konfigurationswerte existieren in `appsettings.json` und
      binden über `BindConfiguration("Updates")` unmittelbar auf die gleichnamigen Eigenschaften von
      `AutoUpdateOptions`. Zu schließen entweder durch Ergänzen der beiden Eigenschaften oder durch
      Anpassen des Plans an den bewusst schlankeren Vertrag.

- [ ] **DI-Registrierung von `IValidateOptions<AutoUpdateOptions>` (Plan Zeile 44, Ablauf
      „Registrierung beim Start", Schritt 4)** — fehlt vollständig: `UseAutoUpdate` registriert
      `AutoUpdateOptions` als Instanz-Singleton und führt `AutoUpdateOptionsValidator` in
      `BuildOptions` direkt aus (Zeile 103–107), meldet Fehler also sofort beim Start über
      `OptionsValidationException`. Eine Registrierung von `IValidateOptions<AutoUpdateOptions>` im
      Servicecontainer existiert nicht. Auswirkung: keine — da die Optionen nicht über das
      `IOptions`-Muster aufgelöst werden, würde ein registrierter Validator ohnehin nie ausgeführt.
      Zu schließen entweder durch die Registrierung oder durch Anpassen des Plans an die
      eager ausgeführte Validierung.

- [ ] **Ablage von Handler-Ausnahmen als `LastError` (Plan Zeile 22, Designentscheidung „Fehler in
      Event-Handlern")** — teilweise umgesetzt: Der Plan verlangt vier Dinge, wenn ein Event-Abonnent
      eine Ausnahme wirft. Drei davon sind erfüllt — die Ausnahme wird in
      `AutoUpdateEvents.RaiseCancelable` gefangen, über `ErrorOccurred` gemeldet, der Ablauf läuft
      weiter und die Abbruchstimme des fehlgeschlagenen Handlers zählt nicht. Die vierte Forderung
      („im Status-Service als `LastError` abgelegt") ist nicht umgesetzt: `AutoUpdateEvents` kennt den
      Status-Service nicht, und kein Dienst abonniert `ErrorOccurred`, um `LastError` zu schreiben —
      im gesamten Produktivcode existiert keine einzige `ErrorOccurred +=`-Registrierung, nur in vier
      Testdateien. `LastError` wird ausschließlich in `AutoUpdateOrchestrator.FailAsync` für Ausnahmen
      der Workflow-Schritte gesetzt; eine Ausnahme aus `BeforeCheckSource`, `BeforeDownload`,
      `BeforeInstall`, `BeforeStartUpdateScript` oder `AfterStartUpdateScript` bleibt im Statusfeld
      unsichtbar. Auswirkung: gering — der Ablauf bleibt stabil und der Fehler wird an Abonnenten
      gemeldet; er taucht aber nicht in der Setup-UI auf. Neu in diesem Durchlauf erkannt; der
      bestehende Test `AutoUpdateEventsTests.Raise_WhenHandlerThrows_ReportsErrorAndContinues` prüft
      diesen Teilaspekt nicht.

## Hinweise

- **Planwiderspruch beim Prüfdienst (unverändert):** Der Programmablauf „Periodische Quellprüfung"
  (Plan Zeile 94) beschreibt ausdrücklich, dass der Dienst vollständig an `RunUpdateAsync` delegiert.
  Umsetzungsreihenfolge Schritt 12 (Zeile 336) und die geplanten Tests `Execute_TriggersCheckOnlyWithinWindow`
  und `Execute_NeverTriggersDownloadOrInstall` (Zeilen 449–450) fordern dagegen, dass ausschließlich
  `CheckForUpdateAsync` aufgerufen wird. Die Implementierung folgt dem Programmablauf; die Tests heißen
  entsprechend `Execute_RunsUpdateWorkflow_WithinWindow`, `Execute_DoesNotRun_OutsideWindow`,
  `Execute_OnlyCallsRunUpdateAsync_NeverIndividualSteps` und `Execute_WhenRunThrows_ContinuesLoop`.
  Das ist keine Implementierungslücke, sondern eine Inkonsistenz im Plan — Zeile 336 und die beiden
  Testzeilen sollten an den Programmablauf angeglichen werden.
- **Abweichende Testnamen ohne inhaltliche Lücke:** Zusätzlich zu den vier Prüfdienst-Tests wurde der
  geplante Test `Commands_DelegateToOrchestrator` in die drei Einzeltests `Check_DelegatesToOrchestrator`,
  `Download_DelegatesToOrchestrator` und `Install_DelegatesToOrchestrator` aufgeteilt. Inhaltlich ist
  alles Geplante abgedeckt.
- **Namensabweichungen ohne inhaltliche Lücke:** `IAutoUpdateInstaller.Start(string)` ist synchron
  statt `StartAsync`; `IUpdateSettingsStore.ApplyToOptions` ist synchron statt `ApplyToOptionsAsync`;
  `IAutoUpdatePackageStore.PendingAssetPath` statt `GetPendingPath`;
  `IAutoUpdateProcessRunner.EnsureUpdateUnitAvailable` statt `StartPrepareEnvironment`;
  `AutoUpdateGithubSource.Create(repositoryOwner, repositoryName, manifestAssetName?)` mit gegenüber
  Plan Zeile 133 vertauschter Parameterreihenfolge.
- **`UpdateSettingsStore` behält `IOptions<UpdateOptions>`:** Plan Zeile 200 formuliert den Wechsel als
  Ersetzung („statt `IUpdateFileStore`/`IOptions<UpdateOptions>` künftig …"). Tatsächlich kommen
  `IAutoUpdatePackageStore` und `AutoUpdateOptions` hinzu, `IOptions<UpdateOptions>` bleibt für die
  FinanceManager-spezifischen Repository- und Manifest-Standardwerte erhalten. Das ist notwendig, da
  diese Felder bewusst nicht in die Bibliothek gewandert sind (Plan Zeile 29), und stellt keine Lücke dar.
- **Ergänzungen über den Plan hinaus** (kein Prüfgegenstand, für die Nachimplementierung aber relevant):
  `ScheduledInstallEvaluator`, `AutoUpdateSourceDownloadHelper`, `ProcessOutputReader`,
  `UpdateStatusMapper` (Web), `AutoUpdateOptions.UpdateUnitName`, `AutoUpdateBuilder.WithUpdateUnitName`
  und `WithDownloadPath`, `IAutoUpdatePackageStore.IsLockStale` sowie
  `UpdateOrchestratorAdapterLockAndScheduleTests` und `UpdateOrchestratorAdapterTestFactory`.
- **Reihenfolge beim Schließen:** Die drei offenen Punkte sind voneinander unabhängig und können in
  beliebiger Reihenfolge bearbeitet werden. Punkt 3 ist der einzige mit sichtbarer, wenn auch geringer
  fachlicher Wirkung; die Punkte 1 und 2 sind reine Vertragsfragen, bei denen auch eine Anpassung des
  Plans an die Implementierung eine valide Auflösung wäre.
