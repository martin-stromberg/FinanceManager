# Umsetzungsplan: Fix Pre-Release-Failure in semantic-release (Issue #574)

## Übersicht

Der `version`-Job des Workflows **Pre-Release** (`.github/workflows/staging-ci.yml`) schlägt fehl, weil die gepinnte `semantic-release@26.0.1` Node `>=26.13` verlangt, der Job aber Node `22` einrichtet; zusätzlich wertet der Step `Determine next version` den Exit-Code des `npx semantic-release`-Aufrufs nicht aus. Umgesetzt wird eine einheitliche Node-26-Basis über `staging-ci.yml`, `release.yml`, `package.json`/`package-lock.json` und alle dokumentierenden Stellen hinweg, die Härtung der Exit-Code-Behandlung im Dry-Run-Step sowie die Konsolidierung des Dry-Runs auf `dryRunPlugins` via `RESOLVE_DRY_RUN`. Reine CI-/Tooling-Änderung — kein Anwendungscode, kein Datenmodell, keine UI.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Release-Versionsermittlung je Workflow-Lauf: `npx semantic-release --dry-run --no-ci --branches staging` im `version`-Job von `staging-ci.yml` sowie die semantic-release-Aufrufe in `release.yml` (`node scripts/resolve-release-version.mjs`, `npm run release`) |
| Auslösende Ereignisse | Push auf `staging` (Workflow `Pre-Release`); Push auf `main` bzw. Tag `v*.*.*` (Workflow `Release`). Keine Nutzeraktion in der Anwendung |
| Erwartete Wirkung | `semantic-release` läuft unter Node `>=26.13`; der `version`-Job liefert entweder `changed=true`/`version=<X.Y.Z>` (mit `rc_tag`/`rc_version`) oder legitimes `changed=false` (nur bei Exit-Code 0 ohne Versionsausgabe); Exit-Code ≠ 0 → sichtbarer Step-Fehlschlag mit `::error::` und Tool-Ausgabe; kein `EBADENGINE` bei `npm ci` |
| Umgang mit fehlenden Objekten | Keine fachlichen Objekte betroffen. Laufzeitvoraussetzung Node 26 ist verifiziert verfügbar: `actions/setup-node@v7` löst `node-version: '26'` auf die neueste 26.x auf (26.13.0 ist seit 28.10.2026 Active LTS; der Major-Pin wählt nie Prereleases) |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein Downgrade von `semantic-release@26.0.1` bzw. keine Änderung an den gepinnten `devDependencies` | Anwender-Entscheidung: Hochrüstung der Node-Basis statt Tool-Downgrade | bestätigt (Anwender, 22.11.2026) |
| 2 | Keine funktionalen Änderungen an `release.config.js` (`releasePlugins`/`dryRunPlugins`, `branches`, `tagFormat`), kein Umbau des RC-Suffix-Mechanismus, keine Änderung an `verify-release-version.cjs` oder der Asset-Repair-Logik in `resolve-release-version.mjs` | Der `RESOLVE_DRY_RUN`-Schalter existiert bereits; die Anforderung verlangt nur, ihn im Workflow zu setzen | bestätigt (requirement.md) |
| 3 | Keine Änderungen an .NET-/Blazor-Code, Datenmodell, UI oder .NET-Tests | Reine CI-/Infrastruktur-Anforderung | bestätigt (requirement.md) |
| 4 | Composite-Action `.github/actions/build-and-package` erhält **kein** `setup-node` | Sie führt nur `node scripts/generate-update-manifest.mjs` aus — kein `npm`/`npx`, daher greift `engines` nicht; das Skript nutzt ausschließlich Node-Builtins und läuft auf jedem aktuellen Runner-Node. Änderung wäre Aufwand ohne Wirkung | bestätigt (Anwender, 22.11.2026 — Ausschlüsse vorgezeigt, keine weiteren Antworten gewünscht) |
| 5 | Kein `.nvmrc`/`.node-version` und kein `node-version-file` | Version bleibt als expliziter `node-version`-Pin je Workflow dokumentiert (etablierte Konvention, siehe Designentscheidungen) | bestätigt (Anwender, 22.11.2026 — Ausschlüsse vorgezeigt, keine weiteren Antworten gewünscht) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| `engines.node`-Format in `package.json` | `">=26.13"` | Spiegelt exakt die deklarierte Mindestanforderung von `semantic-release@26.0.1` — die tatsächlich verletzte Bedingung, die den Ausfall verursacht hat. `"26.x"` würde 26.0–26.12 fälschlich erlauben (semantic-release verweigert dort den Dienst). Ein Major-Deckel (`<27`) ist nicht erforderlich, weil die Workflows die Hauptversion ohnehin auf `26` pinnen und npm für dieses private Release-Tooling keine Runtime-Garantien braucht. |
| `RESOLVE_DRY_RUN` im Step `Determine next version` | Setzen: `RESOLVE_DRY_RUN: "true"` in der `env`-Sektion des Steps | Der Aufruf ist ein reiner Dry-Run. `dryRunPlugins` lädt nur `@semantic-release/commit-analyzer` mit identischen `releaseRules` — die nächste Version wird identisch berechnet, und die Zeile `The next release version is X.Y.Z` wird vom semantic-release-Core im Dry-Run unabhängig von den übrigen Plugins ausgegeben (verifiziert: `resolve-release-version.mjs` fährt exakt dieses Muster und parst dieselbe Ausgabe, Z. 79/70–93). Entfällt: `verify-release-version.cjs` (ohnehin no-op im Dry-Run) und `@semantic-release/github` inkl. `verifyConditions` mit `GITHUB_TOKEN`-Zugriff → schneller, kleinere Fehleroberfläche, konsistent zum Referenzverhalten. Entscheidung vom Anwender an die Umsetzung delegiert und damit getroffen. |
| Exit-Code-Auswertung im pwsh-Step | `$PSNativeCommandUseErrorActionPreference = $false` vor dem `npx`-Aufruf setzen, danach `$LASTEXITCODE` prüfen | Unter pwsh ≥ 7.4 wirft ein nativer Exit-Code ≠ 0 bei `$ErrorActionPreference='Stop'` (Actions-Kontext) eine Exception — die explizite `if`-Auswertung würde sonst nie erreicht werden (genau dieser Mechanismus hat den aktuellen Fehlschlag überhaupt erst sichtbar gemacht). Das Deaktivieren der Preference-Variable nur für diesen Aufruf plus expliziter `$LASTEXITCODE`-Prüfung entspricht dem Muster des Steps `Check out release tag for asset repair` in `release.yml` (Z. 68–82) und der Semantik von `runSemanticReleaseDryRun` in `resolve-release-version.mjs` (wirft bei `status !== 0`). |
| `EBADENGINE`-Behandlung in `build-and-test`-Jobs | `actions/setup-node@v7` mit `node-version: '26'` und `cache: npm` in `staging-ci.yml` (vor Z. 114) **und** `pr-staging-ci.yml` (vor Z. 125) ergänzen | Nach der `engines`-Anhebung liefe `npm ci` dort mit Runner-Default-Node außerhalb der deklarierten Anforderung — nur Warnung (kein `engine-strict`), aber bewusstes Tolerieren von `EBADENGINE`-Rauschen kann echte Engine-Probleme später überdecken. Die Ergänzung stellt alle npm-Aufrufe des Repos auf die deklarierte Basis, kostet ~10–20 s Setup und beschleunigt über `cache: npm` die Installation. Gegenüberstellung „ausklammern als Follow-up" verworfen, weil der Fix trivial ist und derselbe Change thematisch „Node-Basis des Repos" ohnehin betrifft. |
| Node-Versionsangabe in den Workflows | Explizite Pins `node-version: '26'` (bzw. `26` im unquotierten Stil von `release.yml`) statt `node-version-file: package.json` | Entspricht der bisherigen Konvention (bewusst identische Pins je Workflow-Datei); `engines` mit Range `>=26.13` wäre als `node-version-file` ohnehin ungeeignet, weil setup-node Ranges auflöst und dabei semantisch andere Werte installieren könnte als gepinnt. |
| Dokumentationsumfang | Zusätzlich zu `docs/development.md` werden **alle** gefundenen Node-22-Verweise angeglichen: `README.md` (Badge Z. 7, Tech-Stack Z. 42, Voraussetzungen Z. 76), `CI-CD.md` (Z. 100), `Docs/help/systemverwaltung-und-setup/bereitstellung.md` (Z. 36), `Docs/screenshot-generation.md` (Z. 8) sowie der `engines`-Mirror in `package-lock.json` (Z. 16) | Die Bestandsaufnahme nannte nur `engines` + `development.md`; die Volltextsuche zeigt weitere veraltete Stellen — u. a. ein öffentlich sichtbares `node-22.x`-Badge und die Anwenderdoku zur Bereitstellung. „Gesamter Scope in einem Change" verlangt Konsistenz; jede Stelle ist eine einzeilige Textkorrektur ohne Risiko. Bei Abweichung kann `/review-plan` die Zusatzstellen entfernen. |

## Programmabläufe

### Pre-Release: Versionsermittlung auf `staging` (Job `version` in `staging-ci.yml`)

1. `actions/checkout@v7` (unverändert, `fetch-depth: 0`, `fetch-tags: true`).
2. `actions/setup-node@v7` richtet Node `26` ein (statt `22`), `cache: npm` unverändert.
3. `npm ci` installiert die gepinnten devDependencies — `semantic-release@26.0.1` findet jetzt eine unterstützte Node-Version (`>=26.13`).
4. Step `Determine next version` (`id: semver`, `shell: pwsh`):
   a. `env` enthält zusätzlich `RESOLVE_DRY_RUN: "true"` → `release.config.js` lädt `dryRunPlugins` (nur `commit-analyzer`).
   b. Im Skript wird vor dem Aufruf `$PSNativeCommandUseErrorActionPreference = $false` gesetzt.
   c. `npx semantic-release --dry-run --no-ci --branches staging` wird ausgeführt; Ausgabe und `$LASTEXITCODE` werden erfasst und die Ausgabe wie bisher geloggt.
   d. Exit-Code ≠ 0 → `::error::`-Annotation mit Exit-Code und kompletter Tool-Ausgabe (für die Workflow-Command-Syntax escaped: `%` → `%25`, CR → `%0D`, LF → `%0A`), anschließend Step-Fehlschlag (`throw`/`exit 1`). Es werden **keine** Outputs geschrieben.
   e. Exit-Code 0 → unveränderte Regex-Auswertung `The next release version is (...)`: Match → `changed=true`, `version=<X.Y.Z>`; kein Match → legitimes `changed=false` („No new release version determined.").
5. Step `Determine RC number` (unverändert, nur bei `changed == 'true'`) → `rc_tag`, `rc_version`.
6. Job `prerelease` (unverändert, nur bei `changed == 'true'`).

Beteiligte Komponenten: `.github/workflows/staging-ci.yml`, `release.config.js` (nur via `RESOLVE_DRY_RUN` gesteuert, keine Änderung), `scripts/resolve-release-version.mjs` (Referenzverhalten, keine Änderung).

### Release: Versionsermittlung und Veröffentlichung auf `main` (Job `release` in `release.yml`)

1. `actions/setup-node@v7` richtet Node `26` ein (statt `22`).
2. `npm ci` → keine `EBADENGINE`, `semantic-release@26.0.1` läuffähig.
3. `node scripts/resolve-release-version.mjs` führt intern `semantic-release --dry-run` aus — unverändert (hat Exit-Code-Behandlung und `RESOLVE_DRY_RUN` bereits korrekt).
4. `npm run release` (echtes Release) läuft unter Node 26 — unverändert.

Beteiligte Komponenten: `.github/workflows/release.yml`, `scripts/resolve-release-version.mjs`, `package.json`.

### Build-und-Test-Jobs mit npm (Nebenbefund)

1. `staging-ci.yml` Job `build-and-test`: neuer Step `Setup Node.js` (`actions/setup-node@v7`, `node-version: '26'`, `cache: npm`) vor `Install dependencies` (`npm ci`) und `Install Playwright Browsers` (`npx playwright@1.64.1`).
2. `pr-staging-ci.yml` Job `build-and-test`: analoger Step vor `Install dependencies` (Z. 125).

Beteiligte Komponenten: `.github/workflows/staging-ci.yml`, `.github/workflows/pr-staging-ci.yml`.

## Neue Klassen

Keine — es werden ausschließlich YAML-, JSON- und Markdown-Dateien geändert.

## Änderungen an bestehenden Klassen

Keine C#-Klassen. Die Änderungen betreffen Workflow-/Konfigurationsartefakte:

### `.github/workflows/staging-ci.yml` (Workflow)

- **Job `build-and-test`:** Neuer Step `Setup Node.js` (`actions/setup-node@v7`, `node-version: '26'`, `cache: npm`) vor `Install dependencies` (akt. Z. 114).
- **Job `version`, Step `Setup Node.js` (Z. 229–233):** `node-version: '22'` → `'26'`.
- **Job `version`, Step `Determine next version` (Z. 239–269):**
  - `env`: `RESOLVE_DRY_RUN: "true"` ergänzen.
  - Skript: `$PSNativeCommandUseErrorActionPreference = $false` vor dem `npx`-Aufruf; `$LASTEXITCODE` direkt nach dem Aufruf erfassen; bei ≠ 0 `::error::` mit Exit-Code und escapeter Tool-Ausgabe ausgeben und den Step fehlschlagen lassen; bei 0 bisherige Regex-/Output-Logik unverändert.

### `.github/workflows/release.yml` (Workflow)

- **Step `Set up Node.js` (Z. 33–37):** `node-version: 22` → `26` (unquotierter Stil der Datei beibehalten). Keine weiteren Änderungen — insbesondere werden die Zeilen unverändert bleiben, die die beiden Workflow-Struktur-Tests in `resolve-release-version.test.mjs` assertieren (Concurrency-Gruppe Z. 17–19, Asset-Repair-Step Z. 60–82).

### `.github/workflows/pr-staging-ci.yml` (Workflow)

- **Job `build-and-test`:** Neuer Step `Setup Node.js` (`actions/setup-node@v7`, `node-version: '26'`, `cache: npm`) vor `Install dependencies` (Z. 125).

### `package.json` / `package-lock.json` (Tooling-Konfiguration)

- `engines.node`: `"22.x"` → `">=26.13"` (`package.json` Z. 4–6).
- `packages[""].engines.node` in `package-lock.json` (Z. 15–17) identisch angleichen (manuell oder per `npm install --package-lock-only`).

### Dokumentation

- `docs/development.md` Z. 23: `Node.js 22.x` → `Node.js 26.x`.
- `README.md`: Badge `node-22.x` → `node-26.x` (Z. 7), `Node.js 22.x` → `Node.js 26.x` (Z. 42), `Node.js **22.x**` → `Node.js **26.x**` (Z. 76).
- `CI-CD.md` Z. 100: „Node 22" → „Node 26".
- `Docs/help/systemverwaltung-und-setup/bereitstellung.md` Z. 36: „Node 22" → „Node 26".
- `Docs/screenshot-generation.md` Z. 8: „(22.x)" → „(26.x)".

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Exit-Code des `npx semantic-release`-Aufrufs (`staging-ci.yml`, Step `Determine next version`) | Nur Exit-Code 0 darf in die Versions-Regex-Auswertung laufen; „kein Versionsmatch" ist nur bei Exit-Code 0 legitim | Exit-Code ≠ 0 → `::error::` mit Exit-Code und vollständiger Tool-Ausgabe, Step- und damit Job-Fehlschlag, keine `GITHUB_OUTPUT`-Schreibung |
| pwsh-Laufzeitverhalten | `$PSNativeCommandUseErrorActionPreference = $false` vor dem nativen Aufruf setzen, damit die explizite `$LASTEXITCODE`-Auswertung erreichbar bleibt | Ohne die Deaktivierung würde pwsh ≥ 7.4 unter `Stop`-Preference bei Exit-Code ≠ 0 vorzeitig eine Exception werfen und die geplante `::error::`-Ausgabe unterdrücken |
| `node-version`-Auflösung | Der Pin `'26'` muss auf eine Version `>=26.13` auflösen (verifiziert: aktuell 26.13.x Active LTS) | Löste der Pin je < 26.13 auf (nicht erwartet), wäre als Fallback eine exakte Pin-Version wie `'26.13'` zu setzen |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `node-version` in `staging-ci.yml` (`version`-Job) und `release.yml` | Workflow-Pin | `'26'` bzw. `26` | Node-Basis für semantic-release `>=26.13` |
| `node-version` in `staging-ci.yml` und `pr-staging-ci.yml` (`build-and-test`-Jobs, neu) | Workflow-Pin | `'26'` | Einheitliche Node-Basis für `npm ci`/`npx playwright`, vermeidet `EBADENGINE` |
| `engines.node` in `package.json` (+ Lockfile-Mirror) | npm-Deklaration | `">=26.13"` | Deklarierte Node-Anforderung, wirkt auf `npm ci` |
| `RESOLVE_DRY_RUN` im Step `Determine next version` | Step-Env-Variable | `"true"` | Wählt `dryRunPlugins` in `release.config.js` (bestehender Schalter, Z. 85) |

## Seiteneffekte und Risiken

- **Dry-Run-Verhalten mit `RESOLVE_DRY_RUN=true`:** `@semantic-release/github` (inkl. `verifyConditions` mit `GITHUB_TOKEN`-Zugriff) und `verify-release-version.cjs` werden im Dry-Run nicht mehr geladen. Die Ausgabe `The next release version is X.Y.Z` bleibt identisch (Versionsermittlung läuft vollständig über `commit-analyzer`); gleichzeitig entfällt ein früher Token-/Netzwerk-Fehlerpfad, der bisher ebenfalls still auf `changed=false` gefallen wäre — mit der neuen Exit-Code-Prüfung doppelt abgesichert.
- **Workflow-Struktur-Tests:** Zwei Tests in `scripts/resolve-release-version.test.mjs` lesen `release.yml` direkt (Concurrency-Regex Z. 290–295, Asset-Repair-Step Z. 297–304). Die geplanten Änderungen berühren diese Zeilen nicht — die Tests müssen weiter grün sein; Verifikation über `npm run test:release-version`.
- **Lokale Entwicklungsumgebung:** Lokal installiert ist Node `v24.15.0` — unterhalb der neuen `engines`-Anforderung. `node --test` wertet `engines` nicht aus (Suite läuft unverändert grün); `npm install`/`npm ci` lokal erzeugt bis zum Node-Upgrade `EBADENGINE`-Warnungen (nicht blockierend). Die aktualisierte Dokumentation (`development.md`, `README.md`) weist auf Node 26 hin.
- **`build-and-package`-Composite-Action:** Nutzt weiter Runner-Default-Node für `generate-update-manifest.mjs` — bewusst unverändert (kein `npm`-Aufruf, `engines` greift nicht; Scope-Ausschluss 4).
- **`npx playwright@1.64.1` unter Node 26:** Playwright fordert weit niedrigere Node-Mindestversionen; kein Kompatibilitätsrisiko erwartet — wird durch den nächsten CI-Lauf empirisch bestätigt.
- **Kein `.npmrc`/`engine-strict` im Repo:** `engines`-Verletzungen bleiben Warnungen; das neue `setup-node` in den `build-and-test`-Jobs beseitigt die Warnquelle trotzdem vollständig.

## Umsetzungsreihenfolge

1. **`staging-ci.yml`: `version`-Job härtn und hochrüsten**
   - Voraussetzungen: Keine (nur YAML-Edit; Referenzmuster `release.yml` Z. 68–82 und `resolve-release-version.mjs` Z. 70–93 sind im Repo vorhanden).
   - Beschreibung: `node-version: '22'` → `'26'`; im Step `Determine next version` `RESOLVE_DRY_RUN: "true"` als Env ergänzen, `$PSNativeCommandUseErrorActionPreference = $false` vor dem `npx`-Aufruf setzen, `$LASTEXITCODE` auswerten (≠ 0 → `::error::` + Fehlschlag; 0 → bestehende Regex-Logik).

2. **`release.yml`: Node-Pin anheben**
   - Voraussetzungen: Keine.
   - Beschreibung: `node-version: 22` → `26`. Keine sonstigen Zeilen anfassen (Struktur-Tests).

3. **`setup-node` in den `build-and-test`-Jobs ergänzen**
   - Voraussetzungen: Keine.
   - Beschreibung: `staging-ci.yml` (vor `Install dependencies`, Z. 114) und `pr-staging-ci.yml` (vor `Install dependencies`, Z. 125) jeweils Step `Setup Node.js` mit `actions/setup-node@v7`, `node-version: '26'`, `cache: npm` einfügen. Namenskonvention der Dateien beibehalten („Setup Node.js").

4. **`engines` in `package.json` und `package-lock.json` anheben**
   - Voraussetzungen: Schritte 1–3 nicht erforderlich, aber inhaltlich konsistent zu halten.
   - Beschreibung: `package.json` `engines.node` → `">=26.13"`; denselben Wert in `package-lock.json` `packages[""].engines.node` eintragen (manuelle Ein-Zeilen-Korrektur ausreichend; `npm install --package-lock-only` ist Alternative, erzeugt aber lokal unter Node 24 eine `EBADENGINE`-Warnung).

5. **Dokumentationsstellen angleichen**
   - Voraussetzungen: Keine.
   - Beschreibung: `docs/development.md`, `README.md` (3 Stellen inkl. Badge), `CI-CD.md`, `Docs/help/systemverwaltung-und-setup/bereitstellung.md`, `Docs/screenshot-generation.md` auf Node 26 aktualisieren.

6. **Statische Verifikation**
   - Voraussetzungen: Schritte 1–5 abgeschlossen.
   - Beschreibung: YAML-Syntax der drei geänderten Workflows prüfen (z. B. `python -c "import yaml,sys; [yaml.safe_load(open(f)) for f in sys.argv[1:]]" .github/workflows/staging-ci.yml .github/workflows/release.yml .github/workflows/pr-staging-ci.yml` — `nul`-Umleitungen vermeiden, siehe globale Regeln); `npm run test:release-version` ausführen (erwartet: 26/26 grün); Diff-Review der geänderten Blöcke. `npx semantic-release --dry-run` lokal ist **nicht** vorgesehen (lokal Node 24 < 26.13, kein `GITHUB_TOKEN`; Verifikation erfolgt über den CI-Lauf).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| — | — | Keine neuen automatisierten Tests geplant. Der geänderte Fehlerpfad ist reine Workflow-Shell-Logik, für die im Repo keine Testinfrastruktur existiert; die bestehende Suite deckt die Workflow-Struktur bereits als Regression-Guard ab. Der Nachweis läuft über statische Verifikation und den realen Pre-Release-Lauf (Issue-Vorgabe). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `uses one repository-wide release concurrency group` (`resolve-release-version.test.mjs` Z. 290–295) | **Keine Anpassung nötig — Kontrolllauf:** assertiert Zeilen in `release.yml`, die unverändert bleiben; muss nach der Änderung weiter grün sein |
| `checks out the release tag before repairing an existing release asset` (Z. 297–304) | **Keine Anpassung nötig — Kontrolllauf:** dito; der `node-version`-Pin liegt außerhalb der assertierten Regex-Muster |
| Übrige 24 Tests der `test:release-version`-Suite | Nicht betroffen — kein Skript-Code geändert |

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich: Die Anforderung berührt keinen über UI oder Nutzeraktion erreichbaren Ablauf — es gibt weder UI-Änderungen noch Anwendungscode. Die „Benutzer" der geänderten Abläufe sind die GitHub-Actions-Läufe selbst; der primäre Funktionsnachweis ist der vom Issue vorgegebene reale Wiederlauf des Pre-Release-Workflows auf `staging` (siehe Akzeptanzszenarien). Playwright-E2E-Tests wären hier ohne Prüfgegenstand.

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| — | Keine bestehenden E2E-Tests betroffen |

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: neue RC-Version wird ermittelt | Merge dieses Changes nach `staging` bzw. Push eines release-relevanten Commits auf `staging` | Workflows auf Node 26, `engines.node: ">=26.13"` | Pre-Release-Workflow läuft: `Setup Node.js` installiert Node ≥ 26.13 → `npm ci` ohne `EBADENGINE` → `Determine next version` führt Dry-Run mit `dryRunPlugins` aus (Exit 0, Versionszeile) → `changed=true`, `version=X.Y.Z` → RC-Step → `prerelease`-Job | Job `version` grün mit Outputs; RC-Release `vX.Y.Z-rc.N` erstellt; im Log sichtbar: Node-Version `26.x` und `The next release version is X.Y.Z` | Realer Workflow-Lauf auf `staging` (Issue-Vorgabe) |
| 2 | Legitimer Fall „keine neue Version" | Push auf `staging` ohne release-relevante Commits (z. B. nur `docs:`/`chore:`) | Wie Szenario 1 | Dry-Run endet mit Exit-Code 0, aber ohne Versionszeile | `changed=false`, Job `version` grün mit `No new release version determined.`, `prerelease`-Job wird übersprungen | Realer Workflow-Lauf bzw. erster Merge-Lauf ohne releasable Commits |
| 3 | Tool-Fehler schlägt sichtbar fehl | Semantic-release-Aufruf scheitert (z. B. Netzwerkfehler, kaputtes `GITHUB_TOKEN`, zukünftige Versionskonflikte) | Wie Szenario 1 | Dry-Run endet mit Exit-Code ≠ 0 → explizite Auswertung greift | `::error::`-Annotation mit Exit-Code und Tool-Ausgabe; Step und Job `version` schlagen fehl — **kein** stilles `changed=false`, kein falscher Eindruck „kein Release nötig" | Code-Review des geänderten Blocks (lokal nicht sinnvoll provozierbar); optional: gezielter Negativtest in einem Testlauf |
| 4 | Release-Workflow unverändert funktionsfähig | Push auf `main` oder Tag `v*.*.*` (bei nächster Promotion) | `release.yml` mit Node 26 | `setup-node` → `npm ci` → `resolve-release-version.mjs` (Dry-Run) → ggf. `npm run release` | Alle Schritte laufen unter Node 26 ohne Engine-Fehler; `release_kind`/`release_action`-Logik unverändert | Realer Lauf bei nächster staging→main-Promotion; lokale Node-Testsuite grün |
| 5 | Lokale statische Verifikation | Entwickler führt Prüfschritte aus | Repo-Stand mit allen Änderungen | YAML-Syntaxcheck der drei Workflows; `npm run test:release-version`; Diff-Review | YAML valide; 26/26 Node-Tests grün (inkl. der beiden `release.yml`-Struktur-Tests); `engines`/`node-version`/Dokus konsistent auf Node 26 | Lokale Kommandoausgaben |

## Offene Punkte

Keine.

Beide zuvor offenen Punkte wurden dem Anwender vorgezeigt; der Anwender hat die Bearbeitung ohne Einwände fortsetzen lassen („Erledige bitte die Aufgabe" — keine weiteren Antworten gewünscht, 22.11.2026). Es gelten die empfohlenen Vorschläge:

1. Erweiterter Dokumentationsumfang (README inkl. Badge, `CI-CD.md`, `bereitstellung.md`, `screenshot-generation.md`, `package-lock.json`-Mirror) → im Change mitgenommen.
2. Gezielter Negativtest des `::error::`-Pfads → nicht erforderlich; Nachweis über den realen Pre-Release-Wiederlauf laut Issue-Vorgabe.
