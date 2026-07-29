# Offene Aufgaben

Erstellt am: 2026-07-29
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] `IAutoUpdatePackageValidator.ValidateReleaseAsync` fehlt vollständig (weder Interface noch Implementierung). Der Plan listet für `IAutoUpdatePackageValidator` drei Mitglieder (`IsNewerVersion`, `ValidateReleaseAsync`, `ValidateDownloadedPackageAsync`); implementiert sind nur zwei. Funktional folgenlos (kein Aufrufer im Plan), zu entscheiden ist, ob das Mitglied nachgezogen oder der Plan angepasst wird.

## Code-Review-Befunde

- [ ] `AutoUpdateOrchestrator.RunUpdateAsync` hat keinen Produktivaufrufer — `EnableAutomaticDownload`/`EnableAutomaticInstallation` sind zur Laufzeit wirkungslos, obwohl in `appsettings.json`/README als aktive Schalter dokumentiert.
- [ ] `AutoUpdateOrchestrator.InstallCoreAsync` verwirft den Rückgabewert von `DeleteLockAsync` — fehlgeschlagenes Lock-Löschen wird nicht gemeldet, spätere Installationen scheitern dauerhaft.
- [ ] `AutoUpdateCheckerService`: `Task.Delay` im Error-Backoff liegt außerhalb eines inneren try/catch — `OperationCanceledException` beim Shutdown lässt `ExecuteAsync` fehlerhaft enden (Scheduler macht es korrekt).
- [ ] `ProcessOutputReader`: Bei Timeout wird der Kindprozess nicht beendet (`Kill` fehlt) — verwaiste Prozesse und unbeobachtete Read-Tasks.
- [ ] `ProcessOutputReader`: `stderr` wird bei Exit-Code ≠ 0 ohne `throwOnNonZeroExitCode` ersatzlos verworfen, keine Protokollierung.
- [ ] `AutoUpdateOptions.HealthTimeoutSeconds` wird von der Bibliothek nirgends fachlich ausgewertet, nur im Web-Adapter — gehört nicht in die öffentliche Konfigurationsfläche der Bibliothek oder die Lock-Staleness-Prüfung muss in die Bibliothek gezogen werden.
- [ ] Event `ErrorOccured` ist falsch geschrieben (`ErrorOccurred` wäre korrekt) — vor NuGet-Veröffentlichung umbenennen (Breaking Change danach).
- [ ] `AutoUpdateLocalFolderSource`/`AutoUpdateGithubSource`: doppelte Größenprüfung und `Directory.CreateDirectory`-Logik — in gemeinsame Hilfsklasse auslagern.
- [ ] `AutoUpdateLocalFolderSource.DownloadAsync` schreibt direkt in die Zieldatei statt über Temp-Datei + atomarem Move (wie `AutoUpdateGithubSource`) — bei Abbruch bleibt unvollständige Datei liegen.
- [ ] `AutoUpdateGithubSource.ManifestAssetName = "update.json"` ist hartcodiert; die im Setup-UI editierbare Einstellung `Updates:ManifestAssetName` ist wirkungslos.
- [ ] `AutoUpdateStatusService` hält zwei `SemaphoreSlim`, implementiert aber kein `IDisposable` (inkonsistent zu `AutoUpdateOrchestrator`).
- [ ] `AutoUpdatePackageValidator.IsNewerVersion`: Verhalten bei unbekannter installierter Version (liefert `false`, Update wird nie erkannt) ist nicht dokumentiert und nicht getestet.
- [ ] `AutoUpdatePackageValidator.ValidateEntry` (Zip-Slip-Schutz) hat keinerlei Testabdeckung.
- [ ] Plattformweiche (`RuntimeInformation.IsOSPlatform`) ist dreifach dupliziert in `AutoUpdateScriptGenerator`, `AutoUpdateServiceResolver`, `AutoUpdatePlatformResolver` statt zentral über `IAutoUpdatePlatformResolver.CurrentPlatform`.
- [ ] `UpdateOrchestratorAdapter` hat 10 Konstruktorabhängigkeiten und vereint drei Verantwortlichkeiten (Mapping, Settings-Durchreichen, Lock-Staleness-Regel) — Aufsplitten empfohlen.
- [ ] `UpdateOrchestratorAdapter` injiziert gleichzeitig `IAutoUpdateStatusProvider` und die konkrete `AutoUpdateStatusService` (dieselbe Singleton-Instanz zweimal aufgelöst), ebenso `IAutoUpdateOrchestrator` und `IAutoUpdateCommandHandler` parallel.
- [ ] `UpdateOptions.EnableAutomaticDownload`/`EnableAutomaticInstallation` (Web) werden nirgends gelesen — toter Code, da `BindConfiguration` direkt auf `AutoUpdateOptions` bindet.
- [ ] `UpdateSettingsStore.Build` nimmt 10 Einzelparameter; `Defaults()`/`Normalize` zerlegen ein DTO nur, um es wieder zusammenzusetzen — Data Clump.
- [ ] `ProgramExtensions`: Änderungen an Repository-Owner/-Name über das Setup-UI wirken erst nach Neustart, da `AutoUpdateOptionsMapper.ApplySettings` `Source` nicht neu erzeugt.
- [ ] `README.md` Zeile 23: `UseGithubSource("MyRepository", "my-org")` vertauscht Argumentreihenfolge (Signatur ist `owner, name`).
- [ ] `README.md` Zeile 53: `TimeRanges`-Beispiel (22:00–06:00) wird vom eigenen Validator abgelehnt (Start ≥ Ende nicht erlaubt) — Beispiel korrigieren, Mitternachts-Einschränkung dokumentieren.
- [ ] `AutoUpdateCheckerServiceTests.Execute_TriggersCheckOnlyWithinWindow` testet nur den positiven Fall, nicht „außerhalb des Fensters kein Aufruf".
- [ ] `AutoUpdateCheckerServiceTests`: feste `Task.Delay`-Aufrufe kombiniert mit exakten Aufrufzahl-Assertions sind unter Last unzuverlässig — durch `AsyncTestWait.WaitForAsync` ersetzen.
- [ ] `AutoUpdateScriptGeneratorTests.CreateGenerator` liefert ein Tupel, dessen zweiter Wert überall verworfen wird — Rückgabetyp vereinfachen.
- [ ] `UpdateOrchestratorAdapterTests`/`UpdateOrchestratorAdapterTests_LockAndSchedule`: doppelte `CreateStatusService()`-Hilfsmethode und wiederholter 10-Parameter-Konstruktoraufruf statt gemeinsamer Testfabrik; zudem Unterstrich im Klassennamen (Namenskonvention).
- [ ] `PlaywrightWebAppFixture.PrepareInstalledReleaseMetadata` schreibt `release-metadata.json` in den Quellbaum des Repositories statt in ein temporäres Verzeichnis — bei Testabbruch bleibt die Datei zurück bzw. wird überschrieben.
- [ ] `UpdateControllerIntegrationTests.SetDownloadPath` castet `ServiceDescriptor.ImplementationInstance` auf `AutoUpdateOptions` — hängt an internem Registrierungsdetail, bricht bei Umstellung der Registrierung mit `NullReferenceException`.
- [ ] `SoftwareSchmiede.AutoUpdate.Tests.xml` und `test-output.txt` sind untracked und nicht durch `.gitignore` abgedeckt — würden bei `git add -A` mit eingecheckt.

## Fehlgeschlagene Tests

Keine Tests fehlgeschlagen (Stand letzter Testlauf: 1110 bestanden, 1 übersprungen, 0 fehlgeschlagen).
