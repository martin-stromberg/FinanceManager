# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### .github/workflows/staging-to-master.yml (Job `promote`)

- **Fehlerbehandlung** — Der Schritt „Ensure automated-promotion label exists" (Zeile 42–47) ruft `gh label create` auf, um das Label `automated-promotion` im Repository anzulegen bzw. zu aktualisieren. Das Erstellen/Aktualisieren von Repository-Labels läuft über den GitHub-REST-Endpunkt `POST /repos/{owner}/{repo}/labels`, der laut GitHub-Dokumentation die Token-Berechtigung `issues: write` voraussetzt. Der `permissions`-Block des Workflows (Zeile 14–16) gewährt jedoch nur `contents: read` und `pull-requests: write`. Dadurch schlägt dieser Schritt beim ersten Lauf (Label existiert noch nicht) mit einem 403-Fehler („Resource not accessible by integration") fehl, und der nachfolgende Schritt „Create promotion pull request" (der `--label "automated-promotion"` setzt) kann das Label ebenfalls nicht zuverlässig zuweisen, solange es nicht existiert.

  Empfehlung: `issues: write` zum `permissions`-Block hinzufügen (Zeile 14–16), z. B.:
  ```yaml
  permissions:
    contents: read
    pull-requests: write
    issues: write
  ```

- **Fehlerbehandlung (geringe Schwere)** — Der Workflow besitzt keinen `concurrency`-Block. Laufen zwei „Tests"-Workflow-Läufe auf `staging` (z. B. durch zwei schnell aufeinanderfolgende Pushes) nahezu gleichzeitig erfolgreich durch, können zwei parallele `promote`-Jobs gestartet werden. Beide führen die Prüfung „existiert bereits ein offener PR?" (Zeile 54) aus, bevor einer von ihnen den PR erstellt hat, sodass beide `gh pr create` versuchen und einer der beiden Jobs mit einem API-Fehler fehlschlägt (kein Datenverlust, aber ein irreführend rot markierter Workflow-Lauf).

  Empfehlung: Einen `concurrency`-Block ergänzen, z. B. `concurrency: { group: staging-to-master-promotion, cancel-in-progress: false }`, damit parallele Läufe serialisiert statt gleichzeitig ausgeführt werden.

### FinanceManager.Web/wwwroot/help/help-assets.sha256

- **Fehlerhafte/branchfremde Änderung** — Die Hash-Werte für mehrere Dateien unter `Docs/help/systemverwaltung-und-setup/` (u. a. `ablauf-technisch.md`, `bereitstellung.md`, `beschreibung.md`, `business-rules.md`, `installation.md`, Zeilen 51–58) wurden gegenüber dem Stand von `master` verändert, obwohl `git diff` für diese Markdown-Dateien selbst **keine** inhaltliche Änderung in diesem Branch ausweist. Verifikation für `installation.md`: Der SHA256-Hash des tatsächlich in Git committeten (LF-)Blobs ist `8890BDDE...`, während der jetzt im Manifest eingetragene Hash `B508B22C...` dem SHA256 der Arbeitskopie-Bytes mit CRLF-Zeilenenden entspricht. Das Manifest wurde also offenbar aus einer lokalen Windows-Arbeitskopie (Zeilenende-Normalisierung durch `core.autocrlf=true` / `.gitattributes: * text=auto`) neu generiert statt aus dem tatsächlich versionierten Inhalt. `HelpAssetIntegrityValidator.ValidateFile` berechnet den Hash zur Laufzeit über `SHA256.HashData(File.ReadAllBytes(fullPath))` der ausgelieferten Datei — stimmen die Zeilenenden der zur Laufzeit vorliegenden Datei nicht mit denen überein, aus denen der Manifest-Hash erzeugt wurde, meldet der Validator einen Hash-Mismatch (`LogWarning("Help file hash mismatch...")`) und stuft die Hilfedatei als nicht vertrauenswürdig ein. Zusätzlich ist diese Änderung inhaltlich nicht Teil des Themas „Einführung eines Staging-Branch" und sollte nicht unkommentiert in diesem PR mitgeführt werden.

  Empfehlung: Diese Datei auf den Stand von `master` zurücksetzen (`git checkout master -- FinanceManager.Web/wwwroot/help/help-assets.sha256`), sofern keine tatsächliche inhaltliche Änderung an den referenzierten Hilfedateien beabsichtigt war. Falls das Manifest doch bewusst neu generiert werden muss, sicherstellen, dass die Generierung auf Basis der git-versionierten (LF-)Bytes erfolgt, nicht auf Basis einer plattformabhängig zeilenende-konvertierten Arbeitskopie.

## Geprüfte Dateien

- `.github/workflows/staging-to-master.yml`
- `.github/workflows/test.yml`
- `.github/dependabot.yml`
- `CONTRIBUTING.md`
- `README.md`
- `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`
- `FinanceManager.Web/wwwroot/help/help-assets.sha256`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/inventory.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/inventory/branches.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/inventory/tests.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/inventory/workflows.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/plan.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/requirement.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/tasks.md`
- `Docs/features/task/issue-237-5e65e212215b4278810c75cbb96e6858-einfuehrung-eines-staging-bran/todo.md`
