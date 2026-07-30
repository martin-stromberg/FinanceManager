# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs, UpdateSettingsStore.cs, UpdateOrchestratorAdapter.cs

- **Kopplung / Datenverlust bei Konfigurationsänderung** — `AutoUpdateOptionsMapper.ApplySettings` (Zeile 26) setzt `options.DownloadPath = settings.WorkingDirectory`. Genau davon hängt der Ablageort der Einstellungsdatei ab: `UpdateSettingsStore.SettingsPath` (Zeile 31) ist `Path.Combine(_packageStore.RootDirectory, "settings.json")`, und `FileSystemAutoUpdatePackageStore.RootDirectory` (Zeile 29) wird aus `_options.DownloadPath` abgeleitet. `UpdateOrchestratorAdapter.SaveSettingsAsync` (Zeilen 56–61) schreibt erst (altes Verzeichnis) und ruft danach `ApplyToOptions` auf (neues Verzeichnis). Ändert ein Administrator im Setup-UI das Arbeitsverzeichnis, liegt die gerade gespeicherte `settings.json` im alten Verzeichnis und wird nie wieder gelesen; der nächste `GetAsync` fällt auf `Defaults()` zurück und setzt `RepositoryOwner`, `RepositoryName` und `ManifestAssetName` stillschweigend auf die `appsettings`-Werte zurück. Zusätzlich wechseln `status.json` und `update.lock` mitten im Betrieb das Verzeichnis, sodass ein aktiver Installations-Lock unsichtbar wird. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Den Ablageort der `settings.json` von `AutoUpdateOptions.DownloadPath` entkoppeln — entweder einen eigenen, nicht über das UI änderbaren Pfad in `UpdateOptions` verwenden, oder in `SaveSettingsAsync` das Verzeichnis vor dem Schreiben umstellen und den bestehenden Inhalt (`settings.json`, `status.json`, `update.lock`) in das neue Verzeichnis migrieren.

### FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs (AutoUpdateOptionsMapper)

- **Fehlerbehandlung / Nebenläufigkeit** — `ApplySettings` (Zeilen 30–34) ersetzt `options.Source` durch eine neue `AutoUpdateGithubSource` und ruft anschließend `previousSource.Dispose()` auf. Der Aufruf erfolgt aus dem Request-Thread von `SaveSettingsAsync`, komplett außerhalb des Serialisierungs-Semaphors von `AutoUpdateOrchestrator`. Läuft gleichzeitig ein Hintergrund-Check oder ein Download (`AutoUpdateOrchestrator.CheckCoreAsync` Zeile 184 bzw. `DownloadCoreAsync` Zeile 242 holen die Quelle über `RequireSource()`), wird deren `HttpClient` mitten im Aufruf disposed und der Vorgang scheitert mit einer `ObjectDisposedException`, die als generischer Fehler in `LastError` landet.

  Empfehlung: Das Ersetzen der Quelle über die Bibliothek serialisieren (z. B. eine Methode auf `IAutoUpdateOrchestrator`/`AutoUpdateStatusService`, die den vorhandenen Semaphor nutzt) oder die alte Quelle nicht sofort disposen, sondern erst nach Abschluss laufender Operationen.

### FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs (UpdateOrchestratorAdapter)

- **Fehlerbehandlung / Uneinheitlichkeit** — `ResetLockAsync` (Zeile 105) verwirft den Rückgabewert von `_packageStore.DeleteLockAsync(ct)` und setzt anschließend bedingungslos `IsLocked = false`. Schlägt das Löschen fehl (Rückgabe `false`, `FileSystemAutoUpdatePackageStore.DeleteLockAsync` Zeilen 118–126), meldet die API `204 No Content`, der Status zeigt „nicht gesperrt“, die Lock-Datei existiert aber weiterhin — jede weitere Installation scheitert dauerhaft mit „An update lock is already active.“. `AutoUpdateOrchestrator.ReleaseLockAsync` (Zeilen 331–340) behandelt exakt denselben Fall korrekt über `RaiseErrorOccurred`. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Rückgabewert auswerten und bei `false` eine `IOException` mit aussagekräftiger Meldung werfen, statt den Status zu fälschen.

- **Fehlerbehandlung / Widerspruch zur eigenen Dokumentation** — Der Klassenkommentar (Zeilen 10–11) sagt zu: „Errors reported by the library as `AutoUpdateResult.Error` are re-thrown so the controller's existing exception mapping continues to apply.“ `StartInstallAsync` (Zeilen 83–86) tut das, `CheckAsync` (Zeilen 72–77) jedoch nicht: Dort wird `result.Error` vollständig verworfen und lediglich `result.Outcome == Success` als `UpdateAvailable`-Flag zurückgegeben. Ein fehlgeschlagener Check (z. B. Netzwerkfehler) ist für den Aufrufer nicht von „kein Update verfügbar“ zu unterscheiden.

  Empfehlung: `CheckAsync` an `StartInstallAsync` angleichen (bei `Outcome == Failed` und vorhandenem `Error` erneut werfen) oder den Klassenkommentar auf die tatsächlich geltende Regel einschränken.

- **Fehlende Interfaces / Inappropriate Intimacy** — Der Adapter injiziert die konkrete Bibliotheksklasse `AutoUpdateStatusService` (Zeile 17), obwohl die Bibliothek mit `IAutoUpdateStatusProvider` eine Abstraktion anbietet. Grund ist, dass `UpdateAsync(...)` (benötigt in `ResetLockAsync`, Zeile 106) auf keinem Interface liegt. Der Consumer hängt damit an einer konkreten Klasse eines als NuGet-Paket vorgesehenen Pakets; `UpdateOrchestratorAdapterTestFactory.CreateStatusService()` (Zeilen 14–22) muss deshalb eine echte Instanz mit zwei Mocks aufbauen statt eines einzigen Mocks. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: `UpdateAsync` auf ein Interface heben (z. B. `IAutoUpdateStatusWriter` oder Erweiterung von `IAutoUpdateStatusProvider`) und im Adapter nur noch das Interface injizieren.

### SoftwareSchmiede.AutoUpdate/AutoUpdateHostBuilderExtensions.cs (AutoUpdateHostBuilderExtensions)

- **Toter Code / überflüssige Abhängigkeit** — `UseAutoUpdate` registriert in Zeile 46 `builder.Services.AddHttpClient()`. Weder `IHttpClientFactory` noch `HttpClient` werden irgendwo in der Bibliothek aus dem DI-Container aufgelöst: `AutoUpdateGithubSource` wird ausschließlich über `AutoUpdateBuilder.UseGithubSource` (Zeile 100) und damit über `AutoUpdateGithubSource.Create` mit einem selbst erzeugten `HttpClient` gebaut. Die Registrierung ist wirkungslos und erzwingt zugleich die `Microsoft.Extensions.Http`-PackageReference (`SoftwareSchmiede.AutoUpdate.csproj`, Zeile 32) für jeden Konsumenten des NuGet-Pakets.

  Empfehlung: Entweder `AutoUpdateGithubSource` über `IHttpClientFactory` aus dem Container beziehen (dann ist die Registrierung sinnvoll) oder `AddHttpClient()` und die `Microsoft.Extensions.Http`-Abhängigkeit entfernen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateServiceResolver.cs (AutoUpdateServiceResolver)

- **Fehlerbehandlung / unvollständige Validierung** — `ValidateExecutablePath` (Zeile 102) prüft `fullPath.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase)` ohne abschließendes Verzeichnistrennzeichen. Bei `ApplicationDirectory = "C:\app"` besteht auch `C:\application\fremd.exe` die Prüfung, obwohl die Fehlermeldung „Executable path must point to the current application directory“ das ausschließen soll. Der Pfad wird anschließend ungeprüft in das generierte PowerShell-Skript geschrieben (`AutoUpdateScriptGenerator.GenerateWindowsAsync`, Zeile 58). (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: `appRoot` vor dem Vergleich mit `Path.DirectorySeparatorChar` terminieren bzw. `Path.GetRelativePath` verwenden und Ergebnisse mit `..` ablehnen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateGithubSource.cs (AutoUpdateGithubSource)

- **Fehlende Validierung / Fehlermeldung ohne Kontext** — `CheckAsync` (Zeilen 103–125) verwendet das deserialisierte `GithubReleaseManifest` ungeprüft. `Version` und `Assets` sind im Record nicht-nullable deklariert, `System.Text.Json` erzwingt das aber nicht: Fehlt `assets` im Manifest, wirft `manifest.Assets.Select(...)` (Zeile 113) eine `NullReferenceException`; enthält ein Asset eine relative oder leere `assetUrl`, wirft `new Uri(asset.AssetUrl)` (Zeile 119) eine `UriFormatException`. Beide werden in `AutoUpdateOrchestrator.CheckCoreAsync` (Zeile 215) generisch gefangen und landen als „Object reference not set to an instance of an object.“ in `LastError` und im Setup-UI. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Nach dem Deserialisieren `Version` und `Assets` prüfen und bei fehlenden/ungültigen Werten eine `InvalidOperationException` mit Manifest-URL und Feldnamen werfen; `assetUrl` mit `Uri.TryCreate(..., UriKind.Absolute, ...)` validieren.

### SoftwareSchmiede.AutoUpdate/AutoUpdateScriptGenerator.cs, IAutoUpdateScriptGenerator.cs

- **Toter Code / unbenutzter Parameter** — Der Parameter `package` von `GenerateAsync` (Interface Zeile 17, Implementierung Zeile 27) wird in `AutoUpdateScriptGenerator` nirgends verwendet; weder `GenerateWindowsAsync` (Zeile 43) noch `GenerateLinuxAsync` (Zeile 78) erhalten ihn. Der einzige Aufrufer `AutoUpdateInstaller.PrepareAsync` (Zeile 43) reicht ihn nur durch, und `AutoUpdateScriptGeneratorTests.BuildPackage` (Zeile 98) existiert ausschließlich, um diesen ungenutzten Parameter zu befüllen. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Parameter aus Interface, Implementierung und Aufrufer entfernen; `BuildPackage` im Test ersatzlos streichen.

### SoftwareSchmiede.AutoUpdate/JsonFileStore.cs (JsonFileStore)

- **Fehlerbehandlung / uneinheitlich zur Schwesterimplementierung** — `WriteAtomicAsync` (Zeilen 47–57) schreibt in eine `.tmp`-Datei und verschiebt sie anschließend, räumt die temporäre Datei bei einer Ausnahme (Serialisierungsfehler, `IOException`, Abbruch über `ct`) aber nicht auf. Die praktisch identische Logik in `AutoUpdateSourceDownloadHelper.CopyToTargetAsync` (Zeilen 23–61) macht genau das korrekt per `try`/`catch`. Da `WriteAtomicAsync` bei jedem Statuswechsel und bei jedem Speichern der Einstellungen aufgerufen wird, sammeln sich verwaiste `status.json.<guid>.tmp`/`settings.json.<guid>.tmp`-Dateien im Update-Wurzelverzeichnis an.

  Empfehlung: Dieselbe `try`/`catch`-Aufräumlogik wie in `AutoUpdateSourceDownloadHelper.CopyToTargetAsync` ergänzen — idealerweise beide Stellen auf einen gemeinsamen „atomic write“-Helfer zusammenführen.

### SoftwareSchmiede.AutoUpdate/AutoUpdatePlatformResolver.cs (AutoUpdatePlatformResolver)

- **Hardcodierte Werte** — `CurrentRuntimeIdentifier` (Zeilen 48–64) liefert für Windows fest `"win-x64"` und für Linux fest `"linux-x64"`; der tatsächliche `RuntimeInformation.RuntimeIdentifier` wird nur für nicht unterstützte Plattformen verwendet. Auf einem ARM64-Host (`win-arm64`, `linux-arm64`) wählt `SelectPackage` (Zeilen 75–78) damit das x64-Paket aus und das Setup-UI zeigt über `UpdateStatusMapper` (Zeile 60) eine falsche Plattform an.

  Empfehlung: Den echten `RuntimeInformation.RuntimeIdentifier` verwenden bzw. die Architektur über `RuntimeInformation.ProcessArchitecture` ergänzen; falls eine Einschränkung auf x64 gewollt ist, sie als Konstante mit erklärendem Kommentar definieren und beim Nichtübereinstimmen einen expliziten Fehler melden statt still das falsche Paket zu wählen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateCheckerService.cs, AutoUpdateSchedulerService.cs

- **Doppelter Code** — Beide `ExecuteAsync`-Implementierungen (`AutoUpdateCheckerService` Zeilen 46–76, `AutoUpdateSchedulerService` Zeilen 49–82) bestehen aus demselben Gerüst: `while (!stoppingToken.IsCancellationRequested)`, `try` mit fachlicher Aktion und `Task.Delay(..., _timeProvider, stoppingToken)`, `catch (OperationCanceledException) when (...) { break; }`, `catch (Exception ex) { Log; try { await Task.Delay(...); } catch (OperationCanceledException) when (...) { break; } }`. Es unterscheiden sich lediglich die drei Zeilen fachlicher Aktion, das Delay-Intervall und der Log-Text.

  Empfehlung: Das Schleifen-/Backoff-Gerüst in eine gemeinsame Basisklasse oder einen Helfer (`RunPeriodicallyAsync(Func<CancellationToken, Task> action, Func<TimeSpan> interval, TimeSpan errorBackoff, string errorMessage, ...)`) auslagern und beide Hosted Services darauf umstellen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateEvents.cs (AutoUpdateEvents)

- **Namenskonventionen** — Das öffentliche Event heißt korrekt `ErrorOccurred` (Zeile 68), das dahinterliegende private Feld weiterhin `_errorOccured` mit nur einem „r“ (Zeilen 30, 70, 71, 160). Innerhalb derselben Klasse existieren damit zwei Schreibweisen für dasselbe Konzept. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Feld in `_errorOccurred` umbenennen.

### SoftwareSchmiede.AutoUpdate/AutoUpdateStatusSnapshot.cs (AutoUpdateStatusSnapshot)

- **Toter Code** — Die Record-Property `InstalledVersion` (Zeile 19) wird ausschließlich geschrieben (`Idle(...)`, Zeile 36, aufgerufen aus `AutoUpdateStatusService.EnsureLoadedAsync` Zeile 63) und nie gelesen: Weder die Bibliothek noch der Web-Adapter greifen darauf zu. `UpdateStatusMapper.MapAsync` (Zeilen 37, 57) ermittelt die installierte Version stattdessen bei jedem Aufruf erneut über `IInstalledReleaseMetadataProvider`. Der persistierte Wert in `status.json` veraltet damit nach einem Update dauerhaft, ohne dass es auffällt.

  Empfehlung: Entweder die Property aus dem Snapshot entfernen (und das Aktualisieren in `Idle`/`EnsureLoadedAsync` streichen) oder sie konsequent zur Quelle der Wahrheit machen, in `ReconcileAfterRestartAsync` mitpflegen und im `UpdateStatusMapper` verwenden.

### SoftwareSchmiede.AutoUpdate/AutoUpdateCheckResult.cs, AutoUpdateDownloadResult.cs, AutoUpdateInstallResult.cs, AutoUpdateInstallationTarget.cs, AutoUpdatePackageDescriptor.cs, AutoUpdateReleaseInfo.cs, AutoUpdateResult.cs, InstalledReleaseInfo.cs

- **Einheitlichkeit / fehlerhafte XML-Dokumentation** — Alle acht Record-Typen tragen ein `<returns>`-Tag direkt auf der Typdeklaration (z. B. `AutoUpdateDownloadResult.cs` Zeile 9, `AutoUpdateInstallResult.cs` Zeile 9, `AutoUpdatePackageDescriptor.cs` Zeile 13). `<returns>` ist nur auf Methoden/Properties zulässig; auf einem Typ erscheint es in keiner generierten Dokumentation und in keiner IntelliSense-Anzeige. Andere Typen desselben Pakets (`AutoUpdateStatusSnapshot`, `SourceCheckTimeRange`, `AutoUpdateOptions`) kommen ohne aus. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: `<returns>`-Tags auf den acht Record-Typdeklarationen entfernen; der Inhalt gehört, falls gewünscht, in `<summary>`.

### SoftwareSchmiede.AutoUpdate/AutoUpdateBuilder.cs (AutoUpdateBuilder)

- **Veraltete Dokumentation** — Der XML-Kommentar auf `ExplicitDownloadPath` (Zeilen 20–24) nennt ausschließlich `EnableAutomaticDownload` als Setzer. Seit Einführung von `WithDownloadPath` (Zeile 189) setzt auch diese Methode das Feld; der Kommentar ist unvollständig und führt beim Nachvollziehen der Präzedenzregeln in `AutoUpdateHostBuilderExtensions.ReapplyExplicitValues` (Zeile 131) in die Irre. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Kommentar um `WithDownloadPath` ergänzen.

### FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs (UpdateSettingsStore)

- **Doppelter Code / Data Clump** — Die Umwandlung eines vollständigen `UpdateSettingsDto`/`LegacyUpdateSettingsDto` in einen 10-argumentigen `UpdateSettingsUpdateRequest` steht zweimal wortgleich in der Datei: `Defaults()` (Zeilen 70–80) und `ReadSettingsAsync()` (Zeilen 122–132). Beide Male werden dieselben zehn Felder in derselben Reihenfolge einzeln aufgezählt, nur um sie direkt danach an `Build` zu übergeben. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Eine private Hilfsmethode `ToRequest(...)` bzw. eine `Build`-Überladung ergänzen, die direkt ein `UpdateSettingsDto` normalisiert, und beide Stellen darauf umstellen.

### FinanceManager.Web/ProgramExtensions.cs

- **Hardcodierter Wert / inkonsistente Pfadauflösung** — Der Fallback für die lokale Ordnerquelle (Zeilen 185–190) baut `Path.Combine(updateOptions.WorkingDirectory, "source")`. Da `Updates:WorkingDirectory` standardmäßig der relative Pfad `"updates"` ist, entsteht ein relativer Pfad, den `AutoUpdateLocalFolderSource` gegen das aktuelle Arbeitsverzeichnis des Prozesses auflöst — während die Bibliothek denselben Pfad für `DownloadPath` konsistent gegen `ContentRootPath` auflöst (`AutoUpdateHostBuilderExtensions.BuildOptions`, Zeilen 97–100; `FileSystemAutoUpdatePackageStore.ResolveFullPath`, Zeilen 133–139). Als Windows-Dienst gestartet zeigen Quelle und Downloadverzeichnis damit auf unterschiedliche Orte. Zusätzlich wird das Literal `"source"` dupliziert, das in der Bibliothek bereits als `AutoUpdateHostBuilderExtensions.DefaultSourceDirectoryName` existiert (Zeile 21).

  Empfehlung: Den Pfad explizit gegen `builder.Environment.ContentRootPath` auflösen und die Konstante für das Quellverzeichnis aus der Bibliothek öffentlich machen und wiederverwenden, statt sie erneut zu literalisieren.

- **Toter Code (praktisch unerreichbarer Zweig)** — Der Fallback in Zeilen 178–181 (`if (!sourceCheckIntervalConfigured) cfg.WithSourceCheck(...)`) läuft nur, wenn `Updates:SourceCheck:Interval` nicht konfiguriert ist. Genau dieser Schlüssel wurde jedoch in derselben Änderung in `appsettings.json` (Zeilen 46–49) ergänzt und ist damit bei jeder Standardinstallation gesetzt. Damit ist der Zweig — und mit ihm `UpdateOptions.CheckIntervalMinutes` (Zeile 21) — für die ausgelieferte Konfiguration wirkungslos.

  Empfehlung: Entweder `SourceCheck.Interval` aus `appsettings.json` entfernen, damit der Legacy-Alias tatsächlich als Migrationspfad greift, oder den Fallback samt `UpdateOptions.CheckIntervalMinutes` entfernen und die Migration einmalig beim Lesen der Konfiguration abbilden.

### SoftwareSchmiede.AutoUpdate.Tests (projektweit)

- **Doppelter Code** — Das Muster `var dir = Directory.CreateTempSubdirectory(); try { … } finally { dir.Delete(recursive: true); }` steht 26-mal in 9 Testdateien (`UseAutoUpdateRegistrationTests` 6×, `AutoUpdatePackageValidatorTests` 5×, `FileSystemAutoUpdatePackageStoreTests` 5×, `AutoUpdateScriptGeneratorTests` 3×, `AutoUpdateGithubSourceTests` 2×, `AutoUpdateLocalFolderSourceTests` 2×, `AutoUpdateBuilderTests`, `FileSystemAutoUpdateStateStoreTests`, `ProcessOutputReaderTests` je 1×), zusätzlich einmal in `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs` (Zeilen 83–97) und viermal in `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`. Im selben Projekt existiert mit `TestSupport/AutoUpdateTestContext` bereits eine Klasse, die genau dieses Verzeichnis-Lifecycle über `IDisposable` kapselt.

  Empfehlung: Eine kleine `TempDirectory : IDisposable`-Hilfsklasse in `TestSupport` ergänzen (`using var temp = new TempDirectory();`) und alle Vorkommen darauf umstellen.

- **Toter Code** — 22 Dateien im Projekt enthalten `using SoftwareSchmiede.AutoUpdate;`, obwohl sie im Namespace `SoftwareSchmiede.AutoUpdate.Tests` liegen und der übergeordnete Namespace dort ohnehin im Gültigkeitsbereich ist (u. a. `AutoUpdateEventsTests.cs` Zeile 2, `AutoUpdateOrchestratorCheckTests.cs` Zeile 2, `TestSupport/FakeAutoUpdateSource.cs` Zeile 1, `TestSupport/TestAutoUpdateEnvironment.cs` Zeile 1, `TestSupport/AutoUpdateTestContext.cs` Zeile 5).

  Empfehlung: Die überflüssigen `using`-Direktiven entfernen.

### SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AutoUpdateTestContext.cs

- **Ressourcenfreigabe** — Die Klasse implementiert `IDisposable`, gibt in `Dispose()` (Zeilen 126–136) aber nur das temporäre Verzeichnis frei. Die selbst erzeugten `IDisposable`-Instanzen `Orchestrator` (`AutoUpdateOrchestrator`, hält einen `SemaphoreSlim`, Zeilen 50–59) und `StatusService` (`AutoUpdateStatusService`, hält zwei `SemaphoreSlim`, Zeile 40) werden nie freigegeben. Der Kontext wird in fünf Testklassen in nahezu jedem Test neu instanziiert. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: In `Dispose()` zuerst `Orchestrator.Dispose()` und `StatusService.Dispose()` aufrufen, danach das Verzeichnis löschen.

- **Namenskonventionen** — `RecordingProcessRunner.PrepareEnvironmentCallCount` (Zeile 148) zählt die Aufrufe von `EnsureUpdateUnitAvailable` (Zeile 154). Der Name stammt aus einer früheren Methodenbenennung und beschreibt nicht mehr, was gezählt wird.

  Empfehlung: In `EnsureUpdateUnitAvailableCallCount` umbenennen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateSchedulerServiceTests.cs

- **Testqualität** — Alle drei Tests verwenden feste reale `Task.Delay(100)`-Aufrufe (Zeilen 27, 47, 69) und prüfen danach exakte Aufrufzahlen (`Times.Never`, `Times.Once`). Unter Last sind diese Assertions unzuverlässig. Die dafür vorgesehene Hilfsklasse `AsyncTestWait` mit `WaitForAsync` und `AssertStaysFalseAsync` existiert bereits und wird in `AutoUpdateCheckerServiceTests` konsequent genutzt. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: `Task.Delay(100)` in `Execute_WhenNotReady_DoesNotInstall` und `Execute_SameScheduleTwice_InstallsOnce` durch `AsyncTestWait.AssertStaysFalseAsync(...)` ersetzen.

- **Doppelter Code** — Der 10-argumentige `AutoUpdateStatusSnapshot`-Konstruktor wird in den Zeilen 18, 40 und 58 dreimal mit überwiegend `null`-Werten ausgeschrieben. In `FinanceManager.Tests/Updates/UpdateStatusTestData.cs` existiert für exakt dieses Problem bereits ein Builder, der im Bibliotheks-Testprojekt aber fehlt.

  Empfehlung: Einen analogen Snapshot-Builder in `SoftwareSchmiede.AutoUpdate.Tests/TestSupport` ergänzen und in allen drei Tests verwenden.

### SoftwareSchmiede.AutoUpdate.Tests/ProcessOutputReaderTests.cs

- **Testqualität** — `Read_OnTimeout_KillsChildProcessInsteadOfLeavingItRunning` (Zeile 29) wartet mit einem festen `await Task.Delay(3500)`, obwohl `AsyncTestWait.AssertStaysFalseAsync(condition, durationMs)` genau für diese „darf nicht eintreten“-Prüfung vorgesehen ist und im selben Projekt genutzt wird. Der Test läuft dadurch immer volle 3,5 Sekunden.

  Empfehlung: Auf `AsyncTestWait.AssertStaysFalseAsync(() => File.Exists(markerPath), 3500)` umstellen.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdatePackageValidatorTests.cs

- **Fehlende Testabdeckung** — Der zweite sicherheitsrelevante Zweig von `AutoUpdatePackageValidator.ValidateEntry` ist unabgedeckt: die Prüfung der Unix-Dateimodus-Bits (`AutoUpdatePackageValidator.cs`, Zeilen 89–93), die Symlinks, Geräte- und Socket-Einträge ablehnt. Zu dieser Bedingung existiert kein Test. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Test ergänzen, der ein ZIP mit gesetzten Symlink-Bits (`ExternalAttributes = 0xA1FF << 16`) erzeugt und `InvalidOperationException` erwartet.

### SoftwareSchmiede.AutoUpdate.Tests/AutoUpdateGithubSourceTests.cs

- **Ressourcenfreigabe** — In den Tests werden `new HttpClient(handler)` und die daraus erzeugte `AutoUpdateGithubSource` (implementiert `IDisposable`) nie freigegeben (Zeilen 36, 52, 72); `StubHttpMessageHandler` ebenfalls nicht. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: `using`-Deklarationen für Handler, `HttpClient` und Source ergänzen.

### SoftwareSchmiede.AutoUpdate.Tests (fehlende Abdeckung neuer öffentlicher API)

- **Fehlende Testabdeckung für neue öffentliche Methoden** — Zu den neuen öffentlichen Builder-Methoden `AutoUpdateBuilder.WithUpdateUnitName` (Zeile 170) und `AutoUpdateBuilder.WithDownloadPath` (Zeile 189) existiert kein Test — weder für den Erfolgsfall (Wert landet in `AutoUpdateOptions` und überlebt das Konfigurations-Binding via `ReapplyExplicitValues`) noch für die dokumentierte `ArgumentException` bei leerem Wert. Ebenso ohne direkte Tests: die öffentlichen Klassen `ScheduledInstallEvaluator`, `AutoUpdateInstaller`, `DefaultAutoUpdateProcessRunner` und `JsonFileStore` sowie der Zweig `AutoUpdateServiceResolver.ValidateExecutablePath` (nur der `ServiceName`-Pfad ist in `AutoUpdateServiceResolverTests` abgedeckt).

  Empfehlung: Mindestens Tests für die beiden neuen Builder-Methoden (Präzedenz gegenüber Konfiguration analog zu `UseAutoUpdate_ExplicitDownloadPath_TakesPrecedenceOverConfiguration`) sowie eine eigene Testklasse für `ScheduledInstallEvaluator` und den `ExecutablePath`-Zweig des `AutoUpdateServiceResolver` ergänzen.

### FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs

- **Testqualität** — `ApplySettings_WhenSourceIsGithubSource_ReplacesSourceWithUpdatedRepository` (Zeilen 67–78) verspricht im Namen, dass die Quelle mit dem geänderten Repository neu erzeugt wird, prüft aber nur, dass eine andere Instanz desselben Typs vorliegt. Ob `new-owner`/`new-repo`/`manifest.json` tatsächlich übernommen wurden, wird nicht verifiziert — der Test bliebe auch grün, wenn `ApplySettings` die alten Repository-Werte wiederverwendet. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: `AutoUpdateGithubSource` die verwendeten Repository-Werte lesbar machen (interne Properties plus `InternalsVisibleTo`, oder Prüfung über die erzeugte Manifest-URL) und im Test darauf assertieren.

- **Ressourcenfreigabe** — Die in Zeile 70 und in `ApplySettings` erzeugten `AutoUpdateGithubSource`-Instanzen halten je einen selbst erzeugten `HttpClient`; die nach dem Austausch aktive Instanz wird im Test nie disponiert.

  Empfehlung: Die verbliebene `options.Source`-Instanz am Testende disponieren.

### FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs

- **Lazy Class** — `AutoUpdateEnvironmentAdapter` (Zeilen 143–151) existiert nur, um aus einem `TestWebHostEnvironment` den `ContentRootPath` zu lesen und als `ApplicationDirectory` zurückzugeben. Die Bibliothek hat bewusst keine ASP.NET-Abhängigkeit; der Umweg über `IWebHostEnvironment` bringt hier keinen Mehrwert, und mit `HostAutoUpdateEnvironment` existiert in der Bibliothek bereits eine Implementierung, die genau das für jedes `IHostEnvironment` leistet. (Befund unverändert aus dem vorherigen Durchlauf.)

  Empfehlung: Adapter und `TestWebHostEnvironment` durch eine einfache `IAutoUpdateEnvironment`-Implementierung mit festem Pfad ersetzen (analog zu `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/TestAutoUpdateEnvironment`) oder direkt `HostAutoUpdateEnvironment` verwenden.

### FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs

- **God-Klasse / doppelter Code** — Die Klasse umfasst 320 Zeilen, deckt sieben fachlich getrennte Themen ab (Health-Endpunkt, Autorisierung, Settings-Roundtrip, Install-Fehlermapping, Lock-Reset, Restart-Reconciliation) und enthält drei eigene Test-Doubles. Der `_factory.WithWebHostBuilder(builder => builder.ConfigureServices(...))`-Block wird fünfmal wiederholt; die beiden Reconciliation-Tests (Zeilen 148–158 und 181–191) sind bis auf einen Versionsstring zeichengleich, ebenso die drei `ThrowingUpdateOrchestrator`-Aufbauten (Zeilen 76–83, 97–104, 212–219). Das widerspricht der im Repository hinterlegten Konvention `test-class-structure` (Testklassen klein halten, an Themengrenzen aufteilen).

  Empfehlung: Klasse an Themengrenzen aufteilen (z. B. `UpdateControllerAuthorizationTests`, `UpdateControllerInstallErrorTests`, `UpdateControllerLockTests`, `UpdateControllerStatusReconciliationTests`) und die Factory-Konstruktion in Hilfsmethoden wie `CreateFactoryWithThrowingOrchestrator(Exception)` bzw. `CreateFactoryWithDownloadPath(string, string installedVersion)` zusammenfassen.

### FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs

- **Testqualität / Seiteneffekt auf den Quellbaum** — `PrepareInstalledReleaseMetadata` schreibt `release-metadata.json` direkt in den Quellordner `FinanceManager.Web` des Repositorys und stellt den vorherigen Zustand erst in `RestoreInstalledReleaseMetadata` bzw. über einen `AppDomain.ProcessExit`-Handler wieder her. Ein Test verändert damit das Arbeitsverzeichnis des Entwicklers; bei zwei parallel laufenden Testprozessen oder einem harten Abbruch (Kill statt `ProcessExit`) bleibt fremder Inhalt zurück. Die `.gitignore`-Ergänzung mildert nur die Sichtbarkeit, nicht die Ursache.

  Empfehlung: Den Serverprozess mit einem eigenen `ASPNETCORE_CONTENTROOT`/temporären Content-Root starten und die vom Hilfe-Feature benötigten Pfade (`HelpDocumentPathResolver`) über eine Konfigurationsoption auf den Quellbaum zeigen lassen, statt den Quellbaum zu beschreiben.

## Geprüfte Dateien

Bibliothek:
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
- `SoftwareSchmiede.AutoUpdate/AutoUpdateSourceDownloadHelper.cs`
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
- `SoftwareSchmiede.AutoUpdate/ProcessOutputReader.cs`
- `SoftwareSchmiede.AutoUpdate/ReleaseMetadataInstalledVersionProvider.cs`
- `SoftwareSchmiede.AutoUpdate/ScheduledInstallEvaluator.cs`
- `SoftwareSchmiede.AutoUpdate/SoftwareSchmiede.AutoUpdate.csproj`
- `SoftwareSchmiede.AutoUpdate/SourceCheckOptions.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckTimeRange.cs`
- `SoftwareSchmiede.AutoUpdate/SourceCheckWindowEvaluator.cs`

Bibliothekstests:
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
- `SoftwareSchmiede.AutoUpdate.Tests/ProcessOutputReaderTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/SoftwareSchmiede.AutoUpdate.Tests.csproj`
- `SoftwareSchmiede.AutoUpdate.Tests/SourceCheckWindowEvaluatorTests.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AsyncTestWait.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/AutoUpdateTestContext.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/FakeAutoUpdateSource.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/RecordingLogger.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/TestSupport/TestAutoUpdateEnvironment.cs`
- `SoftwareSchmiede.AutoUpdate.Tests/UseAutoUpdateRegistrationTests.cs`

Web-Integration:
- `FinanceManager.Web/ProgramExtensions.cs`
- `FinanceManager.Web/FinanceManager.Web.csproj`
- `FinanceManager.Web/appsettings.json`
- `FinanceManager.Web/Components/Pages/Setup/SetupUpdateTab.razor`
- `FinanceManager.Web/Services/Updates/AutoUpdateOptionsMapper.cs`
- `FinanceManager.Web/Services/Updates/InstalledReleaseMetadataProvider.cs`
- `FinanceManager.Web/Services/Updates/UpdateContracts.cs`
- `FinanceManager.Web/Services/Updates/UpdateOptions.cs`
- `FinanceManager.Web/Services/Updates/UpdateOrchestratorAdapter.cs`
- `FinanceManager.Web/Services/Updates/UpdateSettingsStore.cs`
- `FinanceManager.Web/Services/Updates/UpdateStatusMapper.cs`

Anwendungstests:
- `FinanceManager.Tests/Updates/AutoUpdateOptionsMapperTests.cs`
- `FinanceManager.Tests/Updates/InstalledReleaseMetadataProviderTests.cs`
- `FinanceManager.Tests/Updates/TestWebHostEnvironment.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterLockAndScheduleTests.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTestFactory.cs`
- `FinanceManager.Tests/Updates/UpdateOrchestratorAdapterTests.cs`
- `FinanceManager.Tests/Updates/UpdateSettingsStoreTests.cs`
- `FinanceManager.Tests/Updates/UpdateStatusTestData.cs`
- `FinanceManager.Tests.Integration/UpdateControllerIntegrationTests.cs`
- `FinanceManager.Tests.E2E/Helpers/SetupUpdateGateway.cs`
- `FinanceManager.Tests.E2E/Helpers/TestUserSeeder.cs`
- `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs`
- `FinanceManager.Tests.E2E/Tests/Setup/UpdateSetupPlaywrightTests.cs`

Projektstruktur:
- `FinanceManager.sln`
- `.gitignore`
