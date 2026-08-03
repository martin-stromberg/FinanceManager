# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/wwwroot/help/help-assets.sha256

- **Ungewollte Änderung / fehlerhafte generierte Daten** — Für fünf Einträge unter `..\Docs\help\systemverwaltung-und-setup\` (`ablauf-technisch.md`, `bereitstellung.md`, `beschreibung.md`, `business-rules.md`, `installation.md`) wurden die Hash-Werte geändert, obwohl der eigentliche Inhalt dieser Markdown-Dateien im Branch unverändert ist. Verifiziert: `git show master:<pfad> | sha256sum` (LF-normalisiert) ergibt für alle fünf Dateien denselben Hash wie die aktuelle Working-Tree-Datei nach Entfernen von `\r` — der Inhalt ist byteidentisch bis auf Zeilenumbrüche. Ursache ist `core.autocrlf=true` in Kombination mit `* text=auto` in `.gitattributes`: Beim Regenerieren des Manifests auf einem Windows-Checkout mit CRLF-Zeilenenden entstehen andere SHA256-Werte als beim ursprünglich (vermutlich mit LF) committeten Manifest, obwohl sich am Dokumentinhalt nichts geändert hat. Das Manifest wird dadurch für jeden Windows-Checkout unbrauchbar/instabil und suggeriert Änderungen, die nicht stattgefunden haben.

  Empfehlung: Die fünf geänderten Zeilen in `help-assets.sha256` auf die vorherigen (in `master` committeten) Hash-Werte zurücksetzen, da sie nicht zu diesem Feature gehören. Zusätzlich das Tool/Skript, das diese Datei generiert, so anpassen, dass es die Zeilenenden vor dem Hashen normalisiert (z. B. Datei mit `\n` statt der rohen Working-Tree-Bytes lesen), damit das Ergebnis unabhängig von `core.autocrlf`/Checkout-Plattform reproduzierbar ist.

### .github/workflows/test.yml

- **Fehlende Versionsfixierung** — Der neue Schritt `Install ReportGenerator` (Zeile `dotnet tool install -g dotnet-reportgenerator-globaltool`) installiert das Tool ohne `--version`-Angabe, während alle anderen in diesem Workflow/Branch eingeführten bzw. vorhandenen Abhängigkeiten explizit gepinnt sind (`coverlet.collector` `Version="10.0.1"`, `actions/checkout@v4`, `actions/upload-artifact@v4`, `dotnet-version: '10.0.x'`). Ein zukünftiges Major-Update von `dotnet-reportgenerator-globaltool` kann das Format von `coverage-report/Summary.txt` ändern und dadurch den nachfolgenden Regex-Parse-Schritt (`Enforce coverage threshold`) unbemerkt zum Zeitpunkt eines beliebigen CI-Laufs brechen, ohne dass eine Code-Änderung in diesem Repository die Ursache wäre.

  Empfehlung: Version explizit pinnen, z. B. `dotnet tool install -g dotnet-reportgenerator-globaltool --version <x.y.z>`, konsistent mit der sonstigen Versionsfixierung im Projekt.

### .github/workflows/staging-to-master.yml

- **Unpassende Runner-Konfiguration** — Der Job `promote` läuft auf `runs-on: windows-latest`, führt aber ausschließlich plattformunabhängige `git`- und `gh`-CLI-Befehle aus (kein .NET-Build, kein Test, keine OS-spezifische Abhängigkeit wie bei `test.yml`, wo `windows-latest` laut dortigen Kommentaren u. a. für OS-Interface-Tests benötigt wird). Der Windows-Runner ist für diesen Anwendungsfall unnötig langsamer und teurer als ein Linux-Runner.

  Empfehlung: `runs-on: ubuntu-latest` verwenden, sofern keine anderen (hier nicht ersichtlichen) Gründe für Windows sprechen.

## Geprüfte Dateien

- `.github/workflows/staging-to-master.yml`
- `.github/workflows/test.yml`
- `.github/dependabot.yml` (nicht Teil des Branch-Diffs — untracked, daher nur ergänzend gesichtet)
- `CONTRIBUTING.md`
- `README.md`
- `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`
- `FinanceManager.Web/wwwroot/help/help-assets.sha256`
