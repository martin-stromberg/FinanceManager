# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### DefaultAutoUpdateProcessRunner.cs (DefaultAutoUpdateProcessRunner)

- **Hardcodierte Werte / falsche Verantwortlichkeit** — In `StartPrepareEnvironment` und `StartScript` ist der anwendungsspezifische Unit-Name `FinanceManagerUpdate.service` bzw. `--unit=FinanceManagerUpdate` fest verdrahtet (Zeilen 33, 35, 44, 60). Die Klasse liegt in der wiederverwendbaren NuGet-Bibliothek `SoftwareSchmiede.AutoUpdate`; jede andere konsumierende Anwendung würde denselben systemd-Unit-Namen belegen und sich mit FinanceManager gegenseitig blockieren.

  Empfehlung: Den Unit-Namen als Property in `AutoUpdateOptions` aufnehmen (z. B. `UpdateUnitName` mit Default `"AutoUpdate"`), per Konstruktor in `DefaultAutoUpdateProcessRunner` injizieren und an allen vier Stellen verwenden. In `ProgramExtensions.cs` für FinanceManager auf `"FinanceManagerUpdate"` setzen.

- **Fehlende Validierung** — In `StartPrepareEnvironment` wird die Ausgabe von `systemctl show ... --property=LoadState` mit `.Split('=')[1]` zerlegt (Zeilen 33–36). Liefert `systemctl` eine leere oder unerwartete Ausgabe (z. B. bei fehlendem systemd), wirft der Indexzugriff eine `IndexOutOfRangeException` statt einer aussagekräftigen Fehlermeldung.

  Empfehlung: Eine private Hilfsmethode `ReadUnitProperty(string unit, string property)` einführen, die `Split('=', 2)` verwendet, bei weniger als zwei Teilen `string.Empty` zurückgibt und den Aufrufer damit definiert weiterarbeiten lässt.

- **Doppelter Code** — Die private Methode `Run(string fileName, string arguments)` (Zeilen 73–99) ist nahezu identisch mit `DefaultAutoUpdateServiceProbe.Run` (Zeilen 100–114 dort). Beide bauen dasselbe `ProcessStartInfo` mit `RedirectStandardOutput`/`RedirectStandardError`/`UseShellExecute=false`/`CreateNoWindow=true` auf.

  Empfehlung: Einen gemeinsamen `internal static class ProcessOutputReader` mit einer Methode `Read(string fileName, string arguments, int timeoutMs)` anlegen und beide Klassen darauf umstellen.

- **Fehlende Kapselung** — Die Zeilen `var extension = Path.GetExtension(scriptPath); var isWindows = extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase);` stehen wortgleich in `StartPrepareEnvironment` (Zeilen 26–27) und `StartScript` (Zeilen 56–57).

  Empfehlung: In eine private statische Methode `private static bool IsPowerShellScript(string scriptPath)` auslagern.

### DefaultAutoUpdateServiceProbe.cs (DefaultAutoUpdateServiceProbe)

- **Still geschluckte Exceptions** — In `FindWindowsServicesForCurrentProcess` (Zeilen 45–48) und `FindLinuxServicesForCurrentProcess` (Zeilen 74–77) fangen `catch { return Array.Empty<string>(); }`-Blöcke jede Exception ohne Logging ab. Schlägt die Diensterkennung fehl, ist im Betrieb nicht unterscheidbar, ob kein Dienst gefunden wurde oder `sc.exe`/`systemctl` gar nicht ausführbar war; der Anwender erhält später nur die generische Meldung „Configure a service name…“.

  Empfehlung: `ILogger<DefaultAutoUpdateServiceProbe>` injizieren und in beiden `catch`-Blöcken `LogDebug(ex, ...)` mit dem aufgerufenen Kommando protokollieren, bevor die leere Liste zurückgegeben wird.

- **Toter Code** — In `StartPrepareEnvironment` (Datei `DefaultAutoUpdateProcessRunner.cs`, Zeile 38) wird `unitExists` berechnet, aber nur zur Ableitung von `unitFailedOrHanging` benutzt; die Variable trägt keine eigene Aussage.

  Empfehlung: `unitExists` entfernen und `var unitFailedOrHanging = loadState != "not-found" && activeState != "active";` direkt schreiben.

### AutoUpdateOptionsValidator.cs (AutoUpdateOptionsValidator)

- **Verantwortlichkeitsverletzung (Seiteneffekt in Validierung)** — `Validate` verändert in Zeile 43 das übergebene Objekt: `options.HealthTimeoutSeconds = Math.Clamp(options.HealthTimeoutSeconds, 10, 600);`. Eine `IValidateOptions<T>`-Implementierung muss reine Prüflogik sein; die Instanz wird zusätzlich als Singleton in DI registriert (`AutoUpdateHostBuilderExtensions.cs`, Zeile 47) und kann damit zu jedem späteren Zeitpunkt den Wert überschreiben.

  Empfehlung: Das Clamping aus dem Validator entfernen. Stattdessen bei ungültigem Wert eine Fehlermeldung nach dem Muster der übrigen Prüfungen ergänzen (`"HealthTimeoutSeconds must be between 10 and 600."`) und die Normalisierung einmalig in `UseAutoUpdate` vor dem Validierungsaufruf durchführen.

- **Hardcodierte Werte** — Die Grenzwerte `10` und `600` (Zeile 43) sowie `1` (Zeile 30) stehen als Literale im Code und tauchen zusätzlich in `UpdateSettingsStore.cs` (Zeilen 78, 91, 121) erneut auf.

  Empfehlung: Als `public const int MinHealthTimeoutSeconds = 10;` / `MaxHealthTimeoutSeconds = 600;` auf `AutoUpdateOptions` definieren und an allen Stellen referenzieren.

### AutoUpdateCommandService.cs (AutoUpdateCommandService)

- **Middle Man / Lazy Class** — Alle drei Methoden (`CheckAsync`, `DownloadAsync`, `InstallAsync`, Zeilen 22–31) delegieren eins-zu-eins an `IAutoUpdateOrchestrator`, ohne eigene Logik. Der XML-Kommentar bestätigt das ausdrücklich („Contains no update logic of its own“). `IAutoUpdateCommandHandler` ist damit eine reine Teilmenge von `IAutoUpdateOrchestrator`.

  Empfehlung: `IAutoUpdateCommandHandler` und `AutoUpdateCommandService` entfernen und die Konsumenten (`AutoUpdateSchedulerService`, `UpdateOrchestratorAdapter`) direkt auf `IAutoUpdateOrchestrator` umstellen. Falls die schmalere Schnittstelle als bewusste Fassade für UI-Code erhalten bleiben soll, ist das im Klassenkommentar als Interface-Segregation zu begründen — dann aber `AutoUpdateOrchestrator` direkt `IAutoUpdateCommandHandler` implementieren lassen und die Extraklasse streichen.

### AutoUpdatePlatformResolver.cs (AutoUpdatePlatformResolver)

- **Inkonsistente Abstraktion / Testbarkeit** — `CurrentRuntimeIdentifier` nutzt für den Fallback korrekt das injizierte Feld `_runtimeIdentifier` (Zeile 49), `CurrentPlatform` greift für denselben Fallback dagegen direkt auf `RuntimeInformation.OSDescription` zu (Zeile 59). Der Fallback-Pfad von `CurrentPlatform` ist dadurch nicht testbar.

  Empfehlung: Ein zweites Konstruktorfeld `_platformName` (oder Ableitung aus `_runtimeIdentifier`) einführen und in Zeile 59 statt `RuntimeInformation.OSDescription` verwenden.

- **Unzureichende Testabdeckung** — `AutoUpdatePlatformResolverTests.cs` enthält genau einen Test (`SelectPackage_MatchesRuntimeIdentifier`). Die öffentlichen Properties `CurrentRuntimeIdentifier` und `CurrentPlatform` werden nicht direkt geprüft, ebenso wenig der Fall, dass `SelectPackage` kein passendes Paket findet (`null`-Rückgabe).

  Empfehlung: Je einen Test für `CurrentPlatform`/`CurrentRuntimeIdentifier` unter Windows- und Linux-Stub ergänzen sowie `SelectPackage_WhenNoPackageMatches_ReturnsNull`.

### AutoUpdateGithubSource.cs (AutoUpdateGithubSource)

- **Inkonsistente Parameterreihenfolge** — Der Konstruktor nimmt `(HttpClient, string repositoryOwner, string repositoryName, …)` (Zeile 26), die statische Factory `Create` dagegen `(string repositoryName, string repositoryOwner)` (Zeile 50) — dieselben zwei `string`-Parameter in umgekehrter Reihenfolge. `AutoUpdateBuilder.UseGithubSource` (Zeile 62) folgt der Factory-Reihenfolge. Vertauschte Argumente sind vom Compiler nicht erkennbar.

  Empfehlung: `Create` und `UseGithubSource` auf die Reihenfolge `(repositoryOwner, repositoryName)` des Konstruktors umstellen und die Aufrufstelle in `ProgramExtensions.cs` (Zeile 178) sowie `AutoUpdateBuilderTests.Builder_UseGithubSource_CreatesGithubSource` entsprechend anpassen.

- **Hardcodierter Wert** — Der Manifestname ist als `private const string ManifestAssetName = "update.json"` (Zeile 12) fest verdrahtet; identisch in `AutoUpdateLocalFolderSource.ManifestFileName` (Zeile 10). Gleichzeitig ist `ManifestAssetName` in `UpdateOptions` (Zeile 38), in `appsettings.json` und in `UpdateSettingsDto` als konfigurierbarer Wert modelliert und wird über die Setup-UI gepflegt — er hat auf das Verhalten der Bibliothek jedoch keinerlei Wirkung.

  Empfehlung: Den Manifestnamen als optionalen Konstruktorparameter beider Quellen (Default `"update.json"`) durchreichen und in `AutoUpdateBuilder.UseGithubSource`/`UseLocalFolderSource` konfigurierbar machen. Alternativ das Feld aus `UpdateOptions`/`UpdateSettingsDto`/UI entfernen, damit keine wirkungslose Einstellung angeboten wird.

- **Ressourcenleck im Fehlerfall** — In `DownloadAsync` wird nach `tempPath` geschrieben (Zeilen 97–119). Wirft die Größenprüfung in Zeile 114 oder der Kopiervorgang, bleibt die `.tmp`-Datei im Zielverzeichnis liegen; über wiederholte fehlschlagende Downloads sammeln sich beliebig viele Fragmente an.

  Empfehlung: Den Kopierblock in `try`/`catch` einbetten und im `catch` vor dem erneuten Werfen `File.Delete(tempPath)` best-effort aufrufen.

### AutoUpdateStatusService.cs (AutoUpdateStatusService)

- **Inkonsistente Speicherzugriffs-Semantik** — Der Fast-Path in `EnsureLoadedAsync` liest das Flag mit `Volatile.Read(ref _loaded)` (Zeile 45), gesetzt wird es aber mit einem einfachen `_loaded = true;` (Zeile 65). Das Double-Checked-Locking-Muster ist damit nur halb abgesichert.

  Empfehlung: Zeile 65 auf `Volatile.Write(ref _loaded, true);` ändern. Die Zuweisung muss zudem nach dem Schreiben von `_snapshot` erfolgen, was bereits der Fall ist.

### FileSystemAutoUpdatePackageStore.cs (FileSystemAutoUpdatePackageStore)

- **Umgehung der Zeitabstraktion** — `TryCreateLockAsync` schreibt den Lock-Zeitstempel mit `DateTimeOffset.UtcNow` (Zeile 98), während der gesamte übrige Workflow (`AutoUpdateOrchestrator`, `AutoUpdateSchedulerService`, `AutoUpdateCheckerService`) über den injizierten `TimeProvider` arbeitet. Die Lock-Alterung ist dadurch in Tests nicht steuerbar.

  Empfehlung: `TimeProvider` in den Konstruktor aufnehmen und `_timeProvider.GetUtcNow()` verwenden.

### AutoUpdateHostBuilderExtensions.cs (AutoUpdateHostBuilderExtensions)

- **Konfiguration überschreibt Builder-Aufrufe stillschweigend** — In `UseAutoUpdate` wird zuerst der `configure`-Delegat ausgeführt (Zeile 26) und anschließend `Bind` auf dieselbe Options-Instanz angewendet (Zeile 28). Alles, was per Fluent-API gesetzt wurde und zugleich als Konfigurationsschlüssel existiert, wird überschrieben. Konkret verpufft in `ProgramExtensions.cs` der Aufruf `cfg.WithSourceCheck(Math.Max(1, updateOptions.CheckIntervalMinutes))`, weil `appsettings.json` unter `Updates:SourceCheck:Interval` einen eigenen Wert liefert.

  Empfehlung: Die Reihenfolge umkehren — erst `Bind`, dann `configure?.Invoke(...)`, damit explizite Code-Konfiguration Vorrang vor der Datei-Konfiguration hat. Der Vorrang ist im XML-Kommentar der Methode zu dokumentieren.

### SoftwareSchmiede.AutoUpdate.csproj

- **Toter Code (ungenutzte Abhängigkeit)** — `<PackageReference Include="Microsoft.Extensions.Http" Version="10.0.7" />` (Zeile 32) wird nirgends verwendet; die Bibliothek enthält weder `AddHttpClient` noch `IHttpClientFactory`, `AutoUpdateGithubSource` erzeugt seinen `HttpClient` selbst.

  Empfehlung: Die `PackageReference` entfernen — oder `AutoUpdateGithubSource` auf `IHttpClientFactory` umstellen, was den in `Create` erzeugten, nie freigegebenen `HttpClient` (Zeile 52) gleich mit behebt.

### UpdateOptions.cs (UpdateOptions)

- **Toter Code** — Die Properties `Enabled`, `HealthTimeoutSeconds`, `MaxAssetBytes`, `HostedServicesEnabled`, `ServiceName`, `ExecutablePath`, `EnableAutomaticDownload` und `EnableAutomaticInstallation` werden im gesamten Repository nie gelesen. Gelesen werden ausschließlich `WorkingDirectory`, `CheckIntervalMinutes`, `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`, `SourceType` und `LocalFolderPath`. Alle übrigen Werte bedient inzwischen `AutoUpdateOptions`, das aus derselben Sektion `Updates` gebunden wird.

  Empfehlung: Die acht ungenutzten Properties aus `UpdateOptions` entfernen. Der Klassenkommentar ist anzupassen: `UpdateOptions` hält nur noch die FinanceManager-spezifischen Felder (Repository, Manifestname, Quellenauswahl), alles Übrige gehört zu `AutoUpdateOptions`.

- **Inkonsistente Schreibweise für dasselbe Konzept** — Für zwei fachliche Konzepte existieren jeweils zwei Namen in derselben Konfigurationssektion `Updates`: `WorkingDirectory` (UpdateOptions) vs. `DownloadPath` (AutoUpdateOptions) sowie `CheckIntervalMinutes` (UpdateOptions) vs. `SourceCheck.Interval` (AutoUpdateOptions). In `appsettings.json` stehen beide Intervall-Schlüssel mit dem Wert `360` nebeneinander (Zeilen 37 und 51); welcher gewinnt, ist nur aus der Bindungsreihenfolge in `UseAutoUpdate` ableitbar.

  Empfehlung: Auf die Namen der Bibliothek vereinheitlichen. `Updates:CheckIntervalMinutes` und `Updates:WorkingDirectory` aus `appsettings.json` und `UpdateOptions` entfernen, ausschließlich `Updates:SourceCheck:Interval` und `Updates:DownloadPath` verwenden. Die Umbenennung ist in `PlaywrightWebAppFixture.StartServer` (Environment-Variable `Updates__WorkingDirectory`) und `UpdateSettingsStore` nachzuziehen.

### UpdateSettingsStore.cs (UpdateSettingsStore)

- **Doppelter Code** — Der zehnstellige `UpdateSettingsDto`-Konstruktor wird dreimal mit praktisch identischer Normalisierungslogik aufgerufen: `Defaults()` (Zeilen 68–78), `Normalize(request)` (Zeilen 81–91) und der Legacy-Zweig in `ReadSettingsAsync` (Zeilen 111–121). Jede Änderung an den Normalisierungsregeln muss an drei Stellen nachgezogen werden.

  Empfehlung: Eine private Methode `private UpdateSettingsDto Build(bool enabled, int intervalMinutes, string? owner, string? name, string? manifest, TimeOnly? scheduled, string? serviceName, string? executablePath, string? workingDirectory, int healthTimeout)` einführen, die die Normalisierung genau einmal enthält, und alle drei Stellen darauf umstellen.

- **Hardcodierte Werte** — Die Literale `"martin-stromberg"`, `"FinanceManager"` und `"update.json"` stehen je dreimal im Code (Zeilen 71–73, 84–86, 114–116), obwohl `UpdateOptions.RepositoryOwner`/`RepositoryName`/`ManifestAssetName` bereits dieselben Defaults deklarieren (Zeilen 28, 33, 38 dort). Ebenso `Math.Clamp(..., 10, 600)` dreimal und `Math.Clamp(..., 1, 24 * 60)` zweimal.

  Empfehlung: Auf `UpdateOptions` `public const string DefaultRepositoryOwner`, `DefaultRepositoryName`, `DefaultManifestAssetName` definieren und im Store referenzieren; die Clamp-Grenzen als benannte Konstanten wie oben unter `AutoUpdateOptionsValidator` beschrieben.

- **Doppelter Code über Projektgrenzen** — `WriteAtomicAsync` (Zeilen 127–137) und das statische Feld `JsonOptions` (Zeile 14) duplizieren `JsonFileStore.WriteAtomicAsync` und `JsonFileStore.JsonOptions` aus der Bibliothek Zeile für Zeile (Temp-Datei mit `Guid`-Suffix, `File.Move(overwrite: true)`). Eine vierte Kopie derselben `JsonSerializerOptions`-Konstruktion findet sich in `PlaywrightWebAppFixture` (zweimal, Zeilen der Methoden `PrepareUpdateSource` und `PrepareInstalledReleaseMetadata`).

  Empfehlung: `JsonFileStore` in der Bibliothek von `internal` auf `public` heben (Name z. B. `AutoUpdateJsonFileStore`) und in `UpdateSettingsStore` sowie im E2E-Fixture verwenden; die lokalen Kopien entfernen.

### UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **God-Klasse / Long Parameter List** — Der Konstruktor nimmt neun Abhängigkeiten (Zeilen 37–46). Die Klasse vereint drei getrennte Verantwortlichkeiten: Mapping `AutoUpdateStatusSnapshot` → `UpdateStatusDto` (`MapToStatusDtoAsync`, `MapState`), Weiterreichen der Settings-Operationen an `IUpdateSettingsStore` (`GetSettingsAsync`, `SaveSettingsAsync`, `ScheduleAsync`) und die eigenständige Lock-Reset-Fachlogik (`ResetLockAsync`).

  Empfehlung: Das Mapping in eine eigene Klasse `UpdateStatusDtoMapper` mit den Abhängigkeiten `IInstalledReleaseMetadataProvider`, `IUpdateSettingsStore` und `IAutoUpdatePlatformResolver` auslagern; `ResetLockAsync` in einen `UpdateLockResetService` mit `IAutoUpdatePackageStore`, `AutoUpdateStatusService`, `AutoUpdateOptions` und `TimeProvider`. Der Adapter behält damit vier Abhängigkeiten.

- **Umgehung der Zeitabstraktion** — `ResetLockAsync` vergleicht mit `DateTimeOffset.UtcNow` (Zeile 116), obwohl `TimeProvider` in der Anwendung registriert ist (`ProgramExtensions.cs`, Zeile 162). Die Staleness-Prüfung ist dadurch nicht deterministisch testbar — entsprechend fehlt für `ResetLockAsync` auch jeder Test.

  Empfehlung: `TimeProvider` injizieren und `_timeProvider.GetUtcNow()` verwenden; anschließend Tests für „Lock zu jung → `IOException`“ und „Lock stale → wird gelöscht“ ergänzen.

- **Hardcodierter Wert** — `Math.Max(_autoUpdateOptions.HealthTimeoutSeconds, 60)` (Zeile 115) führt eine zweite, vom Validator abweichende Untergrenze (60 statt 10) für denselben Wert ein.

  Empfehlung: Die Untergrenze als benannte Konstante definieren oder — da `AutoUpdateOptionsValidator` bereits auf mindestens 10 Sekunden normalisiert — `Math.Max` ersatzlos streichen.

- **Unzureichende Testabdeckung** — `UpdateOrchestratorAdapterTests.cs` deckt `GetStatusAsync`, `StartInstallAsync` und `SaveSettingsAsync` ab. Für die öffentlichen Methoden `CheckAsync`, `ScheduleAsync` und `ResetLockAsync` existiert kein Test.

  Empfehlung: Je einen Test ergänzen: `CheckAsync` mappt `AutoUpdateOutcome.Success` auf `UpdateCheckResultDto.Success == true`, `ScheduleAsync` ruft `SaveScheduleAsync` und anschließend `ApplyToOptionsAsync` auf, `ResetLockAsync` wie oben beschrieben.

### InstalledReleaseMetadataProvider.cs (InstalledReleaseMetadataProvider)

- **Middle Man** — Die Klasse besteht ausschließlich aus einer Feld-zu-Feld-Übertragung von `InstalledReleaseInfo` nach `InstalledReleaseMetadataDto` (Zeile 27); beide Records haben dieselben fünf Felder in derselben Reihenfolge. `IInstalledReleaseMetadataProvider` ist damit ein reines Duplikat von `IInstalledVersionProvider`.

  Empfehlung: Prüfen, ob `IInstalledReleaseMetadataProvider` entfallen kann und die Aufrufer (`UpdateOrchestratorAdapter`, ggf. Controller) direkt `IInstalledVersionProvider` verwenden, wobei die Abbildung auf das DTO an der Stelle erfolgt, an der das DTO tatsächlich gebaut wird (`MapToStatusDtoAsync`). Falls die Trennung als Schutz der Web-Schicht vor Bibliothekstypen gewollt ist, ist das im Klassenkommentar zu begründen.

### PlaywrightWebAppFixture.cs (PlaywrightWebAppFixture)

- **Test verändert den Quellbaum** — `PrepareInstalledReleaseMetadata` schreibt nach `<RepoRoot>/FinanceManager.Web/release-metadata.json`, also in eine Datei innerhalb des Arbeitsverzeichnisses, und stellt sie in `RestoreInstalledReleaseMetadata` wieder her. Die Datei ist nicht in `.gitignore` eingetragen und erscheint während des Testlaufs als untracked file. Bricht der Prozess vor dem Teardown ab, bleibt eine Fremddatei im Repository zurück; parallele Testläufe überschreiben sich gegenseitig.

  Empfehlung: Die Metadatendatei nicht im Quellbaum ablegen. Stattdessen den Server mit einem eigenen ContentRoot starten (Umgebungsvariable bzw. `--contentRoot` auf ein temporäres Verzeichnis) und `release-metadata.json` dort erzeugen. Ergänzend `FinanceManager.Web/release-metadata.json` in `.gitignore` aufnehmen, da die Datei laut `.github/workflows/release.yml` ein Release-Artefakt und kein Quelldatei ist.

- **Hardcodierte Werte** — `PrepareUpdateSource` schreibt `platform = "windows"` und `runtimeIdentifier = "win-x64"` fest ins Manifest. Auf einem Linux-Testrunner wählt `AutoUpdatePlatformResolver.SelectPackage` kein Paket aus, `Admin_TriggersCheck_ShowsAvailableUpdate` schlägt fehl.

  Empfehlung: Plattform und Runtime-Identifier aus `RuntimeInformation` ableiten (`OperatingSystem.IsWindows() ? ("windows", "win-x64") : ("linux", "linux-x64")`), damit die E2E-Tests plattformunabhängig laufen.

- **Zu breite Exception-Handler** — `RestoreInstalledReleaseMetadata` und `DeleteDirectoryBestEffort` verwenden je einen parameterlosen `catch { }` mit reinem Kommentar.

  Empfehlung: Auf `catch (IOException)` bzw. `catch (UnauthorizedAccessException)` einschränken, damit unerwartete Fehler (z. B. `NullReferenceException`) nicht unbemerkt verschluckt werden.

### SetupUpdateGateway.cs (SetupUpdateGateway)

- **Toter Parameter / Type-Check-Kette** — `GetDefinitionValueAsync(string localizationKey, int fallbackIndex)` bildet den Lokalisierungsschlüssel über eine `switch`-Kette auf einen festen Zeilenindex ab. Beide Aufrufer übergeben `fallbackIndex: 0` und treffen stets einen der beiden `case`-Zweige; der Parameter ist damit tot. Der Kommentar räumt selbst ein, dass die Zuordnung allein auf der Renderreihenfolge von `SetupUpdateTab.razor` beruht.

  Empfehlung: Den Parameter `fallbackIndex` entfernen und die Methode zu `private Task<string> GetDefinitionValueAsync(int index)` vereinfachen, mit `GetStatusValueAsync() => GetDefinitionValueAsync(1)`. Nachhaltiger: in `SetupUpdateTab.razor` je `dd` ein `data-testid` ergänzen und darüber selektieren, statt über die Position.

- **Fehlerhafte/kollidierende Selektoren** — `CheckNowButton` ist `.setup-update-tab button.secondary` (`.First`), `SaveSettingsButton` ist `.setup-update-tab button` (`.First`). Der zweite Selektor ist eine Obermenge des ersten und liefert damit sehr wahrscheinlich denselben Button; `Admin_SavesSettings_PersistsAcrossReload` würde dann „Jetzt prüfen“ statt „Speichern“ klicken und trotzdem grün sein, weil nur die Checkbox geprüft wird.

  Empfehlung: Beide Buttons über eindeutige `data-testid`-Attribute ansprechen und in `Admin_SavesSettings_PersistsAcrossReload` zusätzlich verifizieren, dass der Speichervorgang stattgefunden hat (z. B. Erfolgsmeldung oder erneutes Laden der Settings über die API).

### UpdateSetupPlaywrightTests.cs (UpdateSetupPlaywrightTests)

- **Doppelter Code** — Die sieben Zeilen Session-, Seeder- und Login-Aufbau (`CreateSessionAsync`, `AuthGateway`, `TestUserSeeder`, `EnsureUserAsync`, `LoginAsync`) sind in allen drei Testmethoden wortgleich wiederholt.

  Empfehlung: Eine private Hilfsmethode `private async Task<(PlaywrightBrowserSession Session, SetupUpdateGateway Gateway)> LoginAsAdminAndOpenUpdateTabAsync()` einführen und in allen drei Tests verwenden.

### UpdateControllerIntegrationTests.cs (UpdateControllerIntegrationTests)

- **Inappropriate Intimacy** — `SetDownloadPath` durchsucht die `IServiceCollection` nach dem Descriptor für `AutoUpdateOptions`, castet dessen `ImplementationInstance` und mutiert das Singleton direkt. Der Test bindet sich damit an ein internes Registrierungsdetail von `UseAutoUpdate` (dass die Options als konkrete Instanz und nicht als Factory registriert werden) und bricht bei jeder Änderung der Registrierungsart.

  Empfehlung: Den Pfad über die Konfiguration setzen (`builder.UseSetting("Updates:DownloadPath", tempDir.FullName)` bzw. eine In-Memory-Konfigurationsquelle), so wie es `TestWebApplicationFactory` für die übrigen Update-Schlüssel bereits tut.

- **Doppelter Code** — `InstallingSnapshot` (Zeilen 227–238) baut lokal einen `AutoUpdateStatusSnapshot`, obwohl mit `UpdateStatusTestData` (Projekt `FinanceManager.Tests`) genau dafür eine geteilte Fixture-Klasse existiert, die im selben Änderungssatz um `ReadyToInstallSnapshot` erweitert wurde.

  Empfehlung: `InstallingSnapshot` als `InstallingSnapshot(string availableVersion)` nach `UpdateStatusTestData` verschieben und den `using FinanceManager.Tests.Updates;`-Import wiederherstellen.

- **Doppelter Code** — `FixedInstalledVersionProvider` ist in dieser Datei (Zeilen 298–309) und in `InstalledReleaseMetadataProviderTests.cs` (Zeilen 21–33) jeweils neu definiert; ein drittes, funktionsgleiches Exemplar heißt `TestInstalledVersionProvider` in `AutoUpdateTestContext.cs`. Für dasselbe Konzept existieren drei Klassen mit zwei Namensschemata.

  Empfehlung: Eine gemeinsame Test-Double-Klasse mit einheitlichem Namen (`FakeInstalledVersionProvider`, passend zu `FakeAutoUpdateSource`) in einem geteilten TestSupport-Namespace bereitstellen und alle drei Definitionen darauf zurückführen.

### UpdateStatusTestData.cs (UpdateStatusTestData)

- **Toter Code** — `InstallingStatus` wird nach der Umstellung von `UpdateControllerIntegrationTests` auf `InstallingSnapshot` von keiner Testklasse mehr aufgerufen (einziger verbleibender Aufruf im Repository ist `ReadyToInstallSnapshot`). Der Klassenkommentar behauptet weiterhin eine Wiederverwendung „across the unit and integration test projects“, die für `InstallingStatus` nicht mehr besteht.

  Empfehlung: `InstallingStatus` entfernen — oder, gemäß dem Befund zu `UpdateControllerIntegrationTests`, dort wieder verwenden. Den Klassenkommentar an den tatsächlichen Stand anpassen.

### AutoUpdateTestContext.cs (AutoUpdateTestContext)

- **Uneinheitliche Ablage und Benennung der Test-Doubles** — `FakeAutoUpdateSource` und `TestAutoUpdateEnvironment` liegen als eigene Dateien in `TestSupport/`, während `TestInstalledVersionProvider`, `RecordingProcessRunner` und `RecordingHostTerminator` als verschachtelte Klassen in `AutoUpdateTestContext` stehen (Zeilen 138–169). Für denselben Zweck existieren drei Präfixe: `Fake*`, `Test*`, `Recording*`.

  Empfehlung: Die drei verschachtelten Klassen als eigene Dateien nach `TestSupport/` verschieben und ein einheitliches Präfix wählen (`Fake*` für gesteuerte Rückgaben, `Recording*` nur dort, wo tatsächlich Aufrufe protokolliert werden — dann ist `TestInstalledVersionProvider` in `FakeInstalledVersionProvider` umzubenennen).

### AutoUpdateScriptGeneratorTests.cs (AutoUpdateScriptGeneratorTests)

- **Stillschweigend bestehende Tests** — `Generate_OnWindows_WritesPowerShellScript` und `Generate_OnLinux_WritesShellScriptWithUnixLineEndings` beginnen mit `if (!OperatingSystem.IsWindows()) return;` bzw. `IsLinux()`; auf der jeweils anderen Plattform gelten sie als bestanden, ohne etwas geprüft zu haben. In `Generate_WithoutTarget_Throws` steht die einzige Assertion in einem `if`-Block und entfällt auf Fremdplattformen vollständig.

  Empfehlung: Auf `Assert.Skip(...)` (xunit.v3 ist im Testprojekt referenziert) bzw. ein `[Fact(Skip = ...)]`-Attribut mit Plattformbedingung umstellen, damit übersprungene Tests im Testbericht sichtbar sind.

- **Toter Code (ungenutzte Variable)** — In beiden plattformspezifischen Tests wird `var (generator, packageStore) = CreateGenerator(dir.FullName);` dekonstruiert, `packageStore` aber nie verwendet.

  Empfehlung: `var (generator, _) = CreateGenerator(...)` schreiben — wie es `Generate_WithoutTarget_Throws` bereits tut — oder den zweiten Rückgabewert aus `CreateGenerator` entfernen, da er von keinem Test benötigt wird.

### AutoUpdateCheckerServiceTests.cs / AutoUpdateSchedulerServiceTests.cs

- **Doppelter Code** — Die private Hilfsmethode `WaitForAsync(Func<bool> condition, int timeoutMs = 3000)` ist in beiden Dateien Zeile für Zeile identisch vorhanden.

  Empfehlung: In eine gemeinsame Klasse `TestSupport/AsyncTestWait.cs` auslagern und aus beiden Testklassen aufrufen.

- **Zeitabhängige Tests** — Obwohl `FakeTimeProvider` verwendet wird, mischen `Execute_WhenCheckThrows_ContinuesLoop`, `Execute_RespectsConfiguredInterval`, `Execute_AtScheduledTime_TriggersInstall`, `Execute_WhenNotReady_DoesNotInstall` und `Execute_SameScheduleTwice_InstallsOnce` echte `await Task.Delay(50)`/`Task.Delay(100)`-Wartezeiten mit `timeProvider.Advance(...)`. Auf ausgelasteten Buildagenten sind das Wettlaufbedingungen; insbesondere `orchestrator.Invocations.Count.Should().Be(1)` nach `Task.Delay(50)` kann sporadisch fehlschlagen.

  Empfehlung: Die festen `Task.Delay`-Aufrufe durch `WaitForAsync`-Bedingungen auf den erwarteten Zustand ersetzen. Wo auf das Ausbleiben eines Aufrufs geprüft wird, statt eines Sleeps ein `TaskCompletionSource` im Mock-Setup verwenden, das erst nach dem `Advance` gesetzt werden darf.

### AutoUpdateStatusServiceTests.cs (AutoUpdateStatusServiceTests)

- **Aussagelose Assertion** — `Update_FromParallelThreads_KeepsLastWriteVisible` prüft nach 25 parallelen Updates lediglich `snapshot.LastError.Should().StartWith("error-")`. Diese Bedingung ist für jedes der 25 möglichen Ergebnisse erfüllt; der eigentliche Testgegenstand (Konsistenz unter Nebenläufigkeit) wird nicht verifiziert.

  Empfehlung: Prüfen, dass der zuletzt sichtbare Wert einem der geschriebenen Werte entspricht **und** dass der persistierte Snapshot mit dem In-Memory-Snapshot übereinstimmt (`(await ctx.StateStore.ReadAsync()).Should().Be(ctx.StatusService.GetSnapshot())`) — das ist die Eigenschaft, die durch die Sperre tatsächlich garantiert wird.

### AutoUpdateEventsTests.cs (AutoUpdateEventsTests)

- **Unzureichende Testabdeckung** — Von den vier abbrechbaren Raise-Methoden ist nur `RaiseBeforeCheckSource` getestet. `RaiseBeforeDownload`, `RaiseBeforeInstall` und `RaiseBeforeStartUpdateScript` werden ebenso wenig direkt geprüft wie die dokumentierte Zusicherung aus `RaiseErrorOccured`, dass eine werfende `ErrorOccured`-Subscription nicht weiter eskaliert (Zeilen 160–163 der Implementierung).

  Empfehlung: Je einen Test für die drei ungetesteten Raise-Methoden ergänzen (Übergabe der Event-Args-Nutzdaten `SourceUri`/`PackageFile`/`ScriptFile` und Cancel-Verhalten) sowie einen Test `Raise_WhenErrorSubscriberThrows_DoesNotPropagate`.

### AutoUpdateCommandServiceTests.cs (AutoUpdateCommandServiceTests)

- **Unzureichende Testabdeckung / irreführender Testname** — `Commands_DelegateToOrchestrator` prüft trotz des Plurals „Commands“ ausschließlich `CheckAsync`. Für `DownloadAsync` und `InstallAsync` existiert kein Test.

  Empfehlung: Den Test auf `Check_DelegatesToOrchestrator` umbenennen und je einen Test für `DownloadAsync` und `InstallAsync` ergänzen. Entfällt `AutoUpdateCommandService` gemäß dem Middle-Man-Befund, entfällt die Testklasse ersatzlos.

### Testprojekte allgemein (temporäre Verzeichnisse)

- **Doppelter Code** — Das Muster `var dir = Directory.CreateTempSubdirectory(); try { … } finally { dir.Delete(recursive: true); }` wiederholt sich in `AutoUpdateBuilderTests` (1×), `UseAutoUpdateRegistrationTests` (3×), `FileSystemAutoUpdatePackageStoreTests` (3×), `FileSystemAutoUpdateStateStoreTests` (1×), `AutoUpdatePackageValidatorTests` (3×), `AutoUpdateScriptGeneratorTests` (3×), `AutoUpdateGithubSourceTests` (2×), `AutoUpdateLocalFolderSourceTests` (2×) und `UpdateSettingsStoreTests` (4×) — insgesamt 22-mal.

  Empfehlung: Eine `sealed class TempDirectory : IDisposable` in `TestSupport/` bereitstellen (Property `FullName`, `Dispose` löscht best-effort rekursiv) und die Blöcke durch `using var dir = new TempDirectory();` ersetzen. `AutoUpdateTestContext` kann diese Klasse intern ebenfalls nutzen.

## Geprüfte Dateien

- `SoftwareSchmiede.AutoUpdate/AutoUpdateBuilder.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCancelEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCheckResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCheckerService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCommandService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateDownloadResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateErrorEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateEvents.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateHostBuilderExtensions.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstallResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstallationTarget.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstaller.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateLocalFolderSource.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOptions.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOptionsValidator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOrchestrator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOutcome.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePackageDescriptor.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePackageValidator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePlatformResolver.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateReleaseInfo.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateResult.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateSchedulerService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateServiceResolver.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateState.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateStatusService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateStatusSnapshot.cs`
- `SoftwareSchmiede.AutoUpdate/BeforeDownloadEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/BeforeInstallEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/BeforeStartUpdateScriptEventArgs.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateHostTerminator.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateProcessRunner.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateServiceProbe.cs`
- `SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdatePackageStore.cs`
- `SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdateStateStore.cs`
- `SoftwareSchmiede.AutoUpdate/HostAutoUpdateEnvironment.cs`
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
- `SoftwareSchmiede.AutoUpdate/InstalledReleaseInfo.cs`
- `SoftwareSchmiede.AutoUpdate/JsonFileStore.cs`
- `SoftwareSchmiede.AutoUpdate/ReleaseMetadataInstalledVersionProvider.cs`
- `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj`
- `SoftwareSchmiede.AutoUpdate/SourceCheckOptions.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckTimeRange.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckWindowEvaluator.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateBuilderTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCheckerServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCommandServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateEventsTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateGithubSourceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateLocalFolderSourceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOptionsValidationTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorCheckTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorDownloadTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorEventTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorInstallTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePackageValidatorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePlatformResolverTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateSchedulerServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateScriptGeneratorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateServiceResolverTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateStatusServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/FileSystemAutoUpdatePackageStoreTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/FileSystemAutoUpdateStateStoreTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.csproj`
- `SoftwareSchmiede.AutoUpdate.Tests/SourceCheckWindowEvaluatorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AutoUpdateTestContext.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/FakeAutoUpdateSource.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/TestAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/UseAutoUpdateRegistrationTests.cs`
- `FinanceManager.Web/FinanceManager.Web.csproj`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`
- `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`
- `FinanceManager.Web/Services/Updates/UpdateContracts.cs`
- `FinanceManager.Web/Services/Updates/UpdateOptions.cs`
- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`
- `FinanceManager.Tests/Updates/InstalledReleaseMetadataProviderTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
- `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
- `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`
