# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

Stand: 2026-07-29 (vierter Review-Durchlauf). Geprüft wurde `plan.md` (514 Zeilen, aktueller Stand)
gegen den Code im Arbeitsverzeichnis. Von 125 Aufgaben sind 123 umgesetzt, 2 sind teilweise umgesetzt.

Gegenüber dem Vorreview:

- **Geschlossen:** Aufgabe 28 (`IAutoUpdatePackageValidator.ValidateReleaseAsync`). Der Plan wurde
  inzwischen angepasst und schließt das Mitglied in Zeile 166 ausdrücklich aus („kein separates
  `ValidateReleaseAsync`"). Der implementierte Vertrag (`IsNewerVersion`,
  `ValidateDownloadedPackageAsync`) entspricht damit dem Plan.
- **Neu offen (Rückschritt):** Aufgabe 21. `UpdateOptions.EnableAutomaticDownload` und
  `UpdateOptions.EnableAutomaticInstallation` waren im letzten Durchlauf vorhanden und sind im
  aktuellen Arbeitsstand wieder entfernt worden (`git diff` auf
  `FinanceManager.Web/Services/Updates/UpdateOptions.cs` zeigt die Löschung beider Eigenschaften).
- **Neu erkannt:** Aufgabe 125. Der Plan fordert in „Registrierung beim Start", Schritt 4, die
  Registrierung von `IValidateOptions<AutoUpdateOptions>`; `UseAutoUpdate` registriert diesen
  Dienst nicht.

Sanity-Check: `dotnet build FinanceManager.sln -c Debug` → **0 Fehler**, 60 Warnungen (sämtlich
Vorbestand: NU1903-Sicherheitshinweise und NU1510-Trimming-Hinweise, keine aus
`SoftwareSchmiede.AutoUpdate`). Testlauf war nicht Teil dieses Schritts; laut `test-results.md`
zuletzt 1.093/1.094 Tests grün (1 Test plattformbedingt übersprungen).

---

## Umgesetzte Planelemente

### Projektstruktur (Planschritte 1, 14)

- [x] `SoftwareSchmiede.AutoUpdate.csproj` — angelegt (net10.0, `Nullable`, `ImplicitUsings`,
  `GenerateDocumentationFile`, `WarningsAsErrors` inkl. `CS1591` für Debug und Release)
- [x] NuGet-Metadaten — `PackageId`, `Version` 0.1.0 (eigener SemVer-Strang gemäß Offenem Punkt 3),
  `Description`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl`, `PackageReadmeFile`
- [x] Alle sechs geplanten Paketreferenzen vorhanden (`Hosting.Abstractions`, `Options`,
  `Logging.Abstractions`, `Http`, `DependencyInjection.Abstractions`, `Configuration.Binder`);
  keine `FrameworkReference` auf `Microsoft.AspNetCore.App`
- [x] `SoftwareSchmiede.AutoUpdate.Tests.csproj` — angelegt (xunit.v3, FluentAssertions, Moq,
  `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.NET.Test.Sdk`, coverlet), Projektreferenz
  auf die Bibliothek gesetzt
- [x] Beide Projekte in `FinanceManager.sln` eingetragen

### Modelle und Zustandsmodell (Planschritt 2)

- [x] `AutoUpdateState` (Enum) — alle neun Werte: Idle, Checking, UpdateAvailable, Downloading,
  ReadyToInstall, Installing, Success, Failed, Disabled
- [x] `AutoUpdateOutcome` (Enum) — alle fünf Werte: Success, NoUpdate, Skipped, Canceled, Failed
- [x] `AutoUpdateResult`, `AutoUpdateCheckResult`, `AutoUpdateDownloadResult`,
  `AutoUpdateInstallResult` (records) — angelegt
- [x] `AutoUpdateStatusSnapshot` (record) — alle zehn geplanten Felder vorhanden
- [x] `AutoUpdatePackageDescriptor`, `AutoUpdateReleaseInfo`, `InstalledReleaseInfo`,
  `AutoUpdateInstallationTarget` (records) — angelegt

### Konfigurationsmodell und Zeitfenster (Planschritt 3)

- [x] `SourceCheckTimeRange` (DayOfWeek, StartTime, EndTime) — angelegt
- [x] `SourceCheckOptions` (Interval, TimeRanges) — angelegt
- [x] `AutoUpdateOptions` — alle 13 geplanten Eigenschaften vorhanden (Enabled,
  EnableAutomaticDownload, DownloadPath, EnableAutomaticInstallation, Source, SourceCheck,
  MaxAssetBytes, HostedServicesEnabled, ScheduledInstallTime, ServiceName, ExecutablePath,
  StopHostAfterScriptStart, HealthTimeoutSeconds)
- [x] `SourceCheckWindowEvaluator.IsWithinWindow(IReadOnlyList<SourceCheckTimeRange>, DateTimeOffset)`
  — leere Liste bedeutet „immer erlaubt"

### Kern-Schnittstellen (Planschritt 4)

- [x] Alle 17 geplanten Interfaces vorhanden: `IAutoUpdateSource`, `IAutoUpdateEnvironment`,
  `IInstalledVersionProvider`, `IAutoUpdatePackageStore`, `IAutoUpdateStateStore`,
  `IAutoUpdatePackageValidator`, `IAutoUpdateScriptGenerator`, `IAutoUpdatePlatformResolver`,
  `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe`, `IAutoUpdateProcessRunner`,
  `IAutoUpdateHostTerminator`, `IAutoUpdateInstaller`, `IAutoUpdateStatusProvider`,
  `IAutoUpdateEventAggregator`, `IAutoUpdateOrchestrator`, `IAutoUpdateCommandHandler`
- [x] `IAutoUpdatePackageValidator` — Vertrag entspricht dem aktuellen Plan (`IsNewerVersion`,
  `ValidateDownloadedPackageAsync`; `ValidateReleaseAsync` ist laut Plan Zeile 166 bewusst nicht Teil
  des Vertrags)

### Event-Infrastruktur (Planschritt 5)

- [x] `AutoUpdateCancelEventArgs`, `BeforeDownloadEventArgs` (`Uri SourceUri`),
  `BeforeInstallEventArgs` (`FileInfo PackageFile`), `BeforeStartUpdateScriptEventArgs`
  (`FileInfo ScriptFile`), `AutoUpdateErrorEventArgs` (`Exception`, Phase) — angelegt
- [x] `AutoUpdateEvents` — thread-sichere Abonnentenverwaltung über `Lock`, alle sechs Ereignisse
  (`BeforeCheckSource`, `BeforeDownload`, `BeforeInstall`, `BeforeStartUpdateScript`,
  `AfterStartUpdateScript`, `ErrorOccurred`)
- [x] Handler-Ausnahmebehandlung gemäß Designentscheidung: Ausnahme wird gefangen, über
  `ErrorOccurred` gemeldet, der Ablauf läuft weiter, die Abbruch-Stimme des fehlgeschlagenen
  Handlers zählt nicht (`AutoUpdateEvents.RaiseCancelable`)

### Umgebung, Persistenz und Validierung (Planschritt 6)

- [x] `HostAutoUpdateEnvironment` — `ApplicationDirectory` über `IHostEnvironment.ContentRootPath`
- [x] `JsonFileStore` — atomares Lesen/Schreiben portiert
- [x] `FileSystemAutoUpdatePackageStore` — Portierung von `UpdateFileStore` ohne
  `IWebHostEnvironment`; Verzeichnislayout `pending`/`staging`, `update.lock`, `update.log` und
  Standardwert `updates` erhalten
- [x] `FileSystemAutoUpdateStateStore` — atomare Persistenz nach `<DownloadPath>/status.json`,
  toleranter Fallback bei `JsonException`/`IOException` inkl. Warnprotokollierung (Offener Punkt 7)
- [x] `AutoUpdatePackageValidator` — SemVer-Vergleich, SHA256, ZIP-Integrität, Größenlimit
- [x] `ReleaseMetadataInstalledVersionProvider` — liest `release-metadata.json`

### Plattform- und Installationsdienste (Planschritt 7)

- [x] `AutoUpdatePlatformResolver`, `DefaultAutoUpdateServiceProbe`, `AutoUpdateServiceResolver`,
  `AutoUpdateScriptGenerator` (Windows `.ps1`, Linux `.sh`, sonst `InvalidOperationException`),
  `DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateHostTerminator` (`IHostApplicationLifetime`),
  `AutoUpdateInstaller` — portiert

### Status-Service (Planschritt 8)

- [x] `AutoUpdateStatusService` — unveränderlicher Snapshot hinter `Lock`, Austausch statt
  feldweiser Mutation, Persistenz über `IAutoUpdateStateStore`, verzögertes Laden über
  `EnsureLoadedAsync`, Fallback auf `AutoUpdateStatusSnapshot.Idle`

### Quellen (Planschritt 9)

- [x] `AutoUpdateLocalFolderSource` — Manifest `update.json`, fehlendes Verzeichnis liefert
  `AutoUpdateCheckResult` ohne Version statt einer Ausnahme, leeres Quellverzeichnis wirft
  `ArgumentException`
- [x] `AutoUpdateGithubSource` — statische Factory `Create(repositoryOwner, repositoryName,
  manifestAssetName?)` mit Argumentprüfung, Manifest-Abruf und Asset-Download portiert

### Orchestrator und Command-Service (Planschritte 10, 11)

- [x] `AutoUpdateOrchestrator` — `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`,
  `InstallAsync`, `GetStatusAsync`; Serialisierung über internes `SemaphoreSlim`
- [x] Ablauf „Vollständiger Update-Workflow" vollständig abgebildet: `Disabled`/`Skipped` bei
  `Enabled = false`, Abbruch über `BeforeCheckSource`/`BeforeDownload`/`BeforeInstall`,
  `NoUpdate` ohne neuere Version, `Skipped` bei deaktiviertem Download bzw. deaktivierter
  Installation, zentrale Fehlerbehandlung ohne Weitergabe der Ausnahme
- [x] Ablauf „Installation und Skriptstart": Sperre über `TryCreateLockAsync`,
  `PrepareAsync` → `BeforeStartUpdateScript` → Zustand `Installing` **vor** Skriptstart
  persistiert → Skriptstart → `AfterStartUpdateScript` → optionaler `StopApplication`;
  Sperrfreigabe bei Abbruch und Fehler
- [x] Ablauf „Zustandsabgleich nach Neustart": `Installing` ohne Sperrdatei → Versionsvergleich →
  `Success` mit geleerten Feldern bzw. `Failed` mit erklärender Meldung
- [x] `AutoUpdateCommandService` — dünne Fassade ohne eigene Update-Logik

### Hintergrunddienste (Planschritt 12)

- [x] `AutoUpdateCheckerService` — Zeitfensterprüfung über `SourceCheckWindowEvaluator`,
  Intervall aus `SourceCheck.Interval`, `TimeProvider`-basiertes Warten, Rückfallwartezeit nach
  Ausnahmen mit eigener Abbruchbehandlung
- [x] `AutoUpdateSchedulerService` — minütliche Prüfung, `InstallAsync(confirmDowntime: true)`,
  Merken von Datum/Uhrzeit des letzten Versuchs (über `ScheduledInstallEvaluator`)

### Builder, Validator und Registrierung (Planschritt 13)

- [x] `AutoUpdateBuilder` — alle acht geplanten Fluent-Methoden (`EnableAutomaticDownload`,
  `EnableAutomaticInstallation`, `UseSource`, `UseGithubSource`, `UseLocalFolderSource`,
  `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices`)
- [x] `AutoUpdateHostBuilderExtensions.UseAutoUpdate(IHostApplicationBuilder, Action<AutoUpdateBuilder>?)`
  — einziger Registrierungspunkt, Erweiterung auf `IHostApplicationBuilder`
- [x] Standardquelle `AutoUpdateLocalFolderSource`, wenn keine Quelle gesetzt wurde
- [x] Alle Dienste per `TryAddSingleton`; `TimeProvider` nur per `TryAddSingleton` (keine
  Doppelregistrierung)
- [x] Hosted Services nur bei `HostedServicesEnabled`
- [x] `AutoUpdateOptionsValidator` (`IValidateOptions<AutoUpdateOptions>`) implementiert und in
  `UseAutoUpdate` ausgeführt; Verstöße führen zu `OptionsValidationException` beim Start

### Validierungsregeln (Planabschnitt „Validierungsregeln")

- [x] `DownloadPath` nicht leer, keine ungültigen Pfadzeichen; Verzeichnisanlage über `EnsureAsync`
- [x] `Source` nach Builder-Auswertung nicht `null`
- [x] `MaxAssetBytes > 0`
- [x] `HealthTimeoutSeconds` auf 10–600 geklemmt (`UseAutoUpdate`)
- [x] `SourceCheckOptions.Interval >= 1`
- [x] `SourceCheckTimeRange.StartTime < EndTime`
- [x] `AutoUpdateGithubSource.Create` — `ArgumentException` bei leerem Owner/Namen
- [x] `AutoUpdateLocalFolderSource` — fehlendes Verzeichnis ohne Ausnahme
- [x] `AutoUpdatePackageDescriptor.FileName` — `InvalidOperationException` bei Pfadsegmenten
  (`FileSystemAutoUpdatePackageStore.PendingAssetPath`)
- [x] Heruntergeladenes Paket — Größe, SHA256, ZIP-Integrität
- [x] Versionsvergleich semantisch; unbekannte installierte Version
- [x] `InstallAsync(confirmDowntime)` — `Outcome.Failed` mit `ArgumentException` als
  `AutoUpdateResult.Error`, vom Adapter für HTTP 400 geworfen
- [x] Installationsziel Windows/Linux — `InvalidOperationException` bei fehlender Konfiguration

### Änderungen an bestehenden Klassen (Planabschnitt „Änderungen an bestehenden Klassen")

- [x] `ProgramExtensions` — Self-update-Block durch einen einzigen `builder.UseAutoUpdate(cfg => …)`
  ersetzt; Sektion `Updates` gebunden, Quellenwahl über `Updates:SourceType`,
  `HostedServicesEnabled` aus Konfiguration; `TimeProvider.System` bleibt registriert
- [x] `ProgramExtensions` — Neuregistrierungen: `IUpdateOrchestrator` → `UpdateOrchestratorAdapter`
  (Scoped), `IUpdateSettingsStore` → `UpdateSettingsStore` (Singleton),
  `IInstalledReleaseMetadataProvider` → `InstalledReleaseMetadataProvider` (Singleton)
- [x] `UpdateContracts.cs` — auf `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`,
  `IUpdateOrchestrator` reduziert; alle zehn entfallenden Interfaces und
  `record UpdateInstallationTarget` entfernt
- [x] `UpdateSettingsStore` — nutzt `IAutoUpdatePackageStore` (Pfad der `settings.json`) und
  `AutoUpdateOptions` (Standardwerte); Legacy-Migration `windowsServiceName`/`linuxServiceName` →
  `ServiceName` unverändert erhalten; Übertragung in die Singleton-Options ergänzt
- [x] `InstalledReleaseMetadataProvider` — delegiert an `IInstalledVersionProvider`, mappt auf
  `InstalledReleaseMetadataDto`; `IWebHostEnvironment`-Abhängigkeit entfallen
- [x] `UpdateOptions` — neue Eigenschaften `SourceType` und `LocalFolderPath`; `WorkingDirectory`
  wird auf `AutoUpdateOptions.DownloadPath`, `CheckIntervalMinutes` auf `SourceCheck.Interval`
  abgebildet
- [x] `UpdateController`, `SetupUpdateViewModel`, `SetupUpdateTab.razor` — unverändert
- [x] `UpdateOrchestratorAdapter` und `AutoUpdateOptionsMapper` — angelegt; Adapter übersetzt
  Fehlerergebnisse in `FileNotFoundException`/`IOException`/`ArgumentException`

### Aufräumen (Planschritt 16)

- [x] Alle 13 geplanten Löschungen erfolgt (`UpdateOrchestrator`, `UpdateExecutor`,
  `UpdateFileStore`, `UpdateValidator`, `UpdateScriptGenerator`, `UpdatePlatformResolver`,
  `UpdateServiceResolver`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator`,
  `UpdateManifestClient`, `UpdateChecker`, `UpdateScheduler`, `JsonFileStore`); repositoryweit
  keine Referenz auf die alten Typen mehr

### Konfiguration (Planschritt 19)

- [x] `Updates:SourceType` (`Github`), `Updates:LocalFolderPath` (`null`),
  `Updates:EnableAutomaticDownload` (`true`), `Updates:EnableAutomaticInstallation` (`false`),
  `Updates:SourceCheck:Interval` (`360`), `Updates:SourceCheck:TimeRanges` (`[]`),
  `Updates:StopHostAfterScriptStart` (`false`) in `appsettings.json` ergänzt
- [x] Bestehende Einträge unverändert weiterverwendet

### Tests (Planschritte 15, 20, 21, 22)

- [x] Testhilfen `FakeAutoUpdateSource`, `TestAutoUpdateEnvironment`, `AutoUpdateTestContext` unter
  `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/`
- [x] Alle 21 geplanten Testklassen vorhanden; sämtliche geplanten Prüfinhalte abgedeckt (teils
  unter präziseren Namen, siehe Hinweise)
- [x] `UpdateOrchestratorAdapterTests` in `FinanceManager.Tests/Updates/` inkl. der drei geplanten
  Fälle (`Adapter_MapsSnapshotToUpdateStatusDto`, `Adapter_MapsFailedResultToExpectedException`,
  `Adapter_SaveSettings_AppliesToAutoUpdateOptions`)
- [x] Verschobene Testklassen aus `FinanceManager.Tests/Updates/` entfernt;
  `UpdateSettingsStoreTests` und `InstalledReleaseMetadataProviderTests` angepasst,
  `UpdateStatusTestData` liefert Bibliotheks-Snapshots
- [x] `UpdateControllerIntegrationTests`/`TestWebApplicationFactory` — lokale Ordnerquelle,
  `Updates:HostedServicesEnabled=false`, Ersetzung von `IUpdateOrchestrator` greift weiterhin
- [x] `PlaywrightWebAppFixture` — alle fünf geplanten `Updates__*`-Umgebungsvariablen gesetzt
- [x] `SetupUpdateGateway` und `UpdateSetupPlaywrightTests` mit allen drei geplanten E2E-Szenarien

### Dokumentation (Planschritt 23)

- [x] `SoftwareSchmiede.AutoUpdate/README.md` — Verwendung von `UseAutoUpdate`, Fluent-Optionen,
  Ereignisse, eigene Quellen, unterstützte Plattformen, Fehlerbehandlung, Projektstruktur
- [x] `CHANGELOG.md`-Eintrag vorhanden
- [x] `Docs/help/systemverwaltung-und-setup/*` aktualisiert

---

## Offene Aufgaben

- [ ] `UpdateOptions.EnableAutomaticDownload` / `UpdateOptions.EnableAutomaticInstallation`
  (`FinanceManager.Web/Services/Updates/UpdateOptions.cs`) — **teilweise umgesetzt**: Der Plan
  („Änderungen an bestehenden Klassen" → `UpdateOptions`) nennt vier neue Eigenschaften.
  Vorhanden sind nur `SourceType` und `LocalFolderPath`; die beiden Schalter waren im letzten
  Durchlauf vorhanden und wurden im aktuellen Arbeitsstand wieder entfernt (nachweisbar über
  `git diff` auf die Datei).

  Einordnung: Funktional folgenlos. Die Konfigurationsschlüssel `Updates:EnableAutomaticDownload`
  und `Updates:EnableAutomaticInstallation` binden über `cfg.BindConfiguration("Updates")` direkt
  auf `AutoUpdateOptions` und sind wirksam (der E2E-Testserver setzt
  `Updates__EnableAutomaticInstallation=false` und verlässt sich darauf). Eigenschaften auf
  `UpdateOptions` wären reiner Spiegel ohne Leser. Empfehlung: Plan an den schlankeren Vertrag
  anpassen statt toten Code nachzuziehen — analog zur bereits erfolgten Anpassung bei
  `ValidateReleaseAsync`.

- [ ] Registrierung von `IValidateOptions<AutoUpdateOptions>`
  (`SoftwareSchmiede.AutoUpdate/AutoUpdateHostBuilderExtensions.cs`) — **teilweise umgesetzt**:
  Der Plan verlangt im Ablauf „Registrierung beim Start", Schritt 4, dass `UseAutoUpdate` neben den
  Standardimplementierungen auch `IValidateOptions<AutoUpdateOptions>` registriert. Die Klasse
  `AutoUpdateOptionsValidator` existiert und wird in `BuildOptions` direkt instanziiert und
  ausgeführt; eine DI-Registrierung erfolgt nicht (repositoryweit kein
  `AddSingleton<IValidateOptions<AutoUpdateOptions>>`).

  Einordnung: Die geplante Wirkung — Startvalidierung mit `OptionsValidationException` — ist
  erreicht und durch `AutoUpdateOptionsValidationTests` abgesichert. Die DI-Registrierung wäre
  ohne zusätzlichen Effekt, da die Bibliothek `AutoUpdateOptions` als Instanz-Singleton und nicht
  über das `IOptions`-Muster registriert. Empfehlung: entweder eine Zeile
  `builder.Services.TryAddSingleton<IValidateOptions<AutoUpdateOptions>, AutoUpdateOptionsValidator>()`
  ergänzen (billig, macht den Validator für Konsumenten erweiterbar) oder den Planablauf an die
  eager ausgeführte Validierung anpassen.

---

## Hinweise

- **Planinterner Widerspruch beim Prüfdienst:** Der Programmablauf „Periodische Quellprüfung"
  (Planzeile 94) verlangt ausdrücklich, dass `AutoUpdateCheckerService` vollständig an
  `RunUpdateAsync` delegiert; die Umsetzungsreihenfolge (Schritt 12, Planzeile 336) und der
  Testname `Execute_NeverTriggersDownloadOrInstall` verlangen „ruft ausschließlich
  `CheckForUpdateAsync`". Die Implementierung folgt dem detaillierteren Programmablauf und ruft
  `RunUpdateAsync`; der Test heißt entsprechend
  `Execute_OnlyCallsRunUpdateAsync_NeverIndividualSteps`. Ohne diese Wahl könnten
  `EnableAutomaticDownload`/`EnableAutomaticInstallation` im Hintergrundbetrieb nie greifen. Der
  Plan sollte in Schritt 12 und in der Testtabelle nachgezogen werden.
- **Namensabweichungen ohne inhaltliche Lücke** (Element jeweils vorhanden und getestet):
  `IAutoUpdatePackageStore.PendingAssetPath` statt geplantem `GetPendingPath`;
  `IAutoUpdateInstaller.Start(string)` statt `StartAsync`;
  `IAutoUpdateProcessRunner.EnsureUpdateUnitAvailable` statt `StartPrepareEnvironment`;
  `IUpdateSettingsStore.ApplyToOptions` (synchron) statt `ApplyToOptionsAsync`;
  `AutoUpdateGithubSource.Create(repositoryOwner, repositoryName, …)` statt der im Plan genannten
  Parameterreihenfolge `Create(repositoryName, repositoryOwner)`.
- **Verdrahtung des Adapters:** Der Plan beschreibt in „Manuelle Steuerung" die Kette
  Adapter → `IAutoUpdateCommandHandler` → Orchestrator und einen Lesezugriff über
  `IAutoUpdateStatusProvider`. Der `UpdateOrchestratorAdapter` injiziert stattdessen direkt
  `IAutoUpdateOrchestrator` und die konkrete `AutoUpdateStatusService` (Letztere, weil
  `ResetLockAsync` den Snapshot schreiben muss, was `IAutoUpdateStatusProvider` nicht anbietet).
  `AutoUpdateCommandService` bleibt registriert und wird vom `AutoUpdateSchedulerService` genutzt.
  Inhaltlich gleichwertig, da der Command-Service laut Plan keine eigene Logik enthält.
- **`JsonFileStore` ist `public static`**, im Plan als „interne statische Klasse" geführt. Die
  Sichtbarkeit wird gebraucht, weil `UpdateSettingsStore` in `FinanceManager.Web` die
  `JsonOptions` und `WriteAtomicAsync` mitverwendet. Für ein öffentliches NuGet-Paket ist das ein
  bewusst mitveröffentlichter Hilfstyp — vor der Veröffentlichung entscheiden, ob er Teil der
  öffentlichen API bleiben soll.
- **Zusätzliche, im Plan nicht genannte Bausteine** (keine Lücke, nur zur Kenntnis):
  `UpdateStatusMapper` (Web, entlastet den Adapter), `ScheduledInstallEvaluator`,
  `AutoUpdateSourceDownloadHelper`, `ProcessOutputReader`, `AutoUpdateOptions.UpdateUnitName` samt
  `AutoUpdateBuilder.WithUpdateUnitName` (verhindert Kollisionen der systemd-Unit zwischen
  mehreren Anwendungen) und `AutoUpdateBuilder.WithDownloadPath` (Download-Pfad setzen, ohne
  automatischen Download zu aktivieren — von `ProgramExtensions` genutzt).
- **Testnamen weichen teilweise vom Plan ab, die Prüfinhalte sind vollständig abgedeckt:**
  `Execute_TriggersCheckOnlyWithinWindow` → `Execute_RunsUpdateWorkflow_WithinWindow` +
  `Execute_DoesNotRun_OutsideWindow`; `Execute_WhenCheckThrows_ContinuesLoop` →
  `Execute_WhenRunThrows_ContinuesLoop`; `Commands_DelegateToOrchestrator` → je ein Test für
  Check/Download/Install; `Read/Write_RoundTrips` → `Read_Write_RoundTrips`. Zusätzlich sind
  mehrere über den Plan hinausgehende Tests vorhanden (u. a. Zip-Slip-Prüfung,
  `Install_WhenCanceledAndLockDeletionFails_ReportsError`, `ProcessOutputReaderTests`).
- **Bekannte, im Plan akzeptierte Einschränkungen bleiben bestehen:** keine macOS-Unterstützung
  (`AutoUpdateScriptGenerator` wirft `InvalidOperationException`), Verlust der Prüfhistorie beim
  ersten Start nach dem Umbau (fremdes `status.json`-Schema → `Idle` mit Warnprotokoll).
