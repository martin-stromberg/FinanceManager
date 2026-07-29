# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Stand: 2026-07-29 (dritter Review-Durchlauf). Geprüft wurde `plan.md` gegen den Code-Stand
im Arbeitsverzeichnis. Von 124 Aufgaben sind 123 umgesetzt, 1 ist teilweise umgesetzt.

Die drei im Vorreview offenen Punkte (Aufgaben 2, 21, 124) sind geschlossen und wurden
einzeln am Code verifiziert. Neu als Lücke erkannt: ein im Plan gelistetes
Interface-Mitglied (`IAutoUpdatePackageValidator.ValidateReleaseAsync`), das im gesamten
Repository nicht existiert.

Sanity-Check: `dotnet build FinanceManager.sln -c Debug` → **0 Fehler**, 60 Warnungen
(sämtlich Vorbestand: NU1903-Sicherheitshinweise und NU1510-Trimming-Hinweise, keine aus
`SoftwareSchmiede.AutoUpdate`). Laut `test-results.md` 1.093/1.094 Tests grün, 1 Test
plattformbedingt übersprungen (Linux-Skripttest auf Windows-Agent).

---

## Umgesetzte Planelemente

### Projektstruktur (Schritte 1, 14)

- [x] `SoftwareSchmiede.AutoUpdate.csproj` — angelegt (net10.0, `Nullable`, `ImplicitUsings`, `GenerateDocumentationFile`, `WarningsAsErrors` inkl. `CS1591` für Debug und Release)
- [x] NuGet-Metadaten — `PackageId`, `Version` (0.1.0, eigener SemVer-Strang gemäß Offenem Punkt 3), `Description`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl`, `PackageReadmeFile` vorhanden
- [x] Alle 6 geplanten Paketreferenzen — `Hosting.Abstractions`, `Options`, `Logging.Abstractions`, `Http`, `DependencyInjection.Abstractions`, `Configuration.Binder` (schließt Aufgabe 2 aus dem Vorreview)
- [x] `SoftwareSchmiede.AutoUpdate.Tests.csproj` — angelegt (xunit.v3, FluentAssertions, Moq, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.NET.Test.Sdk`, coverlet), Projektreferenz auf die Bibliothek gesetzt
- [x] Beide Projekte in `FinanceManager.sln` eingetragen

### Modelle und Zustandsmodell (Schritt 2)

- [x] `AutoUpdateState` (Enum) — alle 9 Werte: Idle, Checking, UpdateAvailable, Downloading, ReadyToInstall, Installing, Success, Failed, Disabled
- [x] `AutoUpdateOutcome` (Enum) — alle 5 Werte: Success, NoUpdate, Skipped, Canceled, Failed
- [x] `AutoUpdateResult`, `AutoUpdateCheckResult`, `AutoUpdateDownloadResult`, `AutoUpdateInstallResult` (records) — angelegt
- [x] `AutoUpdateStatusSnapshot` (record) — alle 10 geplanten Felder vorhanden: State, InstalledVersion, AvailableVersion, LastCheckedAt, LastCheckResult, LastDownloadResult, LastInstallResult, LastError, IsLocked, LockCreatedAt
- [x] `AutoUpdatePackageDescriptor`, `AutoUpdateReleaseInfo`, `InstalledReleaseInfo`, `AutoUpdateInstallationTarget` (records) — angelegt

### Konfigurationsmodell (Schritt 3)

- [x] `SourceCheckTimeRange` — `DayOfWeek`, `TimeOnly StartTime`, `TimeOnly EndTime`
- [x] `SourceCheckOptions` — `Interval`, `TimeRanges`
- [x] `AutoUpdateOptions` — alle 13 geplanten Eigenschaften vorhanden (Enabled, EnableAutomaticDownload, DownloadPath, EnableAutomaticInstallation, Source, SourceCheck, MaxAssetBytes, HostedServicesEnabled, ScheduledInstallTime, ServiceName, ExecutablePath, StopHostAfterScriptStart, HealthTimeoutSeconds)
- [x] `SourceCheckWindowEvaluator.IsWithinWindow` — leere `TimeRanges` bedeuten „immer erlaubt"

### Kern-Schnittstellen (Schritt 4)

- [x] `IAutoUpdateSource` — `CheckAsync`, `DownloadAsync` (zustandsloses Gateway, keine veränderlichen Versions-Properties, wie in den Designentscheidungen gefordert)
- [x] `IAutoUpdateEnvironment`, `IInstalledVersionProvider`, `IAutoUpdatePackageStore`, `IAutoUpdateStateStore` — definiert
- [x] `IAutoUpdateScriptGenerator`, `IAutoUpdatePlatformResolver`, `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe`, `IAutoUpdateProcessRunner`, `IAutoUpdateHostTerminator` — definiert
- [x] `IAutoUpdateInstaller`, `IAutoUpdateStatusProvider`, `IAutoUpdateEventAggregator` — definiert
- [x] `IAutoUpdateOrchestrator` — alle 5 Methoden: `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync`, `GetStatusAsync`
- [x] `IAutoUpdateCommandHandler` — `CheckAsync`, `DownloadAsync`, `InstallAsync(bool confirmDowntime, …)`

### Event-Infrastruktur (Schritt 5)

- [x] `AutoUpdateCancelEventArgs` (Basisklasse), `BeforeDownloadEventArgs` (`Uri SourceUri`), `BeforeInstallEventArgs` (`FileInfo PackageFile`), `BeforeStartUpdateScriptEventArgs` (`FileInfo ScriptFile`), `AutoUpdateErrorEventArgs` (`Exception` + Phase) — angelegt
- [x] `AutoUpdateEvents` — alle 6 Ereignisse (`BeforeCheckSource`, `BeforeDownload`, `BeforeInstall`, `BeforeStartUpdateScript`, `AfterStartUpdateScript`, `ErrorOccured`) mit Raise-Methoden
- [x] Event-Signaturen als `EventHandler<T>` mit `CancelEventArgs`-Ableitung statt `ref bool cancel` — gemäß Designentscheidung
- [x] `AfterStartUpdateScript` ohne Abbruchsemantik — als `EventHandler` (nicht generisch) modelliert
- [x] Handler-Ausnahmebehandlung — Ausnahme wird gefangen, über `ErrorOccured` gemeldet, Abbruch-Stimme des fehlgeschlagenen Handlers zählt nicht

### Umgebung, Persistenz, Validierung (Schritt 6)

- [x] `HostAutoUpdateEnvironment` — Standardimplementierung über `IHostEnvironment.ContentRootPath`, keine ASP.NET-Referenz
- [x] `JsonFileStore` — atomares Lesen/Schreiben portiert
- [x] `FileSystemAutoUpdatePackageStore` — Portierung von `UpdateFileStore` ohne `IWebHostEnvironment`
- [x] `FileSystemAutoUpdateStateStore` — atomare JSON-Persistenz mit tolerantem Fallback auf `Idle` bei fremdem/defektem Schema
- [x] `AutoUpdatePackageValidator.IsNewerVersion` — SemVer-Vergleich
- [x] `AutoUpdatePackageValidator.ValidateDownloadedPackageAsync` — Größenlimit, SHA256 und ZIP-Integrität (`ZipFile.OpenRead` + `ValidateEntry`)
- [x] `ReleaseMetadataInstalledVersionProvider` — liest `release-metadata.json` aus dem Anwendungsverzeichnis

### Plattform- und Installationsdienste (Schritt 7)

- [x] `AutoUpdatePlatformResolver`, `DefaultAutoUpdateServiceProbe`, `AutoUpdateServiceResolver` — portiert
- [x] `AutoUpdateScriptGenerator` — Windows `.ps1` / Linux `.sh`, Pfade über `IAutoUpdateEnvironment` und `IAutoUpdatePackageStore`
- [x] `DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateHostTerminator` — portiert
- [x] `AutoUpdateInstaller` — `PrepareAsync` validiert das Paket erneut, löst das Ziel über `IAutoUpdateServiceResolver.Resolve` auf und lässt das Skript erzeugen; Start ohne Ereignisauslösung (Ereignisse liegen beim Orchestrator)

### Status-Service (Schritt 8)

- [x] `AutoUpdateStatusService` — unveränderlicher Snapshot hinter `Lock`, Austausch statt feldweiser Mutation (Value Object)
- [x] `EnsureLoadedAsync` — verzögertes Laden beim ersten Zugriff über `IAutoUpdateStateStore.ReadAsync`, Fallback `AutoUpdateStatusSnapshot.Idle`
- [x] `UpdateAsync` — Mutation und Persistenz hinter eigenem Schreib-Gate

### Quellen (Schritt 9)

- [x] `AutoUpdateLocalFolderSource` — Manifest und Paket aus lokalem Verzeichnis; fehlendes Verzeichnis liefert Ergebnis ohne Version statt Ausnahme
- [x] `AutoUpdateGithubSource` — inkl. statischer Factory `Create(repositoryOwner, repositoryName)` mit Argumentprüfung; zusätzlich öffentlicher Konstruktor für einen extern verwalteten `HttpClient`

### Orchestrator, Command-Service, Hintergrunddienste (Schritte 10–12)

- [x] `AutoUpdateOrchestrator` — alle 5 Methoden; Ablauf entspricht „Vollständiger Update-Workflow" und „Installation und Skriptstart" Schritt für Schritt
- [x] Serialisierung über internes `SemaphoreSlim` an genau einer Stelle — Hintergrunddienst und UI teilen dieselbe Sperre
- [x] Zentrale Fehlerbehandlung — Ausnahme → `RaiseErrorOccured` → `LastError` + Zustand `Failed` → `Outcome.Failed`; der Aufrufer erhält keine Ausnahme
- [x] Zustand `Installing` wird **vor** dem Skriptstart gesetzt und persistiert (Zeilen 300–309)
- [x] `StopHostAfterScriptStart` — `IAutoUpdateHostTerminator.StopApplication` wird nur bei gesetzter Option gerufen
- [x] Abbruch vor dem Skriptstart gibt die Sperre frei (`DeleteLockAsync`), Zustand zurück auf `ReadyToInstall`
- [x] `ReconcileAfterRestartAsync` — Zustandsabgleich nach Neustart: Versionsgleichheit → `Success` mit geleerten Feldern, Abweichung → `Failed` mit erklärender Meldung
- [x] `AutoUpdateCommandService` — dünne Fassade, reicht ausschließlich durch, keine eigene Update-Logik
- [x] `AutoUpdateCheckerService` — liest die Optionen bei jedem Durchlauf, respektiert Zeitfenster, ruft **ausschließlich** `CheckForUpdateAsync`, Rückfallwartezeit bei Ausnahme, durchgängig `TimeProvider`
- [x] `AutoUpdateSchedulerService` — minütliche Prüfung, Vorbedingung `ReadyToInstall`, `InstallAsync(confirmDowntime: true)`, merkt Datum und Uhrzeit des letzten Versuchs gegen Mehrfachauslösung

### Builder und Registrierung (Schritt 13)

- [x] `AutoUpdateHostBuilderExtensions.UseAutoUpdate(this IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` — Erweiterung auf `IHostApplicationBuilder`, keine `FrameworkReference` auf ASP.NET
- [x] `AutoUpdateBuilder` — alle 8 geplanten Fluent-Methoden vorhanden
- [x] Standardquelle `AutoUpdateLocalFolderSource`, wenn keine Quelle gesetzt wurde
- [x] Alle Dienste per `TryAddSingleton` registriert; Hosted Services nur bei `HostedServicesEnabled`
- [x] `TimeProvider` nur per `TryAddSingleton` — die im Risikoabschnitt genannte Doppelregistrierung ist ausgeschlossen (Test `UseAutoUpdate_DoesNotOverrideExistingTimeProvider`)
- [x] `AutoUpdateOptionsValidator` — alle 5 Validierungsregeln (DownloadPath, Source, MaxAssetBytes, SourceCheck.Interval, TimeRange-Reihenfolge); `HealthTimeoutSeconds` wird auf 10–600 geklemmt

### Aufräumen im Web-Projekt (Schritt 16)

- [x] `UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `UpdateValidator`, `UpdateScriptGenerator`, `UpdatePlatformResolver`, `UpdateServiceResolver`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`, `UpdateManifestClient`, `UpdateChecker`, `UpdateScheduler`, `JsonFileStore` — gelöscht
- [x] `UpdateContracts.cs` — auf `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator` reduziert; `IUpdateOrchestrator` in der Signatur unverändert

### Adapterschicht (Schritt 17)

- [x] `UpdateOrchestratorAdapter` — implementiert `IUpdateOrchestrator`, mappt `AutoUpdateStatusSnapshot` vollständig auf `UpdateStatusDto` (inkl. `UpdateMetadataDto`/`UpdateAssetDto`) und `AutoUpdateState` auf `UpdateStatusKind`
- [x] Fehlerergebnis-Mapping — `StartInstallAsync` wirft `result.Error` erneut, damit das bestehende Ausnahme-Mapping des `UpdateController` (404/409/400) weiter greift
- [x] `ResetLockAsync` — Staleness-Prüfung gegen `HealthTimeoutSeconds`, `IOException` bei fehlender bzw. zu junger Sperre
- [x] `AutoUpdateOptionsMapper` — `ApplySettings` (DTO → Options) und `ToSettingsDto` (Options → DTO)
- [x] `UpdateSettingsStore` — nutzt `IAutoUpdatePackageStore` und `AutoUpdateOptions` statt `IUpdateFileStore`/`IOptions<UpdateOptions>`; Legacy-Migration (`windowsServiceName`/`linuxServiceName`) unverändert erhalten
- [x] `InstalledReleaseMetadataProvider` — delegiert an `IInstalledVersionProvider`, `IWebHostEnvironment`-Abhängigkeit entfallen

### Registrierung und Konfiguration (Schritte 18, 19)

- [x] `ProgramExtensions` — Self-update-Block durch einen einzigen `builder.UseAutoUpdate(cfg => …)`-Aufruf ersetzt; Sektion `Updates` gebunden, Quellenauswahl über `Updates:SourceType`
- [x] `IUpdateOrchestrator` → `UpdateOrchestratorAdapter` (Scoped), `IUpdateSettingsStore` (Singleton), `IInstalledReleaseMetadataProvider` (Singleton) registriert
- [x] `UpdateOptions` — um `SourceType`, `LocalFolderPath`, `EnableAutomaticDownload`, `EnableAutomaticInstallation` erweitert (schließt Aufgabe 21 aus dem Vorreview)
- [x] `appsettings.json` — alle 7 geplanten neuen Einträge vorhanden; die 10 unverändert weiterverwendeten Einträge binden direkt auf `AutoUpdateOptions` (namensgleich)
- [x] Präzedenz `Updates:SourceCheck:Interval` vor `CheckIntervalMinutes` — `WithSourceCheck` wird nur noch aufgerufen, wenn der neue Schlüssel fehlt (schließt Aufgabe 124 aus dem Vorreview)

### Tests (Schritte 15, 20–22)

- [x] Testhilfen `FakeAutoUpdateSource`, `TestAutoUpdateEnvironment`, `AutoUpdateTestContext` — bereitgestellt
- [x] Alle im Plan gelisteten Bibliothekstestklassen vorhanden (19 Testklassen, 87 Testmethoden)
- [x] Sämtliche namentlich geplanten Testmethoden auffindbar; `Commands_DelegateToOrchestrator` wurde thematisch in `Check_`/`Download_`/`Install_DelegatesToOrchestrator` aufgeteilt
- [x] Die 8 obsoleten Testklassen unter `FinanceManager.Tests/Updates/` — gelöscht
- [x] `UpdateSettingsStoreTests`, `InstalledReleaseMetadataProviderTests`, `UpdateStatusTestData`, `TestWebHostEnvironment` — angepasst
- [x] `UpdateOrchestratorAdapterTests` — alle 3 geplanten Tests vorhanden (plus `_LockAndSchedule`-Ergänzung)
- [x] `UpdateControllerIntegrationTests` / `TestWebApplicationFactory` — auf `SourceType=LocalFolder` und `HostedServicesEnabled=false` umgestellt, keine GitHub-Anfragen im Testserver
- [x] `PlaywrightWebAppFixture` — alle geplanten `Updates__*`-Umgebungsvariablen gesetzt
- [x] `UpdateSetupPlaywrightTests` — alle 3 geplanten E2E-Szenarien implementiert
- [x] `VersionDisplayPlaywrightTests` — als Regressionsnachweis grün

### Dokumentation (Schritt 23)

- [x] `SoftwareSchmiede.AutoUpdate/README.md` — angelegt
- [x] `CHANGELOG.md` — Eintrag unter „Unreleased/Added" mit Bibliothek, Fluent-Optionen, Quellen, Ereignissen und Hintergrunddiensten
- [x] `Docs/help/systemverwaltung-und-setup/` — `ablauf-technisch.md`, `api.md`, `beschreibung.md`, `business-rules.md` aktualisiert

---

## Offene Aufgaben

- [ ] `IAutoUpdatePackageValidator.ValidateReleaseAsync` — **teilweise umgesetzt**: Der Plan
  listet für `IAutoUpdatePackageValidator` drei Mitglieder (`IsNewerVersion`,
  `ValidateReleaseAsync`, `ValidateDownloadedPackageAsync`). Implementiert sind nur
  `IsNewerVersion` und `ValidateDownloadedPackageAsync`. `ValidateReleaseAsync` existiert
  weder im Interface (`SoftwareSchmiede.AutoUpdate/IAutoUpdatePackageValidator.cs`) noch in
  der Implementierung (`SoftwareSchmiede.AutoUpdate/AutoUpdatePackageValidator.cs`) — eine
  Volltextsuche über das gesamte Repository liefert keinen Treffer.

  Einordnung: Die Lücke ist funktional folgenlos. Keiner der im Plan beschriebenen
  Programmabläufe ruft `ValidateReleaseAsync` auf; die Manifest-/Release-Prüfung findet
  faktisch in den Quellenimplementierungen statt (`AutoUpdateLocalFolderSource`,
  `AutoUpdateGithubSource`) und die Paketprüfung in `ValidateDownloadedPackageAsync`.
  Zu entscheiden ist daher, ob das Mitglied nachgezogen oder der Plan an den bewusst
  schlankeren Vertrag angepasst wird — Letzteres ist die naheliegendere Auflösung, da ein
  ungenutztes öffentliches Interface-Mitglied in einem NuGet-Paket dauerhaft mitgeschleppt
  werden müsste.

---

## Hinweise

### Bewusste Abweichungen ohne funktionale Lücke

Die folgenden Punkte weichen von der Buchstabenform des Plans ab, erfüllen aber den
beschriebenen Zweck vollständig. Sie sind als umgesetzt gewertet:

| Plan | Implementierung | Bewertung |
|------|-----------------|-----------|
| `IAutoUpdateInstaller.StartAsync` | `Start(string scriptPath)` (synchron) | Das Starten eines Prozesses ist synchron; ein `Task`-Rückgabewert wäre irreführend |
| `IAutoUpdatePackageStore.GetPendingPath` | `PendingAssetPath(fileName)` | Reine Umbenennung |
| `IAutoUpdateProcessRunner.StartPrepareEnvironment` | `EnsureUpdateUnitAvailable(scriptPath)` | Reine Umbenennung, gleiche Aufgabe |
| `UpdateSettingsStore.ApplyToOptionsAsync` | `ApplyToOptions` (synchron) | Reines Feldkopieren in die Singleton-Options, kein I/O |
| `SourceCheckWindowEvaluator.IsWithinWindow(DateTimeOffset)` | `IsWithinWindow(IReadOnlyList<SourceCheckTimeRange>, DateTimeOffset)` | Zeitfenster als Parameter statt als Feld — hält die Klasse zustandslos und rein, wie in der Designentscheidung („Specification") gefordert |
| `AutoUpdateGithubSource.Create(repositoryName, repositoryOwner)` | `Create(repositoryOwner, repositoryName)` | Der Plan ist in sich uneinheitlich (die Validierungstabelle nennt die umgekehrte Reihenfolge); die Implementierung folgt der üblichen GitHub-Konvention `owner/name` |
| `JsonFileStore` als „interne statische Klasse" | `public static class` | Sichtbarkeitsabweichung. Für ein NuGet-Paket wäre `internal` die sauberere Wahl, da die Klasse zur öffentlichen API-Oberfläche zählt und damit versioniert werden muss |
| `IValidateOptions<AutoUpdateOptions>` wird registriert | Validator wird in `UseAutoUpdate` direkt ausgeführt und wirft `OptionsValidationException` | Der Typ implementiert `IValidateOptions<AutoUpdateOptions>`, wird aber nicht im Container registriert. Die eager-Validierung erfüllt die Planvorgabe „Fehler beim Start" sogar zuverlässiger, weil `AutoUpdateOptions` als nackte Singleton-Instanz und nicht über `IOptions<T>` registriert ist — eine DI-Registrierung des Validators würde dort nie greifen |

### Ergänzungen über den Plan hinaus

Nicht im Plan vorgesehen, aber vorhanden und unschädlich:

- `AutoUpdateOptions.UpdateUnitName` und `AutoUpdateBuilder.WithUpdateUnitName` — verhindert, dass mehrere Anwendungen, die die Bibliothek nutzen, auf Linux dieselbe systemd-Unit belegen. Sinnvolle Härtung für ein wiederverwendbares Paket.
- `AutoUpdateBuilder.WithDownloadPath` — setzt den Download-Pfad, ohne den automatischen Download zu aktivieren; wird von `ProgramExtensions` genutzt, um `Updates:WorkingDirectory` zu übernehmen.
- `ScheduledInstallEvaluator` — zieht die Auslöselogik des Schedulers in eine rein testbare Klasse, analog zum geplanten `SourceCheckWindowEvaluator`.
- `ProcessOutputReader` (internal), `AsyncTestWait`, `AutoUpdateOptionsMapperTests`, `UpdateOrchestratorAdapterTests_LockAndSchedule`.

### Beobachtungen zur Nacharbeit

- **ZIP-Integrität ohne eigenen Test:** Die Planzeile `ValidateDownloadedPackageAsync_*` nennt „Prüfsumme, Größe, ZIP-Integrität". Der Code prüft die ZIP-Integrität (`ZipFile.OpenRead` plus `ValidateEntry` je Eintrag), die drei vorhandenen Tests decken jedoch nur Prüfsumme, Größenlimit und den Positivfall ab. Ein Test mit einem beschädigten Archiv würde diesen Pfad absichern.
- **Risiken des Plans sind adressiert:** Statusdatei-Schemawechsel (toleranter `Idle`-Fallback, Test `Load_WithUnreadableStateFile_FallsBackToIdle`), Lebensdauer-Wechsel auf Singleton (Adapter bleibt Scoped und zustandslos), `TimeProvider`-Doppelregistrierung (`TryAddSingleton` plus Test), Testprojekt-Aufteilung (Projektmappe baut mit 0 Fehlern) — alle im Risikoabschnitt genannten Punkte sind geschlossen.
- **Offene Punkte 1–7 des Plans:** Namensgebung (`SoftwareSchmiede.AutoUpdate`), Paketierungsvorbereitung ohne Veröffentlichung, eigener SemVer-Strang ab 0.1.0, kein macOS, kein `.bat`, gemeinsames Manifestschema für beide Quellen und tolerantes `status.json`-Verhalten sind durchgängig gemäß den empfohlenen Vorschlägen umgesetzt.
