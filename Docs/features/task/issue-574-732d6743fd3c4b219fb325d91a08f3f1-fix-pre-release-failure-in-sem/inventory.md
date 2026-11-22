# Bestandsaufnahme: Fix Pre-Release-Failure in semantic-release (Issue #574)

Bestandsaufnahme der CI-/Release-Infrastruktur bezogen auf die Anforderung in
[requirement.md](requirement.md): Der `version`-Job des Workflows **Pre-Release**
(`.github/workflows/staging-ci.yml`) schlägt fehl, weil `semantic-release@26.0.1` unter
dem gepinnten Node 22 läuft, und sein `Determine next version`-Step wertet den Exit-Code
des Tools nicht aus. Der Scope ist rein infrastrukturell — es existieren keine
C#-Datenmodell-, Logik- oder UI-Klassen; die geforderten Detaildokumente wurden
entsprechend angepasst (`workflows.md` + `scripts.md` statt `models.md`/`logic.md`;
`enums.md`/`interfaces.md` entfallen, da es keine Enums/Contracts im Scope gibt).

## Zusammenfassung

- **Fehlstelle 1 (Node-Basis) ist genau wie beschrieben vorhanden:**
  `staging-ci.yml` Step `Setup Node.js` pinnt `node-version: '22'` (Z. 229–233),
  `release.yml` Step `Set up Node.js` pinnt `node-version: 22` (Z. 33–37),
  `package.json` deklariert `engines.node: "22.x"` (Z. 4–6) und `docs/development.md`
  fordert „Node.js 22.x" (Z. 23). Die gepinnte `semantic-release@26.0.1`
  (`package.json` Z. 16) verlangt Node `>=26.13` — der Versionskonflikt ist im Stand
  vollständig nachgebildet.
- **Fehlstelle 2 (fehlende Exit-Code-Auswertung) ist bestätigt:** Der Step
  `Determine next version` (`staging-ci.yml` Z. 239–269) parst die Tool-Ausgabe per
  Regex und wertet `$LASTEXITCODE` nirgends aus — ein Exit-Code ≠ 0 fällt still auf den
  `changed=false`-Pfad („No new release version determined."). Das korrekte
  Referenzverhalten existiert bereits in `scripts/resolve-release-version.mjs`
  (`runSemanticReleaseDryRun`, Z. 70–93: wirft bei `status !== 0` samt Tool-Ausgabe) und
  im selben Repo auch als pwsh-Muster im `release.yml`-Step `Check out release tag for
  asset repair` (Z. 68–82).
- **`RESOLVE_DRY_RUN`-Inkonsistenz ist bestätigt:** `release.config.js` wählt die
  Plugin-Liste über `process.env.RESOLVE_DRY_RUN === "true"` (Z. 85);
  `resolve-release-version.mjs` setzt die Variable beim Spawn (Z. 79), der
  `staging-ci.yml`-Dry-Run jedoch nicht — er lädt die volle `releasePlugins`-Liste
  (inkl. `verify-release-version.cjs` und `@semantic-release/github`).
- **Korrektur zur Anforderungsannahme:** `pr-staging-ci.yml` nutzt **doch** `npm ci`
  (Z. 125) und `npx playwright` (Z. 128) im Job `build-and-test` — nur ohne eigenes
  `setup-node`, also mit Runner-Default-Node. Ebenso `staging-ci.yml` `build-and-test`
  (Z. 114–118). Nach einer `engines.node`-Anhebung können dort `EBADENGINE`-Warnungen
  auftreten (kein `.npmrc`/`engine-strict` im Repo → Warnung, kein Abbruch).
- **Keine weiteren Node-Nutzer:** Die übrigen Workflows
  (`staging-to-main-promotion.yml`, `sync-staging-with-main.yml`, `verify-pr-source.yml`,
  `security-scan.yml`, `monthly-architecture-security-review.yml`,
  `weekly-nuget-review.yml`, `yearly-compliance-review.yml`) enthalten weder
  `setup-node` noch `npm`/`npx` (Volltextsuche). Die Composite-Action
  `build-and-package` nutzt Runner-vorinstalliertes Node nur für
  `generate-update-manifest.mjs` — ohne `npm ci`/semantic-release.
- **Test-Ausgangszustand:** `npm run test:release-version` → **26/26 Tests erfolgreich,
  0 Fehlschläge, 0 übersprungen** (Exit-Code 0). Zwei der Tests lesen `release.yml`
  direkt und assertieren Workflow-Struktur (Concurrency-Gruppe, Asset-Repair-Step) —
  relevante implizite Regression-Guards für die geplante Änderung. Die .NET-Suiten
  wurden nicht ausgeführt (nicht im Scope, siehe [tests.md](inventory/tests.md)).
- **Nebenbefunde:** Die in Kommentaren zitierte `ci-target-schema.md` existiert nicht im
  Repo (externe Referenz). Lokal läuft Node `v24.15.0` — selbst außerhalb des deklarierten
  `engines.node: "22.x"`, für die Node-Tests ohne semantic-release-Aufruf irrelevant.
  `node_modules` war zum Testzeitpunkt nicht installiert; die Suite benötigt es nicht.

## Details

- [Workflows](inventory/workflows.md) — Jobs/Steps von `staging-ci.yml` und
  `release.yml` mit den zu ändernden Stellen, Composite-Action `build-and-package`,
  Node-/npm-Prüfung aller übrigen Workflows
- [Skripte und Tooling-Konfiguration](inventory/scripts.md) — `package.json`,
  `release.config.js`, `scripts/resolve-release-version.mjs`,
  `scripts/verify-release-version.cjs`, `scripts/generate-update-manifest.mjs`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Lauf-Nachweis und Testinventar
  (Pflichtdokument)
