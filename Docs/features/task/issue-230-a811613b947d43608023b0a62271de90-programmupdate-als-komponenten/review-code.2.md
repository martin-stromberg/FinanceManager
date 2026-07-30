# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### SoftwareSchmiede.AutoUpdate/AutoUpdateOrchestrator.cs (AutoUpdateOrchestrator)

- **Fehlerbehandlung** — `InstallCoreAsync` (Zeile 306) und `CheckCoreAsync`/`DownloadCoreAsync` fangen `catch (Exception ex)` und leiten alles nach `FailAsync`. Dadurch wird auch eine `OperationCanceledException` aus dem übergebenen `ct` als `AutoUpdateState.Failed` mit `LastError` persistiert, obwohl der Vorgang lediglich abgebrochen wurde. Die Bibliothek kennt mit `AutoUpdateOutcome.Canceled` bereits ein passendes Ergebnis.

  Empfehlung: Vor dem generischen `catch (Exception ex)` jeweils ein `catch (OperationCanceledException) when (ct.IsCancellationRequested)` ergänzen, das `AutoUpdateOutcome.Canceled` zurückgibt und den vorherigen Status nicht auf `Failed` setzt.

- **Doppelter Code** — Die Prüfung `var source = _options.Source ?? throw new InvalidOperationException("No update source is configured.");` steht identisch in `CheckCoreAsync` (Zeile 179) und `DownloadCoreAsync` (Zeile 233).

  Empfehlung: In eine private Methode `private IAutoUpdateSource RequireSource()` auslagern und an beiden Stellen aufrufen.

- **Fehlende Kapselung** — Das Muster `await _semaphore.WaitAsync(ct); try { await _statusService.EnsureLoadedAsync(ct); … } finally { _semaphore.Release(); }` wiederholt sich wortgleich in `RunUpdateAsync`, `CheckForUpdateAsync`, `DownloadAsync`, `InstallAsync` und `GetStatusAsync` (Zeilen 58–159).

  Empfehlung: Eine private Hilfsmethode `private async Task<T> RunSerializedAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)` einführen, die Semaphore und `EnsureLoadedAsync` kapselt, und alle fünf öffentlichen Methoden darauf umstellen.

- **God-Klasse** — Neben der Koordination von Check/Download/Install enthält die Klasse mit `ReconcileAfterRestartAsync` (Zeilen 313–350) eine fachlich eigenständige zweite Verantwortlichkeit: die Nachbereitung eines über den Prozessneustart hinweg laufenden Updates (Lock-Auswertung, Versionsvergleich, Erfolgs-/Fehlerentscheidung).

  Empfehlung: Die Reconcile-Logik in eine eigene Klasse `AutoUpdateRestartReconciler` mit den Abhängigkeiten `IAutoUpdatePackageStore`, `IInstalledVersionProvider` und `AutoUpdateStatusService` verschieben; `GetStatusAsync` delegiert nur noch dorthin.

- **Nicht freigegebene Ressource** — Das Feld `_semaphore` (Zeile 21) wird nie freigegeben, die Klasse implementiert kein `IDisposable`.

  Empfehlung: `IDisposable` implementieren und `_semaphore.Dispose()` aufrufen, damit der DI-Container die Instanz beim Shutdown korrekt entsorgen kann.

### SoftwareSchmiede.AutoUpdate/AutoUpdateEvents.cs (AutoUpdateEvents)

- **Fehlerbehandlung** — `RaiseCancelable` (Zeilen 167–193) übergibt allen Subscribern **dieselbe** `TArgs`-Instanz. Sobald ein Subscriber `Cancel = true` setzt, sehen alle nachfolgenden Subscriber ein bereits abgebrochenes Args-Objekt und können den Abbruch versehentlich zurücknehmen (`args.Cancel = false`), was die lokale Variable `canceled` dann nicht mehr korrigiert.

  Empfehlung: Pro Subscriber eine frische Args-Instanz erzeugen (Factory-Delegate `Func<TArgs>` statt fertiger Instanz übergeben) und das Ergebnis über alle Subscriber hinweg mit `canceled |= args.Cancel` aufsammeln.

- **Fehlerbehandlung** — Leerer `catch`-Block in `RaiseErrorOccured` (Zeilen 160–164): Ausnahmen aus `ErrorOccured`-Subscribern werden vollständig verschluckt, ohne jede Diagnosemöglichkeit.

  Empfehlung: Die Klasse einen optionalen `ILogger<AutoUpdateEvents>` entgegennehmen lassen (Default `NullLogger`, analog zu `FileSystemAutoUpdateStateStore`) und im catch ein `LogWarning` absetzen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateHostBuilderExtensions.cs (AutoUpdateHostBuilderExtensions)

- **Toter Code** — `UseAutoUpdate` klemmt `options.HealthTimeoutSeconds` in Zeile 39 per `Math.Clamp` auf den gültigen Bereich, bevor in Zeile 50 der Validator läuft. Die Regel in `AutoUpdateOptionsValidator` Zeilen 43–46 kann dadurch im Startup-Pfad nie auslösen; gleichzeitig wird eine Fehlkonfiguration stillschweigend korrigiert statt gemeldet.

  Empfehlung: Für einen einheitlichen Ansatz entscheiden — entweder den `Math.Clamp`-Aufruf in Zeile 39 entfernen (Fehlkonfiguration schlägt sichtbar fehl) oder die Validierungsregel im Validator streichen.

- **Toter Code** — `builder.Services.AddSingleton<IValidateOptions<AutoUpdateOptions>>(validator)` (Zeile 57) hat keine Wirkung: `AutoUpdateOptions` wird über `AddSingleton(options)` als konkrete Instanz registriert, nicht über `AddOptions<AutoUpdateOptions>()`. Das Options-Framework ruft den registrierten Validator daher nie auf; die Validierung findet ausschließlich manuell in Zeile 50 statt.

  Empfehlung: Die Registrierung in Zeile 57 entfernen, da die Validierung bereits explizit vor der Registrierung ausgeführt wird.

- **Hardcodierte Werte** — Der Unterordnername `"source"` für die Fallback-Quelle (Zeile 46) und der Default-Sektionsname `"AutoUpdate"` (`AutoUpdateBuilder.cs` Zeile 17) stehen als String-Literale im Code.

  Empfehlung: Beide als `private const string DefaultSourceDirectoryName = "source"` bzw. `public const string DefaultConfigurationSectionName = "AutoUpdate"` definieren; letzteres öffentlich, damit konsumierende Anwendungen darauf verweisen können.

- **God-Methode** — `UseAutoUpdate` (Zeilen 30–86) erledigt sechs konzeptuell getrennte Aufgaben nacheinander: Builder ausführen, Konfiguration binden, explizite Werte reapplizieren, Default-Quelle ableiten, validieren, Services registrieren.

  Empfehlung: Die Ermittlung der Options (Binding, Reapply, Default-Source, Validierung) in eine private Methode `private static AutoUpdateOptions BuildOptions(IHostApplicationBuilder builder, AutoUpdateBuilder autoUpdateBuilder)` auslagern, sodass `UseAutoUpdate` nur noch aus Options-Aufbau und Registrierungsblock besteht.

### SoftwareSchmiede.AutoUpdate/AutoUpdateBuilder.cs (AutoUpdateBuilder)

- **Temporäres Feld** — Die sechs `Explicit*`-Properties (`ExplicitDownloadPath`, `ExplicitEnableAutomaticDownload`, `ExplicitEnableAutomaticInstallation`, `ExplicitSourceCheckInterval`, `ExplicitSourceCheckTimeRanges`, `ExplicitUpdateUnitName`) sind reine Schattenbuchhaltung, die nur existiert, weil das Konfigurations-Binding *nach* dem `configure`-Delegate läuft und die Werte in `ReapplyExplicitValues` erneut gesetzt werden müssen. Jede neue Fluent-Option erzwingt eine weitere Property plus einen weiteren `if`-Block.

  Empfehlung: Reihenfolge umkehren — zuerst `builder.Configuration.GetSection(...).Bind(options)` ausführen, danach das `configure`-Delegate auf dieselbe Options-Instanz anwenden. Damit gewinnt Code automatisch gegen Konfiguration und alle `Explicit*`-Properties sowie `ReapplyExplicitValues` entfallen. Der Sektionsname muss dafür vorab separat ermittelt werden (z. B. über einen eigenen Parameter von `UseAutoUpdate` statt über `BindConfiguration`).

- **Einheitlichkeit** — `ExplicitUpdateUnitName` ist als einzige Property unterhalb der Methoden am Dateiende deklariert (Zeile 178), während alle übrigen Properties oben gruppiert sind (Zeilen 12–44).

  Empfehlung: Die Property zu den übrigen `Explicit*`-Properties nach oben verschieben.

### SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs (AutoUpdateScriptGenerator)

- **Fehlende Validierung** — In `GenerateLinuxAsync` wird `target.ServiceName` in den Zeilen 107 und 123 (`log_msg "Stoppe Dienst {{target.ServiceName}}..."`) unescaped in einen doppelt gequoteten Shell-String interpoliert, während derselbe Wert in den unmittelbar benachbarten Zeilen 108 und 124 korrekt über `Sh()` maskiert wird. In doppelten Anführungszeichen findet Shell-Expansion für `$`, Backticks und `\` statt.

  Empfehlung: Auch in den `log_msg`-Zeilen `{{Sh(target.ServiceName)}}` verwenden bzw. den Dienstnamen als bereits maskierte Shell-Variable am Skriptanfang definieren und in den Meldungen darauf verweisen.

- **Einheitlichkeit** — Sprachmischung: Die Log-Meldungen des generierten Linux-Skripts sind deutsch („Update gestartet.", „Stoppe Dienst", „Bereinige Staging-Verzeichnis…", Zeilen 103–126), während sämtliche Exception-Texte, XML-Dokumentation und das Windows-Skript derselben Klasse englisch sind. Für eine als NuGet-Paket vorgesehene Bibliothek ist das inkonsistent.

  Empfehlung: Die Log-Meldungen im Linux-Skript auf Englisch umstellen, passend zur übrigen Bibliothek.

### SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs (AutoUpdateGithubSource)

- **Nicht freigegebene Ressource** — Die Factory `Create` (Zeilen 50–55) erzeugt einen `HttpClient`, den die Instanz besitzt, aber nie freigibt; die Klasse implementiert kein `IDisposable`. Dieser Pfad wird von `AutoUpdateBuilder.UseGithubSource` und damit von `ProgramExtensions` produktiv genutzt.

  Empfehlung: `AutoUpdateGithubSource` `IDisposable` implementieren lassen, ein Feld `_ownsHttpClient` setzen (nur bei `Create` true) und den Client in `Dispose()` freigeben.

- **Fehlende Validierung** — Beim Aufbau der Manifest-URL (Zeile 60) wird nur `ManifestAssetName` über `Uri.EscapeDataString` maskiert, `_repositoryOwner` und `_repositoryName` werden dagegen ungeprüft interpoliert, obwohl beide aus Konfiguration stammen.

  Empfehlung: Beide Bestandteile ebenfalls über `Uri.EscapeDataString` maskieren oder im Konstruktor auf ein zulässiges Zeichenmuster (`^[A-Za-z0-9._-]+$`) prüfen.

- **Hardcodierte Werte** — Timeout `TimeSpan.FromMinutes(5)` und User-Agent-String `"SoftwareSchmiede.AutoUpdate/1.0"` (Zeilen 52–53); die dort fest verdrahtete Version „1.0" weicht zudem von der Paketversion `0.1.0` in der csproj ab.

  Empfehlung: Timeout als `private static readonly TimeSpan DefaultHttpTimeout` konstant machen und die User-Agent-Version aus `typeof(AutoUpdateGithubSource).Assembly.GetName().Version` ableiten.

### SoftwareSchmiede.AutoUpdate/AutoUpdatePackageValidator.cs (AutoUpdatePackageValidator)

- **Toter Code** — `ValidateReleaseAsync` (Zeilen 34–64, rund 30 Zeilen Logik inklusive der privaten Hilfsmethode `ValidatePackage`) wird nirgends aufgerufen: weder von `AutoUpdateOrchestrator`, `AutoUpdateInstaller`, einer der beiden `IAutoUpdateSource`-Implementierungen noch von einem Test. Die Methode ist ausschließlich auf `IAutoUpdatePackageValidator` deklariert und implementiert.

  Empfehlung: Entweder die Methode in `AutoUpdateLocalFolderSource.CheckAsync` und `AutoUpdateGithubSource.CheckAsync` tatsächlich auf das gelesene `AutoUpdateReleaseInfo` anwenden (dann wird auch das Manifest validiert, nicht nur das heruntergeladene Paket), oder Methode und Interface-Mitglied ersatzlos entfernen.

- **Namenskonvention** — `ValidateReleaseAsync` gibt zwar `Task` zurück, arbeitet aber vollständig synchron und wirft synchron statt eine faulted Task zurückzugeben; das `Async`-Suffix ist damit irreführend.

  Empfehlung: Bei Beibehaltung der Methode die Signatur auf `void ValidateRelease(AutoUpdateReleaseInfo release, string currentPlatform)` ändern.

### SoftwareSchmiede.AutoUpdate/IAutoUpdateInstaller.cs, AutoUpdateInstaller.cs (AutoUpdateInstaller)

- **Namenskonvention** — `void StartAsync(string scriptPath)` (`AutoUpdateInstaller.cs` Zeile 47) trägt das `Async`-Suffix, gibt aber `void` zurück und ist vollständig synchron. Der Aufruf in `AutoUpdateOrchestrator.cs` Zeile 296 (`_installer.StartAsync(scriptPath);` ohne `await`) sieht dadurch wie ein vergessenes `await` bzw. ein Fire-and-Forget aus.

  Empfehlung: Die Methode auf `void Start(string scriptPath)` umbenennen — im Interface, in der Implementierung und an der Aufrufstelle.

### SoftwareSchmiede.AutoUpdate/ProcessOutputReader.cs (ProcessOutputReader)

- **Fehlerbehandlung** — `Read` (Zeilen 30–41) liest zuerst `StandardOutput.ReadToEnd()` vollständig und danach `StandardError.ReadToEnd()`. Füllt der Kindprozess zwischenzeitlich den stderr-Puffer, blockieren beide Seiten dauerhaft (klassischer Redirect-Deadlock). Der Parameter `timeoutMs` greift dagegen nicht, weil `WaitForExit` erst nach den blockierenden Lesevorgängen aufgerufen wird — der Timeout ist damit faktisch wirkungslos.

  Empfehlung: Auf asynchrones Auslesen umstellen (`process.StandardOutput.ReadToEndAsync()` und `StandardError.ReadToEndAsync()` gemeinsam über `Task.WhenAll` starten) und `WaitForExit(timeoutMs)` davor bzw. parallel ausführen.

- **Fehlerbehandlung** — Der Rückgabewert von `process.WaitForExit(timeoutMs)` (Zeile 33) wird verworfen. Bei Timeout läuft der Prozess weiter und der anschließende Zugriff auf `process.ExitCode` (Zeile 35) wirft eine nichtssagende `InvalidOperationException`.

  Empfehlung: Rückgabewert prüfen und bei `false` eine `TimeoutException` mit Kommandoname und Timeout werfen.

### SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateProcessRunner.cs (DefaultAutoUpdateProcessRunner)

- **Fehlende Validierung** — In `StartScript` (Zeile 57) wird `scriptPath` ohne Quoting in die Argumentzeile `--unit={...} --service-type=exec /bin/bash {scriptPath}` eingesetzt. Da der Pfad über `AutoUpdateOptions.DownloadPath` bzw. `UpdateSettingsDto.WorkingDirectory` aus der Setup-UI konfigurierbar ist, bricht jedes Leerzeichen im Pfad den Aufruf. Die Windows-Variante (Zeile 56) quotet zwar mit `"`, maskiert aber keine im Pfad enthaltenen Anführungszeichen.

  Empfehlung: Statt eines Argument-Strings die `ProcessStartInfo.ArgumentList` befüllen; damit übernimmt .NET das plattformkonforme Quoting.

- **Hardcodierte Werte** — Timeout `timeoutMs: 10000` an zwei Stellen (Zeilen 43, 82), `timeoutMs: 3000` in `DefaultAutoUpdateServiceProbe.cs` (Zeilen 34, 79).

  Empfehlung: Je Klasse eine `private const int SystemctlTimeoutMs = 10000;` bzw. `ServiceProbeTimeoutMs = 3000;` definieren und verwenden.

- **Namenskonvention** — `StartPrepareEnvironment` beschreibt nicht, was die Methode tut: sie startet nichts, sondern prüft den systemd-Unit-Zustand, setzt bei Bedarf einen fehlgeschlagenen Unit zurück und wirft, wenn bereits ein Update läuft.

  Empfehlung: In `EnsureUpdateUnitAvailable` umbenennen (Interface `IAutoUpdateProcessRunner`, Implementierung und die Aufrufstelle in `AutoUpdateInstaller.cs` Zeile 49).

### SoftwareSchmiede.AutoUpdate/AutoUpdateSchedulerService.cs (AutoUpdateSchedulerService)

- **Fehlerbehandlung** — Das `await Task.Delay(TimeSpan.FromMinutes(1), _timeProvider, stoppingToken)` steht in Zeile 66 **außerhalb** des `try`-Blocks. Beim Herunterfahren wirft dieser Delay eine `TaskCanceledException`, die weder vom `catch (OperationCanceledException)` (Zeile 57) noch vom generischen `catch` (Zeile 61) erfasst wird und `ExecuteAsync` als faulted Task beendet. In `AutoUpdateCheckerService.cs` liegt der entsprechende Delay korrekterweise innerhalb des try-Blocks (Zeile 52) — die beiden Services verhalten sich also unterschiedlich.

  Empfehlung: Den `Task.Delay`-Aufruf in `AutoUpdateSchedulerService` in den `try`-Block verschieben, analog zu `AutoUpdateCheckerService`.

- **Hardcodierte Werte** — Poll-Intervall `TimeSpan.FromMinutes(1)` (Zeile 66) sowie in `AutoUpdateCheckerService.cs` der Fehler-Backoff `TimeSpan.FromMinutes(5)` (Zeile 61) und die Mindestintervall-Untergrenze `Math.Max(1, …)` (Zeile 52).

  Empfehlung: Als benannte `private static readonly TimeSpan PollInterval` / `ErrorBackoff` bzw. `private const int MinimumIntervalMinutes = 1` definieren.

- **Kopplung** — `ShouldInstall` (Zeile 77) ist `public`, nimmt aber gleichzeitig alle Entscheidungsdaten als Parameter entgegen *und* liest die privaten Felder `_lastAttemptedDate`/`_lastAttemptedTime`. Die Methode ist offensichtlich nur für den Test öffentlich, ist aber wegen des internen Zustands nicht isoliert aufrufbar.

  Empfehlung: Die Tageslogik in eine eigene, zustandslose Klasse `ScheduledInstallEvaluator` mit der Signatur `bool ShouldInstall(TimeOnly? scheduledTime, AutoUpdateStatusSnapshot snapshot, DateTimeOffset now, DateOnly? lastAttemptedDate, TimeOnly? lastAttemptedTime)` auslagern (analog zum vorhandenen `SourceCheckWindowEvaluator`) und `ShouldInstall` am Service auf `private` setzen.

- **Doppelter Code** — `AutoUpdateCheckerService.ExecuteAsync` und `AutoUpdateSchedulerService.ExecuteAsync` besitzen dasselbe Schleifengerüst (while über `stoppingToken`, try, `catch (OperationCanceledException) when (…) break`, `catch (Exception) LogError`, Delay).

  Empfehlung: Eine gemeinsame abstrakte Basisklasse `AutoUpdatePeriodicService : BackgroundService` einführen, die das Gerüst inklusive Fehlerbehandlung bereitstellt, und beide Services nur noch `Task RunIterationAsync(CancellationToken)` sowie `TimeSpan GetDelay()` implementieren lassen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateStatusSnapshot.cs (AutoUpdateStatusSnapshot)

- **Toter Code** — Die Record-Komponente `InstalledVersion` (Zeile 20) wird ausschließlich in `AutoUpdateStatusSnapshot.Idle(installed.Version)` gesetzt und danach nirgends im Produktivcode gelesen; auch `UpdateOrchestratorAdapter.MapToStatusDtoAsync` verwendet stattdessen `_installedProvider.GetAsync`. Gleiches gilt für `LastInstallResult` (Zeile 25): geschrieben in `AutoUpdateOrchestrator.cs` Zeile 290, an keiner Stelle gelesen. Beide Felder werden dauerhaft mitserialisiert und veralten still.

  Empfehlung: Entweder beide Komponenten aus dem Record entfernen, oder `InstalledVersion` bei jedem `EnsureLoadedAsync`/Statuswechsel konsistent nachführen und über den Adapter statt des separaten Providers ausliefern.

- **Dokumentation** — Der Record-Kommentar enthält ein `<returns>`-Tag (Zeile 17), das für einen Typ nicht gültig ist.

  Empfehlung: Das `<returns>`-Tag entfernen.

### SoftwareSchmiede.AutoUpdate/AutoUpdatePlatformResolver.cs (AutoUpdatePlatformResolver)

- **Kopplung** — `CurrentPlatform` (Zeilen 54–59) greift im Fallback-Zweig direkt auf das statische `RuntimeInformation.OSDescription` zu, während `CurrentRuntimeIdentifier` (Zeile 49) konsistent den injizierten `_runtimeIdentifier` nutzt. Der eigens für Tests eingeführte Konstruktor-Seam wird damit in einem der beiden Pfade umgangen.

  Empfehlung: Ein zusätzliches Feld für die Plattformbezeichnung injizieren oder den Fallback ebenfalls aus `_runtimeIdentifier` ableiten, damit beide Properties denselben Seam verwenden.

### SoftwareSchmiede.AutoUpdate/AutoUpdateServiceResolver.cs (AutoUpdateServiceResolver)

- **Doppelter Code** — `ResolveWindows` (Zeilen 44–69) und `ResolveLinux` (Zeilen 71–90) sind bis auf den Plattformnamen, den Probe-Aufruf und den zusätzlichen Executable-Zweig strukturgleich; insbesondere die drei Zweige „genau ein Treffer / mehrere Treffer / kein Treffer" inklusive Meldungstexten sind dupliziert.

  Empfehlung: Eine private Methode `private AutoUpdateInstallationTarget ResolveFromProbe(string platform, IReadOnlyList<string> detected)` einführen, die die Auswertung der Kandidatenliste inklusive Fehlermeldungen übernimmt; `ResolveWindows`/`ResolveLinux` behalten nur ihre plattformspezifischen Vorprüfungen.

- **Speculative Generality** — `ValidateServiceName(string value, string label)` (Zeile 115) besitzt einen `label`-Parameter, der an beiden Aufrufstellen (Zeilen 48, 75) mit demselben Literal `"Service name"` versorgt wird.

  Empfehlung: Den Parameter entfernen und die Meldung fest formulieren.

### SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdatePackageStore.cs (FileSystemAutoUpdatePackageStore)

- **Hardcodierte Werte** — Der Fallback `"updates"` in `RootDirectory` (Zeile 29) dupliziert den Standardwert von `AutoUpdateOptions.DownloadPath` (`AutoUpdateOptions.cs` Zeile 32); derselbe Literal steht ein drittes Mal in `UpdateSettingsStore.NormalizeWorkingDirectory` (Zeile 179).

  Empfehlung: In `AutoUpdateOptions` eine `public const string DefaultDownloadPath = "updates";` einführen und an allen drei Stellen verwenden.

- **Fehlerbehandlung** — `DeleteLockAsync` (Zeilen 111–120) ruft `File.Delete` ohne jede Absicherung auf, während das Gegenstück `TryCreateLockAsync` `IOException` gezielt behandelt. Der Aufruf im Fehlerpfad von `AutoUpdateOrchestrator.InstallCoreAsync` (Zeile 308) kann dadurch die ursprüngliche Ausnahme mit einer Folgeausnahme überdecken.

  Empfehlung: `File.Delete` in `try`/`catch (IOException)` kapseln und im Fehlerfall `false` zurückgeben, passend zum `Task<bool>`-Rückgabetyp.

### SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj

- **Fehlende Konfiguration** — Das Projekt enthält eine `README.md` (123 Zeilen), setzt aber weder `PackageReadmeFile` noch nimmt es die Datei als `<None Include="README.md" Pack="true" PackagePath="\" />` auf. Bei einem als NuGet-Paket vorgesehenen Projekt (Ziel der Anforderung) fehlt die README damit im Paket.

  Empfehlung: `<PackageReadmeFile>README.md</PackageReadmeFile>` in die PropertyGroup und das zugehörige `None`-Item in eine ItemGroup aufnehmen.

### FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **Long Parameter List** — Der Konstruktor (Zeilen 39–49) nimmt zehn Abhängigkeiten entgegen. Das ist ein direktes Symptom davon, dass die Klasse drei Verantwortlichkeiten bündelt: DTO-Mapping, Durchreichen der Settings-Operationen und die Lock-Reset-Fachlogik.

  Empfehlung: Das Mapping (`MapToStatusDtoAsync`, `MapState`) in eine eigene Klasse `UpdateStatusDtoMapper` verschieben und die Lock-Reset-Logik gemäß dem folgenden Befund in die Bibliothek verlagern; der Adapter benötigt dann noch etwa vier Abhängigkeiten.

- **Feature Envy / Inappropriate Intimacy** — `ResetLockAsync` (Zeilen 111–132) implementiert eine Kernregel des Update-Subsystems (ab wann ein Lock als „stale" gilt) im Host-Adapter, greift dafür ausschließlich auf Bibliotheks-Bausteine zu (`IAutoUpdatePackageStore`, `AutoUpdateOptions.HealthTimeoutSeconds`, `TimeProvider`) und manipuliert anschließend den Bibliotheks-Status direkt über die **konkrete** Klasse `AutoUpdateStatusService`, obwohl mit `IAutoUpdateStatusProvider` bereits ein Interface injiziert ist (Zeile 42 vs. Zeile 47).

  Empfehlung: Eine Methode `Task<bool> ResetStaleLockAsync(string? reason, CancellationToken ct)` auf `IAutoUpdateOrchestrator` ergänzen und die gesamte Logik dorthin verschieben. Der Adapter ruft sie nur noch auf; die Abhängigkeiten `IAutoUpdatePackageStore`, `AutoUpdateStatusService`, `AutoUpdateOptions` und `TimeProvider` entfallen aus dem Konstruktor.

- **Switch Statement** — `MapState` (Zeilen 169–178) mappt die Zustände `Idle`, `Success` und `Disabled` über den Default-Arm `_ =>` allesamt auf `UpdateStatusKind.NoUpdate`. Ein deaktiviertes Auto-Update ist in der Setup-UI dadurch nicht von „kein Update verfügbar" unterscheidbar, und ein neu hinzugefügter `AutoUpdateState` fällt stillschweigend ebenfalls auf `NoUpdate`.

  Empfehlung: Alle Enum-Werte explizit auflisten und den Default-Arm eine `ArgumentOutOfRangeException` werfen lassen, damit neue Zustände beim Erweitern auffallen. Für `Disabled` prüfen, ob ein eigener `UpdateStatusKind` in der UI sinnvoll ist.

### FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs (UpdateSettingsStore)

- **Long Parameter List / Data Clump** — `Build` (Zeilen 101–122) hat zehn Parameter, die stets als geschlossene Gruppe auftreten und an drei Stellen (`Defaults` Zeilen 75–85, `Normalize` Zeilen 89–99, Legacy-Pfad Zeilen 143–153) einzeln aufgezählt werden.

  Empfehlung: `Build` auf `private static UpdateSettingsDto Normalize(UpdateSettingsDto raw)` umstellen und den Legacy-Record sowie den `UpdateSettingsUpdateRequest` zuvor in ein `UpdateSettingsDto` überführen; die drei Aufrufstellen reduzieren sich damit auf je eine Zeile.

- **Hardcodierte Werte / Doppelter Code** — In `Build` sind die Grenzen `Math.Clamp(healthTimeoutSeconds, 10, 600)` (Zeile 122) fest verdrahtet, obwohl die Bibliothek mit `AutoUpdateOptions.MinHealthTimeoutSeconds` und `MaxHealthTimeoutSeconds` bereits genau diese Konstanten öffentlich anbietet. Zusätzlich duplizieren die Fallbacks `"martin-stromberg"`, `"FinanceManager"` (Zeilen 115–116) und `"update.json"` (Zeile 117) exakt die Defaults aus `UpdateOptions.cs` (Zeilen `RepositoryOwner`, `RepositoryName`, `ManifestAssetName`).

  Empfehlung: Die Clamp-Grenzen durch `AutoUpdateOptions.MinHealthTimeoutSeconds`/`MaxHealthTimeoutSeconds` ersetzen und die Repository-Fallbacks aus dem bereits injizierten `_webOptions` beziehen statt sie erneut zu literalisieren.

- **Doppelter Code** — `WriteAtomicAsync` (Zeilen 159–169) ist eine wortgleiche Kopie von `JsonFileStore.WriteAtomicAsync` (`SoftwareSchmiede.AutoUpdate/JsonFileStore.cs` Zeilen 26–36); dasselbe Temp-Datei-Muster steht ein drittes Mal in `AutoUpdateGithubSource.DownloadAsync` (Zeilen 97–137). Die Wiederverwendung scheitert nur daran, dass `JsonFileStore` `internal` ist.

  Empfehlung: `JsonFileStore` in der Bibliothek auf `public` heben (z. B. als `AutoUpdateJsonFileStore`) und aus `UpdateSettingsStore` darauf zugreifen; alternativ die Schreiblogik in `UpdateSettingsStore` beibehalten, dann aber nicht zusätzlich in der Bibliothek pflegen.

- **Namenskonvention** — `ApplyToOptionsAsync` (Zeile 61) trägt das `Async`-Suffix, führt keine asynchrone Arbeit aus und ignoriert den `ct`-Parameter vollständig.

  Empfehlung: Interface- und Implementierungsmethode auf `void ApplyToOptions(UpdateSettingsDto settings)` ändern und die beiden Aufrufstellen in `UpdateOrchestratorAdapter` (Zeilen 78, 86) anpassen.

### FinanceManager.Web/Services/Updates/UpdateOptions.cs (UpdateOptions)

- **Primitive Obsession** — `SourceType` ist ein `string` mit den fachlich geschlossenen Werten `"Github"` und `"LocalFolder"`; die Auswertung erfolgt in `ProgramExtensions.cs` Zeile 171 über einen `string.Equals(..., OrdinalIgnoreCase)`-Vergleich gegen das Literal `"LocalFolder"`, jeder Tippfehler fällt still auf den GitHub-Zweig zurück.

  Empfehlung: Ein `public enum UpdateSourceType { Github, LocalFolder }` einführen und die Property darauf umstellen; der Configuration-Binder konvertiert Enum-Werte automatisch aus Strings und meldet ungültige Werte.

### FinanceManager.Web/ProgramExtensions.cs

- **Doppelter Code** — Die Konfigurationssektion `Updates` wird zweimal gebunden: einmal manuell nach `UpdateOptions` (Zeile 163) und einmal über `cfg.BindConfiguration(UpdateOptions.SectionName)` nach `AutoUpdateOptions` (Zeile 166). Überlappende Schlüssel wie `CheckIntervalMinutes`/`SourceCheck:Interval` und `WorkingDirectory`/`DownloadPath` existieren dadurch in zwei Objekten mit unterschiedlichen Namen und Defaults (beide 360 bzw. beide `"updates"`).

  Empfehlung: In der XML-Dokumentation von `UpdateOptions` festhalten, welche Schlüssel welchem Objekt zugeordnet sind (teilweise vorhanden), und die redundanten Felder `CheckIntervalMinutes` und `WorkingDirectory` aus `UpdateOptions` entfernen — sie werden nur an `cfg.WithSourceCheck` bzw. `cfg.EnableAutomaticDownload` weitergereicht und können direkt aus den gebundenen `AutoUpdateOptions` gelesen werden.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCheckerServiceTests.cs, AutoUpdateSchedulerServiceTests.cs

- **Testqualität** — Beide Testklassen kombinieren den `FakeTimeProvider` mit echten Wanduhr-Wartezeiten: `await Task.Delay(100)` bzw. `Task.Delay(50)` in `AutoUpdateSchedulerServiceTests.cs` Zeilen 27, 47, 69 und `AutoUpdateCheckerServiceTests.cs` Zeilen 70, 91, 94. `Execute_RespectsConfiguredInterval` (Zeilen 79–102) prüft danach exakte Aufrufzahlen (`Should().Be(1)`, `Should().Be(2)`), die unter Last kippen können.

  Empfehlung: Die festen `Task.Delay`-Aufrufe durch das bereits vorhandene `AsyncTestWait.WaitForAsync` mit der jeweils erwarteten Bedingung ersetzen und die exakten Zählerprüfungen auf `BeGreaterThanOrEqualTo`/`Be(0)` im jeweils sinnvollen Zeitfenster umstellen.

### SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AsyncTestWait.cs (AsyncTestWait)

- **Testqualität** — `WaitForAsync` (Zeilen 17–24) kehrt nach Ablauf des Timeouts kommentarlos zurück, ohne den Test scheitern zu lassen. Ein tatsächlich hängender Service führt dadurch nicht zu einer aussagekräftigen Timeout-Meldung, sondern erst zu einem schwer deutbaren Folgefehler in der nachgelagerten Assertion.

  Empfehlung: Bei Ablauf des Timeouts eine `TimeoutException` mit der erwarteten Bedingung im Text werfen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateEventsTests.cs (AutoUpdateEventsTests)

- **Testqualität** — `Raise_WhenHandlerThrows_ReportsErrorAndContinues` (Zeilen 19–33) registriert nur einen einzigen `BeforeCheckSource`-Subscriber. Der im Namen zugesagte Teil „Continues" (weitere Subscriber werden trotz Ausnahme aufgerufen) wird damit gar nicht geprüft.

  Empfehlung: Einen zweiten Subscriber registrieren, der ein Flag setzt, und zusätzlich zusichern, dass dieses Flag nach dem Raise gesetzt ist — oder den Testnamen auf `ReportsError` kürzen.

### SoftwareSchmiede.AutoUpdate.Tests/UseAutoUpdateRegistrationTests.cs (UseAutoUpdateRegistrationTests)

- **Doppelter Code** — Das Muster `var dir = Directory.CreateTempSubdirectory(); try { … } finally { dir.Delete(recursive: true); }` wiederholt sich in sechs von sieben Tests (Zeilen 15/45, 64/77, 84/101, 108/126, 132/149, 157/169). Dasselbe Muster steht erneut in `AutoUpdatePackageValidatorTests.cs` (Zeilen 25/39 ff.).

  Empfehlung: Ein wiederverwendbares `TempDirectory : IDisposable` in `TestSupport` anlegen (analog zum bestehenden `AutoUpdateTestContext`) und die Tests auf `using var dir = new TempDirectory();` umstellen.

### FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs, UpdateOrchestratorAdapterTests_LockAndSchedule.cs

- **Doppelter Code** — Die private Hilfsmethode `CreateStatusService()` ist in beiden Dateien wortgleich vorhanden (`UpdateOrchestratorAdapterTests.cs` Zeilen 141–149, `UpdateOrchestratorAdapterTests_LockAndSchedule.cs` Zeilen 125–133). Zusätzlich wird der zehnargumentige `new UpdateOrchestratorAdapter(...)`-Aufruf sechsmal ausgeschrieben (Zeilen 27, 61, 93, 123 der ersten Datei sowie Zeilen 86, 112 der zweiten), obwohl die zweite Datei mit `CreateAdapter` bereits eine Factory besitzt, die im Test `ScheduleAsync_SavesScheduleAndAppliesToAutoUpdateOptions` dennoch nicht genutzt wird.

  Empfehlung: `CreateStatusService` und eine parametrisierbare `CreateAdapter`-Factory in eine gemeinsame `UpdateOrchestratorAdapterTestFactory` in `FinanceManager.Tests/Updates` auslagern und in beiden Testklassen verwenden.

### FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs (SetupUpdateGateway)

- **Einheitlichkeit** — Die Klasse mischt zwei Selektor-Strategien: die neu in `SetupUpdateTab.razor` ergänzten `data-testid`-Attribute (Zeile 79 ff.) einerseits, andererseits die sprachabhängige Textsuche `new Regex("Update|Aktualisierung", RegexOptions.IgnoreCase)` (Zeile 7) und den positionsabhängigen CSS-Selektor `".setup-update-tab input[type=checkbox]"` (Zeile 50), der beim Hinzufügen einer weiteren Checkbox das falsche Element trifft.

  Empfehlung: Für den Sektions-Toggle und die Enabled-Checkbox ebenfalls `data-testid`-Attribute in `SetupUpdateTab.razor` bzw. der Accordion-Komponente ergänzen und den Gateway durchgängig darauf umstellen.

## Geprüfte Dateien

Bibliothek (neu):
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOrchestrator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateEvents.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateBuilder.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateHostBuilderExtensions.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOptions.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateOptionsValidator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateLocalFolderSource.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePackageValidator.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateServiceResolver.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateInstaller.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCommandService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateStatusService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateCheckerService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateSchedulerService.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdatePlatformResolver.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateStatusSnapshot.cs`
- `SoftwareSchmiede.AutoUpdate/AutoUpdateState.cs`
- `SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdatePackageStore.cs`
- `SoftwareSchmiede.AutoUpdate/FileSystemAutoUpdateStateStore.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateProcessRunner.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateServiceProbe.cs`
- `SoftwareSchmiede.AutoUpdate/DefaultAutoUpdateHostTerminator.cs`
- `SoftwareSchmiede.AutoUpdate/HostAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate/ReleaseMetadataInstalledVersionProvider.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckWindowEvaluator.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckOptions.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckTimeRange.cs`
- `SoftwareSchmiede.AutoUpdate/ProcessOutputReader.cs`
- `SoftwareSchmiede.AutoUpdate/JsonFileStore.cs`
- `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj`
- `SoftwareSchmiede.AutoUpdate/README.md`
- Interfaces und Model-Records: `IAutoUpdateOrchestrator.cs`, `IAutoUpdateCommandHandler.cs`, `IAutoUpdateEventAggregator.cs`, `IAutoUpdateSource.cs`, `IAutoUpdatePackageStore.cs`, `IAutoUpdateStateStore.cs`, `IAutoUpdatePackageValidator.cs`, `IAutoUpdateInstaller.cs`, `IAutoUpdateScriptGenerator.cs`, `IAutoUpdateProcessRunner.cs`, `IAutoUpdateServiceProbe.cs`, `IAutoUpdateServiceResolver.cs`, `IAutoUpdatePlatformResolver.cs`, `IAutoUpdateStatusProvider.cs`, `IAutoUpdateEnvironment.cs`, `IAutoUpdateHostTerminator.cs`, `IInstalledVersionProvider.cs`, `AutoUpdateResult.cs`, `AutoUpdateOutcome.cs`, `AutoUpdateCheckResult.cs`, `AutoUpdateDownloadResult.cs`, `AutoUpdateInstallResult.cs`, `AutoUpdateReleaseInfo.cs`, `AutoUpdatePackageDescriptor.cs`, `AutoUpdateInstallationTarget.cs`, `InstalledReleaseInfo.cs`, `AutoUpdateCancelEventArgs.cs`, `AutoUpdateErrorEventArgs.cs`, `BeforeDownloadEventArgs.cs`, `BeforeInstallEventArgs.cs`, `BeforeStartUpdateScriptEventArgs.cs`

Bibliothekstests (neu):
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorCheckTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorDownloadTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorInstallTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOrchestratorEventTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateEventsTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateStatusServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCheckerServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateSchedulerServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateCommandServiceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePackageValidatorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePlatformResolverTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateServiceResolverTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateScriptGeneratorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateGithubSourceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateLocalFolderSourceTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateBuilderTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateOptionsValidationTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/UseAutoUpdateRegistrationTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/SourceCheckWindowEvaluatorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/FileSystemAutoUpdatePackageStoreTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/FileSystemAutoUpdateStateStoreTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AutoUpdateTestContext.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/FakeAutoUpdateSource.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/TestAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AsyncTestWait.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.csproj`

FinanceManager (geändert/neu):
- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`
- `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateContracts.cs`
- `FinanceManager.Web/Services/Updates/UpdateOptions.cs`
- `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- `FinanceManager.Web/FinanceManager.Web.csproj`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests_LockAndSchedule.cs`
- `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
- `FinanceManager.Tests/Updates/InstalledReleaseMetadataProviderTests.cs`
- `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
- `FinanceManager.Tests.Integration/TestWebApplicationFactory.cs`
- `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Helpers/TestUserSeeder.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `FinanceManager.sln`
