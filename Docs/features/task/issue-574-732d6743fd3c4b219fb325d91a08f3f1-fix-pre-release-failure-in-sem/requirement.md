# Übersetzte Anforderung

## Fachliche Zusammenfassung

Der Job `version` des Workflows **Pre-Release** (`.github/workflows/staging-ci.yml`) schlägt im Step `Determine next version` fehl, weil die gepinnte `semantic-release@26.0.1` Node.js `>=26.13` verlangt, der Job aber über `actions/setup-node@v7` Node `22` einrichtet (Fehlermeldung: `[semantic-release]: node version >=26.13 is required. Found v22.23.5.`). Zu korrigieren ist die Node.js-Versionsbasis der gesamten Release-Infrastruktur: `node-version` in `staging-ci.yml` und `release.yml` ist auf Node 26 anzuheben (vom Anwender entschieden: Hochrüstung, kein Downgrade von semantic-release), `engines.node` in `package.json` sowie die dokumentierte Entwicklungsvoraussetzung in `docs/development.md` sind anzugleichen. Zusätzlich ist die Fehlerbehandlung des Dry-Run-Steps zu härtensoden: ein Exit-Code ≠ 0 des `npx semantic-release`-Aufrufs darf nicht mehr als „keine neue Release-Version" interpretiert werden, sondern ist mit `::error::` samt Tool-Ausgabe als Fehler zu melden.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Release-Versionsermittlung je Workflow-Lauf — konkret der `semantic-release --dry-run`-Aufruf im `version`-Job des Pre-Release-Workflows (`staging`-Push) sowie die analogen semantic-release-Aufrufe im Release-Workflow (`main`-Push / manueller `v*.*.*`-Tag) über `scripts/resolve-release-version.mjs` und `npm run release`.
- **Auslösende Ereignisse:** Push auf den Branch `staging` (Workflow `Pre-Release`); Push auf `main` bzw. Push eines `v*.*.*`-Tags (Workflow `Release`). Kein Nutzer-Trigger in der Anwendung — reine CI-/Infrastruktur-Ereignisse.
- **Erwartete Wirkung:** `semantic-release --dry-run` läuft unter einer unterstützten Node-Version erfolgreich durch und der `version`-Job erzeugt entweder die Outputs `changed=true`/`version=<X.Y.Z>` (mit nachgelagertem `rc_tag`/`rc_version`) oder den legitimen Fall „keine neue Version erforderlich" (`changed=false` bei Exit-Code 0 ohne Versionsausgabe). Ein Tool-/Laufzeitfehler (Exit-Code ≠ 0) lässt den Step und damit den Job sichtbar fehlschlagen (`::error::` mit Tool-Ausgabe) statt still als `changed=false` weiterzulaufen und fälschlich „kein Release erforderlich" zu signalisieren. `npm ci` meldet keine `EBADENGINE`-Warnungen mehr, weil `engines.node` zu den installierten devDependencies passt.
- **Umgang mit fehlenden Objekten:** Keine fachlichen Objekte (Entitäten, Konfigurationen, Sitzungen) betroffen — es geht ausschließlich um die Laufzeitumgebung der Workflows. Voraussetzung: Node 26 muss über `actions/setup-node@v7` auf `ubuntu-latest`/`windows-latest` verfügbar sein (zu verifizieren, siehe Offene Fragen).
- **Scope-Ausschlüsse:**
  - Keine Änderung an der gepinnten `semantic-release@26.0.1` bzw. den übrigen devDependencies (Upgrade-Strategie ist entschieden; ein semantic-release-Downgrade ist ausdrücklich nicht gewünscht).
  - Keine funktionalen Änderungen an `release.config.js` (Plugin-Listen `releasePlugins`/`dryRunPlugins`, `branches: ["main"]`, `tagFormat`) und keine Änderung am Release-/Prerelease-Konzept (RC-Suffix-Mechanismus, `verify-release-version.cjs`, Asset-Repair-Logik in `resolve-release-version.mjs`).
  - Keine Änderungen an .NET-/Blazor-Anwendungscode, Datenmodell, UI oder Tests der Anwendung.
  - Keine Umstellung anderer Workflows, die kein Node/semantic-release nutzen (`pr-staging-ci.yml`, `staging-to-main-promotion.yml`, `sync-staging-with-main.yml` u. a. verwenden nach Prüfung weder `setup-node` noch `npm`).

## Betroffene Klassen und Komponenten

Reine CI-/Release-Infrastruktur — keine C#-Klassen, keine UI-Komponenten, kein Datenmodell.

### Zu ändernde Artefakte

- `.github/workflows/staging-ci.yml` — Job `version` (ca. Z. 211–287):
  - Step `Setup Node.js` (Z. 229–233): `node-version: '22'` → `'26'`.
  - Step `Determine next version` (Z. 239–269): Der PowerShell-Block ruft `npx semantic-release --dry-run --no-ci --branches staging` auf und parst die Ausgabe per Regex `The next release version is ([0-9]+\.[0-9]+\.[0-9]+...)`. Er wertet `$LASTEXITCODE` **nicht** aus — genau hier entsteht die falsche „No new release version determined."-Meldung bei Tool-Fehlschlag. Um die explizite Exit-Code-Auswertung zu ergänzen: Exit-Code ≠ 0 → `::error::` mit Tool-Ausgabe und Step-Fehlschlag (`throw`/`exit 1`); Exit-Code 0 ohne Versionsmatch → legitimer `changed=false`-Fall.
  - Optional im selben Step: `env: RESOLVE_DRY_RUN: "true"` ergänzen (Nebenbefund, siehe Implementierungsansatz).
- `.github/workflows/release.yml` — Step `Set up Node.js` (Z. 33–37): `node-version: 22` → `26`. Betrifft beide nachgelagerten semantic-release-Aufrufe: `node scripts/resolve-release-version.mjs` (Step `Resolve release version`, Z. 47–58 — führt intern `semantic-release --dry-run` aus) und `npm run release` (Step `Create automatic GitHub release`, Z. 124–135 — echtes Release).
- `package.json` — `"engines": { "node": "22.x" }` (Z. 4–6) auf die neue Node-Basis anheben (z. B. `"26.x"` oder `">=26.13"`; Format in der Umsetzung festlegen, siehe Offene Fragen).
- `docs/development.md` — Voraussetzungsliste „Für release-nahe Aufgaben" (Z. 23): `Node.js 22.x` → `Node.js 26.x` (lokale Entwicklungsumgebung angleichen).

### Kontext-Artefakte (kein Code-Change zwingend, fachlich relevant)

- `release.config.js` — Z. 82–86: `plugins: process.env.RESOLVE_DRY_RUN === "true" ? dryRunPlugins : releasePlugins`. Im `version`-Job von `staging-ci.yml` ist `RESOLVE_DRY_RUN` aktuell **nicht** gesetzt; der Dry-Run lädt daher die volle `releasePlugins`-Liste (`@semantic-release/commit-analyzer`, `verify-release-version.cjs`, `@semantic-release/release-notes-generator`, `@semantic-release/github`) statt nur `dryRunPlugins`. Nicht ursächlich für den Fehler, aber inkonsistent zum Release-Workflow.
- `scripts/resolve-release-version.mjs` — `runSemanticReleaseDryRun` (Z. 70–93): Setzt `RESOLVE_DRY_RUN: "true"` bereits korrekt beim Spawn und wirft bei `result.status !== 0` einen Fehler — dies ist das Referenzverhalten für die geforderte Exit-Code-Behandlung, das `staging-ci.yml` bisher nicht abbildet.
- `scripts/verify-release-version.cjs` — `verifyRelease`-Hook in `releasePlugins`; toleriert Dry-Runs ohne `RELEASE_VERSION` (`context.options.dryRun`-Guard), daher funktionsneutral beim Plugin-Wechsel.
- `.github/actions/build-and-package/action.yml` — Step `Generate update manifest` (Z. ~120–135) nutzt runner-vorinstalliertes Node für `scripts/generate-update-manifest.mjs` (Kommentar: „Node is preinstalled on GitHub-hosted runners, so no separate setup-node step is needed"); ruft kein `npm ci`/semantic-release auf.
- `.github/workflows/staging-ci.yml` — Job `build-and-test`, Step `Install dependencies` (Z. 114–115): `npm ci` **ohne** `setup-node`, d. h. mit dem Runner-Default-Node. Nach der `engines`-Anhebung können hier `EBADENGINE`-Warnungen auftreten — im Umsetzungsschritt zu bewerten, ob dort ebenfalls `setup-node` ergänzt wird (kein `engine-strict`/`.npmrc` im Repo gefunden → Warnung, kein Abbruch; Annahme).

### Tests / Verifikation

- `scripts/resolve-release-version.test.mjs` / `scripts/generate-update-manifest.test.mjs` (`npm run test:release-version`) — bestehende Node-Tests der Release-Skripte; durch die Node-Hebung weiterlaufend, fachlich unverändert.
- Kein automatisierbarer Workflow-Test im Repo: Verifikation erfolgt durch erneuten Lauf des Pre-Release-Workflows auf `staging` (Outputs `changed`/`version`, Fehler-Pfad bei Exit-Code ≠ 0) — im Issue explizit als Abnahmeschritt genannt.

## Implementierungsansatz

- **Node-Basis anheben:** `node-version` in beiden `setup-node`-Steps auf `'26'` setzen und `engines.node` in `package.json` sowie die Voraussetzung in `docs/development.md` angleichen, damit `npm ci` ohne `EBADENGINE` läuft und lokale Reproduktion der Workflow-Umgebung möglich bleibt. Eine neue Klasse/ein Interface ist nicht erforderlich — es sind YAML-/JSON-Konfigurationsänderungen.
- **Exit-Code-Auswertung im Step `Determine next version`:** Die PowerShell-Logik um die Auswertung von `$LASTEXITCODE` unmittelbar nach dem `npx`-Aufruf erweitern: Bei ≠ 0 die komplette Tool-Ausgabe als `::error::` ausgeben und den Step fehlschlagen lassen; nur bei Exit-Code 0 die bisherige Regex-Auswertung durchführen (Versionsmatch → `changed=true`, sonst legitimes „kein Release"). Damit wird die Semantik an das bereits korrekte Verhalten von `runSemanticReleaseDryRun` in `resolve-release-version.mjs` angeglichen.
- **`RESOLVE_DRY_RUN` (Umsetzungsentscheidung, vom Anwender delegiert):** Empfehlung — `RESOLVE_DRY_RUN: "true"` im `Determine next version`-Step setzen. Begründung: Der Aufruf ist ein reiner Dry-Run; `dryRunPlugins` enthält nur den `commit-analyzer`, während `releasePlugins` zusätzlich `verify-release-version.cjs`, `release-notes-generator` und `@semantic-release/github` (inkl. `verifyConditions` mit `GITHUB_TOKEN`-Zugriff) lädt. Das ist konsistent zum bereits implementierten Verhalten von `resolve-release-version.mjs` (Z. 79) und reduziert die Fehleroberfläche des Dry-Runs. Alternativ bliebe der Status quo fachlich korrekt, solange `GITHUB_TOKEN` gesetzt ist — Entscheidung in der Planung dokumentieren.
- **Erweiterungspunkte/Abhängigkeiten:** Keine Anwendungs-Extension-Points betroffen. Interne Konsistenz zwischen drei Stellen herstellen: `staging-ci.yml`-`version`-Job, `release.yml`-`release`-Job und `package.json`-`engines` müssen auf derselben Node-Hauptversion stehen; `docs/development.md` dokumentiert dieselbe Basis für lokale Läufe.
- **Verfügbarkeits-Check:** Vor bzw. während der Umsetzung verifizieren, dass `actions/setup-node@v7` Node 26 (`>=26.13`) auflöst (ggf. exakte Version pinnen oder `check-latest` erwägen) und dass der `build-and-test`-Job mit Runner-Default-Node weiterhin sauber `npm ci` ausführt.

## Konfiguration

- Keine Anwendungs- oder Benutzerkonfiguration — ausschließlich CI-/Tooling-Konfiguration:
  - Node-Version je Workflow als `node-version`-Pinning in `actions/setup-node@v7` (Single Source je Workflow-Datei; bewusst zwei identische Pins in `staging-ci.yml` und `release.yml`).
  - Deklarierte Node-Anforderung über `package.json` → `engines.node` (wirkt auf `npm ci` in allen Jobs und lokal).
  - `RESOLVE_DRY_RUN` als Step-Env-Variable in `staging-ci.yml` (falls umgesetzt; Steuerung der Plugin-Liste in `release.config.js` bereits vorhanden).

## Offene Fragen

1. **Node-Verfügbarkeit:** Löst `actions/setup-node@v7` `node-version: '26'` bereits auf (`>=26.13` erforderlich), oder muss eine exakte Version (z. B. `26.13.x`) gepinnt bzw. `check-latest: true` gesetzt werden? — vor bzw. während der Umsetzung zu verifizieren.
2. **`engines`-Format:** Soll `engines.node` als `"26.x"` (Major-Pin, analog zum bisherigen `"22.x"`) oder als `">=26.13"` (exakte semantic-release-Anforderung) deklariert werden? — Umsetzungsdetail, beide Varianten erfüllen die Anforderung.
3. **`RESOLVE_DRY_RUN`:** Entscheidung in der Umsetzung treffen (Empfehlung oben: setzen) — vom Anwender explizit delegiert.
4. **`build-and-test`-Job:** Reicht der Runner-Default-Node für `npm ci` ohne `setup-node` aus (nur Warnungen, kein Abbruch), oder soll dort aus Konsistenzgründen ebenfalls `setup-node` mit Node 26 ergänzt werden? — Nebenbefund, in der Umsetzung zu bewerten; ohne `engine-strict` nicht blockierend.
5. **Abnahme:** Reicht als Nachweis der erfolgreiche Wiederlauf des Pre-Release-Workflows auf `staging` (Issue-Vorgabe), oder soll zusätzlich ein gezielter Negativtest des neuen Fehlerpfads (künstlicher Exit-Code ≠ 0) demonstriert werden?
