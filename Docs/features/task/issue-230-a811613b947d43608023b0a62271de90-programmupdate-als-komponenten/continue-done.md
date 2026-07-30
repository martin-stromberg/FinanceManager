# Offene Aufgaben

Erstellt am: 2026-07-29
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die meisten unten gelisteten Punkte wurden in einem weiteren `/implement`-Lauf abgearbeitet
(Stand: 2026-07-29, zweiter Durchlauf). Entscheidungen bei offenen Fragen sind bei den jeweiligen
Punkten vermerkt. Nach erneutem Plan-Review (`review.md`, vierter Durchlauf) und Code-Review
(`review-code.md`, vierter Durchlauf) verbleibt ein Punkt offen (Testklassen-Namenskonvention);
zusätzlich haben die neuen Reviews 2 offene Planelemente und 16 neue Code-Review-Befunde ergeben,
die separat in `review.md`/`review-code.md` dokumentiert sind und in der laufenden Iterationsschleife
weiterbearbeitet werden.

## Offene Planelemente

- [x] `IAutoUpdatePackageValidator.ValidateReleaseAsync` fehlt vollständig (weder Interface noch Implementierung). Der Plan listet für `IAutoUpdatePackageValidator` drei Mitglieder (`IsNewerVersion`, `ValidateReleaseAsync`, `ValidateDownloadedPackageAsync`); implementiert sind nur zwei. Funktional folgenlos (kein Aufrufer im Plan), zu entscheiden ist, ob das Mitglied nachgezogen oder der Plan angepasst wird.
  Entscheidung: Plan angepasst (`plan.md`) — `ValidateReleaseAsync` ersatzlos aus dem dokumentierten Vertrag entfernt, da kein Programmablauf es aufruft und ein ungenutztes öffentliches Interface-Mitglied in einem NuGet-Paket dauerhaft mitgeschleppt werden müsste.

## Code-Review-Befunde

- [x] `AutoUpdateOrchestrator.RunUpdateAsync` hat keinen Produktivaufrufer — `EnableAutomaticDownload`/`EnableAutomaticInstallation` sind zur Laufzeit wirkungslos, obwohl in `appsettings.json`/README als aktive Schalter dokumentiert.
  Fix: `AutoUpdateCheckerService` ruft jetzt `RunUpdateAsync` statt `CheckForUpdateAsync` auf (Download/Installation bleiben durch `RunUpdateAsync`s eigene Flag-Prüfung intern gesteuert); `AutoUpdateSchedulerService` unverändert. Plan und `SoftwareSchmiede.AutoUpdate/README.md` aktualisiert, Tests angepasst/ergänzt.
- [x] `AutoUpdateOrchestrator.InstallCoreAsync` verwirft den Rückgabewert von `DeleteLockAsync` — fehlgeschlagenes Lock-Löschen wird nicht gemeldet, spätere Installationen scheitern dauerhaft.
  Fix: neue private `ReleaseLockAsync`-Hilfsmethode meldet ein fehlgeschlagenes Löschen über `RaiseErrorOccurred`. Test mit fehlschlagendem `IAutoUpdatePackageStore`-Double ergänzt.
- [x] `AutoUpdateCheckerService`: `Task.Delay` im Error-Backoff liegt außerhalb eines inneren try/catch — `OperationCanceledException` beim Shutdown lässt `ExecuteAsync` fehlerhaft enden (Scheduler macht es korrekt).
  Fix: verschachteltes try/catch analog `AutoUpdateSchedulerService` ergänzt.
- [x] `ProcessOutputReader`: Bei Timeout wird der Kindprozess nicht beendet (`Kill` fehlt) — verwaiste Prozesse und unbeobachtete Read-Tasks.
  Fix: `Process.Kill(entireProcessTree: true)` bei Timeout, verbleibende Read-Tasks werden über eine Continuation beobachtet. Test verifiziert, dass ein per Timeout abgebrochener Prozess nicht weiterläuft.
- [x] `ProcessOutputReader`: `stderr` wird bei Exit-Code ≠ 0 ohne `throwOnNonZeroExitCode` ersatzlos verworfen, keine Protokollierung.
  Fix: optionaler `ILogger?`-Parameter, `LogWarning` mit Exit-Code und stderr bei nicht-werfendem Aufruf. Aufrufer (`DefaultAutoUpdateProcessRunner`, `DefaultAutoUpdateServiceProbe`) reichen ihren Logger durch. Test ergänzt.
- [x] `AutoUpdateOptions.HealthTimeoutSeconds` wird von der Bibliothek nirgends fachlich ausgewertet, nur im Web-Adapter — gehört nicht in die öffentliche Konfigurationsfläche der Bibliothek oder die Lock-Staleness-Prüfung muss in die Bibliothek gezogen werden.
  Entscheidung: Lock-Staleness-Prüfung in die Bibliothek gezogen — neue `IAutoUpdatePackageStore.IsLockStale(DateTimeOffset)` wertet `HealthTimeoutSeconds` aus; `UpdateOrchestratorAdapter.ResetLockAsync` delegiert dorthin statt selbst zu rechnen. Tests in `FileSystemAutoUpdatePackageStoreTests` und den Adapter-Tests ergänzt.
- [x] Event `ErrorOccured` ist falsch geschrieben (`ErrorOccurred` wäre korrekt) — vor NuGet-Veröffentlichung umbenennen (Breaking Change danach).
  Fix: in Interface, Implementierung, Orchestrator, Tests, README und CHANGELOG umbenannt.
- [x] `AutoUpdateLocalFolderSource`/`AutoUpdateGithubSource`: doppelte Größenprüfung und `Directory.CreateDirectory`-Logik — in gemeinsame Hilfsklasse auslagern.
  Fix: neue interne `AutoUpdateSourceDownloadHelper.CopyToTargetAsync` (Verzeichnis anlegen, Größenlimit während des Streamens durchsetzen, Temp-Datei + atomarer Move); von beiden Quellen genutzt.
- [x] `AutoUpdateLocalFolderSource.DownloadAsync` schreibt direkt in die Zieldatei statt über Temp-Datei + atomarem Move (wie `AutoUpdateGithubSource`) — bei Abbruch bleibt unvollständige Datei liegen.
  Fix: nutzt jetzt denselben `AutoUpdateSourceDownloadHelper` wie `AutoUpdateGithubSource`.
- [x] `AutoUpdateGithubSource.ManifestAssetName = "update.json"` ist hartcodiert; die im Setup-UI editierbare Einstellung `Updates:ManifestAssetName` ist wirkungslos.
  Fix: `manifestAssetName`-Parameter auf `AutoUpdateGithubSource`/`AutoUpdateLocalFolderSource`/`AutoUpdateBuilder.UseGithubSource`/`UseLocalFolderSource` ergänzt; `ProgramExtensions` reicht `updateOptions.ManifestAssetName` durch.
- [x] `AutoUpdateStatusService` hält zwei `SemaphoreSlim`, implementiert aber kein `IDisposable` (inkonsistent zu `AutoUpdateOrchestrator`).
  Fix: `IDisposable` ergänzt, disposed beide Semaphoren (wird als DI-Singleton automatisch beim Host-Shutdown disposed).
- [x] `AutoUpdatePackageValidator.IsNewerVersion`: Verhalten bei unbekannter installierter Version (liefert `false`, Update wird nie erkannt) ist nicht dokumentiert und nicht getestet.
  Fix: XML-Doku ergänzt (inkl. Begründung), zusätzliche Testfälle für leer/Whitespace/nicht parsbar.
- [x] `AutoUpdatePackageValidator.ValidateEntry` (Zip-Slip-Schutz) hat keinerlei Testabdeckung.
  Fix: Theory-Tests für Pfad-Traversal, absolute Pfade, Laufwerksbuchstaben sowie ein Positivtest für verschachtelte, sichere Einträge ergänzt.
- [x] Plattformweiche (`RuntimeInformation.IsOSPlatform`) ist dreifach dupliziert in `AutoUpdateScriptGenerator`, `AutoUpdateServiceResolver`, `AutoUpdatePlatformResolver` statt zentral über `IAutoUpdatePlatformResolver.CurrentPlatform`.
  Fix: beide Klassen erhalten optionalen `IAutoUpdatePlatformResolver`-Parameter und verzweigen über `CurrentPlatform`/neue `WindowsPlatform`/`LinuxPlatform`-Konstanten.
- [x] `UpdateOrchestratorAdapter` hat 10 Konstruktorabhängigkeiten und vereint drei Verantwortlichkeiten (Mapping, Settings-Durchreichen, Lock-Staleness-Regel) — Aufsplitten empfohlen.
  Fix: Mapping in neue `UpdateStatusMapper`-Klasse ausgelagert, Lock-Staleness-Regel in die Bibliothek gezogen (siehe `HealthTimeoutSeconds`-Punkt); Konstruktor auf 5 Abhängigkeiten reduziert.
- [x] `UpdateOrchestratorAdapter` injiziert gleichzeitig `IAutoUpdateStatusProvider` und die konkrete `AutoUpdateStatusService` (dieselbe Singleton-Instanz zweimal aufgelöst), ebenso `IAutoUpdateOrchestrator` und `IAutoUpdateCommandHandler` parallel.
  Fix: nur noch `AutoUpdateStatusService` (konkret) injiziert; `IAutoUpdateCommandHandler` entfernt, Check/Install rufen direkt über `IAutoUpdateOrchestrator`.
- [x] `UpdateOptions.EnableAutomaticDownload`/`EnableAutomaticInstallation` (Web) werden nirgends gelesen — toter Code, da `BindConfiguration` direkt auf `AutoUpdateOptions` bindet.
  Fix: beide Properties aus `UpdateOptions` entfernt, Klassendoku angepasst.
- [x] `UpdateSettingsStore.Build` nimmt 10 Einzelparameter; `Defaults()`/`Normalize` zerlegen ein DTO nur, um es wieder zusammenzusetzen — Data Clump.
  Fix: `Build` nimmt jetzt ein `UpdateSettingsUpdateRequest`; `Normalize` entfällt (trivialer Wrapper), `Defaults()` und der Legacy-Migrationspfad konvertieren einmalig in ein `UpdateSettingsUpdateRequest`.
- [x] `ProgramExtensions`: Änderungen an Repository-Owner/-Name über das Setup-UI wirken erst nach Neustart, da `AutoUpdateOptionsMapper.ApplySettings` `Source` nicht neu erzeugt.
  Fix: `ApplySettings` erzeugt bei `AutoUpdateGithubSource` eine neue Instanz mit den aktuellen Werten und disposed die alte. Tests ergänzt.
- [x] `README.md` Zeile 23: `UseGithubSource("MyRepository", "my-org")` vertauscht Argumentreihenfolge (Signatur ist `owner, name`).
  Fix: korrigiert zu `UseGithubSource("my-org", "MyRepository")`.
- [x] `README.md` Zeile 53: `TimeRanges`-Beispiel (22:00–06:00) wird vom eigenen Validator abgelehnt (Start ≥ Ende nicht erlaubt) — Beispiel korrigieren, Mitternachts-Einschränkung dokumentieren.
  Fix: Beispiel auf `08:00:00`–`18:00:00` geändert, Hinweis zur Mitternachts-Einschränkung ergänzt.
- [x] `AutoUpdateCheckerServiceTests.Execute_TriggersCheckOnlyWithinWindow` testet nur den positiven Fall, nicht „außerhalb des Fensters kein Aufruf".
  Fix: neuer Test `Execute_DoesNotRun_OutsideWindow`.
- [x] `AutoUpdateCheckerServiceTests`: feste `Task.Delay`-Aufrufe kombiniert mit exakten Aufrufzahl-Assertions sind unter Last unzuverlässig — durch `AsyncTestWait.WaitForAsync` ersetzen.
  Fix: neue `AsyncTestWait.AssertStaysFalseAsync`-Hilfsmethode für Negativ-Assertions, in `Execute_RespectsConfiguredInterval` statt fixer Delay+Count-Kombination genutzt.
- [x] `AutoUpdateScriptGeneratorTests.CreateGenerator` liefert ein Tupel, dessen zweiter Wert überall verworfen wird — Rückgabetyp vereinfachen.
  Fix: Rückgabetyp auf `AutoUpdateScriptGenerator` vereinfacht.
- [x] `UpdateOrchestratorAdapterTests`/`UpdateOrchestratorAdapterTests_LockAndSchedule`: doppelte `CreateStatusService()`-Hilfsmethode und wiederholter 10-Parameter-Konstruktoraufruf statt gemeinsamer Testfabrik; zudem Unterstrich im Klassennamen (Namenskonvention).
  Teilweise behoben (voriger Lauf): gemeinsame `UpdateOrchestratorAdapterTestFactory` ergänzt (dedupliziert `CreateStatusService` und Adapter-Konstruktion, jetzt nur noch 5 statt 10 Parameter). Zur Namenskonvention wurde zunächst bewusst **nicht** umbenannt, da `{Klasse}Tests_{Thema}` mit Unterstrich an anderer Stelle im Repo etabliert ist (u. a. `AccountServiceTests_CollectionAccount`, `AggregateBarChartTests_MobileScroll`, `GenericListPageTests_MobileFilters`, `StatementDraftServiceTests_CollectionAccount`).
  Endgültige Entscheidung (dieser Lauf): Ein erneuter, unabhängiger Code-Review-Lauf hat den Befund trotz der oben dokumentierten Begründung unverändert erneut gemeldet — die Beibehaltung der Konvention allein (ohne Umbenennung) hat die Rückfrage also nicht dauerhaft beendet. Um die wiederkehrende Rückfrage endgültig aufzulösen, wurde die Klasse **umbenannt**: `UpdateOrchestratorAdapterTests_LockAndSchedule` → `UpdateOrchestratorAdapterLockAndScheduleTests` (Datei entsprechend von `UpdateOrchestratorAdapterTests_LockAndSchedule.cs` nach `UpdateOrchestratorAdapterLockAndScheduleTests.cs` umbenannt, `<see cref>`-Verweis in `UpdateOrchestratorAdapterTestFactory.cs` angepasst). Die an anderer Stelle im Repo verwendete `{Klasse}Tests_{Thema}`-Konvention mit Unterstrich (`AccountServiceTests_CollectionAccount` u. a.) bleibt davon unberührt und wird nicht rückwirkend geändert — dies betrifft ausschließlich diese eine, wiederholt bemängelte Testklasse. Begründung für PascalCase hier statt erneuter Dokumentation der Unterstrich-Konvention: eine reine Dokumentation der Ausnahme hat sich in der Praxis (siehe vierter Review-Durchlauf) als nicht ausreichend erwiesen, um den automatisierten Code-Review-Befund dauerhaft zu unterdrücken; die Umbenennung entfernt das Muster an der Quelle und beendet die Rückfrage endgültig.
- [x] `PlaywrightWebAppFixture.PrepareInstalledReleaseMetadata` schreibt `release-metadata.json` in den Quellbaum des Repositories statt in ein temporäres Verzeichnis — bei Testabbruch bleibt die Datei zurück bzw. wird überschrieben.
  Geprüft und bewusst **nicht** auf ein Temp-Verzeichnis umgestellt: Der Serverprozess läuft mit `WorkingDirectory` = Quellordner `FinanceManager.Web` (= `ContentRootPath`), da `HelpDocumentPathResolver` Hilfe-Markdown live aus `ContentRootPath/../Docs/help` liest — eine Verlagerung des Content-Root würde das Hilfe-Feature in diesen Tests brechen. Stattdessen gehärtet: `AppDomain.ProcessExit`-Handler stellt den ursprünglichen Inhalt auch bei abnormalem Prozessende bestmöglich wieder her (zusätzlich zum bestehenden `DisposeAsync`-Pfad), und `/FinanceManager.Web/release-metadata.json` ist jetzt in `.gitignore` aufgenommen (die Datei war ohnehin nie versioniert). Begründung als Codekommentar hinterlegt.
- [x] `UpdateControllerIntegrationTests.SetDownloadPath` castet `ServiceDescriptor.ImplementationInstance` auf `AutoUpdateOptions` — hängt an internem Registrierungsdetail, bricht bei Umstellung der Registrierung mit `NullReferenceException`.
  Fix: `SingleOrDefault` + Pattern-Match statt blindem Cast; bei unerwarteter Registrierungsform wird jetzt eine `InvalidOperationException` mit erklärendem Hinweis geworfen statt einer unklaren `NullReferenceException`.
- [x] `SoftwareSchmiede.AutoUpdate.Tests.xml` und `test-output.txt` sind untracked und nicht durch `.gitignore` abgedeckt — würden bei `git add -A` mit eingecheckt.
  Geprüft: `SoftwareSchmiede.AutoUpdate.Tests.xml` war zum Zeitpunkt dieses Laufs bereits über `.gitignore` abgedeckt (frühere Änderung). `test-output.txt` existierte nicht mehr, wurde aber vorsorglich als generisches Ignore-Muster ergänzt.

## Fehlgeschlagene Tests

Keine Tests fehlgeschlagen. Stand nach diesem Lauf: `SoftwareSchmiede.AutoUpdate.Tests` 103 bestanden/1 übersprungen,
`FinanceManager.Tests` 893 bestanden, `FinanceManager.Tests.Integration` 103 bestanden, 0 fehlgeschlagen
(`FinanceManager.Tests.E2E` nicht in diesem Lauf ausgeführt, da es einen echten Browser/Server benötigt; das
Projekt wurde erfolgreich gebaut).
