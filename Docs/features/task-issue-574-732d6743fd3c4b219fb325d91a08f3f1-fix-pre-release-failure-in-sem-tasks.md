# Tasks: Fix Pre-Release-Failure in semantic-release (Issue #574)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Workflow `staging-ci.yml` | `node-version: '22'` → `'26'` im Step `Setup Node.js` des `version`-Jobs (Z. 229–233) | Erledigt | `python -c "yaml.safe_load"` OK; `npm run test:release-version` 26/26 grün |
| 2 | Workflow `staging-ci.yml` | `RESOLVE_DRY_RUN: "true"` in der `env`-Sektion des Steps `Determine next version` ergänzen (Z. 250–252) | Erledigt | `python -c "yaml.safe_load"` OK; `npm run test:release-version` 26/26 grün |
| 3 | Workflow `staging-ci.yml` | Exit-Code-Auswertung im Step `Determine next version`: `$PSNativeCommandUseErrorActionPreference = $false` vor dem `npx`-Aufruf, `$LASTEXITCODE` erfassen, bei ≠ 0 `::error::` mit Exit-Code und escapeter Tool-Ausgabe ausgeben und Step fehlschlagen lassen (Z. 252–269) | Erledigt | `python -c "yaml.safe_load"` OK; `npm run test:release-version` 26/26 grün |
| 4 | Workflow `staging-ci.yml` | Step `Setup Node.js` (`actions/setup-node@v7`, `node-version: '26'`, `cache: npm`) im Job `build-and-test` vor `Install dependencies` einfügen (Z. 114) | Erledigt | `python -c "yaml.safe_load"` OK; `npm run test:release-version` 26/26 grün |
| 5 | Workflow `release.yml` | `node-version: 22` → `26` im Step `Set up Node.js` (Z. 33–37); keine sonstigen Zeilen ändern (Struktur-Tests) | Erledigt | `python -c "yaml.safe_load"` OK; `npm run test:release-version` 26/26 grün (inkl. beider `release.yml`-Struktur-Tests) |
| 6 | Workflow `pr-staging-ci.yml` | Step `Setup Node.js` (`actions/setup-node@v7`, `node-version: '26'`, `cache: npm`) im Job `build-and-test` vor `Install dependencies` einfügen (Z. 125) | Erledigt | `python -c "yaml.safe_load"` OK; `npm run test:release-version` 26/26 grün |
| 7 | Tooling-Konfiguration | `engines.node` in `package.json` von `"22.x"` auf `">=26.13"` anheben (Z. 4–6) | Erledigt | `npm run test:release-version` 26/26 grün (package.json wird von npm geparst) |
| 8 | Tooling-Konfiguration | `packages[""].engines.node` in `package-lock.json` identisch angleichen (Z. 15–17) | Erledigt | `npm run test:release-version` 26/26 grün; Diff-Review |
| 9 | Dokumentation | `docs/development.md` Z. 23: `Node.js 22.x` → `Node.js 26.x` | Erledigt | Diff-Review; Volltextsuche: keine Node-22-Verweise mehr |
| 10 | Dokumentation | `README.md`: Badge `node-22.x` → `node-26.x` (Z. 7), Tech-Stack `Node.js 22.x` → `26.x` (Z. 42), Voraussetzung `Node.js **22.x**` → `**26.x**` (Z. 76) | Erledigt | Diff-Review; Volltextsuche: keine Node-22-Verweise mehr |
| 11 | Dokumentation | `CI-CD.md` Z. 100: „Node 22" → „Node 26" | Erledigt | Diff-Review |
| 12 | Dokumentation | `Docs/help/systemverwaltung-und-setup/bereitstellung.md` Z. 36: „Node 22" → „Node 26" | Erledigt | Diff-Review |
| 13 | Dokumentation | `Docs/screenshot-generation.md` Z. 8: `Node.js` „(22.x)" → „(26.x)" | Erledigt | Diff-Review |
| 14 | Verifikation | YAML-Syntax der geänderten Workflows (`staging-ci.yml`, `release.yml`, `pr-staging-ci.yml`) validieren | Erledigt | `python -c "import yaml,sys; [yaml.safe_load(open(f)) for f in sys.argv[1:]]"` → „YAML OK" für alle drei Dateien |
| 15 | Verifikation | `npm run test:release-version` ausführen — Erwartung: 26/26 grün (inkl. der beiden `release.yml`-Struktur-Tests) | Erledigt | 26/26 Tests bestanden (pass 26, fail 0), inkl. `uses one repository-wide release concurrency group` und `checks out the release tag before repairing an existing release asset` |
| 16 | Abnahme | Nach Merge: Pre-Release-Workflow auf `staging` erneut ausführen und prüfen, dass der `version`-Job unter Node 26 erfolgreich läuft (Issue-Vorgabe) | Offen | Erfordert Merge nach `staging` — nicht lokal durchführbar |
