# Tasks: Programmupdate als Komponentenpaket auslagern

Stand: Plan-Review vom 2026-07-29 (dritter Durchlauf). Umgesetzt: 123 von 124. Offen: 1 (Aufgabe 28).

Gegenueber dem Vorreview geschlossen: 2 (Paketreferenz `Microsoft.Extensions.Http` gesetzt), 21 (`UpdateOptions` um `EnableAutomaticDownload`/`EnableAutomaticInstallation` ergaenzt), 124 (`WithSourceCheck` nur noch bei fehlendem `Updates:SourceCheck:Interval`).
Neu als teilweise umgesetzt erkannt: 28 (`IAutoUpdatePackageValidator.ValidateReleaseAsync` fehlt).

Sanity-Check: `dotnet build FinanceManager.sln -c Debug` → 0 Fehler. Tests laut `test-results.md` 1.093/1.094 gruen (1 plattformbedingt uebersprungen).

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Projektstruktur | Projekt `SoftwareSchmiede.AutoUpdate.csproj` (net10.0, Nullable, XML-Doku, NuGet-Metadaten) anlegen | Erledigt | `UseAutoUpdateRegistrationTests.UseAutoUpdate_RegistersAllServices` |
| 2 | Projektstruktur | Paketreferenzen der Bibliothek setzen (`Microsoft.Extensions.Hosting.Abstractions`, `Options`, `Logging.Abstractions`, `Http`, `DependencyInjection.Abstractions`, `Configuration.Binder`) | Erledigt | `UseAutoUpdateRegistrationTests.UseAutoUpdate_RegistersAllServices` (alle 6 Referenzen gesetzt; `UseAutoUpdate` ruft `AddHttpClient()`, `Create` nutzt einen eigenen `SocketsHttpHandler` mit `PooledConnectionLifetime` und wird per `Dispose` freigegeben) |
| 3 | Projektstruktur | `SoftwareSchmiede.AutoUpdate` in `FinanceManager.sln` eintragen | Erledigt | Kein direkter Test (Build der Projektmappe, 0 Fehler) |
| 4 | Projektstruktur | Testprojekt `SoftwareSchmiede.AutoUpdate.Tests.csproj` anlegen und in `FinanceManager.sln` eintragen | Erledigt | Kein direkter Test (79 Tests laufen im Projekt) |
| 5 | Datenmodell | `AutoUpdateState` Enum anlegen | Erledigt | `AutoUpdateOrchestratorCheckTests.Check_WhenNewerVersionAvailable_SetsUpdateAvailable` |
| 6 | Datenmodell | `AutoUpdateOutcome` Enum anlegen | Erledigt | `AutoUpdateOrchestratorCheckTests.Check_WhenNoNewerVersion_ReturnsNoUpdate` |
| 7 | Datenmodell | `AutoUpdateResult` Record anlegen | Erledigt | `AutoUpdateCommandServiceTests.Commands_DelegateToOrchestrator` |
| 8 | Datenmodell | `AutoUpdateCheckResult` Record anlegen | Erledigt | `AutoUpdateLocalFolderSourceTests.Check_ReadsManifestFromFolder` |
| 9 | Datenmodell | `AutoUpdateDownloadResult` Record anlegen | Erledigt | `AutoUpdateOrchestratorDownloadTests.Run_DownloadsAndValidatesPackage` |
| 10 | Datenmodell | `AutoUpdateInstallResult` Record anlegen | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_GeneratesScriptAndStartsIt` |
| 11 | Datenmodell | `AutoUpdateStatusSnapshot` Record anlegen | Erledigt | `AutoUpdateStatusServiceTests.GetSnapshot_ReturnsConsistentState` |
| 12 | Datenmodell | `AutoUpdatePackageDescriptor` Record anlegen | Erledigt | `AutoUpdatePlatformResolverTests.SelectPackage_MatchesRuntimeIdentifier` |
| 13 | Datenmodell | `AutoUpdateReleaseInfo` Record anlegen | Erledigt | `AutoUpdatePlatformResolverTests.SelectPackage_MatchesRuntimeIdentifier` |
| 14 | Datenmodell | `InstalledReleaseInfo` Record anlegen | Erledigt | `InstalledReleaseMetadataProviderTests` (FinanceManager.Tests) |
| 15 | Datenmodell | `AutoUpdateInstallationTarget` Record anlegen (Portierung aus `UpdateContracts.cs`) | Erledigt | `AutoUpdateServiceResolverTests.Resolve_WithConfiguredServiceName_ReturnsServiceTarget` |
| 16 | Konfiguration | `SourceCheckTimeRange` (DayOfWeek, StartTime, EndTime) anlegen | Erledigt | `SourceCheckWindowEvaluatorTests.IsWithinWindow_InsideRange_ReturnsTrue` |
| 17 | Konfiguration | `SourceCheckOptions` (Interval, TimeRanges) anlegen | Erledigt | `AutoUpdateCheckerServiceTests.Execute_RespectsConfiguredInterval` |
| 18 | Konfiguration | `AutoUpdateOptions` als laufzeitveränderliche Singleton-Optionsklasse anlegen | Erledigt | `AutoUpdateBuilderTests.Builder_FluentChain_SetsAllOptions` |
| 19 | Konfiguration | `AutoUpdateBuilder` mit Fluent-Methoden `EnableAutomaticDownload`, `EnableAutomaticInstallation`, `UseSource`, `UseGithubSource`, `UseLocalFolderSource`, `WithSourceCheck`, `BindConfiguration`, `DisableHostedServices` anlegen | Erledigt | `AutoUpdateBuilderTests.Builder_FluentChain_SetsAllOptions`, `Builder_UseGithubSource_CreatesGithubSource`, `Builder_BindConfiguration_ReadsSection` |
| 20 | Konfiguration | `AutoUpdateHostBuilderExtensions.UseAutoUpdate(IHostApplicationBuilder, Action<AutoUpdateBuilder>?)` implementieren | Erledigt | `UseAutoUpdateRegistrationTests.UseAutoUpdate_RegistersAllServices` |
| 21 | Konfiguration | `UpdateOptions` (Web) um `SourceType`, `LocalFolderPath`, `EnableAutomaticDownload`, `EnableAutomaticInstallation` erweitern | Erledigt | Kein direkter Test (alle vier Eigenschaften vorhanden; `ProgramExtensions` ruft `EnableAutomaticDownload()`/`EnableAutomaticInstallation()` nicht auf, daher bindet `Updates:EnableAutomaticDownload=false` unveraendert durch) |
| 22 | Konfiguration | Sektion `Updates` in `appsettings.json` um die neuen Einträge ergänzen | Erledigt | Kein direkter Test (Werte verifiziert, verhaltensneutral für Bestandsinstallationen) |
| 23 | Schnittstellen | `IAutoUpdateSource` (CheckAsync, DownloadAsync) definieren | Erledigt | `AutoUpdateLocalFolderSourceTests.Download_CopiesPackageToTarget` |
| 24 | Schnittstellen | `IAutoUpdateEnvironment` definieren | Erledigt | `AutoUpdateScriptGeneratorTests.Generate_OnWindows_WritesPowerShellScript` |
| 25 | Schnittstellen | `IInstalledVersionProvider` definieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Reconcile_AfterRestart_WhenVersionMatches_SetsSuccess` |
| 26 | Schnittstellen | `IAutoUpdatePackageStore` definieren | Erledigt | `FileSystemAutoUpdatePackageStoreTests.Lock_CreateAndDelete_RoundTrips` |
| 27 | Schnittstellen | `IAutoUpdateStateStore` definieren | Erledigt | `FileSystemAutoUpdateStateStoreTests.Read_Write_RoundTrips` |
| 28 | Schnittstellen | `IAutoUpdatePackageValidator` mit `IsNewerVersion`, `ValidateReleaseAsync`, `ValidateDownloadedPackageAsync` definieren | Offen | — (`ValidateReleaseAsync` fehlt im Interface und in der Implementierung, repositoryweit kein Treffer; `IsNewerVersion` und `ValidateDownloadedPackageAsync` sind vorhanden. Funktional folgenlos, da kein Planablauf das Mitglied aufruft — alternativ Plan an den schlankeren Vertrag anpassen) |
| 29 | Schnittstellen | `IAutoUpdateScriptGenerator`, `IAutoUpdateProcessRunner`, `IAutoUpdateHostTerminator` definieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_WhenStopHostAfterScriptStart_TerminatesHost` |
| 30 | Schnittstellen | `IAutoUpdatePlatformResolver`, `IAutoUpdateServiceResolver`, `IAutoUpdateServiceProbe` definieren | Erledigt | `AutoUpdateServiceResolverTests.Resolve_WithoutServiceOrExecutable_Throws` |
| 31 | Schnittstellen | `IAutoUpdateInstaller` definieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_GeneratesScriptAndStartsIt` |
| 32 | Schnittstellen | `IAutoUpdateStatusProvider` definieren | Erledigt | `UpdateOrchestratorAdapterTests.Adapter_MapsSnapshotToUpdateStatusDto` |
| 33 | Schnittstellen | `IAutoUpdateEventAggregator` definieren | Erledigt | `AutoUpdateEventsTests.Raise_BeforeCheckSource_HonorsCancel` |
| 34 | Schnittstellen | `IAutoUpdateOrchestrator` definieren | Erledigt | `AutoUpdateCommandServiceTests.Commands_DelegateToOrchestrator` |
| 35 | Schnittstellen | `IAutoUpdateCommandHandler` definieren | Erledigt | `AutoUpdateSchedulerServiceTests.Execute_AtScheduledTime_TriggersInstall` |
| 36 | Events | `AutoUpdateCancelEventArgs` anlegen | Erledigt | `AutoUpdateEventsTests.Raise_BeforeCheckSource_HonorsCancel` |
| 37 | Events | `BeforeDownloadEventArgs` (Uri SourceUri) anlegen | Erledigt | `AutoUpdateOrchestratorEventTests.Run_WhenBeforeDownloadCanceled_DoesNotDownload` |
| 38 | Events | `BeforeInstallEventArgs` (FileInfo PackageFile) anlegen | Erledigt | `AutoUpdateOrchestratorEventTests.Run_WhenBeforeInstallCanceled_DoesNotInstall` |
| 39 | Events | `BeforeStartUpdateScriptEventArgs` (FileInfo ScriptFile) anlegen | Erledigt | `AutoUpdateOrchestratorEventTests.Run_WhenBeforeStartUpdateScriptCanceled_ReleasesLock` |
| 40 | Events | `AutoUpdateErrorEventArgs` (Exception, Phase) anlegen | Erledigt | `AutoUpdateEventsTests.Raise_WhenHandlerThrows_ReportsErrorAndContinues` |
| 41 | Events | `AutoUpdateEvents` mit thread-sicherer Abonnentenverwaltung implementieren | Erledigt | `AutoUpdateEventsTests.Subscribe_FromMultipleThreads_IsSafe` |
| 42 | Events | Raise-Methoden inkl. Handler-Ausnahmebehandlung (Meldung über `ErrorOccured`, kein Abbruch) implementieren | Erledigt | `AutoUpdateEventsTests.Raise_WhenHandlerThrows_ReportsErrorAndContinues`, `Raise_AfterStartUpdateScript_HasNoCancelSemantics` |
| 43 | Logik | `HostAutoUpdateEnvironment` implementieren | Erledigt | `UseAutoUpdateRegistrationTests.UseAutoUpdate_RegistersAllServices` |
| 44 | Logik | `JsonFileStore` (atomares JSON-Lesen/Schreiben) portieren | Erledigt | `FileSystemAutoUpdateStateStoreTests.Read_Write_RoundTrips` |
| 45 | Logik | `FileSystemAutoUpdatePackageStore` aus `UpdateFileStore` portieren (ohne `IWebHostEnvironment`) | Erledigt | `FileSystemAutoUpdatePackageStoreTests.PendingPath_CreatesDirectoryLayoutOnEnsure` |
| 46 | Logik | `FileSystemAutoUpdateStateStore` inkl. tolerantem Fallback bei fremdem Schema implementieren | Erledigt | `AutoUpdateStatusServiceTests.Load_WithUnreadableStateFile_FallsBackToIdle` |
| 47 | Logik | `AutoUpdatePackageValidator` aus `UpdateValidator` portieren | Erledigt | `AutoUpdatePackageValidatorTests.IsNewerVersion_ComparesInstalledAndAvailableVersions`, `ValidateDownloadedPackageAsync_RejectsChecksumMismatch` |
| 48 | Logik | `ReleaseMetadataInstalledVersionProvider` implementieren | Erledigt | `InstalledReleaseMetadataProviderTests` (FinanceManager.Tests) |
| 49 | Logik | `AutoUpdatePlatformResolver` aus `UpdatePlatformResolver` portieren | Erledigt | `AutoUpdatePlatformResolverTests.SelectPackage_MatchesRuntimeIdentifier` |
| 50 | Logik | `DefaultAutoUpdateServiceProbe` aus `DefaultUpdateServiceProbe` portieren | Erledigt | `AutoUpdateServiceResolverTests.Resolve_WithoutServiceOrExecutable_Throws` |
| 51 | Logik | `AutoUpdateServiceResolver` aus `UpdateServiceResolver` portieren | Erledigt | `AutoUpdateServiceResolverTests.Resolve_WithConfiguredServiceName_ReturnsServiceTarget`, `Resolve_WithInvalidServiceName_Throws` |
| 52 | Logik | `AutoUpdateScriptGenerator` aus `UpdateScriptGenerator` portieren | Erledigt | `AutoUpdateScriptGeneratorTests.Generate_OnWindows_WritesPowerShellScript`, `Generate_OnLinux_WritesShellScriptWithUnixLineEndings` |
| 53 | Logik | `DefaultAutoUpdateProcessRunner` aus `DefaultUpdateProcessRunner` portieren | Erledigt | `UseAutoUpdateRegistrationTests.UseAutoUpdate_RegistersAllServices` |
| 54 | Logik | `DefaultAutoUpdateHostTerminator` aus `DefaultUpdateHostTerminator` portieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_WhenStopHostAfterScriptStart_TerminatesHost` |
| 55 | Logik | `AutoUpdateInstaller` (PrepareAsync, StartAsync) aus `UpdateExecutor` portieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_GeneratesScriptAndStartsIt` |
| 56 | Logik | `AutoUpdateStatusService` mit Snapshot hinter `lock` und Persistenz implementieren | Erledigt | `AutoUpdateStatusServiceTests.Update_FromParallelThreads_KeepsLastWriteVisible`, `Snapshot_IsPersistedAndReloaded` |
| 57 | Logik | `SourceCheckWindowEvaluator.IsWithinWindow` implementieren | Erledigt | `SourceCheckWindowEvaluatorTests.IsWithinWindow_WithoutRanges_AlwaysTrue`, `IsWithinWindow_WrongDayOfWeek_ReturnsFalse`, `IsWithinWindow_OutsideRange_ReturnsFalse` |
| 58 | Logik | `AutoUpdateLocalFolderSource` implementieren (Standardquelle) | Erledigt | `AutoUpdateLocalFolderSourceTests.Check_ReadsManifestFromFolder`, `Check_WhenFolderMissing_ReturnsNoUpdate` |
| 59 | Logik | `AutoUpdateGithubSource` inkl. Factory `Create(repositoryName, repositoryOwner)` implementieren | Erledigt | `AutoUpdateGithubSourceTests.Create_WithEmptyOwner_Throws`, `Check_ParsesManifestResponse`, `Download_WhenHttpFails_Throws` |
| 60 | Logik | `AutoUpdateOrchestrator.CheckForUpdateAsync` implementieren | Erledigt | `AutoUpdateOrchestratorCheckTests.Check_WhenNewerVersionAvailable_SetsUpdateAvailable` |
| 61 | Logik | `AutoUpdateOrchestrator.DownloadAsync` implementieren | Erledigt | `AutoUpdateOrchestratorDownloadTests.Run_DownloadsAndValidatesPackage` |
| 62 | Logik | `AutoUpdateOrchestrator.InstallAsync` inkl. `BeforeStartUpdateScript`/`AfterStartUpdateScript` implementieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_GeneratesScriptAndStartsIt`, `AutoUpdateOrchestratorEventTests.Run_WhenBeforeStartUpdateScriptCanceled_ReleasesLock` |
| 63 | Logik | `AutoUpdateOrchestrator.RunUpdateAsync` als Vollworkflow implementieren | Erledigt | `AutoUpdateOrchestratorEventTests.Run_RaisesEventsInDocumentedOrder` |
| 64 | Logik | `AutoUpdateOrchestrator.GetStatusAsync` inkl. Zustandsabgleich nach Neustart implementieren | Erledigt | `AutoUpdateOrchestratorInstallTests.Reconcile_AfterRestart_WhenVersionMatches_SetsSuccess`, `Reconcile_AfterRestart_WhenVersionDiffers_SetsFailed` |
| 65 | Logik | Zentrale Fehlerbehandlung des Orchestrators (Ausnahme → `ErrorOccured` + Zustand `Failed` + `Outcome.Failed`) implementieren | Erledigt | `AutoUpdateOrchestratorCheckTests.Check_WhenSourceThrows_ReportsErrorAndFails` |
| 66 | Logik | Serialisierung paralleler Orchestrator-Aufrufe über `SemaphoreSlim` implementieren | Erledigt | `AutoUpdateCommandServiceTests.Commands_ParallelCalls_AreSerialized` |
| 67 | Logik | `AutoUpdateCommandService` als Fassade über den Orchestrator implementieren | Erledigt | `AutoUpdateCommandServiceTests.Commands_DelegateToOrchestrator`, `Commands_UpdateStatusService` |
| 68 | Hintergrunddienste | `AutoUpdateCheckerService` mit Intervall- und Zeitfensterprüfung implementieren | Erledigt | `AutoUpdateCheckerServiceTests.Execute_TriggersCheckOnlyWithinWindow`, `Execute_NeverTriggersDownloadOrInstall`, `Execute_WhenCheckThrows_ContinuesLoop`, `Execute_RespectsConfiguredInterval` |
| 69 | Hintergrunddienste | `AutoUpdateSchedulerService` für geplante Installationen implementieren | Erledigt | `AutoUpdateSchedulerServiceTests.Execute_AtScheduledTime_TriggersInstall`, `Execute_WhenNotReady_DoesNotInstall`, `Execute_SameScheduleTwice_InstallsOnce` |
| 70 | Validierung | `AutoUpdateOptionsValidator` (`IValidateOptions<AutoUpdateOptions>`) implementieren | Erledigt | `AutoUpdateOptionsValidationTests.Validate_WithInvalidInterval_Fails`, `Validate_WithInvertedTimeRange_Fails`, `Validate_WithEmptyDownloadPath_Fails`, `Validate_WithNonPositiveMaxAssetBytes_Fails` |
| 71 | Validierung | Pfadsicherheit für `AutoUpdatePackageDescriptor.FileName` (keine Pfadsegmente) implementieren | Erledigt | `FileSystemAutoUpdatePackageStoreTests.PendingPath_RejectsPathSegments` |
| 72 | Validierung | Bestätigungspflicht `confirmDowntime` in `InstallAsync` durchsetzen | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_WithoutConfirmDowntime_Fails` |
| 73 | Integration | `UpdateOrchestratorAdapter` (implementiert `IUpdateOrchestrator`) anlegen | Erledigt | `UpdateOrchestratorAdapterTests.Adapter_MapsSnapshotToUpdateStatusDto` |
| 74 | Integration | `AutoUpdateOptionsMapper` für `UpdateSettingsDto` ↔ `AutoUpdateOptions` anlegen | Erledigt | `AutoUpdateOptionsMapperTests.ApplySettings_ThenToSettingsDto_RoundTripsRuntimeRelevantFields`, `ToSettingsDto_ReflectsCurrentOptionsState` |
| 75 | Integration | Fehlerergebnis-Mapping des Adapters auf `FileNotFoundException`/`IOException`/`ArgumentException` implementieren | Erledigt | `UpdateOrchestratorAdapterTests.Adapter_MapsFailedResultToExpectedException` |
| 76 | Integration | `UpdateSettingsStore` auf `IAutoUpdatePackageStore` und `AutoUpdateOptions` umstellen, `ApplyToOptionsAsync` ergänzen | Erledigt | `UpdateSettingsStoreTests` (FinanceManager.Tests), `UpdateOrchestratorAdapterTests.Adapter_SaveSettings_AppliesToAutoUpdateOptions` |
| 77 | Integration | `InstalledReleaseMetadataProvider` auf `IInstalledVersionProvider` umstellen | Erledigt | `InstalledReleaseMetadataProviderTests` (FinanceManager.Tests) |
| 78 | Integration | Self-update-Block in `ProgramExtensions` durch `builder.UseAutoUpdate(...)` ersetzen | Erledigt | `UpdateControllerIntegrationTests` (Testserver startet vollständig hoch) |
| 79 | Integration | Adapter und verbleibende Web-Dienste in `ProgramExtensions` registrieren | Erledigt | `UpdateControllerIntegrationTests` (alle Endpunkte auflösbar) |
| 80 | Aufräumen | `UpdateOrchestrator`, `UpdateExecutor`, `UpdateFileStore`, `UpdateValidator` aus `FinanceManager.Web` entfernen | Erledigt | Kein direkter Test (Build der Projektmappe, 0 Fehler) |
| 81 | Aufräumen | `UpdateScriptGenerator`, `UpdatePlatformResolver`, `UpdateServiceResolver`, `DefaultUpdateProcessRunner`, `DefaultUpdateHostTerminator` entfernen | Erledigt | Kein direkter Test (Build der Projektmappe, 0 Fehler) |
| 82 | Aufräumen | `UpdateManifestClient`, `UpdateChecker`, `UpdateScheduler`, `JsonFileStore` aus `FinanceManager.Web` entfernen | Erledigt | Kein direkter Test (Build der Projektmappe, 0 Fehler) |
| 83 | Aufräumen | `UpdateContracts.cs` auf `IUpdateSettingsStore`, `IInstalledReleaseMetadataProvider`, `IUpdateOrchestrator` reduzieren | Erledigt | Kein direkter Test (Build der Projektmappe, 0 Fehler) |
| 84 | Tests | Testhilfe `FakeAutoUpdateSource` bereitstellen | Erledigt | `AutoUpdateOrchestratorCheckTests.Check_WhenSourceThrows_ReportsErrorAndFails` |
| 85 | Tests | Testhilfe `TestAutoUpdateEnvironment` bereitstellen | Erledigt | `AutoUpdateScriptGeneratorTests.Generate_OnWindows_WritesPowerShellScript` |
| 86 | Tests | Testhilfe `AutoUpdateTestContext` bereitstellen | Erledigt | `AutoUpdateOrchestratorInstallTests.Install_GeneratesScriptAndStartsIt` |
| 87 | Tests | `UseAutoUpdateRegistrationTests` schreiben | Erledigt | 4 Tests, u. a. `UseAutoUpdate_DoesNotOverrideExistingTimeProvider` |
| 88 | Tests | `AutoUpdateBuilderTests` schreiben | Erledigt | 3 Tests |
| 89 | Tests | `AutoUpdateOptionsValidationTests` schreiben | Erledigt | 4 Tests |
| 90 | Tests | `AutoUpdateEventsTests` schreiben | Erledigt | 4 Tests |
| 91 | Tests | `AutoUpdateStatusServiceTests` schreiben | Erledigt | 4 Tests |
| 92 | Tests | `AutoUpdateOrchestratorCheckTests` schreiben | Erledigt | 4 Tests |
| 93 | Tests | `AutoUpdateOrchestratorDownloadTests` schreiben | Erledigt | 4 Tests |
| 94 | Tests | `AutoUpdateOrchestratorInstallTests` schreiben | Erledigt | 8 Tests |
| 95 | Tests | `AutoUpdateOrchestratorEventTests` schreiben | Erledigt | 5 Tests |
| 96 | Tests | `AutoUpdateCommandServiceTests` schreiben | Erledigt | 3 Tests |
| 97 | Tests | `AutoUpdateLocalFolderSourceTests` schreiben | Erledigt | 3 Tests |
| 98 | Tests | `AutoUpdateGithubSourceTests` schreiben | Erledigt | 4 Tests |
| 99 | Tests | `SourceCheckWindowEvaluatorTests` schreiben | Erledigt | 4 Tests |
| 100 | Tests | `AutoUpdateCheckerServiceTests` schreiben | Erledigt | 4 Tests |
| 101 | Tests | `AutoUpdateSchedulerServiceTests` schreiben | Erledigt | 3 Tests |
| 102 | Tests | `AutoUpdatePackageValidatorTests` schreiben | Erledigt | 4 Tests (inkl. Theory `IsNewerVersion_ComparesInstalledAndAvailableVersions`) |
| 103 | Tests | `AutoUpdateScriptGeneratorTests` schreiben | Erledigt | 3 Tests |
| 104 | Tests | `AutoUpdatePlatformResolverTests` schreiben | Erledigt | `SelectPackage_MatchesRuntimeIdentifier` |
| 105 | Tests | `AutoUpdateServiceResolverTests` schreiben | Erledigt | 3 Tests |
| 106 | Tests | `FileSystemAutoUpdatePackageStoreTests` schreiben | Erledigt | 3 Tests |
| 107 | Tests | `FileSystemAutoUpdateStateStoreTests` schreiben | Erledigt | `Read_Write_RoundTrips` |
| 108 | Tests | `UpdateOrchestratorAdapterTests` in `FinanceManager.Tests/Updates/` schreiben | Erledigt | 3 Tests |
| 109 | Tests | Verschobene Testklassen aus `FinanceManager.Tests/Updates/` entfernen | Erledigt | Kein direkter Test (8 Dateien gelöscht, Build 0 Fehler) |
| 110 | Tests | `UpdateSettingsStoreTests` an die neuen Abhängigkeiten anpassen | Erledigt | `UpdateSettingsStoreTests` (29 Update-Tests grün) |
| 111 | Tests | `InstalledReleaseMetadataProviderTests` an `IInstalledVersionProvider` anpassen | Erledigt | `InstalledReleaseMetadataProviderTests` |
| 112 | Tests | `UpdateStatusTestData` und `TestWebHostEnvironment` an die verbliebene Nutzung angleichen | Erledigt | `UpdateOrchestratorAdapterTests.Adapter_MapsSnapshotToUpdateStatusDto` |
| 113 | Tests | `UpdateControllerIntegrationTests` auf lokale Quelle und deaktivierte Hintergrunddienste umstellen | Erledigt | `UpdateControllerIntegrationTests` (19 Tests grün) |
| 114 | E2E-Tests | `PlaywrightWebAppFixture` um `Updates__*`-Umgebungsvariablen erweitern | Erledigt | `UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` |
| 115 | E2E-Tests | Test-Gateway für den Setup-Update-Tab anlegen | Erledigt | `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs` |
| 116 | E2E-Tests | `UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` schreiben | Erledigt | `UpdateSetupPlaywrightTests.Admin_OpensUpdateTab_ShowsStatus` |
| 117 | E2E-Tests | `UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` schreiben | Erledigt | `UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` |
| 118 | E2E-Tests | `UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` schreiben | Erledigt | `UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` |
| 119 | E2E-Tests | `VersionDisplayPlaywrightTests` als Regressionsnachweis erneut ausführen | Erledigt | `VersionDisplayPlaywrightTests` (E2E-Lauf 28/28 gruen, `test-results.md`) |
| 120 | Dokumentation | `README.md` der Bibliothek (Verwendung, Optionen, Ereignisse, eigene Quellen, Plattformgrenzen) schreiben | Erledigt | Kein direkter Test (`SoftwareSchmiede.AutoUpdate/README.md`, alle Planabschnitte enthalten) |
| 121 | Dokumentation | `CHANGELOG.md`-Eintrag ergänzen | Erledigt | Kein direkter Test (Eintrag vorhanden) |
| 122 | Dokumentation | Betroffene Abschnitte in `Docs/help` aktualisieren | Erledigt | Kein direkter Test (`Docs/help/systemverwaltung-und-setup/ablauf-technisch.md`, `beschreibung.md`, `business-rules.md`, `api.md` beschreiben Bibliothek und neue `Updates`-Eintraege) |
| 123 | Paketierung | `dotnet pack` für `SoftwareSchmiede.AutoUpdate` lauffähig machen und gegen einen lokalen Paketordner prüfen | Erledigt | Kein direkter Test (`dotnet pack -c Release` erzeugt `SoftwareSchmiede.AutoUpdate.0.1.0.nupkg`) |
| 124 | Konfiguration | `Updates:SourceCheck:Interval` muss das Pruefintervall bestimmen, `CheckIntervalMinutes` nur noch als Alias | Erledigt | `UseAutoUpdateRegistrationTests.UseAutoUpdate_WithoutFluentSourceCheck_UsesConfiguredInterval`, `UseAutoUpdate_ExplicitSourceCheckInterval_TakesPrecedenceOverConfiguration` |
