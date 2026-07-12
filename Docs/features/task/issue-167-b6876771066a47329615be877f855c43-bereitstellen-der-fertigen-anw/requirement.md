### Fachliche Zusammenfassung

Für die Anwendung soll eine GitHub-Actions-CI/CD-Pipeline bereitgestellt werden, die nach einem Push beziehungsweise Merge nach `master` automatisch eine neue Version ermittelt, die vollständige .NET-10-Anwendung baut, das Publish-Ergebnis als ZIP-Datei verpackt und als Asset eines GitHub-Releases veröffentlicht. Die Versionierung soll auf Conventional Commits und Semantic Release basieren. Ein manuell gesetzter Git-Tag im Format `vX.Y.Z` soll die automatisch ermittelte Version überschreiben und ebenfalls ein Release inklusive Release Notes und ZIP-Artefakt auslösen.

---

### Akzeptanzkriterien

- Ein GitHub-Actions-Workflow startet bei jedem Push auf `master`; dadurch werden auch Merge-Ergebnisse aus Pull Requests verarbeitet.
- Der Workflow ermittelt anhand der Commits seit dem letzten Release die nächste Semantic-Version.
- `feat:` erhöht die Minor-Version.
- `fix:` erhöht die Patch-Version.
- `feat!:` oder ein `BREAKING CHANGE:` erhöht die Major-Version.
- Commits mit Typen wie `docs:`, `refactor:` oder `chore:` erzeugen keine neue Version.
- Ein manuell gepushter Tag im Format `vX.Y.Z` wird als verbindliche Version übernommen und funktioniert unabhängig von den Commit-Typen.
- Für die Anwendung wird ein vollständiges .NET-10-Publish ausgeführt.
- Der Build läuft auf einem Windows-Runner.
- Das gesamte Publish-Verzeichnis wird als ZIP-Datei gepackt.
- Für jede erzeugte Version wird ein GitHub-Release mit Release Notes angelegt.
- Die ZIP-Datei wird als Asset am passenden GitHub-Release veröffentlicht.
- Ein fehlgeschlagener Versions-, Build- oder Paketierungsschritt verhindert die Veröffentlichung eines unvollständigen Releases.

---

### Betroffene Dateien und Komponenten

#### GitHub-Actions-Workflow

- **Neue Workflow-Datei** unter `.github/workflows/`
  - Trigger für Pushes auf `master`.
  - Verwendung eines Windows-Runners.
  - Einrichtung der Node-Umgebung für Semantic Release.
  - Einrichtung der .NET-10-Umgebung beziehungsweise Verwendung einer installierten .NET-10-Version.
  - Ausführung der Versionsanalyse und Release-Erstellung.
  - Ausführung von `dotnet publish` in ein definiertes `publish/`-Verzeichnis.
  - Erstellung eines ZIP-Archivs aus dem vollständigen Publish-Verzeichnis.
  - Upload des ZIP-Archivs als GitHub-Release-Asset.

#### Semantic-Release-Konfiguration

- **Neue Konfigurationsdatei** als `.releaserc` oder `release.config.js`
  - Konfiguration der Conventional-Commit-Regeln und der Release-Branches.
  - Einbindung von `@semantic-release/changelog` für Release Notes beziehungsweise Changelog.
  - Einbindung von `@semantic-release/github` für GitHub-Tags, Releases und Assets.
  - Einbindung von `@semantic-release/git`, sofern generierte Dateien wie Changelog oder Versionsdateien in das Repository zurückgeschrieben werden sollen.
  - Unterstützung vorhandener manueller `vX.Y.Z`-Tags als Versionsvorgabe.

#### Node-Projektkonfiguration

- **`package.json`** oder eine bestehende äquivalente Node-Konfiguration
  - Aufnahme der Semantic-Release-Abhängigkeiten.
  - Definition eines reproduzierbaren Aufrufs für Semantic Release.
  - Festlegung einer kompatiblen Node-Version.

#### .NET-Anwendung

- **Bestehende .NET-Projekt- und Lösungsstruktur**
  - Ermittlung des tatsächlich zu veröffentlichenden Web- oder Startprojekts.
  - Sicherstellung, dass das Zielprojekt mit .NET 10 gebaut und veröffentlicht werden kann.
  - Festlegung der Publish-Parameter, insbesondere Konfiguration, Runtime und optionales Self-Contained-Verhalten.

#### Repository- und GitHub-Berechtigungen

- Der Workflow benötigt die erforderlichen Schreibrechte für Contents, damit Tags, Releases und Release-Assets erstellt werden können.
- Das verwendete GitHub-Token muss in der Workflow-Konfiguration beziehungsweise in den Repository-Einstellungen verfügbar sein.
- Die Release-Pipeline darf nur vom vorgesehenen Release-Branch beziehungsweise vom Push auf `master` ausgelöst werden.

---

### Ablauf

1. Ein Feature- oder Entwicklungs-Branch wird über einen Pull Request nach `master` gemergt.
2. Der Push auf `master` startet den Workflow.
3. Der Workflow prüft Repository, Node- und .NET-Voraussetzungen.
4. Semantic Release analysiert die Commits seit dem letzten Versionstag und bestimmt die nächste Version.
5. Falls ein manueller `vX.Y.Z`-Tag als Auslöser vorliegt, wird dessen Version verwendet.
6. Die Anwendung wird mit .NET 10 auf dem Windows-Runner veröffentlicht.
7. Der Inhalt des Publish-Verzeichnisses wird in ein ZIP-Archiv gepackt.
8. Semantic Release erstellt den Versions-Tag, die Release Notes und das GitHub-Release.
9. Das ZIP-Archiv wird als Asset des erstellten Releases hochgeladen.

---

### Implementierungsansatz

1. Zunächst das veröffentlichbare .NET-Projekt und dessen Ziel-Framework aus der bestehenden Lösungsstruktur ermitteln.
2. Eine Windows-basierte GitHub-Actions-Datei mit dem Trigger `push` auf `master` anlegen.
3. Node und .NET 10 im Workflow reproduzierbar einrichten und Abhängigkeiten aus einer Lock-Datei installieren, sofern vorhanden.
4. Semantic Release mit den benötigten Plugins konfigurieren und die GitHub-Token-Berechtigungen setzen.
5. Die Versionsausgabe aus Semantic Release für den Namen des ZIP-Archivs und die Release-Verknüpfung verfügbar machen.
6. `dotnet publish` in ein bereinigtes `publish/`-Verzeichnis ausführen und dieses Verzeichnis vollständig archivieren.
7. Das ZIP als Release-Asset veröffentlichen und den Workflow bei Fehlern sofort abbrechen lassen.
8. Einen manuellen Tag-Workflow beziehungsweise die Tag-Behandlung so integrieren, dass `vX.Y.Z` nicht durch eine automatisch berechnete Version ersetzt wird.
9. Den Ablauf mit `feat`, `fix`, Breaking-Change-, nicht release-relevanten Commits und einem manuellen Tag verifizieren.

---

### Konventionen und Randbedingungen

- Für release-relevante Commits ist die Conventional-Commit-Syntax verbindlich.
- Breaking Changes müssen als `feat!:` beziehungsweise mit `BREAKING CHANGE:` gekennzeichnet werden.
- Release-relevante Tags verwenden ausschließlich das Format `vX.Y.Z`.
- Merges nach `master` erfolgen über Pull Requests.
- Der Build verwendet Windows, da die Anwendung beziehungsweise die Anforderung keinen Ubuntu-Runner vorsieht.
- Das ZIP enthält den vollständigen Inhalt des Publish-Verzeichnisses und keine nur teilweise ausgewählten Dateien.
- Versionierung, Release Notes, GitHub-Release und Artefakt-Upload müssen für dieselbe Version erfolgen.
- Die Pipeline muss wiederholbar und nachvollziehbar sein; Versionen dürfen nicht stillschweigend doppelt veröffentlicht werden.

---

### Offene Fragen

1. Welches konkrete .NET-Projekt aus der Lösung ist das veröffentlichbare Startprojekt?
2. Soll das ZIP framework-dependent oder self-contained veröffentlicht werden, und für welche Windows-Runtime?
3. Welche .NET-10-SDK-Version und welche Node-Version sollen verbindlich verwendet werden?
4. Soll `@semantic-release/git` den generierten Changelog in `master` zurückschreiben, oder sollen Release Notes ausschließlich im GitHub-Release entstehen?
5. Soll der Workflow ausschließlich auf Pushes nach `master` reagieren, oder zusätzlich direkt auf das Push-Ereignis eines manuellen `vX.Y.Z`-Tags?
6. Wie soll mit einem Push auf `master` umgegangen werden, dessen Commits keine neue Version ergeben: Soll der Workflow erfolgreich ohne Release enden oder als übersprungener Lauf sichtbar sein?
7. Wie soll verhindert werden, dass ein manuell gesetzter Tag eine bereits veröffentlichte Version dupliziert?
8. Wie soll das ZIP benannt werden, beispielsweise `FinanceManager-vX.Y.Z.zip` oder `vX.Y.Z.zip`?
9. Wird für die Veröffentlichung das Standard-`GITHUB_TOKEN` mit erweiterten `contents: write`-Berechtigungen verwendet oder ein separates Repository- beziehungsweise Personal-Access-Token?
10. Soll der Workflow zusätzlich Tests vor dem Publish ausführen, und welche Testprojekte sind dafür verbindlich?
