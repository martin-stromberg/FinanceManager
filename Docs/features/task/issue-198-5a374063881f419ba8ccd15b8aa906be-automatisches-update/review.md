# Plan-Review - Automatisches Update

Status: **Offene Aufgaben vorhanden**

## Zusammenfassung

Die offenen Punkte aus `review.2.md` sind teilweise geschlossen. Die Manifestvalidierung ist inzwischen deutlich erweitert und wird vor dem Download ausgefuehrt: `UpdateOrchestrator.CheckAsync` ruft `ValidateManifest(...)` vor Assetauswahl und Download auf, `UpdateValidator` prueft Version, PublishedAt, Release Notes, Repository-Metadaten, Assetnamen, HTTPS-GitHub-URLs, SHA-256-Format, positive Groesse sowie Plattform-/Runtime-Konsistenz, und `UpdatePlatformResolver.SelectAsset` gleicht Plattform und RuntimeIdentifier ab.

Die zentrale MVP-Implementierung aus dem Plan ist weiterhin weitgehend vorhanden: Shared-DTOs und ApiClient-Erweiterung, Update-Controller, Health-Endpunkt, Release-Metadatenprovider, Dateispeicher, Settings-/Status-Store, Manifest-Client, Plattformresolver, Validator, Scriptgenerator, Executor, Checker/Scheduler, Setup-Sektion, Release-Manifest-Skript und Windows-/Linux-Release-Pipeline.

Vollstaendig umgesetzt ist der Plan aber noch nicht. Es verbleibt eine fachliche Abweichung beim Admin-Lock-Reset, und die im Plan geforderte Testabdeckung ist weiterhin nur teilweise vorhanden.

## Offene Aufgaben

- Admin-Lock-Reset nur fuer haengende Locks erlauben. Der Plan verlangt fuer `POST lock/reset`, dass nur eine als haengend bewertete Lock-Datei geloescht wird und der Reset verweigert wird, wenn der aktuelle Prozess noch eine laufende Installation kennt. Die zweite Bedingung ist umgesetzt (`UpdateOrchestrator.ResetLockAsync`, `FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs:118`), aber danach wird `_fileStore.DeleteLockAsync(...)` ohne Alters-, Owner- oder Stale-Bewertung aufgerufen (`FinanceManager.Web/Services/Updates/UpdateOrchestrator.cs:125`). Damit kann ein Admin aktuell jede vorhandene Lock-Datei loeschen, auch wenn sie nicht als verwaist/haengend klassifiziert wurde.

- Testabdeckung gemaess Plan vervollstaendigen. Die Manifestvalidierung ist inzwischen unit-getestet (`FinanceManager.Tests/Updates/UpdateValidatorTests.cs:106`), und Executor-Fehlerpfade decken Lock-Aufraeumen und Failed-Status ab (`FinanceManager.Tests/Updates/UpdateExecutorTests.cs:14`). Mehrere im Plan genannte Szenarien fehlen aber weiterhin oder sind nur indirekt abgedeckt: `InstalledReleaseMetadataProvider` mit vorhandener `release-metadata.json`, explizite Plattform-/Assetauswahl fuer Windows und Linux, Lock-Datei-Verhalten fuer freien/aktiven/verwaisten Lock, Admin-Lock-Reset-Regeln fuer verwaiste vs. nicht verwaiste Locks, vollstaendige Statusuebergaenge fuer Check/Download/Ready/Installing/Failed, Integrationstests fuer `POST install/start` mit Conflict und BadRequest, ApiClient-Flows fuer Check/Schedule/Install/Reset sowie ViewModel-Flows fuer Laden/Speichern/Installationsfehler.

## Verifikation

- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~Updates|FullyQualifiedName~SetupUpdateTab|FullyQualifiedName~SetupCardViewModel"`: bestanden, 48 Tests. Es gab bestehende NuGet-/Trim-Warnungen, unter anderem `SQLitePCLRaw.lib.e_sqlite3` mit bekannter Sicherheitswarnung.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~UpdateControllerIntegrationTests"`: bestanden, 3 Tests. Es gab bestehende NuGet-/Trim-Warnungen.
- `node --test scripts\generate-update-manifest.test.mjs scripts\resolve-release-version.test.mjs`: bestanden, 23 Tests.
