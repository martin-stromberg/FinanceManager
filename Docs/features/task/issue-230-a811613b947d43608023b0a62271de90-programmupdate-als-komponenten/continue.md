# Offene Aufgaben

Erstellt am: 2026-07-30
Abbruchgrund: Kein Fortschritt zwischen den letzten zwei Iterationen (Iteration 1: 18 offene Punkte,
Iteration 2: 37 offene Punkte — die gründlichere Code-Review-Runde deckte deutlich mehr, teils neue
Befunde auf als die vorherige beheben konnte)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

Hinweis: Die vorherige `continue.md` (erster Nacharbeits-Zyklus, 29 Punkte) wurde vollständig
abgearbeitet und liegt jetzt als `continue-done.md` vor. Die unten gelisteten Punkte stammen aus dem
fünften Plan-Review-Durchlauf (`review.md`) und dem sechsten Code-Review-Durchlauf (`review-code.md`).

## Offene Planelemente

- [ ] `UpdateOptions.EnableAutomaticDownload` / `UpdateOptions.EnableAutomaticInstallation` (Plan Zeile 212) fehlen vollständig in `FinanceManager.Web/Services/Updates/UpdateOptions.cs` (nur `SourceType`/`LocalFolderPath` vorhanden). Funktional folgenlos, da `BindConfiguration("Updates")` direkt auf `AutoUpdateOptions` bindet. Entscheidung nötig: Eigenschaften ergänzen oder Plan an den schlankeren Vertrag anpassen.
- [ ] DI-Registrierung von `IValidateOptions<AutoUpdateOptions>` (Plan Zeile 44, Ablauf „Registrierung beim Start", Schritt 4) fehlt. `AutoUpdateOptionsValidator` läuft eager in `BuildOptions`, Startvalidierung funktioniert bereits. Entscheidung nötig: Registrierung ergänzen (`TryAddSingleton<IValidateOptions<AutoUpdateOptions>, AutoUpdateOptionsValidator>()`) oder Plan an die eager ausgeführte Validierung anpassen.
- [ ] Ablage von Handler-Ausnahmen als `LastError` (Plan Zeile 22, Designentscheidung „Fehler in Event-Handlern") ist nur teilweise umgesetzt: Ausnahmen aus Event-Handlern werden gefangen, über `ErrorOccurred` gemeldet und der Ablauf läuft weiter — aber nirgends im Produktivcode abonniert ein Dienst `ErrorOccurred`, um `LastError` im Status-Service zu setzen. Ausnahmen aus `BeforeCheckSource`/`BeforeDownload`/`BeforeInstall`/`BeforeStartUpdateScript`/`AfterStartUpdateScript` bleiben damit in der Setup-UI unsichtbar.

## Code-Review-Befunde

- [ ] `AutoUpdateOptionsMapper.ApplySettings` setzt `options.DownloadPath = settings.WorkingDirectory`; davon hängt der Ablageort von `settings.json`/`status.json`/`update.lock` ab (`UpdateSettingsStore.SettingsPath` über `FileSystemAutoUpdatePackageStore.RootDirectory`). Ändert ein Administrator das Arbeitsverzeichnis im Setup-UI, liegt die gerade gespeicherte `settings.json` im alten Verzeichnis und wird nie wieder gelesen; Repository-Owner/-Name/Manifest fallen still auf appsettings-Defaults zurück, ein aktiver Lock wird unsichtbar.
- [ ] `AutoUpdateOptionsMapper.ApplySettings` disposed die alte `AutoUpdateGithubSource` außerhalb des Serialisierungs-Semaphors von `AutoUpdateOrchestrator`. Ein paralleler Check/Download kann in eine `ObjectDisposedException` laufen, da der `HttpClient` der Quelle mitten im Aufruf disposed wird.
- [ ] `UpdateOrchestratorAdapter.ResetLockAsync` verwirft den Rückgabewert von `DeleteLockAsync` und setzt bedingungslos `IsLocked = false` — fehlgeschlagenes Lock-Löschen bleibt unsichtbar, spätere Installationen scheitern dauerhaft. `AutoUpdateOrchestrator.ReleaseLockAsync` behandelt denselben Fall bereits korrekt.
- [ ] `UpdateOrchestratorAdapter.CheckAsync` widerspricht dem eigenen Klassenkommentar (der Re-Throw von `AutoUpdateResult.Error` zusagt): `result.Error` wird verworfen, ein fehlgeschlagener Check ist von „kein Update verfügbar" nicht unterscheidbar. `StartInstallAsync` macht es korrekt.
- [ ] `UpdateOrchestratorAdapter` injiziert die konkrete Klasse `AutoUpdateStatusService` statt der Abstraktion `IAutoUpdateStatusProvider`, weil `UpdateAsync(...)` auf keinem Interface liegt — Kopplung an eine konkrete NuGet-Paket-Klasse.
- [ ] `AutoUpdateHostBuilderExtensions.UseAutoUpdate` registriert `AddHttpClient()`, obwohl die Bibliothek nirgends `IHttpClientFactory`/`HttpClient` aus dem Container auflöst — erzwingt eine überflüssige `Microsoft.Extensions.Http`-Abhängigkeit für jeden NuGet-Konsumenten.
- [ ] `AutoUpdateServiceResolver.ValidateExecutablePath` prüft `fullPath.StartsWith(appRoot, ...)` ohne abschließendes Verzeichnistrennzeichen — `C:\application\fremd.exe` besteht die Prüfung gegen `C:\app` fälschlich.
- [ ] `AutoUpdateGithubSource.CheckAsync` validiert das deserialisierte Manifest nicht; fehlendes `assets` oder eine ungültige `assetUrl` wirft eine unklare NRE/`UriFormatException`, die generisch als „Object reference not set..." in der Setup-UI landet.
- [ ] Unbenutzter Parameter `package` in `IAutoUpdateScriptGenerator.GenerateAsync`/`AutoUpdateScriptGenerator` — öffentliche NuGet-API, sollte vor Veröffentlichung entfernt werden.
- [ ] `JsonFileStore.WriteAtomicAsync` räumt die `.tmp`-Datei bei einer Ausnahme nicht auf (im Gegensatz zu `AutoUpdateSourceDownloadHelper.CopyToTargetAsync`, die es korrekt per try/catch macht) — verwaiste `status.json.<guid>.tmp`/`settings.json.<guid>.tmp`-Dateien sammeln sich an.
- [ ] `AutoUpdatePlatformResolver.CurrentRuntimeIdentifier` liefert für Windows/Linux fest `"win-x64"`/`"linux-x64"`, ignoriert die tatsächliche Architektur — auf ARM64-Hosts wird das falsche Paket gewählt und die Setup-UI zeigt die falsche Plattform.
- [ ] `AutoUpdateCheckerService`/`AutoUpdateSchedulerService`: dupliziertes Schleifen-/Backoff-Gerüst (while/try/Task.Delay/catch OperationCanceledException/catch Exception mit Backoff) — in eine gemeinsame Basisklasse/Hilfsmethode auslagern.
- [ ] `AutoUpdateEvents`: privates Feld `_errorOccured` (ein „r") vs. öffentliches Event `ErrorOccurred` (zwei „r") — uneinheitliche Schreibweise innerhalb derselben Klasse.
- [ ] `AutoUpdateStatusSnapshot.InstalledVersion` wird nur geschrieben, nie gelesen (`UpdateStatusMapper` ermittelt die Version stattdessen live über `IInstalledReleaseMetadataProvider`) — toter Code, veraltet nach einem Update dauerhaft in `status.json`.
- [ ] `<returns>`-XML-Doc-Tag auf 8 Record-Typdeklarationen (`AutoUpdateCheckResult`, `AutoUpdateDownloadResult`, `AutoUpdateInstallResult`, `AutoUpdateInstallationTarget`, `AutoUpdatePackageDescriptor`, `AutoUpdateReleaseInfo`, `AutoUpdateResult`, `InstalledReleaseInfo`) ist ungültig (nur für Methoden/Properties zulässig) und erscheint in keiner generierten Doku.
- [ ] `AutoUpdateBuilder.ExplicitDownloadPath`-XML-Kommentar nennt nur `EnableAutomaticDownload` als Setzer, nicht die neuere Methode `WithDownloadPath`, die ebenfalls das Feld setzt.
- [ ] `UpdateSettingsStore`: `Defaults()` und `ReadSettingsAsync()` wandeln wortgleich dieselben zehn Felder in ein `UpdateSettingsUpdateRequest` um (Data Clump) — in eine private `ToRequest(...)`-Hilfsmethode auslagern.
- [ ] `ProgramExtensions`: Fallback für die lokale Ordnerquelle baut `Path.Combine(updateOptions.WorkingDirectory, "source")` relativ zum Arbeitsverzeichnis, während die Bibliothek `DownloadPath` konsistent gegen `ContentRootPath` auflöst — als Windows-Dienst zeigen Quelle und Downloadverzeichnis auf unterschiedliche Orte. Zusätzlich dupliziertes Literal `"source"`.
- [ ] `ProgramExtensions`: Der Fallback-Zweig `if (!sourceCheckIntervalConfigured) ...` (samt `UpdateOptions.CheckIntervalMinutes`) ist praktisch unerreichbar, da `Updates:SourceCheck:Interval` bereits standardmäßig in `appsettings.json` gesetzt ist.
- [ ] `SoftwareSchmiede.AutoUpdate.Tests`: Das Muster `Directory.CreateTempSubdirectory()` + try/finally steht 26× in 9 Testdateien, obwohl `TestSupport/AutoUpdateTestContext` das Verzeichnis-Lifecycle bereits kapselt — gemeinsame `TempDirectory : IDisposable`-Hilfsklasse ergänzen.
- [ ] 22 Testdateien im Namespace `SoftwareSchmiede.AutoUpdate.Tests` enthalten ein überflüssiges `using SoftwareSchmiede.AutoUpdate;` (übergeordneter Namespace ohnehin im Gültigkeitsbereich).
- [ ] `AutoUpdateTestContext.Dispose()` gibt nur das temporäre Verzeichnis frei, disposed aber weder `Orchestrator` noch `StatusService` (beide `IDisposable`, halten Semaphoren).
- [ ] `RecordingProcessRunner.PrepareEnvironmentCallCount` zählt Aufrufe von `EnsureUpdateUnitAvailable` — veralteter Name aus früherer Methodenbenennung.
- [ ] `AutoUpdateSchedulerServiceTests`: alle drei Tests nutzen feste `Task.Delay(100)` statt der vorhandenen `AsyncTestWait.AssertStaysFalseAsync`-Hilfsmethode (unzuverlässig unter Last).
- [ ] `AutoUpdateSchedulerServiceTests`: der 10-argumentige `AutoUpdateStatusSnapshot`-Konstruktor wird dreimal mit überwiegend `null`-Werten ausgeschrieben, obwohl in `FinanceManager.Tests/Updates/UpdateStatusTestData.cs` bereits ein Builder für dieses Problem existiert (fehlt im Bibliotheks-Testprojekt).
- [ ] `ProcessOutputReaderTests.Read_OnTimeout_KillsChildProcessInsteadOfLeavingItRunning` wartet mit festem `Task.Delay(3500)` statt `AsyncTestWait.AssertStaysFalseAsync`.
- [ ] `AutoUpdatePackageValidatorTests`: der Unix-Dateimodus-Zweig von `ValidateEntry` (Symlinks/Geräte/Sockets ablehnen) hat keine Testabdeckung.
- [ ] `AutoUpdateGithubSourceTests`: `HttpClient`, `AutoUpdateGithubSource` und `StubHttpMessageHandler` werden in mehreren Tests nie disposed.
- [ ] Fehlende Testabdeckung für neue öffentliche API: `AutoUpdateBuilder.WithUpdateUnitName`, `AutoUpdateBuilder.WithDownloadPath` (weder Erfolgsfall noch `ArgumentException`), sowie `ScheduledInstallEvaluator`, `AutoUpdateInstaller`, `DefaultAutoUpdateProcessRunner`, `JsonFileStore` und der `ExecutablePath`-Zweig von `AutoUpdateServiceResolver.ValidateExecutablePath`.
- [ ] `AutoUpdateOptionsMapperTests.ApplySettings_WhenSourceIsGithubSource_ReplacesSourceWithUpdatedRepository` prüft nur, dass eine andere Instanz desselben Typs vorliegt, nicht dass `new-owner`/`new-repo`/`manifest.json` tatsächlich übernommen wurden — würde auch bei unveränderten Repository-Werten grün bleiben.
- [ ] `AutoUpdateOptionsMapperTests`: erzeugte `AutoUpdateGithubSource`-Instanzen (mit eigenem `HttpClient`) werden am Testende nicht disposed.
- [ ] `UpdateSettingsStoreTests.AutoUpdateEnvironmentAdapter` ist eine Lazy Class, die nur `TestWebHostEnvironment.ContentRootPath` durchreicht — obwohl mit `HostAutoUpdateEnvironment`/`TestAutoUpdateEnvironment` bereits einfachere Implementierungen existieren.
- [ ] `UpdateControllerIntegrationTests` ist eine God-Klasse (320 Zeilen, 7 Themen, 3 Test-Doubles, mehrfach wortgleiche Testaufbauten) — widerspricht der Repo-Konvention `test-class-structure`; an Themengrenzen aufteilen.
- [ ] `PlaywrightWebAppFixture.PrepareInstalledReleaseMetadata` schreibt weiterhin `release-metadata.json` in den Quellordner des Repositories (mit `.gitignore`- und `ProcessExit`-Absicherung als Milderung, nicht als Ursachenbehebung) — Empfehlung: Serverprozess mit eigenem `ASPNETCORE_CONTENTROOT` starten.

## Fehlgeschlagene Tests

Keine Tests fehlgeschlagen (Stand letzter Testlauf: 893/893 bestanden in `FinanceManager.Tests`,
nach Verifikation des umgebungsbedingten `help-assets.sha256`-Vorfalls; `SoftwareSchmiede.AutoUpdate.Tests`
und `FinanceManager.Tests.Integration` zuvor ebenfalls vollständig grün).
