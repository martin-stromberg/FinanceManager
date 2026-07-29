# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SoftwareSchmiede.AutoUpdate/AutoUpdateOrchestrator.cs (AutoUpdateOrchestrator)

- **Toter Code** — `RunUpdateAsync` (Zeile 63) hat keinen Produktivaufrufer. `AutoUpdateCheckerService` ruft nur `CheckForUpdateAsync`, `AutoUpdateSchedulerService` nur `InstallAsync`. Die komplette automatische Kette (Check → Download → Install) wird ausschließlich aus Tests heraus aufgerufen. Damit sind auch `AutoUpdateOptions.EnableAutomaticDownload` und `AutoUpdateOptions.EnableAutomaticInstallation` zur Laufzeit wirkungslos, obwohl sie in `appsettings.json` und im README als aktive Schalter dokumentiert sind.

  Empfehlung: Entweder `RunUpdateAsync` in `AutoUpdateCheckerService` verwenden (statt `CheckForUpdateAsync`), sodass die konfigurierten Automatikschalter greifen, oder `RunUpdateAsync` samt der beiden Optionen entfernen und die Dokumentation/`appsettings.json` entsprechend bereinigen.

- **Fehlerbehandlung** — In `InstallCoreAsync` (Zeilen 321 und 326) wird der Rückgabewert von `_packageStore.DeleteLockAsync(...)` verworfen. Schlägt das Löschen fehl (Rückgabe `false`, siehe `FileSystemAutoUpdatePackageStore.DeleteLockAsync`), bleibt eine verwaiste Lock-Datei zurück, ohne dass dies irgendwo gemeldet oder protokolliert wird; nachfolgende Installationsversuche scheitern dann dauerhaft mit „An update lock is already active."

  Empfehlung: Rückgabewert auswerten und bei `false` über `_events.RaiseErrorOccured(...)` bzw. in `LastError` melden.

### SoftwareSchmiede.AutoUpdate/AutoUpdateCheckerService.cs (AutoUpdateCheckerService)

- **Fehlerbehandlung** — Im `catch (Exception)`-Block (Zeilen 61–65) wird `await Task.Delay(ErrorBackoff, _timeProvider, stoppingToken)` ohne inneres `try` ausgeführt. Wird der Host während des Backoffs beendet, verlässt eine `OperationCanceledException` `ExecuteAsync` und wird als Fehler des Hosted Service gemeldet. `AutoUpdateSchedulerService` behandelt exakt denselben Fall korrekt mit einem inneren `try/catch` (Zeilen 72–79) — die beiden Services sind uneinheitlich.

  Empfehlung: Den Backoff-`Task.Delay` im Checker analog zum Scheduler in ein inneres `try { ... } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }` einbetten. Besser noch: das Delay-mit-Abbruchbehandlung in eine gemeinsame private Hilfsmethode auslagern, die beide Services nutzen.

### SoftwareSchmiede.AutoUpdate/ProcessOutputReader.cs (ProcessOutputReader)

- **Fehlerbehandlung** — Läuft `process.WaitForExit(timeoutMs)` in den Timeout (Zeile 37), wird eine `TimeoutException` geworfen, der Kindprozess aber nicht beendet. `sc.exe queryex` bzw. `systemctl` laufen dann verwaist weiter; zusätzlich bleiben die beiden `ReadToEndAsync`-Tasks unbeobachtet.

  Empfehlung: Vor dem Werfen der `TimeoutException` `process.Kill(entireProcessTree: true)` aufrufen (in `try/catch` gekapselt).

- **Fehlerbehandlung** — `stderr` wird gelesen (Zeile 43), aber nur im Fall `throwOnNonZeroExitCode == true` verwendet. Bei Exit-Code ≠ 0 ohne dieses Flag (Standardfall in `DefaultAutoUpdateServiceProbe` und `ReadUnitProperty`) geht die Fehlerausgabe ersatzlos verloren.

  Empfehlung: Exit-Code und `stderr` mit zurückgeben (z. B. als Ergebnis-Record) oder bei Exit-Code ≠ 0 über einen optional durchgereichten `ILogger` auf Debug-Level protokollieren.

### SoftwareSchmiede.AutoUpdate/AutoUpdateOptions.cs (AutoUpdateOptions)

- **Speculative Generality** — `HealthTimeoutSeconds` (Zeile 93) wird von der Bibliothek nirgends fachlich ausgewertet; sie wird in `AutoUpdateHostBuilderExtensions.BuildOptions` lediglich geclampt. Die einzige Auswertung erfolgt außerhalb der Bibliothek in `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.ResetLockAsync`. Eine Option ohne Verhalten in der Bibliothek gehört nicht in deren öffentliche Konfigurationsfläche.

  Empfehlung: Entweder die Lock-Staleness-Prüfung (Vergleich `now - lockCreatedAt >= HealthTimeoutSeconds` inkl. Zurücksetzen des Locks) als `ResetStaleLockAsync` in `IAutoUpdateOrchestrator`/`IAutoUpdatePackageStore` in die Bibliothek ziehen, oder `HealthTimeoutSeconds` aus `AutoUpdateOptions` entfernen und auf `FinanceManager.Web/Services/Updates/UpdateOptions` belassen.

### SoftwareSchmiede.AutoUpdate/IAutoUpdateEventAggregator.cs, AutoUpdateEvents.cs, AutoUpdateErrorEventArgs.cs

- **Namenskonventionen** — Das Event heißt durchgängig `ErrorOccured` (Raise-Methode `RaiseErrorOccured`, Phase-Strings). Korrekt wäre `ErrorOccurred` (doppeltes „r"). Es handelt sich um öffentliche, als NuGet-Paket vorgesehene API — nach der ersten Veröffentlichung ist die Umbenennung ein Breaking Change.

  Empfehlung: Vor Veröffentlichung des Pakets in `ErrorOccurred` / `RaiseErrorOccurred` umbenennen (Interface, Implementierung, README, Tests).

### SoftwareSchmiede.AutoUpdate/AutoUpdateLocalFolderSource.cs, AutoUpdateGithubSource.cs

- **Doppelter Code** — Beide `DownloadAsync`-Implementierungen enthalten dieselbe Größenprüfung mit identischer Fehlermeldung („Update package exceeds the configured size limit.", `AutoUpdateGithubSource.cs:127`/`150` und `AutoUpdateLocalFolderSource.cs:69`) sowie dasselbe `Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!)`.

  Empfehlung: Beide Bausteine in eine gemeinsame `internal static`-Hilfsklasse (z. B. `AutoUpdateDownloadTarget`) auslagern und aus beiden Quellen aufrufen.

- **Fehlende Kapselung** — `AutoUpdateGithubSource.DownloadAsync` schreibt in eine `.tmp`-Datei und verschiebt atomar (Zeilen 131–157); `AutoUpdateLocalFolderSource.DownloadAsync` (Zeilen 72–75) schreibt direkt in die Zieldatei. Bricht das Kopieren ab, bleibt eine unvollständige Zieldatei liegen, die erst später an der Checksummenprüfung scheitert.

  Empfehlung: Das Temp-Datei-plus-`File.Move`-Muster aus `AutoUpdateGithubSource` in die gemeinsame Hilfsklasse ziehen und in beiden Quellen verwenden.

### SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs (AutoUpdateGithubSource)

- **Hardcodierte Werte** — `ManifestAssetName = "update.json"` (Zeile 13) ist eine Konstante ohne Konfigurationsmöglichkeit; `AutoUpdateBuilder.UseGithubSource(owner, name)` bietet keinen Parameter dafür. In der Folge ist die Einstellung `Updates:ManifestAssetName` (`FinanceManager.Web/Services/Updates/UpdateOptions.ManifestAssetName`), die im Setup-UI editierbar ist und in `settings.json` persistiert wird, wirkungslos.

  Empfehlung: Optionalen Parameter `manifestAssetName` an `AutoUpdateGithubSource`/`AutoUpdateLocalFolderSource` und `AutoUpdateBuilder.UseGithubSource`/`UseLocalFolderSource` ergänzen und in `ProgramExtensions` aus `updateOptions.ManifestAssetName` befüllen — oder das Feld aus DTO und Setup-UI entfernen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateStatusService.cs (AutoUpdateStatusService)

- **Einheitlichkeit / Ressourcenfreigabe** — Die Klasse hält zwei `SemaphoreSlim` (`_loadGate`, `_writeGate`, Zeilen 13–14), implementiert aber kein `IDisposable`. `AutoUpdateOrchestrator` implementiert für seinen einzelnen `SemaphoreSlim` sehr wohl `IDisposable` (Zeile 60) — beide Klassen behandeln denselben Fall unterschiedlich.

  Empfehlung: `AutoUpdateStatusService` ebenfalls `IDisposable` implementieren lassen und beide Semaphoren freigeben.

### SoftwareSchmiede.AutoUpdate/AutoUpdatePackageValidator.cs (AutoUpdatePackageValidator)

- **Fehlerbehandlung / undokumentierte Vorbedingung** — `IsNewerVersion` liefert `false`, wenn `installedVersion` null oder leer ist (Zeilen 15–18). Der XML-Kommentar auf `IAutoUpdatePackageValidator.IsNewerVersion` beschreibt `installedVersion` als „or `null` if unknown", sagt aber nicht, dass bei unbekannter installierter Version niemals ein Update erkannt wird. Ein frisch installiertes System ohne `release-metadata.json` aktualisiert damit stillschweigend nie.

  Empfehlung: Das Verhalten im XML-Kommentar des Interfaces explizit dokumentieren („returns `false` if `installedVersion` is unknown") und einen Test ergänzen, der genau diesen Fall festschreibt.

- **Testqualität / fehlende Abdeckung** — `ValidateEntry` (Zeilen 61–94) implementiert den Schutz gegen Zip-Slip (absolute Pfade, `..`-Segmente, Sonderdateien). Zu dieser sicherheitsrelevanten Logik existiert in `AutoUpdatePackageValidatorTests.cs` kein einziger Test; getestet werden nur Checksumme und Größe.

  Empfehlung: Tests für ein Archiv mit `../evil.txt`, mit `/absolute/path.txt` und mit einem Symlink-Eintrag (`ExternalAttributes` mit Modus `0xA000`) ergänzen, die jeweils `InvalidOperationException` erwarten.

### SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs, AutoUpdateServiceResolver.cs, AutoUpdatePlatformResolver.cs

- **Doppelter Code / Type Checks** — Die Plattformweiche `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` / `OSPlatform.Linux` / sonst „Unsupported platform for self update." steht wortgleich in `AutoUpdateScriptGenerator.GenerateAsync` (Zeilen 29–39) und `AutoUpdateServiceResolver.Resolve` (Zeilen 31–41); eine dritte Variante derselben Verzweigung findet sich in `AutoUpdatePlatformResolver.CurrentPlatform`/`CurrentRuntimeIdentifier`.

  Empfehlung: Beide Klassen die bereits vorhandene Abstraktion `IAutoUpdatePlatformResolver.CurrentPlatform` verwenden lassen und dort einmalig bei unbekannter Plattform werfen, statt `RuntimeInformation` je dreimal direkt abzufragen.

### FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **Long Parameter List / God-Klasse** — Der Konstruktor nimmt 10 Abhängigkeiten (Zeilen 39–49). Die Klasse vereint drei getrennte Verantwortlichkeiten: Mapping der Bibliothekstypen auf die DTOs (`MapToStatusDtoAsync`, `MapState`), reines Durchreichen an den Settings-Store (`GetSettingsAsync`, `SaveSettingsAsync`, `ScheduleAsync`) und die fachliche Lock-Staleness-Regel (`ResetLockAsync`).

  Empfehlung: Das DTO-Mapping in eine eigene, zustandslose Klasse `UpdateStatusDtoMapper` (Abhängigkeiten: `IInstalledReleaseMetadataProvider`, `IUpdateSettingsStore`, `IAutoUpdatePlatformResolver`) auslagern und `ResetLockAsync` in einen eigenen `UpdateLockService` verschieben. Der Adapter reduziert sich damit auf drei bis vier Abhängigkeiten.

- **Inappropriate Intimacy / überflüssige Abhängigkeiten** — Es werden gleichzeitig `IAutoUpdateStatusProvider` und die konkrete Klasse `AutoUpdateStatusService` injiziert (Zeilen 17 und 22). Laut `AutoUpdateHostBuilderExtensions` (Zeile 63) ist `IAutoUpdateStatusProvider` genau dieselbe Singleton-Instanz — es wird also dasselbe Objekt zweimal aufgelöst, einmal über das Interface, einmal an der Abstraktion vorbei. Analog werden `IAutoUpdateOrchestrator` und `IAutoUpdateCommandHandler` beide injiziert, obwohl `AutoUpdateCommandService` laut eigener Dokumentation eine reine Fassade über denselben Orchestrator ist.

  Empfehlung: `AutoUpdateStatusService` streichen und `UpdateAsync` über eine schreibfähige Schnittstelle der Bibliothek anbieten (z. B. `IAutoUpdateOrchestrator.ResetLockAsync`, siehe Befund zu `HealthTimeoutSeconds`). `IAutoUpdateStatusProvider` als einzigen Status-Zugang behalten und entweder `IAutoUpdateOrchestrator` oder `IAutoUpdateCommandHandler` verwenden, nicht beide.

### FinanceManager.Web/Services/Updates/UpdateOptions.cs (UpdateOptions)

- **Toter Code** — `EnableAutomaticDownload` (Zeile 58) und `EnableAutomaticInstallation` (Zeile 64) werden nirgends im Code gelesen; die zugehörigen Konfigurationsschlüssel binden über `BindConfiguration` direkt auf `AutoUpdateOptions`. Die Properties existieren nur noch als Kommentar-Spiegel.

  Empfehlung: Beide Properties aus `UpdateOptions` entfernen; der erklärende XML-Kommentar zur Doppelbindung kann als Klassenkommentar erhalten bleiben.

### FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs (UpdateSettingsStore)

- **Long Parameter List / Data Clump** — `Build` (Zeilen 96–117) nimmt 10 Einzelparameter. `Defaults()` (Zeilen 62–81) zerlegt eigens ein bereits vollständiges `UpdateSettingsDto` in diese 10 Argumente, nur um es wieder zusammenzusetzen; `Normalize` (Zeilen 83–94) tut dasselbe mit dem Request-DTO.

  Empfehlung: `Build` in eine Methode `Normalize(UpdateSettingsDto raw) → UpdateSettingsDto` umbauen, die ein DTO entgegennimmt und normalisiert zurückgibt. `Defaults()` und `SaveAsync` konvertieren ihre Eingabe dann jeweils einmal in ein `UpdateSettingsDto` und rufen `Normalize` auf — die 10-Parameter-Signatur entfällt an allen drei Stellen.

### FinanceManager.Web/ProgramExtensions.cs

- **Kopplung / wirkungslose Konfiguration** — Die Update-Quelle wird einmalig beim Start aus `updateOptions.SourceType`, `RepositoryOwner` und `RepositoryName` gebaut (Zeilen 183–192) und in `AutoUpdateOptions.Source` abgelegt. `AutoUpdateOptionsMapper.ApplySettings` setzt `Source` nicht. Änderungen an Repository-Owner/-Name über das Setup-UI werden daher zwar persistiert und in `SetupUpdateTab.razor` angezeigt, ändern die tatsächlich abgefragte Quelle aber erst nach einem Neustart.

  Empfehlung: In `AutoUpdateOptionsMapper.ApplySettings` (bzw. in `UpdateSettingsStore.ApplyToOptions`) die Quelle bei geänderten Repository-Angaben über `AutoUpdateGithubSource.Create(...)` neu erzeugen und `options.Source` setzen — oder die Repository-Felder im Setup-UI als schreibgeschützt kennzeichnen.

### SoftwareSchmiede.AutoUpdate/README.md

- **Fehlerhaftes Beispiel** — Zeile 23: `cfg.UseGithubSource("MyRepository", "my-org")` vertauscht die Argumente; die Signatur lautet `UseGithubSource(string repositoryOwner, string repositoryName)`.

  Empfehlung: Auf `UseGithubSource("my-org", "MyRepository")` korrigieren.

- **Fehlerhaftes Beispiel** — Zeile 53: Das dokumentierte `TimeRanges`-Beispiel `{ "StartTime": "22:00:00", "EndTime": "06:00:00" }` wird von `AutoUpdateOptionsValidator` (Zeilen 38–44) mit `StartTime >= EndTime` abgelehnt und führt beim Start zu einer `OptionsValidationException`. Über Mitternacht laufende Fenster werden nicht unterstützt.

  Empfehlung: Das Beispiel auf ein gültiges Fenster ändern (z. B. `"StartTime": "02:00:00"`, `"EndTime": "06:00:00"`) und im Abschnitt „Configuration" explizit vermerken, dass Zeitfenster nicht über Mitternacht hinausgehen dürfen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCheckerServiceTests.cs

- **Testqualität** — `Execute_TriggersCheckOnlyWithinWindow` prüft ausschließlich den positiven Fall (Zeitfenster trifft zu). Der im Namen versprochene Fall „nur innerhalb des Fensters" — also kein Aufruf außerhalb des Fensters — wird nicht getestet.

  Empfehlung: Entweder einen zweiten Test `Execute_OutsideWindow_DoesNotTriggerCheck` ergänzen oder den Test in `Execute_WithinWindow_TriggersCheck` umbenennen.

- **Testqualität** — `Execute_RespectsConfiguredInterval` und `Execute_WhenCheckThrows_ContinuesLoop` kombinieren `FakeTimeProvider.Advance` mit festen realen `Task.Delay(100)`/`Task.Delay(50)` und prüfen anschließend exakte Aufrufzahlen (`Invocations.Count.Should().Be(1)` / `.Be(2)`). Unter Last werden diese Assertions unzuverlässig.

  Empfehlung: Die festen `Task.Delay`-Aufrufe durch die bereits vorhandene Hilfsmethode `AsyncTestWait.WaitForAsync` ersetzen und die exakten Gleichheits-Assertions dort, wo nur „noch kein weiterer Aufruf" gemeint ist, gegen ein kurzes Warten mit anschließender Prüfung auf Nichterhöhung tauschen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateScriptGeneratorTests.cs

- **Toter Code** — `CreateGenerator` (Zeilen 92–99) liefert ein Tupel `(Generator, PackageStore)`; alle drei Aufrufer verwerfen den zweiten Wert mit `var (generator, _)`.

  Empfehlung: Rückgabetyp auf `AutoUpdateScriptGenerator` reduzieren.

### FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs, UpdateOrchestratorAdapterTests_LockAndSchedule.cs

- **Doppelter Code** — Die private Hilfsmethode `CreateStatusService()` ist in beiden Dateien wortgleich vorhanden. Zusätzlich wird der 10-argumentige `new UpdateOrchestratorAdapter(...)`-Aufruf in `UpdateOrchestratorAdapterTests` viermal ausgeschrieben, obwohl `UpdateOrchestratorAdapterTests_LockAndSchedule` dafür bereits eine `CreateAdapter`-Hilfsmethode besitzt.

  Empfehlung: Eine gemeinsame `internal static`-Testhilfsklasse (z. B. `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterFactory.cs`) mit `CreateStatusService()` und einem `CreateAdapter(...)` mit optionalen Parametern anlegen und aus beiden Testklassen verwenden.

- **Namenskonventionen** — Der Klassenname `UpdateOrchestratorAdapterTests_LockAndSchedule` enthält einen Unterstrich; alle übrigen Testklassen im Branch (`AutoUpdateOrchestratorCheckTests`, `AutoUpdateOrchestratorInstallTests`, `AutoUpdateOptionsMapperTests`, …) verwenden durchgängig reines PascalCase mit Suffix-Bezeichnung.

  Empfehlung: In `UpdateOrchestratorAdapterLockAndScheduleTests` umbenennen (Datei entsprechend).

### FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs

- **Testqualität** — `PrepareInstalledReleaseMetadata()` schreibt `release-metadata.json` in den Quellbaum des Repositories (`GetRepoRoot()/FinanceManager.Web/release-metadata.json`) und stellt den Originalzustand erst in `DisposeAsync` über `RestoreInstalledReleaseMetadata()` wieder her. Bricht der Testlauf ab (Crash, Abbruch durch CI-Timeout, paralleler Lauf), bleibt eine fremde Datei im Arbeitsverzeichnis zurück bzw. wird eine vorhandene überschrieben.

  Empfehlung: Die Datei stattdessen in ein temporäres Verzeichnis schreiben und dem Testserver dieses Verzeichnis als `ContentRootPath` bzw. über die bereits genutzte Umgebungsvariablen-Konfiguration (`Updates__WorkingDirectory`-Muster) zuweisen, sodass der Quellbaum unangetastet bleibt.

### FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs

- **Inappropriate Intimacy** — `SetDownloadPath` (Zeilen 236–240) greift auf den `ServiceDescriptor` zu und castet dessen `ImplementationInstance` auf `AutoUpdateOptions`, um darauf zu schreiben. Der Test hängt damit am internen Registrierungsdetail, dass `UseAutoUpdate` die Optionen als Instanz-Singleton (`AddSingleton(options)`) und nicht als Typ-Registrierung ablegt; eine Umstellung in der Bibliothek lässt den Test mit `NullReferenceException` scheitern.

  Empfehlung: Die Optionen regulär über `services.BuildServiceProvider()` bzw. nach dem Erstellen der Factory über `factory.Services.GetRequiredService<AutoUpdateOptions>()` beziehen und dort `DownloadPath` setzen — oder den Pfad über `Updates:DownloadPath` in der `AddInMemoryCollection`-Konfiguration von `TestWebApplicationFactory` vorgeben.

### SoftwareSchmiede.AutoUpdate.Tests/ (Projektartefakte)

- **Toter Code / Artefakte im Arbeitsverzeichnis** — `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.xml` (durch `<DocumentationFile>` im Projektverzeichnis erzeugt) und `test-output.txt` im Repository-Root sind untracked, aber nicht durch `.gitignore` abgedeckt (`git check-ignore` meldet nur `TestResults/`). Sie würden bei einem `git add -A` mit eingecheckt.

  Empfehlung: `test-output.txt` löschen, im Testprojekt das `<DocumentationFile>`-Element entfernen (bei `<GenerateDocumentationFile>` landet die XML-Datei ohnehin im `obj`/`bin`-Ausgabepfad) und beide Muster vorsorglich in `.gitignore` aufnehmen.

## Geprüfte Dateien

Bibliothek:
- `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj`
- `SoftwareSchmiede.AutoUpdate/README.md`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateHostBuilderExtensions.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateBuilder.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOptions.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOptionsValidator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOrchestrator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCommandService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateStatusService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateStatusSnapshot.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateState.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOutcome.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCheckResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateDownloadResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstallResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateReleaseInfo.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePackageDescriptor.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstallationTarget.cs`
- `SoftwareSchmiede.AutoUpdate/InstalledReleaseInfo.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateEvents.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCancelEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateErrorEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/BeforeDownloadEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/BeforeInstallEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/BeforeStartUpdateScriptEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCheckerService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateSchedulerService.cs`
- `SoftwareSchmiede.AutoUpdate/ScheduledInstallEvaluator.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckOptions.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckTimeRange.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckWindowEvaluator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateLocalFolderSource.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstaller.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePackageValidator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePlatformResolver.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateServiceResolver.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateHostTerminator.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateProcessRunner.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateServiceProbe.cs`
- `SoftwareSchmiede.AutoUpdate/ProcessOutputReader.cs`
- `SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdatePackageStore.cs`
- `SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdateStateStore.cs`
- `SoftwareSchmiede.AutoUpdate/JsonFileStore.cs`
- `SoftwareSchmiede.AutoUpdate/HostAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate/ReleaseMetadataInstalledVersionProvider.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateCommandHandler.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateEventAggregator.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateHostTerminator.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateInstaller.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateOrchestrator.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdatePackageStore.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdatePackageValidator.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdatePlatformResolver.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateProcessRunner.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateScriptGenerator.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateServiceProbe.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateServiceResolver.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateSource.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateStateStore.cs`
- `SoftwareSchmiede.AutoUpdate/IAutoUpdateStatusProvider.cs`
- `SoftwareSchmiede.AutoUpdate/IInstalledVersionProvider.cs`

Bibliothekstests:
- `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.csproj`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AutoUpdateTestContext.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/FakeAutoUpdateSource.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/TestAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AsyncTestWait.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateBuilderTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/UseAutoUpdateRegistrationTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOptionsValidationTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorCheckTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorDownloadTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorInstallTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorEventTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateEventsTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCommandServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateStatusServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCheckerServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateSchedulerServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateGithubSourceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateLocalFolderSourceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePackageValidatorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePlatformResolverTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateScriptGeneratorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateServiceResolverTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/FileSystemAutoUpdatePackageStoreTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/FileSystemAutoUpdateStateStoreTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/SourceCheckWindowEvaluatorTests.cs`

FinanceManager.Web:
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/FinanceManager.Web.csproj`
- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`
- `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateContracts.cs`
- `FinanceManager.Web/Services/Updates/UpdateOptions.cs`
- `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`

FinanceManager-Tests:
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
