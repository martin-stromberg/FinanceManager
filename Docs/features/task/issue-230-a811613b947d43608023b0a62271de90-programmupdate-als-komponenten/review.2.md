# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Geprüft wurde `plan.md` gegen den tatsächlichen Code-Stand im Repository (flache Projektablage, kein `src/`-Verzeichnis). Von 124 abgeleiteten Planelementen sind 121 vollständig umgesetzt, 3 sind teilweise umgesetzt. Alle drei offenen Punkte betreffen die Verdrahtung im Host (`FinanceManager.Web`), nicht die Bibliothek selbst.

Sanity-Check: `dotnet build FinanceManager.sln` → 0 Fehler (60 Warnungen, alle bestandsbedingt: NU1903/NU1510). `SoftwareSchmiede.AutoUpdate.Tests` → 87 bestanden, 1 übersprungen (Linux-only Skriptgenerator-Test auf Windows-Agent). `FinanceManager.Tests --filter Updates` → 37 bestanden, 0 fehlgeschlagen.

---

## Umgesetzte Planelemente

### Projektstruktur

- [x] `SoftwareSchmiede.AutoUpdate` (Bibliotheksprojekt) — angelegt, net10.0, `Nullable`, `ImplicitUsings`, `GenerateDocumentationFile`, `WarningsAsErrors` inkl. `CS1591` in Debug und Release
- [x] NuGet-Metadaten in `SoftwareSchmiede.AutoUpdate.csproj` — `PackageId`, `Version` (0.1.0), `Description`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl` vorhanden
- [x] `SoftwareSchmiede.AutoUpdate.Tests` (Testprojekt) — angelegt, Referenz auf die Bibliothek gesetzt
- [x] Beide Projekte in `FinanceManager.sln` eingetragen — verifiziert über die Projekteinträge und den erfolgreichen Solution-Build

### Registrierung und Konfiguration

- [x] `AutoUpdateHostBuilderExtensions` (statische Klasse) — angelegt
- [x] Methode `UseAutoUpdate(this IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` — vorhanden; keine ASP.NET-Referenz, keine `FrameworkReference`
- [x] Registrierung aller Dienste per `TryAddSingleton` — vorhanden (17 Registrierungen)
- [x] `TimeProvider` nur per `TryAddSingleton` — vorhanden, keine Doppelregistrierung
- [x] Bedingte Registrierung der Hosted Services über `HostedServicesEnabled` — vorhanden
- [x] Standardquelle `AutoUpdateLocalFolderSource` bei fehlender Quelle — vorhanden
- [x] Registrierung von `IValidateOptions<AutoUpdateOptions>` — vorhanden
- [x] `AutoUpdateBuilder` (Klasse) — angelegt
- [x] Methoden `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `UseSource`, `UseGithubSource`, `UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices` in `AutoUpdateBuilder` — alle 8 vorhanden
- [x] `AutoUpdateOptions` (Konfigurationsklasse) — angelegt; alle 13 geplanten Felder vorhanden: `Enabled`, `EnableAutomaticDownload`, `DownloadPath`, `EnableAutomaticInstallation`, `Source`, `SourceCheck`, `MaxAssetBytes`, `HostedServicesEnabled`, `ScheduledInstallTime`, `ServiceName`, `ExecutablePath`, `StopHostAfterScriptStart`, `HealthTimeoutSeconds`
- [x] `SourceCheckOptions` (Konfigurationsklasse) mit `Interval` und `TimeRanges` — angelegt
- [x] `SourceCheckTimeRange` (Datenmodellklasse) mit `DayOfWeek`, `StartTime`, `EndTime` — angelegt
- [x] `AutoUpdateOptionsValidator` (`IValidateOptions<AutoUpdateOptions>`) — angelegt

### Datenmodell

- [x] `AutoUpdateState` (Enum) — angelegt; alle 9 Werte: Idle, Checking, UpdateAvailable, Downloading, ReadyToInstall, Installing, Success, Failed, Disabled
- [x] `AutoUpdateOutcome` (Enum) — angelegt; alle 5 Werte: Success, NoUpdate, Skipped, Canceled, Failed
- [x] `AutoUpdateStatusSnapshot` (record) — angelegt; alle 10 geplanten Felder: `State`, `InstalledVersion`, `AvailableVersion`, `LastCheckedAt`, `LastCheckResult`, `LastDownloadResult`, `LastInstallResult`, `LastError`, `IsLocked`, `LockCreatedAt`
- [x] `AutoUpdateResult` (record) — angelegt
- [x] `AutoUpdateCheckResult` (record) — angelegt, inkl. `AvailableVersion` und `AutoUpdatePackageDescriptor`
- [x] `AutoUpdateDownloadResult` (record) — angelegt
- [x] `AutoUpdateInstallResult` (record) — angelegt
- [x] `AutoUpdatePackageDescriptor` (record) — angelegt
- [x] `AutoUpdateReleaseInfo` (record) — angelegt
- [x] `InstalledReleaseInfo` (record) — angelegt
- [x] `AutoUpdateInstallationTarget` (record) — angelegt

### Schnittstellen

- [x] `IAutoUpdateSource` — angelegt; Methoden `CheckAsync`, `DownloadAsync` vorhanden; zustandsloses Gateway ohne veränderliche Versions-Properties
- [x] `IAutoUpdateOrchestrator` — angelegt; Methoden `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync(bool confirmDowntime, …)`, `GetStatusAsync` vorhanden
- [x] `IAutoUpdateCommandHandler` — angelegt; Methoden `CheckAsync`, `DownloadAsync`, `InstallAsync(bool confirmDowntime, …)` vorhanden
- [x] `IAutoUpdateStatusProvider` — angelegt; Methode `GetSnapshot` vorhanden
- [x] `IAutoUpdateEventAggregator` — angelegt; 6 Events und 6 Raise-Methoden vorhanden
- [x] `IAutoUpdateEnvironment`, `IInstalledVersionProvider`, `IAutoUpdatePackageStore`, `IAutoUpdateStateStore`, `IAutoUpdatePackageValidator`, `IAutoUpdateScriptGenerator`, `IAutoUpdatePlatformResolver`, `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe`, `IAutoUpdateProcessRunner`, `IAutoUpdateHostTerminator`, `IAutoUpdateInstaller` — alle angelegt
- [x] Methoden `PrepareAsync`, `StartAsync` in `IAutoUpdateInstaller` — vorhanden

### Ereignisse

- [x] `AutoUpdateEvents` (Klasse) — angelegt, thread-sichere Abonnentenverwaltung
- [x] `AutoUpdateCancelEventArgs` — angelegt (Basis für abbrechbare Ereignisse)
- [x] `BeforeDownloadEventArgs` mit `Uri SourceUri` — angelegt
- [x] `BeforeInstallEventArgs` mit `FileInfo PackageFile` — angelegt
- [x] `BeforeStartUpdateScriptEventArgs` mit `FileInfo ScriptFile` — angelegt
- [x] `AutoUpdateErrorEventArgs` mit `Exception` und Ablaufphase — angelegt
- [x] Events `BeforeCheckSource`, `BeforeDownload`, `BeforeInstall`, `BeforeStartUpdateScript`, `AfterStartUpdateScript`, `ErrorOccured` — alle vorhanden; `AfterStartUpdateScript` ohne Abbruchsemantik (`EventHandler`)
- [x] Handler-Ausnahmebehandlung (Meldung über `ErrorOccured`, kein Abbruch, Abbruchstimme zählt nicht) — implementiert

### Dienste und Logik

- [x] `AutoUpdateOrchestrator` (Klasse, Singleton) — angelegt, Serialisierung über internes `SemaphoreSlim`
- [x] `AutoUpdateCommandService` (Klasse) — angelegt, dünne Fassade ohne eigene Update-Logik
- [x] `AutoUpdateStatusService` (Klasse) — angelegt, Snapshot hinter `lock`, Persistenz, verzögertes Laden
- [x] `HostAutoUpdateEnvironment` — angelegt, liest `IHostEnvironment.ContentRootPath`
- [x] `ReleaseMetadataInstalledVersionProvider` — angelegt
- [x] `FileSystemAutoUpdatePackageStore` — angelegt (Portierung von `UpdateFileStore`, ohne `IWebHostEnvironment`)
- [x] `FileSystemAutoUpdateStateStore` — angelegt, inkl. tolerantem Fallback auf `Idle` bei unlesbarem/fremdem Schema
- [x] `JsonFileStore` (interne statische Klasse) — angelegt
- [x] `AutoUpdatePackageValidator` — angelegt (SemVer-Vergleich, SHA256, ZIP-Integrität, Größenlimit)
- [x] `AutoUpdatePlatformResolver`, `AutoUpdateServiceResolver`, `DefaultAutoUpdateServiceProbe` — angelegt
- [x] `AutoUpdateScriptGenerator` — angelegt (Windows `.ps1`, Linux `.sh`)
- [x] `DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateHostTerminator` — angelegt
- [x] `AutoUpdateInstaller` — angelegt (Portierung der Kernlogik von `UpdateExecutor` ohne Ereignisauslösung)
- [x] `AutoUpdateLocalFolderSource` — angelegt
- [x] `AutoUpdateGithubSource` inkl. statischer Factory `Create(...)` — angelegt
- [x] `SourceCheckWindowEvaluator` mit `IsWithinWindow(DateTimeOffset)` — angelegt, leere `TimeRanges` bedeuten „immer erlaubt"
- [x] `AutoUpdateCheckerService` (Hosted Service) — angelegt, ruft ausschließlich `CheckForUpdateAsync`
- [x] `AutoUpdateSchedulerService` (Hosted Service) — angelegt, keine Mehrfachauslösung desselben Termins

### Programmabläufe

- [x] Ablauf „Vollständiger Update-Workflow (`RunUpdateAsync`)" — implementiert inkl. Ereignisreihenfolge, Abbruchbehandlung und zentraler Fehlerbehandlung (Ausnahme → `ErrorOccured` + `LastError` + `Failed`, keine Weitergabe an den Aufrufer)
- [x] Ablauf „Installation und Skriptstart" — implementiert inkl. Sperrdatei, Persistenz des Zustands `Installing` **vor** dem Skriptstart und optionalem Host-Stopp über `StopHostAfterScriptStart`
- [x] Ablauf „Zustandsabgleich nach Neustart" — implementiert (Versionsvergleich → `Success`/`Failed`)
- [x] Ablauf „Periodische Quellprüfung" — implementiert mit `TimeProvider` und `SourceCheckWindowEvaluator`
- [x] Ablauf „Geplante Installation" — implementiert
- [x] Ablauf „Manuelle Steuerung aus der UI" — implementiert über den Adapter

### Validierungsregeln

- [x] `DownloadPath` nicht leer — validiert
- [x] `Source` nach Builder-Auswertung nicht `null` (Fallback lokale Ordnerquelle) — implementiert
- [x] `MaxAssetBytes > 0` — validiert
- [x] `HealthTimeoutSeconds` auf 10–600 geklemmt — implementiert (`Math.Clamp` mit `MinHealthTimeoutSeconds`/`MaxHealthTimeoutSeconds`)
- [x] `SourceCheckOptions.Interval >= 1` — validiert
- [x] `SourceCheckTimeRange.StartTime < EndTime` — validiert
- [x] `AutoUpdateGithubSource.Create`: Owner und Name nicht leer — validiert
- [x] `AutoUpdateLocalFolderSource`: fehlendes Verzeichnis liefert Ergebnis ohne Version statt Ausnahme — implementiert
- [x] `AutoUpdatePackageDescriptor.FileName` ohne Pfadsegmente — implementiert
- [x] Heruntergeladenes Paket: Größe, SHA256, ZIP-Gültigkeit — implementiert
- [x] Versionsvergleich per SemVer, unbekannte installierte Version gilt nicht als älter — implementiert
- [x] `InstallAsync(confirmDowntime)` muss `true` sein — durchgesetzt
- [x] Installationsziel Windows/Linux — `InvalidOperationException` bei fehlendem Ziel implementiert

### Änderungen an bestehenden Klassen (`FinanceManager.Web`)

- [x] `UpdateOrchestratorAdapter` (Klasse) — angelegt, implementiert `IUpdateOrchestrator` auf Basis der Bibliothek
- [x] Fehlerergebnis-Mapping des Adapters auf `FileNotFoundException`/`IOException`/`ArgumentException` — implementiert
- [x] `AutoUpdateOptionsMapper` (statische Klasse) — angelegt
- [x] Methode `ApplySettings` in `AutoUpdateOptionsMapper` (DTO → Options) — vorhanden
- [x] Methode `ToSettingsDto` in `AutoUpdateOptionsMapper` (Options → DTO) — vorhanden *(im Vorreview noch offen, jetzt geschlossen)*
- [x] `ProgramExtensions`: Self-update-Block durch einen einzigen `builder.UseAutoUpdate(cfg => …)`-Aufruf ersetzt — umgesetzt
- [x] `ProgramExtensions`: Registrierung von `IUpdateOrchestrator` → `UpdateOrchestratorAdapter` (Scoped), `IUpdateSettingsStore` (Singleton), `IInstalledReleaseMetadataProvider` (Singleton) — vorhanden
- [x] `UpdateContracts.cs` auf `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator` reduziert — umgesetzt; alle 10 entfallenden Interfaces und `record UpdateInstallationTarget` entfernt
- [x] Methode `ApplyToOptionsAsync` in `IUpdateSettingsStore`/`UpdateSettingsStore` — vorhanden
- [x] `UpdateSettingsStore` auf `IAutoUpdatePackageStore` und `AutoUpdateOptions` umgestellt, Legacy-Migration erhalten — umgesetzt
- [x] `InstalledReleaseMetadataProvider` delegiert an `IInstalledVersionProvider`, `IWebHostEnvironment` entfallen — umgesetzt
- [x] Felder `SourceType` (`string`) und `LocalFolderPath` (`string?`) in `UpdateOptions` — vorhanden
- [x] `UpdateController`, `SetupUpdateViewModel`, `SetupUpdateTab.razor` ohne Signaturänderung — bestätigt
- [x] Entfernung der alten Web-Klassen (`UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `UpdateValidator`, `UpdateScriptGenerator`, `UpdatePlatformResolver`, `UpdateServiceResolver`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`, `UpdateManifestClient`, `UpdateChecker`, `UpdateScheduler`, `JsonFileStore`) — alle 13 Dateien nachweislich gelöscht

### Konfiguration

- [x] Einträge `Updates:SourceType`, `Updates:LocalFolderPath`, `Updates:EnableAutomaticDownload`, `Updates:EnableAutomaticInstallation`, `Updates:SourceCheck:Interval`, `Updates:SourceCheck:TimeRanges`, `Updates:StopHostAfterScriptStart` in `appsettings.json` — alle vorhanden mit den geplanten Standardwerten
- [x] Umgebungsvariablen `Updates__SourceType`, `Updates__LocalFolderPath`, `Updates__EnableAutomaticInstallation`, `Updates__HostedServicesEnabled`, `Updates__WorkingDirectory` in `PlaywrightWebAppFixture` — alle vorhanden

### Tests

- [x] Testhilfen `FakeAutoUpdateSource`, `TestAutoUpdateEnvironment`, `AutoUpdateTestContext` — alle bereitgestellt
- [x] `UseAutoUpdateRegistrationTests` — angelegt; alle 4 geplanten Testmethoden vorhanden (plus 3 zusätzliche zur Präzedenz von Fluent-Werten)
- [x] `AutoUpdateBuilderTests` (3), `AutoUpdateOptionsValidationTests` (4), `AutoUpdateEventsTests` (4 geplante + 3 zusätzliche), `AutoUpdateStatusServiceTests` (4) — alle geplanten Testmethoden vorhanden
- [x] `AutoUpdateOrchestratorCheckTests` (4), `AutoUpdateOrchestratorDownloadTests` (4), `AutoUpdateOrchestratorInstallTests` (8), `AutoUpdateOrchestratorEventTests` (5) — alle geplanten Testmethoden vorhanden
- [x] `AutoUpdateCommandServiceTests` — angelegt; `Commands_UpdateStatusService` und `Commands_ParallelCalls_AreSerialized` vorhanden, `Commands_DelegateToOrchestrator` in `Check_/Download_/Install_DelegatesToOrchestrator` aufgeteilt (Abdeckung vollständig)
- [x] `AutoUpdateLocalFolderSourceTests` (3), `AutoUpdateGithubSourceTests` (4), `SourceCheckWindowEvaluatorTests` (4) — vollständig
- [x] `AutoUpdateCheckerServiceTests` (4), `AutoUpdateSchedulerServiceTests` (3) — vollständig
- [x] `AutoUpdatePackageValidatorTests`, `AutoUpdateScriptGeneratorTests` (3), `AutoUpdatePlatformResolverTests`, `AutoUpdateServiceResolverTests` (3) — vollständig
- [x] `FileSystemAutoUpdatePackageStoreTests` (3), `FileSystemAutoUpdateStateStoreTests` — vollständig
- [x] `UpdateOrchestratorAdapterTests` in `FinanceManager.Tests/Updates/` — angelegt; `Adapter_MapsSnapshotToUpdateStatusDto`, `Adapter_MapsFailedResultToExpectedException`, `Adapter_SaveSettings_AppliesToAutoUpdateOptions` vorhanden
- [x] Entfernung der verschobenen Testklassen aus `FinanceManager.Tests/Updates/` — alle 8 Dateien gelöscht
- [x] Anpassung von `UpdateSettingsStoreTests`, `InstalledReleaseMetadataProviderTests`, `UpdateStatusTestData`, `TestWebHostEnvironment` — umgesetzt
- [x] Anpassung von `UpdateControllerIntegrationTests` auf lokale Quelle und deaktivierte Hintergrunddienste — umgesetzt
- [x] E2E: `SetupUpdateGateway` (Test-Gateway) — angelegt
- [x] E2E: `UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus`, `Admin_TriggersCheck_ShowsAvailableUpdate`, `Admin_SavesSettings_PersistsAcrossReload` — alle 3 vorhanden
- [x] E2E: `VersionDisplayPlaywrightTests` als Regressionsnachweis — unverändert vorhanden und im dokumentierten Lauf grün (28/28 E2E-Tests) *(im Vorreview noch offen, jetzt geschlossen)*

### Dokumentation

- [x] `SoftwareSchmiede.AutoUpdate/README.md` — angelegt; alle geplanten Abschnitte vorhanden (Installation, Getting started, Configuration, Update sources, Events, Status and manual control, Background services, Supported platforms, Testing)
- [x] `CHANGELOG.md`-Eintrag — vorhanden
- [x] Aktualisierung der betroffenen Abschnitte in `Docs/help` — umgesetzt in `ablauf-technisch.md`, `beschreibung.md`, `business-rules.md`, `api.md`; neue `Updates`-Einträge dort beschrieben *(im Vorreview noch offen, jetzt geschlossen)*

### Sonstiges

- [x] Keine Datenbankmigrationen — bestätigt, das Update-System persistiert ausschließlich in Dateien

---

## Offene Aufgaben

- [ ] **Felder `EnableAutomaticDownload` und `EnableAutomaticInstallation` in `UpdateOptions` (`FinanceManager.Web/Services/Updates/UpdateOptions.cs`)** — fehlen vollständig. Der Plan listet sie unter „Änderungen an bestehenden Klassen → `UpdateOptions` → Neue Eigenschaften" ausdrücklich als die beiden Schalter, die die neuen Anforderungen abbilden. Die Klasse enthält nur `SourceType` und `LocalFolderPath` von den vier geplanten neuen Eigenschaften.
  Konkrete Auswirkung: `Updates:EnableAutomaticInstallation` funktioniert zufällig trotzdem, weil `BindConfiguration("Updates")` die Sektion direkt auf `AutoUpdateOptions` bindet. `Updates:EnableAutomaticDownload` funktioniert dagegen **nicht** in der Richtung `false`: `ProgramExtensions` ruft in Zeile 168 bedingungslos `cfg.EnableAutomaticDownload(updateOptions.WorkingDirectory)` auf, wodurch `AutoUpdateBuilder.ExplicitEnableAutomaticDownload` gesetzt wird und `AutoUpdateHostBuilderExtensions.ReapplyExplicitValues` den gebundenen Wert nach dem Binden wieder auf `true` überschreibt. Der in `appsettings.json` und im Plan dokumentierte Konfigurationseintrag ist damit wirkungslos.

- [ ] **Präzedenz von `Updates:SourceCheck:Interval`** — teilweise umgesetzt. Der Plan legt in „Konfigurationsänderungen" fest, dass `Updates:SourceCheck:Interval` das Prüfintervall bestimmt und `CheckIntervalMinutes` nur noch als Alias erhalten bleibt. Umgesetzt ist die umgekehrte Rangfolge: `ProgramExtensions` Zeile 169 ruft bedingungslos `cfg.WithSourceCheck(Math.Max(1, updateOptions.CheckIntervalMinutes))`, wodurch `ExplicitSourceCheckInterval` immer gesetzt ist und `ReapplyExplicitValues` den aus `Updates:SourceCheck:Interval` gebundenen Wert stets überschreibt. Der Eintrag ist in `appsettings.json` vorhanden, aber im Host wirkungslos. Die Bibliothek selbst verhält sich korrekt — der Test `UseAutoUpdateRegistrationTests.UseAutoUpdate_WithoutFluentSourceCheck_UsesConfiguredInterval` belegt, dass der Konfigurationswert greift, wenn `WithSourceCheck` nicht aufgerufen wird. Die Lücke liegt ausschließlich in der Host-Verdrahtung.

- [ ] **Paketreferenz `Microsoft.Extensions.Http` in `SoftwareSchmiede.AutoUpdate.csproj`** — fehlt. Umsetzungsschritt 1 des Plans listet sie unter den sechs erforderlichen Paketreferenzen; vorhanden sind nur fünf (`Hosting.Abstractions`, `Options`, `Logging.Abstractions`, `DependencyInjection.Abstractions`, `Configuration.Binder`). `AutoUpdateGithubSource.Create` erzeugt stattdessen direkt einen `new HttpClient { Timeout = … }` statt über `IHttpClientFactory` zu gehen. Funktional lauffähig und durch `AutoUpdateGithubSourceTests` abgedeckt, aber eine Abweichung vom Plan und für ein NuGet-Paket die schlechtere Variante (kein Handler-Pooling, keine DNS-Aktualisierung bei langlebigen Singletons).

---

## Hinweise

- **Alle drei offenen Punkte sind unabhängig voneinander** und lassen sich einzeln schließen. Die ersten beiden betreffen dieselbe Ursachenklasse: `ProgramExtensions` ruft die Fluent-Setter bedingungslos auf, obwohl `UseAutoUpdate` explizit gesetzte Fluent-Werte absichtlich über die Konfiguration stellt. Ein bedingter Aufruf (nur setzen, wenn in `UpdateOptions` bzw. der Konfiguration explizit hinterlegt) schließt beide Punkte gemeinsam.
- Die drei im Vorreview offenen Punkte (Rückrichtung `Options → DTO` im Mapper, erneuter Lauf von `VersionDisplayPlaywrightTests`, Aktualisierung von `Docs/help`) sind in dieser Runde geschlossen.
- **Abweichung ohne Lücke:** Der Plan nennt die Factory-Signatur `AutoUpdateGithubSource.Create(repositoryName, repositoryOwner)`; implementiert ist `Create(repositoryOwner, repositoryName)`. Die Reihenfolge ist gegenüber dem Plan vertauscht, aber in sich konsistent (Builder und Aufrufer verwenden dieselbe Reihenfolge) und durch `Create_WithEmptyOwner_Throws` abgesichert. Keine Nacharbeit nötig, sofern die Signatur als bewusst gewählt gilt.
- **Ergänzungen über den Plan hinaus:** `AutoUpdateOptions.UpdateUnitName` mit `AutoUpdateBuilder.WithUpdateUnitName` (systemd-Unit-Name, damit mehrere Anwendungen auf demselben Host nicht kollidieren) sowie die interne Hilfsklasse `ProcessOutputReader`. Beide sind sinnvolle Zusätze, stehen aber nicht im Plan und sind entsprechend auch nicht in dessen Konfigurationstabelle dokumentiert — beim Doku-Schritt mitziehen.
- `IAutoUpdateInstaller.StartAsync` ist als `void StartAsync(string scriptPath)` deklariert, trägt also das `Async`-Suffix ohne `Task`-Rückgabe. Der Plan verlangt lediglich die Existenz von `StartAsync`; die Namenskonvention ist Gegenstand des Code-Reviews, nicht dieses Plan-Reviews.
- Der übersprungene Test `AutoUpdateScriptGeneratorTests.Generate_OnLinux_WritesShellScriptWithUnixLineEndings` ist plattformbedingt (Linux-only auf Windows-Agent) und keine Lücke. Der Linux-Pfad des Skriptgenerators bleibt damit auf dem Windows-Agent unverifiziert.
- Die Bestandsaufnahme (`inventory.md` und `inventory/`) beschreibt durchgängig `src/`-Pfade und Klassennamen, die im Repository nicht existieren. Der Plan weist in seiner Vorbemerkung selbst darauf hin; dieses Review wurde entsprechend gegen den tatsächlichen Code-Stand geführt.
