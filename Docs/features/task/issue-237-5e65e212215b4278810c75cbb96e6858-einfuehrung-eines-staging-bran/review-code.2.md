# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/wwwroot/help/help-assets.sha256

- **Ungewollte Änderung / fehlerhafte generierte Daten** — Fünf Einträge unter `..\Docs\help\systemverwaltung-und-setup\` (`ablauf-technisch.md`, `bereitstellung.md`, `beschreibung.md`, `business-rules.md`, `installation.md`) haben in diesem Branch geänderte Hash-Werte, obwohl der Inhalt dieser fünf Markdown-Dateien gegenüber `master` unverändert ist (`git diff master -- Docs/help/systemverwaltung-und-setup/<datei>` liefert keinen Treffer). Verifiziert per Hash-Vergleich: Der LF-normalisierte SHA256 der aktuellen Working-Tree-Datei ist für alle fünf Dateien identisch mit dem in `master` committeten Blob-Hash; nur der rohe (CRLF-behaftete) Working-Tree-Hash weicht ab, z. B. `ablauf-technisch.md`: LF-Hash `2a31b39f...` (== `master`-Blob), roher CRLF-Hash `e33adb7d...` (== neuer Wert im Manifest). Ursache ist der MSBuild-Task `GetFileHash` in `FinanceManager.Web/FinanceManager.Web.csproj` (Zeilen ~102–105), der beim Neugenerieren von `help-assets.sha256` die rohen Datei-Bytes hasht statt zeilenendungs-normalisiert zu hashen; auf einem Windows-Checkout mit CRLF (`core.autocrlf`/`.gitattributes`) entstehen dadurch andere Hashes als beim ursprünglich committeten (LF-basierten) Manifest, obwohl sich am Dokumentinhalt nichts geändert hat. Diese Änderung ist vermutlich als Nebenwirkung eines lokalen Builds/Testlaufs in diesen Branch gerutscht und gehört inhaltlich nicht zum Feature „Einführung eines staging-Branches im CI-Prozess". Sie erzeugt eine irreführende Diff-Zeile im PR und würde bei jedem Windows-Checkout erneut „flackern".

  Empfehlung: Die fünf betroffenen Zeilen in `help-assets.sha256` auf die in `master` committeten Werte zurücksetzen (z. B. `git checkout master -- FinanceManager.Web/wwwroot/help/help-assets.sha256` und danach nur die für dieses Feature tatsächlich relevanten Änderungen erneut einpflegen, falls vorhanden). Die zeilenendungs-abhängige Hash-Generierung im `GetFileHash`-Target selbst ist ein bestehendes, nicht in diesem Branch eingeführtes Problem und sollte separat (außerhalb dieses Features) behoben werden, z. B. durch Normalisieren der Zeilenenden vor dem Hashen oder durch ein festes `.gitattributes`-`eol=lf` für die betroffenen Help-Dokumente.

## Geprüfte Dateien

- `.github/workflows/staging-to-master.yml`
- `.github/workflows/test.yml`
- `.github/dependabot.yml` (neu, aktuell untracked — inhaltlich Teil dieses Features, da im README referenziert)
- `CONTRIBUTING.md`
- `README.md`
- `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`
- `FinanceManager.Web/wwwroot/help/help-assets.sha256`

Nicht inhaltlich code-reviewt (reine Planungs-/Prozessdokumentation ohne Code-Charakter, außerhalb des Geltungsbereichs dieses Reviews):
- `Docs/features/task/issue-237-.../inventory.md`, `inventory/branches.md`, `inventory/tests.md`, `inventory/workflows.md`, `plan.md`, `requirement.md`, `tasks.md`, `todo.md`
