# Skripte und Tooling-Konfiguration — Bestandsaufnahme

Statt C#-Logikklassen betrifft die Anforderung Node-Skripte und Konfigurationsdateien der
Release-Infrastruktur. Diese Datei nimmt die Rolle von `logic.md`/`models.md` ein.
`enums.md` und `interfaces.md` entfallen — es gibt keine Enums oder Contracts im Scope.

## `package.json`

Datei: `package.json` (Repo-Root, 18 Zeilen)

| Schlüssel | Zeilen | Wert | Zweck |
|-----------|--------|------|-------|
| `name` | 2 | `finance-manager-release` | Privates Paket nur für Release-Tooling (`"private": true`) |
| `engines.node` | 4–6 | **`"22.x"`** | Deklarierte Node-Anforderung; wirkt auf `npm ci` (EBADENGINE bei Verletzung) — **zu ändernde Stelle** |
| `scripts.release` | 8 | `semantic-release` | Wird von `release.yml` Step `Create automatic GitHub release` (Z. 135) aufgerufen |
| `scripts.test:release-version` | 9 | `node --test scripts/resolve-release-version.test.mjs scripts/generate-update-manifest.test.mjs` | Node-Testsuite der Release-Skripte |
| `devDependencies` | 11–17 | `@semantic-release/commit-analyzer@13.0.1`, `@semantic-release/github@12.0.11`, `@semantic-release/release-notes-generator@14.1.1`, `conventional-changelog-conventionalcommits@9.3.1`, **`semantic-release@26.0.1`** | Alle gepinnt (exakte Versionen); `semantic-release@26.0.1` verlangt Node `>=26.13` — laut Anforderung bewusst nicht anzufassen |

Begleitend: `package-lock.json` vorhanden (`lockfileVersion: 3`). Kein `.npmrc`, kein
`engine-strict` im Repo gefunden — `engines`-Verletzungen sind Warnungen, keine Abbrüche.

## `release.config.js`

Datei: `release.config.js` (Repo-Root, CommonJS, 86 Zeilen) — wird von `semantic-release`
sowohl in `staging-ci.yml` (`npx semantic-release --dry-run --no-ci --branches staging`)
als auch in `release.yml` (`npm run release` und indirekt über
`scripts/resolve-release-version.mjs`) geladen.

| Element | Zeilen | Beschreibung |
|---------|--------|--------------|
| `releaseAssetPaths` / `releaseAssets` | 3–11 | Baut die Asset-Liste aus `RELEASE_ASSET_PATHS`, `RELEASE_ASSET_PATH` und `RELEASE_MANIFEST_PATH` für `@semantic-release/github` |
| `releasePlugins` | 13–53 | Volle Plugin-Liste: `@semantic-release/commit-analyzer` (Preset `conventionalcommits`, eigene `releaseRules`), `./scripts/verify-release-version.cjs`, `@semantic-release/release-notes-generator`, `@semantic-release/github` (mit `successComment: false`, `failComment: false`) |
| `dryRunPlugins` | 55–73 | Reduzierte Liste: nur `@semantic-release/commit-analyzer` mit identischer Regelkonfiguration |
| `module.exports` | 82–86 | `branches: ["main"]`, `tagFormat: "v${version}"`, **`plugins: process.env.RESOLVE_DRY_RUN === "true" ? dryRunPlugins : releasePlugins`** |

Befund zur Anforderung: Der `RESOLVE_DRY_RUN`-Schalter (Z. 85) existiert bereits, wird in
`staging-ci.yml` Step `Determine next version` aber **nicht** gesetzt — der Dry-Run lädt
dort die volle `releasePlugins`-Liste. `resolve-release-version.mjs` setzt die Variable
dagegen korrekt (s. u.). Der Kommentar Z. 75–81 erklärt, warum `staging` nicht als
Prerelease-Branch konfiguriert ist (RC-Suffix wird im Workflow ergänzt).

## `scripts/resolve-release-version.mjs`

Datei: `scripts/resolve-release-version.mjs` (ES-Modul, 345 Zeilen) — aufgerufen von
`release.yml` Step `Resolve release version` (Z. 58: `node scripts/resolve-release-version.mjs`).

Konstanten:

| Name | Zeilen | Inhalt |
|------|--------|--------|
| `VERSION_PATTERN` | 6 | Strenges `X.Y.Z(-prerelease)`-Regex ohne führende Nullen |
| `NEXT_RELEASE_PATTERN` | 7 | `The next release version is\s+(<semver>)` — Pendant zum Inline-Regex in `staging-ci.yml` Z. 259 |
| `AUTOMATIC_RELEASE_BRANCHES` | 10 | `["main"]` — `staging` läuft ausschließlich über `staging-ci.yml` |

Exportierte Funktionen:

| Funktion | Zeilen | Kurzbeschreibung |
|----------|--------|------------------|
| `parseManualTag(tagName)` | 12–23 | Validiert `vX.Y.Z`-Tags, wirft bei ungültigem Format |
| `parseNextReleaseVersion(output)` | 25–29 | Entfernt ANSI-Escapes, matcht `NEXT_RELEASE_PATTERN`, liefert Version oder `null` |
| `classifyWorkflowRef({refType, refName})` | 31–41 | `tag` → `{kind:"manual", version, tag}`; `branch`+`main` → `{kind:"automatic"}`; sonst Fehler |
| `releaseAssetName(version, runtimeIdentifier)` | 43–45 | `FinanceManager-v<version>-<rid>.zip` (Default `win-x64`) |
| `expectedReleaseAssetNames(version)` | 47–53 | `[win-x64-ZIP, linux-x64-ZIP, "update.json"]` |
| `releaseHasExpectedAsset(release, version)` | 55–60 | Prüft, ob alle erwarteten Assets `state === "uploaded"` und `size > 0` haben |
| `listGitHubReleases({repository, token, githubRequest})` | 150–174 | Paginiert die GitHub-Releases-API (100/Seite); `githubRequest` ist injizierbar (Testbarkeit) |
| `resolveReleaseVersion(environment, dependencies)` | 242–333 | Orchestriert die gesamte Entscheidung; schreibt Outputs `released`, `reason`, `version`, `tag`, `release_kind`, `release_action` nach `GITHUB_OUTPUT` |

Interne (nicht exportierte) Funktionen:

| Funktion | Zeilen | Kurzbeschreibung |
|----------|--------|------------------|
| `requireValue` | 62–68 | Pflicht-Env-Guard (`GITHUB_OUTPUT`, `GITHUB_REPOSITORY`, `GITHUB_TOKEN`) |
| **`runSemanticReleaseDryRun`** | 70–93 | **Referenzverhalten für die geforderte Exit-Code-Behandlung:** `spawnSync` auf `node_modules/semantic-release/bin/semantic-release.js --dry-run` mit `env: {..., CI: "true", RESOLVE_DRY_RUN: "true"}`; wirft bei `result.error` und bei **`result.status !== 0`** einen Fehler samt kompletter Tool-Ausgabe — genau die Semantik, die `staging-ci.yml` fehlt |
| `remoteTagExists(tag)` | 95–110 | `git ls-remote --exit-code --tags origin refs/tags/<tag>`; unterscheidet „existiert nicht" (Status 2) von echten Fehlern |
| `releaseApiUrl` / `githubRequest` / `getGitHubRelease` | 112–148 | GitHub-API-Helfer (Release-Lookup per Tag; `404` → `null`) |
| `writeOutputs` | 176–179 | Append `name=value`-Zeilen an `GITHUB_OUTPUT` |
| `createDefaultDependencies` | 181–190 | Dependency-Bundle für Injektion in Tests |
| `releaseVersion` / `incompleteReleases` | 192–217 | Asset-Repair-Erkennung; `incompleteReleases` ignoriert Prereleases explizit (Kommentar Z. 200–207 dokumentiert den Produktionsvorfall auf msTools.Updater) |
| `releaseOutputs` / `repairRelease` | 219–240 | Output-Objekt-Fabriken (`release_action: "none"` bzw. `"upload-existing"`) |
| `main` | 335–345 | Einstieg bei direktem Aufruf (`isMainModule`-Guard); Fehler → `console.error` + `process.exitCode = 1` |

Ablauf `resolveReleaseVersion` (Z. 242–333):

1. `classifyWorkflowRef` → manueller Tag-Pfad (Z. 257–292): Release existiert + Assets
   komplett → `released=false, reason=existing-release`; Assets fehlen → Repair
   (`upload-existing`); kein Release → `released=true, reason=manual-tag, action=create`.
2. Automatischer Pfad (ab Z. 294): `runSemanticReleaseDryRun()` → `parseNextReleaseVersion`;
   bei Version: vorhandenes komplettes Release oder vorhandener Remote-Tag → harter Fehler;
   fehlende Assets → Repair; sonst `released=true, reason=semantic-release, action=create`.
3. Keine neue Version: `incompleteReleases` → Repair des ersten unvollständigen stabilen
   Releases; sonst `released=false, reason="no-release"`.

## `scripts/verify-release-version.cjs`

Datei: `scripts/verify-release-version.cjs` (CommonJS, 19 Zeilen) —
`verifyRelease`-Plugin innerhalb von `releasePlugins` in `release.config.js` (Z. 31).

- Z. 3–8: Ohne `RELEASE_VERSION` kehrt der Hook im Dry-Run still zurück
  (`context.options.dryRun`-Guard), wirft sonst einen Fehler — daher funktionsneutral,
  falls der Dry-Run zwischen `releasePlugins` und `dryRunPlugins` wechselt.
- Z. 13–17: Vergleicht `context.nextRelease.version` mit `RELEASE_VERSION`; Mismatch → Fehler.

## `scripts/generate-update-manifest.mjs`

Datei: `scripts/generate-update-manifest.mjs` (ES-Modul, 110 Zeilen) — aufgerufen von
`.github/actions/build-and-package/action.yml` Z. 137 (`node scripts/generate-update-manifest.mjs`);
nicht im direkten Scope der Anforderung, aber Teil der `test:release-version`-Suite.

| Funktion | Zeilen | Kurzbeschreibung |
|----------|--------|------------------|
| `releaseAssetName` | 11–13 | `FinanceManager-v<version>-<rid>.zip` |
| `releaseMetadata` | 15–23 | Metadaten-Objekt für `release-metadata.json` |
| `sha256File` | 25–34 | SHA-256 einer Datei; wirft bei fehlender/leerer Datei |
| `createUpdateManifest` | 36–84 | Baut `update.json`-Struktur (Plattformen win-x64/linux-x64, `assetUrl` aus `tag` oder Default `v${version}`); validiert `version`, `releaseNotes`, `publishedAt`, `repository` |
| `writeUpdateManifest` | 86–90 | Schreibt das Manifest (Default `update.json`) |
| `main` | 92–105 | Liest `UPDATE_MANIFEST_PATH`, `RELEASE_VERSION`, `RELEASE_NOTES`, `RELEASE_PUBLISHED_AT`, `GITHUB_REPOSITORY`, `RELEASE_TAG`, `RELEASE_ASSET_DIRECTORY` aus der Umgebung |

## Sonstige Befunde

- **Kommentarverweis `ci-target-schema.md`:** Mehrere Kommentare (u. a. `release.config.js`
  Z. 78, `resolve-release-version.mjs` Z. 9, `build-and-package/action.yml` Z. 123,
  Workflow-Kommentare) verweisen auf Abschnitte einer `ci-target-schema.md`. Diese Datei
  existiert **nicht** im Repo (`docs/` und `Docs/` geprüft) — offenbar eine externe
  Referenz aus dem Softwareschmiede-Vorlagenprojekt. Keine Auswirkung auf die Anforderung,
  aber bei der Umsetzung nicht als lesbare Quelle verfügbar.
- **Lokale Toolchain:** `node_modules/` ist im analysierten Arbeitsstand nicht installiert
  (kein `npm ci` ausgeführt); die Node-Tests laufen dennoch, weil sie nur Repo-Skripte mit
  Node-Builtins importieren. Lokal installiert: Node `v24.15.0`, npm `11.10.0`, .NET SDK
  `11.0.100` — erfüllt weder das deklarierte `engines.node: "22.x"` noch die künftige
  Node-26-Basis; für die reinen Node-Tests ohne semantic-release-Aufruf irrelevant.
