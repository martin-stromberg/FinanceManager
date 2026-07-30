# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs, UpdateSettingsStore.cs

- **Kopplung / Datenverlust bei Konfigurationsänderung** — `ApplySettings` (Zeile 26) setzt `options.DownloadPath = settings.WorkingDirectory`. Der Ablageort der Einstellungsdatei hängt aber genau davon ab: `UpdateSettingsStore.SettingsPath` (Zeile 31) ist `Path.Combine(_packageStore.RootDirectory, "settings.json")`, und `FileSystemAutoUpdatePackageStore.RootDirectory` wird aus `_options.DownloadPath` abgeleitet. In `UpdateOrchestratorAdapter.SaveSettingsAsync` (Zeilen 58–60) wird erst geschrieben (altes Verzeichnis) und danach `ApplyToOptions` aufgerufen (neues Verzeichnis). Ändert ein Administrator im Setup-UI das Arbeitsverzeichnis, liegt die gerade gespeicherte `settings.json` im alten Verzeichnis und wird nie wieder gelesen; der nächste `GetAsync` fällt auf `Defaults()` zurück und setzt `RepositoryOwner`, `RepositoryName` und `ManifestAssetName` stillschweigend auf die `appsettings`-Werte zurück. Zusätzlich wechseln `status.json` und `update.lock` mitten im Betrieb das Verzeichnis, sodass ein aktiver Installations-Lock unsichtbar wird.

  Empfehlung: In `UpdateSettingsStore.SaveAsync`/`SaveScheduleAsync` bei geändertem `WorkingDirectory` die Datei nach dem Umschalten der Optionen erneut ins neue Zielverzeichnis schreiben (bzw. die vorhandene Datei dorthin verschieben), oder `WorkingDirectory` aus `ApplySettings` herausnehmen und im Setup-UI als neustartpflichtiges Feld kennzeichnen.

### FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **Fehlerbehandlung / Uneinheitlichkeit** — `ResetLockAsync` (Zeile 105) verwirft den Rückgabewert von `_packageStore.DeleteLockAsync(ct)` und setzt anschließend bedingungslos `IsLocked = false` im Statussnapshot. Schlägt das Löschen fehl (Rückgabe `false`, siehe `FileSystemAutoUpdatePackageStore.DeleteLockAsync`, Zeilen 123–126), meldet die API `204 No Content`, der Status zeigt „nicht gesperrt", die Lock-Datei existiert aber weiterhin — jede weitere Installation scheitert dauerhaft mit „An update lock is already active.". `AutoUpdateOrchestrator.ReleaseLockAsync` (Zeilen 331–340) behandelt exakt denselben Fall korrekt und meldet ihn über `RaiseErrorOccurred`; die beiden Stellen sind uneinheitlich.

  Empfehlung: Rückgabewert auswerten und bei `false` eine `IOException` werfen (analog zu den beiden bereits vorhandenen Vorbedingungsprüfungen in derselben Methode), damit der Controller sie auf `Err_Update_Locked` abbilden kann; den Statussnapshot erst nach erfolgreichem Löschen aktualisieren.

- **Fehlende Interfaces / Inappropriate Intimacy** — Der Adapter injiziert die konkrete Bibliotheksklasse `AutoUpdateStatusService` (Zeile 17), obwohl die Bibliothek mit `IAutoUpdateStatusProvider` eine Abstraktion anbietet. Grund ist, dass `UpdateAsync(...)` (benötigt in `ResetLockAsync`, Zeile 107) auf keinem Interface liegt. Damit hängt der Consumer an einer konkreten Klasse eines als NuGet-Paket vorgesehenen Pakets und kann den Statusdienst weder mocken noch austauschen; die Testhilfsklasse `UpdateOrchestratorAdapterTestFactory.CreateStatusService()` muss deshalb eine echte Instanz mit zwei Mocks aufbauen, statt einen einzigen Mock zu verwenden.

  Empfehlung: In der Bibliothek entweder `IAutoUpdateStatusProvider` um eine schreibende Operation erweitern oder — passender zur bereits vorhandenen `IAutoUpdatePackageStore.IsLockStale`-Auslagerung — eine `ResetStaleLockAsync(string? reason, CancellationToken)` auf `IAutoUpdateOrchestrator` ergänzen, die Lock-Löschung und Statusaktualisierung atomar erledigt. Der Adapter reduziert sich dann auf `IAutoUpdateOrchestrator`, `IUpdateSettingsStore` und `UpdateStatusMapper`.

### SoftwareSchmiede.AutoUpdate/AutoUpdateServiceResolver.cs (AutoUpdateServiceResolver)

- **Fehlerbehandlung / unvollständige Validierung** — `ValidateExecutablePath` (Zeile 102) prüft `fullPath.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)` ohne abschließendes Verzeichnistrennzeichen. Bei `ApplicationDirectory = "C:\app"` besteht damit auch `C:\application\fremd.exe` die Prüfung, obwohl die Fehlermeldung „Executable path must point to the current application directory" das explizit ausschließen soll. Der Pfad wird anschließend ungeprüft in das generierte PowerShell-Skript geschrieben (`AutoUpdateScriptGenerator.GenerateWindowsAsync`, Zeile 58).

  Empfehlung: Vor dem Vergleich `appRoot` auf ein abschließendes `Path.DirectorySeparatorChar` normalisieren (z. B. `Path.TrimEndingDirectorySeparator(appRoot) + Path.DirectorySeparatorChar`) und den Vergleich gegen diesen Wert führen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs (AutoUpdateGithubSource)

- **Fehlende Validierung / Fehlermeldung ohne Kontext** — `CheckAsync` (Zeilen 103–122) verwendet das deserialisierte `GithubReleaseManifest` ungeprüft. `Version` und `Assets` sind im Record als nicht-nullable deklariert, `System.Text.Json` erzwingt das aber nicht: Fehlt `assets` im Manifest, wirft `manifest.Assets.Select(...)` eine `NullReferenceException`; enthält ein Asset eine relative oder leere `assetUrl`, wirft `new Uri(asset.AssetUrl)` eine `UriFormatException`. Beide werden vom Orchestrator in `CheckCoreAsync` (Zeile 215) generisch gefangen und landen als „Object reference not set to an instance of an object." in `LastError` und im Setup-UI — ohne Hinweis, dass das Release-Manifest fehlerhaft ist.

  Empfehlung: Nach der Deserialisierung explizit prüfen (`Version` nicht leer, `Assets` nicht `null`) und bei Verstoß eine `InvalidOperationException` mit aussagekräftigem Text werfen (z. B. „The release manifest '<name>' is missing the required 'version'/'assets' field."); `assetUrl` mit `Uri.TryCreate(..., UriKind.Absolute, out ...)` prüfen und das betroffene Asset benennen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs, IAutoUpdateScriptGenerator.cs

- **Toter Code / unbenutzter Parameter** — Der Parameter `package` von `GenerateAsync` (Interface Zeile 17, Implementierung Zeile 27) wird in `AutoUpdateScriptGenerator` nirgends verwendet; weder `GenerateWindowsAsync` noch `GenerateLinuxAsync` erhalten ihn. Der einzige Aufrufer `AutoUpdateInstaller.PrepareAsync` (Zeile 43) reicht ihn nur durch, und `AutoUpdateScriptGeneratorTests.BuildPackage` (Zeile 98) existiert ausschließlich, um diesen ungenutzten Parameter zu befüllen.

  Empfehlung: Den Parameter aus `IAutoUpdateScriptGenerator.GenerateAsync` und der Implementierung entfernen und `AutoUpdateInstaller.PrepareAsync` sowie `AutoUpdateScriptGeneratorTests` (inkl. `BuildPackage`) entsprechend anpassen. Da die Signatur öffentliche NuGet-API ist, sollte das vor der ersten Veröffentlichung geschehen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateEvents.cs (AutoUpdateEvents)

- **Namenskonventionen** — Das öffentliche Event heißt korrekt `ErrorOccurred` (Zeile 68), das dahinterliegende private Feld weiterhin `_errorOccured` mit einem „r" (Zeilen 30, 70, 71, 160). Innerhalb derselben Klasse werden damit zwei Schreibweisen für dasselbe Konzept verwendet.

  Empfehlung: Feld in `_errorOccurred` umbenennen (vier Fundstellen, rein internal, kein Breaking Change).

### SoftwareSchmiede.AutoUpdate/AutoUpdateCheckResult.cs, AutoUpdateDownloadResult.cs, AutoUpdateInstallResult.cs, AutoUpdateInstallationTarget.cs, AutoUpdatePackageDescriptor.cs, AutoUpdateReleaseInfo.cs, AutoUpdateResult.cs, InstalledReleaseInfo.cs

- **Einheitlichkeit / fehlerhafte XML-Dokumentation** — Alle acht Record-Typen tragen ein `<returns>`-Tag direkt auf der Typdeklaration (z. B. `AutoUpdatePackageDescriptor.cs`, Zeile 205: „An immutable descriptor identifying a single downloadable update package."). `<returns>` ist nur auf Methoden/Properties zulässig; auf einem Typ ist es kein gültiges XML-Doc-Element und erscheint in keiner generierten Dokumentation oder IntelliSense. Andere Typen im selben Paket (`AutoUpdateStatusSnapshot`, `SourceCheckTimeRange`, `AutoUpdateOptions`) kommen ohne aus.

  Empfehlung: Die `<returns>`-Blöcke auf diesen acht Typen entfernen; der Inhalt steht bereits inhaltsgleich im `<summary>`.

### FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs (UpdateSettingsStore)

- **Doppelter Code / Data Clump** — Die Umwandlung eines vollständigen `UpdateSettingsDto`/`LegacyUpdateSettingsDto` in einen 10-argumentigen `UpdateSettingsUpdateRequest` steht zweimal wortgleich in der Datei: `Defaults()` (Zeilen 70–80) und `ReadSettingsAsync()` (Zeilen 122–132). Beide Male werden dieselben zehn Felder in derselben Reihenfolge einzeln aufgezählt, nur um sie direkt danach an `Build` zu übergeben.

  Empfehlung: Eine private `static UpdateSettingsUpdateRequest ToRequest(UpdateSettingsDto dto)` sowie eine Überladung für `LegacyUpdateSettingsDto` ergänzen (oder `Build` zusätzlich eine `UpdateSettingsDto`-Überladung geben) und beide Stellen darauf umstellen.

### FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests_LockAndSchedule.cs

- **Namenskonventionen** — Der Klassen- und Dateiname enthält weiterhin einen Unterstrich (`UpdateOrchestratorAdapterTests_LockAndSchedule`). Alle übrigen Testklassen im Branch (`AutoUpdateOrchestratorCheckTests`, `AutoUpdateOrchestratorInstallTests`, `AutoUpdateOptionsMapperTests`, `UpdateOrchestratorAdapterTests`, …) verwenden durchgängig reines PascalCase mit beschreibendem Suffix. Der Befund stammt bereits aus dem vorherigen Review-Durchlauf und ist unverändert.

  Empfehlung: Klasse und Datei in `UpdateOrchestratorAdapterLockAndScheduleTests` umbenennen und die Verweise in `UpdateOrchestratorAdapterTestFactory.cs` (Zeile 9, XML-Kommentar) sowie im Klassenkommentar anpassen.

### SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AutoUpdateTestContext.cs

- **Ressourcenfreigabe** — Die Klasse implementiert `IDisposable`, gibt in `Dispose()` (Zeilen 126–136) aber nur das temporäre Verzeichnis frei. Die von ihr selbst erzeugten `IDisposable`-Instanzen `Orchestrator` (`AutoUpdateOrchestrator`, hält einen `SemaphoreSlim`) und `StatusService` (`AutoUpdateStatusService`, hält zwei `SemaphoreSlim`) werden nie freigegeben. Der Kontext wird in fünf Testklassen in nahezu jedem Test neu instanziiert.

  Empfehlung: In `Dispose()` zuerst `Orchestrator.Dispose()` und `StatusService.Dispose()` aufrufen, dann das Verzeichnis löschen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateSchedulerServiceTests.cs

- **Testqualität** — Alle drei Tests verwenden feste reale `Task.Delay(100)`-Aufrufe (Zeilen 27, 47, 69) und prüfen danach exakte Aufrufzahlen (`Times.Never`, `Times.Once`). Unter Last sind diese Assertions unzuverlässig. Die dafür vorgesehene Hilfsklasse `AsyncTestWait` mit `WaitForAsync` und `AssertStaysFalseAsync` existiert bereits und wird in `AutoUpdateCheckerServiceTests` konsequent genutzt — die beiden Testklassen für die zwei Hosted Services sind damit uneinheitlich.

  Empfehlung: In `Execute_WhenNotReady_DoesNotInstall` (Zeile 47) und `Execute_SameScheduleTwice_InstallsOnce` (Zeile 69) `Task.Delay(100)` durch `AssertStaysFalseAsync(() => commandService.Invocations.Count > 0)` bzw. `AssertStaysFalseAsync(() => commandService.Invocations.Count > 1)` ersetzen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePackageValidatorTests.cs

- **Fehlende Testabdeckung** — Der Zip-Slip-Schutz ist inzwischen abgedeckt (`ValidateDownloadedPackageAsync_RejectsZipSlipEntry`), der zweite sicherheitsrelevante Zweig von `AutoUpdatePackageValidator.ValidateEntry` jedoch nicht: die Prüfung der Unix-Dateimodus-Bits (`AutoUpdatePackageValidator.cs`, Zeilen 89–93), die Symlinks, Geräte- und Socket-Einträge ablehnt. Zu dieser Bedingung existiert kein Test.

  Empfehlung: Einen Test ergänzen, der einen Eintrag mit `entry.ExternalAttributes = 0xA000 << 16` (Symlink) erzeugt und `InvalidOperationException` mit der Meldung „unsupported special file entry" erwartet; ergänzend einen Positivfall mit `0x8000 << 16` (reguläre Datei).

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateGithubSourceTests.cs

- **Ressourcenfreigabe** — In allen vier Tests werden `new HttpClient(handler)` und die daraus erzeugte `AutoUpdateGithubSource` (implementiert `IDisposable`) nie freigegeben (Zeilen 36, 52, 72). `StubHttpMessageHandler` wird ebenfalls nicht disposed.

  Empfehlung: `using var httpClient = new HttpClient(handler);` bzw. `using var source = ...` verwenden.

### FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs

- **Testqualität** — `ApplySettings_WhenSourceIsGithubSource_ReplacesSourceWithUpdatedRepository` (Zeilen 67–78) verspricht im Namen, dass die Quelle mit dem geänderten Repository neu erzeugt wird, prüft aber nur, dass eine andere Instanz desselben Typs vorliegt. Ob `new-owner`/`new-repo`/`manifest.json` tatsächlich übernommen wurden, wird nicht verifiziert — der Test würde auch grün bleiben, wenn `ApplySettings` die alten Repository-Werte wiederverwendet.

  Empfehlung: `AutoUpdateGithubSource` die Repository-Angaben nach außen sichtbar machen (z. B. `public string RepositoryOwner { get; }`, `RepositoryName`, `ManifestAssetName`) und im Test darauf assertieren; alternativ die Quelle über `CheckAsync` mit einem Stub-Handler ansprechen und die angeforderte URL prüfen.

### FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs

- **Lazy Class** — `AutoUpdateEnvironmentAdapter` (Zeilen 143–151) existiert nur, um aus einem `TestWebHostEnvironment` den `ContentRootPath` zu lesen und als `ApplicationDirectory` zurückzugeben. Die Bibliothek hat bewusst keine ASP.NET-Abhängigkeit; der Umweg über `IWebHostEnvironment` bringt hier keinen Mehrwert.

  Empfehlung: Die Adapterklasse und `TestWebHostEnvironment` aus `CreateStore` entfernen und stattdessen eine minimale `IAutoUpdateEnvironment`-Implementierung mit `string`-Konstruktor verwenden (analog zu `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/TestAutoUpdateEnvironment.cs`).

### SoftwareSchmiede.AutoUpdate/AutoUpdateBuilder.cs (AutoUpdateBuilder)

- **Veraltete Dokumentation** — Der XML-Kommentar auf `ExplicitDownloadPath` (Zeilen 20–23) nennt ausschließlich `EnableAutomaticDownload` als Setzer. Seit Einführung von `WithDownloadPath` (Zeile 189) setzt auch diese Methode das Feld; der Kommentar ist damit unvollständig und führt beim Nachvollziehen der Präzedenzregeln in die Irre.

  Empfehlung: Kommentar auf „set via <see cref=\"EnableAutomaticDownload\"/> or <see cref=\"WithDownloadPath\"/>" erweitern.

## Geprüfte Dateien

Bibliothek (`SoftwareSchmiede.AutoUpdate/`):
- `SoftwareSchmiede.AutoUpdate.csproj`
- `README.md`
- `AutoUpdateHostBuilderExtensions.cs`
- `AutoUpdateBuilder.cs`
- `AutoUpdateOptions.cs`
- `AutoUpdateOptionsValidator.cs`
- `AutoUpdateOrchestrator.cs`
- `AutoUpdateCommandService.cs`
- `AutoUpdateStatusService.cs`
- `AutoUpdateStatusSnapshot.cs`
- `AutoUpdateState.cs`
- `AutoUpdateOutcome.cs`
- `AutoUpdateResult.cs`
- `AutoUpdateCheckResult.cs`
- `AutoUpdateDownloadResult.cs`
- `AutoUpdateInstallResult.cs`
- `AutoUpdateReleaseInfo.cs`
- `AutoUpdatePackageDescriptor.cs`
- `AutoUpdateInstallationTarget.cs`
- `InstalledReleaseInfo.cs`
- `AutoUpdateEvents.cs`
- `AutoUpdateCancelEventArgs.cs`
- `AutoUpdateErrorEventArgs.cs`
- `BeforeDownloadEventArgs.cs`
- `BeforeInstallEventArgs.cs`
- `BeforeStartUpdateScriptEventArgs.cs`
- `AutoUpdateCheckerService.cs`
- `AutoUpdateSchedulerService.cs`
- `ScheduledInstallEvaluator.cs`
- `SourceCheckOptions.cs`
- `SourceCheckTimeRange.cs`
- `SourceCheckWindowEvaluator.cs`
- `AutoUpdateGithubSource.cs`
- `AutoUpdateLocalFolderSource.cs`
- `AutoUpdateSourceDownloadHelper.cs`
- `AutoUpdateInstaller.cs`
- `AutoUpdatePackageValidator.cs`
- `AutoUpdatePlatformResolver.cs`
- `AutoUpdateScriptGenerator.cs`
- `AutoUpdateServiceResolver.cs`
- `DefaultAutoUpdateHostTerminator.cs`
- `DefaultAutoUpdateProcessRunner.cs`
- `DefaultAutoUpdateServiceProbe.cs`
- `ProcessOutputReader.cs`
- `FileSystemAutoUpdatePackageStore.cs`
- `FileSystemAutoUpdateStateStore.cs`
- `JsonFileStore.cs`
- `HostAutoUpdateEnvironment.cs`
- `ReleaseMetadataInstalledVersionProvider.cs`
- `IAutoUpdateCommandHandler.cs`
- `IAutoUpdateEnvironment.cs`
- `IAutoUpdateEventAggregator.cs`
- `IAutoUpdateHostTerminator.cs`
- `IAutoUpdateInstaller.cs`
- `IAutoUpdateOrchestrator.cs`
- `IAutoUpdatePackageStore.cs`
- `IAutoUpdatePackageValidator.cs`
- `IAutoUpdatePlatformResolver.cs`
- `IAutoUpdateProcessRunner.cs`
- `IAutoUpdateScriptGenerator.cs`
- `IAutoUpdateServiceProbe.cs`
- `IAutoUpdateServiceResolver.cs`
- `IAutoUpdateSource.cs`
- `IAutoUpdateStateStore.cs`
- `IAutoUpdateStatusProvider.cs`
- `IInstalledVersionProvider.cs`

Bibliothekstests (`SoftwareSchmiede.AutoUpdate.Tests/`):
- `SoftwareSchmiede.AutoUpdate.Tests.csproj`
- `TestSupport/AutoUpdateTestContext.cs`
- `TestSupport/FakeAutoUpdateSource.cs`
- `TestSupport/TestAutoUpdateEnvironment.cs`
- `TestSupport/AsyncTestWait.cs`
- `TestSupport/RecordingLogger.cs`
- `AutoUpdateBuilderTests.cs`
- `UseAutoUpdateRegistrationTests.cs`
- `AutoUpdateOptionsValidationTests.cs`
- `AutoUpdateOrchestratorCheckTests.cs`
- `AutoUpdateOrchestratorDownloadTests.cs`
- `AutoUpdateOrchestratorInstallTests.cs`
- `AutoUpdateOrchestratorEventTests.cs`
- `AutoUpdateEventsTests.cs`
- `AutoUpdateCommandServiceTests.cs`
- `AutoUpdateStatusServiceTests.cs`
- `AutoUpdateCheckerServiceTests.cs`
- `AutoUpdateSchedulerServiceTests.cs`
- `AutoUpdateGithubSourceTests.cs`
- `AutoUpdateLocalFolderSourceTests.cs`
- `AutoUpdatePackageValidatorTests.cs`
- `AutoUpdatePlatformResolverTests.cs`
- `AutoUpdateScriptGeneratorTests.cs`
- `AutoUpdateServiceResolverTests.cs`
- `FileSystemAutoUpdatePackageStoreTests.cs`
- `FileSystemAutoUpdateStateStoreTests.cs`
- `ProcessOutputReaderTests.cs`
- `SourceCheckWindowEvaluatorTests.cs`

FinanceManager.Web:
- `ProgramExtensions.cs`
- `FinanceManager.Web.csproj`
- `appsettings.json`
- `Components/Pages/Setup/SetupUpdateTab.razor`
- `Services/Updates/UpdateOrchestratorAdapter.cs`
- `Services/Updates/UpdateStatusMapper.cs`
- `Services/Updates/AutoUpdateOptionsMapper.cs`
- `Services/Updates/UpdateSettingsStore.cs`
- `Services/Updates/UpdateContracts.cs`
- `Services/Updates/UpdateOptions.cs`
- `Services/Updates/InstalledReleaseMetadataProvider.cs`

FinanceManager-Tests:
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests_LockAndSchedule.cs`
- `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
- `FinanceManager.Tests/Updates/InstalledReleaseMetadataProviderTests.cs`
- `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
- `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`
- `FinanceManager.Tests.E2E/Helpers/TestUserSeeder.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`

Sonstige:
- `.gitignore`
- `FinanceManager.sln`
