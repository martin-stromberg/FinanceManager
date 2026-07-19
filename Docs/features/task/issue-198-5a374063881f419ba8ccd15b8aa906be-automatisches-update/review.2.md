# Plan-Review - Automatisches Update

Status: **Offene Aufgaben vorhanden**

## Zusammenfassung

Die aktuelle Implementierung schliesst die meisten offenen Punkte aus `review.1.md`: `IUpdateServiceResolver` inklusive Best-Effort-Probe ist vorhanden, Installationsfehler aus fehlender/mehrdeutiger Dienstkonfiguration werden ueber `400 BadRequest` abgebildet, die Setup-UI zeigt Admin-only, Updatequelle, bearbeitbare Repository-/Manifest-/WorkingDirectory-/HealthTimeout-Felder sowie Asset-Metadaten, der manuelle Installationsstart hat einen Downtime-Bestaetigungsdialog, `appsettings.Production.json` enthaelt `Updates`, und README/Hilfedokumentation beschreiben das Self-Update.

Auch die zentralen MVP-Bausteine aus dem Plan sind implementiert: Shared-DTOs und ApiClient-Erweiterung, Update-Controller, Health-Endpunkt, Release-Metadatenprovider, Dateispeicher, Settings-/Status-Store, Manifest-Client, Plattformresolver, Validator, Scriptgenerator, Executor, Checker/Scheduler, Setup-Sektion, Release-Manifest-Skript und Windows-/Linux-Release-Pipeline.

Der Plan ist aber noch nicht vollstaendig umgesetzt, weil Manifest-Inhalte nicht verbindlich genug validiert werden und die im Plan geforderte Testabdeckung weiterhin nicht vollstaendig erreicht ist.

## Offene Aufgaben

- Manifestvalidierung vor Download/Installation vervollstaendigen. `UpdateManifestClient.GetManifestAsync` deserialisiert `update.json` derzeit im Wesentlichen nur zu `UpdateMetadataDto`; eine explizite fachliche Validierung der Manifestfelder fehlt. Der Plan verlangt, dass Manifestfelder, Plattform, Dateiname, Groesse, SHA-256 und ZIP-Struktur vor Installation validiert werden. ZIP, Groesse und SHA-256 werden nach dem Download geprueft, aber z. B. leere Versionen, falsche Repository-Metadaten, ungueltige/fehlende Asset-URLs, leere oder formal falsche Hashes, nicht positive Groessen, inkonsistente Plattform-/Runtime-Kombinationen und Assetnamen ausserhalb des erwarteten Release-Schemas werden nicht als Manifestfehler sauber abgelehnt. `UpdatePlatformResolver.SelectAsset` waehlt nur nach `RuntimeIdentifier`; das `Platform`-Feld des Assets wird nicht gegen die aktuelle Plattform geprueft.

- Testabdeckung gemaess Plan ergaenzen. Es gibt inzwischen deutlich mehr Update-Tests, aber mehrere im Plan genannte Szenarien fehlen weiterhin oder sind nur sehr schmal abgedeckt: ungueltige Manifestfelder, `InstalledReleaseMetadataProvider` inklusive vorhandener `release-metadata.json`, explizite Plattformauswahl fuer Windows und Linux, Lock-Datei-Verhalten fuer freien/aktiven/verwaisten Lock, Admin-Lock-Reset-Regeln fuer verwaiste Locks, Statusuebergaenge fuer Check/Download/Ready/Installing/Failed, `POST install/start` mit Conflict und BadRequest in Integrationstests, ApiClient-Flows fuer Check/Schedule/Install/Reset, ViewModel-Flows fuer Laden/Speichern/Installationsfehler sowie Release-Skript-Fehlerfaelle fuer fehlende ZIP-Dateien, fehlende Release Notes oder leere Hashes.

## Verifikation

- `dotnet test FinanceManager.Tests\FinanceManager.Tests.csproj --no-restore --filter "FullyQualifiedName~Updates|FullyQualifiedName~SetupUpdateTab"`: bestanden, 29 Tests. Es gab bestehende NuGet-/Analyzer-Warnungen, u. a. `SQLitePCLRaw.lib.e_sqlite3` mit bekannter Sicherheitswarnung.
- `dotnet test FinanceManager.Tests.Integration\FinanceManager.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~UpdateControllerIntegrationTests"`: bestanden, 3 Tests. Es gab bestehende Build-/Analyzer-Warnungen.
- `node --test scripts\generate-update-manifest.test.mjs scripts\resolve-release-version.test.mjs`: bestanden, 21 Tests.
