# Umsetzungsplan: Bereitstellung der fertigen Anwendung

## Ziel

Eine reproduzierbare GitHub-Actions-Release-Pipeline baut `FinanceManager.Web` auf
einem Windows-Runner mit .NET 10, ermittelt eine Semantic-Version aus
Conventional Commits, verpackt das vollständige Publish-Ergebnis als ZIP und
veröffentlicht es als Asset eines GitHub-Releases. Manuelle Tags im Format
`vX.Y.Z` sind verbindlich und haben Vorrang vor der automatischen
Versionsberechnung.

## Festgelegte Entscheidungen

- Veröffentlichungsziel ist `FinanceManager.Web/FinanceManager.Web.csproj`.
- Der Workflow verwendet `windows-latest`.
- Es wird `dotnet publish` für `net10.0` mit `--configuration Release`,
  `--runtime win-x64` und `--self-contained true` ausgeführt. Damit enthält das
  ZIP die Windows-Laufzeit und ist auf einer passenden Windows-x64-Umgebung
  ohne separate .NET-Runtime installierbar.
- Die .NET-SDK-Familie wird mit `actions/setup-dotnet` auf `10.0.x` festgelegt.
  Node wird mit `actions/setup-node` auf Node 22 festgelegt; die exakten
  Semantic-Release-Versionen kommen aus `package-lock.json`.
- Releases werden auf Pushes nach `master` und auf Pushes von `v*`-Tags
  verarbeitet. Ein Tag-Workflow baut denselben Commit wie der Tag.
- Ein Push nach `master`, für den keine neue Version berechnet wird, beendet
  den Job erfolgreich mit dem sichtbaren Ergebnis `released=false`; es wird
  kein leeres Release angelegt.
- Ein manuelles `vX.Y.Z`-Tag wird vor der automatischen Berechnung validiert und
  als Version verwendet. Existiert der Tag oder das Release bereits, wird kein
  vorhandenes Release überschrieben; der Lauf endet idempotent bzw. mit einer
  klaren Fehlermeldung bei widersprüchlichem Zustand.
- Das ZIP heißt `FinanceManager-vX.Y.Z-win-x64.zip`.
- Es wird das Standard-`GITHUB_TOKEN` mit `contents: write` verwendet. Kein
  Token oder Secret wird in Dateien abgelegt.
- Vor dem Publish werden `dotnet test FinanceManager.sln --configuration Release`
  und ein Build der Solution ausgeführt. Schlägt einer dieser Schritte, die
  Versionsberechnung oder die Paketierung fehl, laufen Release und Asset-Upload
  nicht.
- Der bestehende `CHANGELOG.md` wird nicht automatisch zurückgeschrieben. Die
  Release Notes werden über Semantic Release im GitHub-Release erzeugt; dadurch
  entstehen keine bot-erzeugten Folgecommits auf `master`.

## Betroffene Dateien

### Neue Dateien

- `.github/workflows/release.yml`
  - Trigger für `push.branches: [master]` und `push.tags: [v*]`.
  - `permissions: contents: write`, Windows-Runner und eine Concurrency-Gruppe
    pro Ref, damit parallele Läufe denselben Release-Zustand nicht doppelt
    bearbeiten.
  - Checkout mit vollständiger Historie und Tags (`fetch-depth: 0`).
  - Einrichtung von Node 22 und .NET 10, `npm ci`, Tests, Publish, ZIP und
    Release-Asset-Upload in strikt voneinander abhängigen Schritten.
- `package.json`
  - Private Node-Konfiguration, Node-Engine `22.x` und ein reproduzierbarer
    `release`-Aufruf.
  - Semantic-Release sowie Conventional-Commit-, Release-Notes- und GitHub-
    Plugins als direkte Entwicklungsabhängigkeiten.
- `package-lock.json`
  - Gesperrter Dependency-Baum für `npm ci`.
- `release.config.js`
  - Release-Branch `master`, Conventional-Commit-Regeln und Release-Notes-
    Generator.
  - GitHub-Plugin für Tag, Release Notes und das vorbereitete ZIP-Asset.
  - Kein Changelog-/Git-Plugin, da `CHANGELOG.md` unverändert bleiben soll.
- `scripts/resolve-release-version.mjs`
  - Ermittelt anhand der Workflow-Ref, ob ein gültiges manuelles `vX.Y.Z`-Tag
    vorliegt.
  - Liefert für `master` die von Semantic Release ermittelte Version bzw. eine
    Markierung für `released=false`.
  - Prüft vorhandene Tags und GitHub-Releases auf Duplikate und schreibt die
    Version sowie Release-Entscheidung ausschließlich über GitHub-Outputs.

### Bestehende Dateien ohne erforderliche Änderung

- `FinanceManager.Web/FinanceManager.Web.csproj` bleibt Publish-Ziel und erhält
  keine dauerhafte Runtime- oder Self-contained-Vorgabe.
- `FinanceManager.sln` und die Testprojekte werden über die bestehenden
  Solution-Befehle verwendet.
- `CHANGELOG.md` bleibt unverändert.

## Umsetzungsschritte

1. `package.json` und `package-lock.json` mit Node-22-Vorgabe sowie den
   kompatiblen Semantic-Release-Paketen anlegen. Mit `npm ci` und
   `npm run release -- --dry-run` die Konfiguration lokal prüfbar machen.
2. `release.config.js` konfigurieren:
   - nur `master` als automatischen Release-Branch zulassen;
   - `feat` als Minor-, `fix` als Patch- und `!`/`BREAKING CHANGE` als
     Major-Release behandeln;
   - `docs`, `refactor` und `chore` ohne Release behandeln;
   - Release Notes aus den analysierten Commits erzeugen;
   - GitHub als Release-Ziel mit dem Workflow-Token verwenden.
3. `scripts/resolve-release-version.mjs` implementieren. Eingaben müssen
   vollständig validiert werden; eine nicht passende Tag-Ref oder eine
   Versionskollision darf nicht stillschweigend in einen anderen Release
   umgedeutet werden.
4. `.github/workflows/release.yml` erstellen. Die Schritte sind:
   - vollständigen Checkout einschließlich Tags;
   - Node/.NET einrichten und `npm ci` ausführen;
   - Release-Version bzw. `released=false` bestimmen;
   - bei `released=false` alle folgenden Build-/Release-Schritte überspringen;
   - Solution testen und `FinanceManager.Web` nach `publish/` veröffentlichen;
   - `publish/` vollständig und ohne zusätzliche Dateien in das versionierte
     ZIP packen;
   - Semantic Release beziehungsweise den manuellen Tag-Release mit exakt
     derselben Version und demselben ZIP als Asset ausführen.
5. Für alle Pfade und Shell-Aufrufe Windows-PowerShell-Syntax verwenden. Das
   ZIP wird aus dem Inhalt von `publish/` erstellt, nicht aus dem Verzeichnis
   selbst, damit beim Entpacken direkt die Anwendung vorliegt.
6. Fehlerbedingungen explizit absichern: fehlende Version, ungültiger Tag,
   fehlendes Publish-Verzeichnis, leeres ZIP, vorhandenes abweichendes Release
   und fehlende `GITHUB_TOKEN`-Berechtigung müssen den Job vor der
   Veröffentlichung abbrechen.

## Verifikation

### Automatisierte Prüfungen

- `npm ci` funktioniert mit der Lock-Datei.
- `npm run release -- --dry-run` akzeptiert `master` und erkennt die
  Conventional-Commit-Fälle korrekt.
- `dotnet restore` beziehungsweise der Workflow-Setup erfolgt erfolgreich.
- `dotnet test FinanceManager.sln --configuration Release` läuft vor dem
  Publish.
- `dotnet publish FinanceManager.Web/FinanceManager.Web.csproj --configuration
  Release --framework net10.0 --runtime win-x64 --self-contained true
  --output publish` erzeugt eine vollständige Ausgabe.

### Pipeline-/Versionsszenarien

1. `feat: ...` erzeugt Minor, `fix: ...` Patch und `feat!: ...` beziehungsweise
   `BREAKING CHANGE:` Major.
2. `docs:`, `refactor:` und `chore:` erzeugen keinen Release und keinen ZIP-
   Upload.
3. Ein Push von `v2.3.4` verwendet exakt `2.3.4`, unabhängig von den Commits.
4. Ein bereits vorhandenes Release beziehungsweise Tag wird nicht doppelt
   veröffentlicht.
5. Der ZIP-Inhalt entspricht vollständig dem Inhalt von `publish/` und der
   Dateiname enthält exakt dieselbe Version wie Tag und Release.
6. Ein absichtlich fehlschlagender Test, Build-, Publish- oder ZIP-Schritt
   verhindert die Erstellung des GitHub-Releases.

Die Workflow- und Versionsszenarien werden, soweit lokal möglich, über
Konfigurations-/Dry-Run-Prüfungen und isolierte Skripttests verifiziert. Die
tatsächliche GitHub-Release-Erstellung bleibt ein CI-Lauf mit Repository-
Berechtigungen.

## Akzeptanzkriterien-Mapping

| Kriterium | Umsetzung/Prüfung |
|---|---|
| Push nach `master` | `release.yml`-Branch-Trigger |
| Semantic-Version aus Commits | Semantic Release und Dry-Run-Szenarien |
| `feat`, `fix`, Breaking Changes | `release.config.js` und Skripttests |
| nicht release-relevante Typen | Commit-Analyzer-Regeln und No-release-Lauf |
| manueller `vX.Y.Z`-Tag | Tag-Trigger und Versionsauflösung |
| vollständiges .NET-10-Publish | `FinanceManager.Web`, `net10.0`, `win-x64`, self-contained |
| Windows-Build | `windows-latest` |
| vollständiges ZIP | Archivierung des gesamten `publish/`-Inhalts |
| Release Notes und GitHub-Release | Semantic-Release-GitHub-Plugin |
| ZIP als Release-Asset | Asset-Upload mit identischer Versionsvariable |
| kein unvollständiges Release | abhängige Schritte und harte Fehlerbehandlung |

## Offene Punkte

Keine. Die oben genannten Entscheidungen sind innerhalb der im Inventar
festgestellten Randbedingungen gewählt und sollen bei der Implementierung als
verbindliche Vorgaben gelten.
