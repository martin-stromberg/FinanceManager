← [Zurück zur Übersicht](index.md)

# Systemverwaltung und Setup — Release und Bereitstellung

## Zweck

Die Release-Pipeline baut die fertige Web-Anwendung, bestimmt eine
Semantic-Version und veröffentlicht ein GitHub-Release mit dem vollständigen
Windows-Publish als ZIP-Asset. Die Pipeline ist in
`.github/workflows/release.yml` definiert.

## Auslöser und Versionierung

Der Workflow läuft bei:

- einem Push auf `master`, einschließlich eines Merge-Ergebnisses eines Pull
  Requests;
- einem Push eines Tags im Format `vX.Y.Z`.

Auf `master` bestimmt Semantic Release die nächste Version aus den Commits
seit dem letzten Release. Es gelten die folgenden Regeln:

| Commit-Typ | Versionsänderung |
|---|---|
| `feat:` | Minor |
| `fix:` | Patch |
| `feat!:` oder `BREAKING CHANGE:` | Major |
| `docs:`, `refactor:`, `chore:` | kein Release |

Ein gültiger manueller Tag `vX.Y.Z` hat Vorrang vor der automatischen
Berechnung. Ein Push auf `master` ohne release-relevante Commits wird
erfolgreich mit `released=false` beendet und erzeugt kein leeres Release.

## Build und Veröffentlichungsartefakt

Der Workflow verwendet `windows-latest`, Node 22 und das .NET-SDK `10.0.x`.
Vor der Veröffentlichung laufen:

1. `npm ci` für die gesperrten Semantic-Release-Abhängigkeiten;
2. `dotnet test FinanceManager.sln --configuration Release`;
3. `dotnet build FinanceManager.sln --configuration Release --no-restore`;
4. `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj` mit
   `--framework net10.0`, `--runtime win-x64` und
   `--self-contained true` in das Verzeichnis `publish/`.

Der gesamte Inhalt von `publish/` wird als
`FinanceManager-vX.Y.Z-win-x64.zip` archiviert. Ein fehlendes oder leeres
Publish-Verzeichnis sowie ein leeres Archiv brechen den Workflow vor der
Veröffentlichung ab.

## Release und Asset

Für automatische Releases erzeugt Semantic Release den Tag, die Release Notes
und das GitHub-Release. Für manuelle Tags erstellt `gh release create` das
Release mit generierten Notes. Das ZIP wird in beiden Fällen als Asset
derselben Version angehängt.

Vor der Versionsfreigabe prüft `scripts/resolve-release-version.mjs` bereits
vorhandene Tags und Releases. Ein vollständiges vorhandenes Release wird nicht
überschrieben. Für ein vorhandenes ZIP gelten der erwartete Name,
`state: uploaded` und eine positive Dateigröße. Ein unvollständiges Asset wird
als `upload-existing` repariert; dabei checkt der Workflow vor Test, Build und
Paketierung den zugehörigen Release-Tag aus. Die Suche nach unvollständigen
Releases durchläuft alle Seiten der GitHub-Release-API.

## Betriebshinweise und aktueller Stand

Die Release-Versionsprüfungen, der Semantic-Release-Dry-Run und der
vollständige .NET-Testlauf (809 Tests einschließlich mobiler E2E-Tests) sind
erfolgreich. Die tatsächliche GitHub-Veröffentlichung bleibt ein CI-Lauf mit
Repository-Berechtigungen.
