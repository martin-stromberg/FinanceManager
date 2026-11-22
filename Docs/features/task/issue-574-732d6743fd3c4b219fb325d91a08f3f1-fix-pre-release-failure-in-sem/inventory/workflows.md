# Workflows — Bestandsaufnahme

CI-/Release-Workflows, die für die Anforderung (Node-Basis und Fehlerbehandlung der
Release-Versionsermittlung) relevant sind. Es existieren keine C#-Datenmodelle oder
Logikklassen im Scope; die „Verarbeitungseinheiten" sind hier Jobs und Steps der
GitHub-Actions-Workflows.

## `Pre-Release` — `.github/workflows/staging-ci.yml`

Trigger: `push` auf `staging` (Z. 6–9). Permissions: `contents: write`, `checks: write`
(Z. 15–17). Concurrency-Gruppe `staging-ci` mit `cancel-in-progress: true` (Z. 11–13).

### Job `detect-backmerge` (Z. 20–57)

- `runs-on: ubuntu-latest`; Output `is_backmerge`.
- Erkennt reine Back-Merges von `main` (Merge-Parent- und Tree-Diff-Prüfung); alle
  nachgelagerten Jobs werden bei `is_backmerge == 'true'` übersprungen.

### Job `static-checks` (Z. 59–97)

- `runs-on: windows-latest`, `timeout-minutes: 15`; `needs: detect-backmerge`.
- Steps: `actions/checkout@v7`, `actions/setup-dotnet@v6` (`10.0.x`), `dotnet restore`,
  `dotnet format --verify-no-changes --severity error`, Composite-Action
  `./.github/actions/security-scan`, statische Analyse via `dotnet build` mit
  `TreatWarningsAsErrors` und kuratierter `WarningsNotAsErrors`-Liste.
- Kein Node/npm-Bezug.

### Job `build-and-test` (Z. 99–209)

- `runs-on: windows-latest`, `timeout-minutes: 60`; `needs: detect-backmerge`.
- Step `Install dependencies` (Z. 114–115): `npm ci` — **ohne** `setup-node`-Step, läuft
  also mit dem Runner-vorinstalliertem Node. Relevanz: Nach einer `engines.node`-Anhebung
  in `package.json` können hier `EBADENGINE`-Warnungen auftreten (kein `.npmrc` /
  `engine-strict` im Repo → Warnung, kein Abbruch).
- Step `Install Playwright Browsers` (Z. 117–118): `npx playwright@1.64.1 install --with-deps`,
  ebenfalls Runner-Default-Node.
- Übrige Schritte: .NET-Build/-Test der drei Testprojekte (Z. 120–148), Coverage-Report
  und -Schwelle 70 % (Z. 150–172), Artefakt-Uploads (Z. 174–209). Der E2E-Testschritt
  läuft mit `continue-on-error: true` (Z. 143).

### Job `version` (Z. 211–286) — **primär betroffen**

- `runs-on: ubuntu-latest`, `timeout-minutes: 10`;
  `needs: [detect-backmerge, static-checks, build-and-test]`.
- Outputs (Z. 217–221): `changed`, `version` (aus Step `semver`), `rc_tag`, `rc_version`
  (aus Step `rc`).
- Step `Checkout` (Z. 223–227): `actions/checkout@v7` mit `fetch-depth: 0`,
  `fetch-tags: true`.
- Step `Setup Node.js` (Z. 229–233): `actions/setup-node@v7` mit
  **`node-version: '22'`** und `cache: npm` — **die zu korrigierende Node-Pinning-Stelle.**
- Step `Install dependencies` (Z. 235–237): `npm ci` (installiert u. a. die gepinnte
  `semantic-release@26.0.1`, die Node `>=26.13` verlangt — hier entsteht der Laufzeitfehler).
- Step `Determine next version` (Z. 239–269, `id: semver`, `shell: pwsh`):
  - `env`: nur `GITHUB_TOKEN` — **`RESOLVE_DRY_RUN` ist nicht gesetzt**, daher lädt
    `release.config.js` die volle `releasePlugins`-Liste statt `dryRunPlugins`.
  - Z. 253: `$output = & npx semantic-release --dry-run --no-ci --branches staging 2>&1 | Out-String`
  - Z. 259–265: Regex `The next release version is ([0-9]+\.[0-9]+\.[0-9]+(?:[-+][^\s]+)?)`
    → bei Match `changed=true`/`version=<X.Y.Z>`, sonst nur `Write-Host "No new release
    version determined."` — **`$LASTEXITCODE` wird nirgends ausgewertet**; ein Tool-Fehler
    (Exit-Code ≠ 0) fällt damit still auf den `changed=false`-Pfad. Das ist die in der
    Anforderung beschriebene Fehlstelle.
  - Z. 267–268: Schreibt `version=` und `changed=` nach `$env:GITHUB_OUTPUT`.
- Step `Determine RC number` (Z. 271–286, `id: rc`, `if: steps.semver.outputs.changed == 'true'`):
  zählt vorhandene Tags `v$version-rc.*` und emittiert `rc_number`, `rc_tag` (`v$version-rc.N`)
  und `rc_version` (`$version-rc.N`).

### Job `prerelease` (Z. 288–336)

- `runs-on: windows-latest`; `needs: [detect-backmerge, version]`, Bedingung
  `needs.version.outputs.changed == 'true'`.
- Steps: Release-Notes via `gh api .../releases/generate-notes` (Z. 298–313), Composite-Action
  `./.github/actions/build-and-package` mit `release-version: rc_version` /
  `release-tag: rc_tag` (Z. 315–322), `gh release create --prerelease` (Z. 323–336).
- Nutzt kein `setup-node`/`npm` direkt; der Node-Aufruf liegt innerhalb der Composite-Action.

## `Release` — `.github/workflows/release.yml`

Trigger: `push` auf `main` oder Tag `v*.*.*` (Z. 3–6). Permissions: `contents: write`,
`issues: write`, `pull-requests: write` (Z. 8–11). Concurrency-Gruppe
`release-${{ github.repository }}` ohne Cancel (Z. 17–19) — von den Node-Tests
`resolve-release-version.test.mjs` per Regex abgesichert.

### Job `release` (Z. 22–157) — **betroffen**

- `runs-on: windows-latest`, `timeout-minutes: 30`.
- Step `Check out repository` (Z. 27–31): `actions/checkout@v7`, `fetch-depth: 0`,
  `fetch-tags: true`.
- Step `Set up Node.js` (Z. 33–37): `actions/setup-node@v7` mit **`node-version: 22`**
  und `cache: npm` — **zweite zu korrigierende Node-Pinning-Stelle.**
- Step `Set up .NET` (Z. 39–42): `actions/setup-dotnet@v6`, `10.0.x`.
- Step `Install release dependencies` (Z. 44–45): `npm ci`.
- Step `Resolve release version` (Z. 47–58, `id: version`): `node scripts/resolve-release-version.mjs`
  mit `GITHUB_TOKEN`; führt intern `semantic-release --dry-run` aus (siehe
  [scripts.md](scripts.md), `runSemanticReleaseDryRun`).
- Step `Check out release tag for asset repair` (Z. 60–82): nur bei
  `release_action == 'upload-existing'`; prüft Tag-Checkout mit expliziten
  `$LASTEXITCODE`-Auswertungen — Beispiel im selben Workflow für korrekte
  Exit-Code-Behandlung in pwsh.
- Step `Release gate - regular tests` (Z. 84–95): `dotnet restore` + `dotnet test` der
  Regular- und Integration-Projekte, nur bei `release_action == 'create'`.
- Step `Generate release notes for update manifest` (Z. 97–113): `gh api .../generate-notes`.
- Step `Build and package` (Z. 115–122): Composite-Action `./.github/actions/build-and-package`.
- Step `Create automatic GitHub release` (Z. 124–135): `npm run release` (= `semantic-release`,
  echtes Release) nur bei `release_kind == 'automatic'`; setzt `RELEASE_VERSION`,
  `RELEASE_ASSET_PATHS`, `RELEASE_MANIFEST_PATH`, `GITHUB_TOKEN`.
- Step `Create manual-tag GitHub release` (Z. 137–146): `gh release create` für
  `release_kind == 'manual'`.
- Step `Upload asset to existing GitHub release` (Z. 148–157): `gh release upload --clobber`
  für `release_action == 'upload-existing'`.

## `.github/actions/build-and-package/action.yml` — Kontext-Artefakt

Composite-Action, verwendet von `staging-ci.yml` (`prerelease`-Job, Z. 317) und
`release.yml` (`release`-Job, Z. 118).

| Step | Zeilen | Inhalt |
|------|--------|--------|
| `Setup .NET 10` | 33–36 | `actions/setup-dotnet@v6`, `10.0.x` |
| `Restore solution` | 38–40 | `dotnet restore FinanceManager.sln` |
| `Publish application` | 42–71 | `dotnet publish` für `win-x64`/`linux-x64`, self-contained, inkl. `release-metadata.json` |
| `Package publish output` | 73–118 | ZIP-Erzeugung `FinanceManager-v<version>-<rid>.zip`, Output `asset_paths` |
| `Generate update manifest` | 120–143 | `node scripts/generate-update-manifest.mjs` mit **Runner-vorinstalliertem** Node (Kommentar Z. 126–127: „Node is preinstalled on GitHub-hosted runners, so no separate setup-node step is needed"); kein `npm ci`, kein semantic-release; Output `manifest_path` |

Inputs: `release-version`, `release-tag`, `release-notes` (Z. 10–21). Outputs:
`asset-paths`, `manifest-path` (Z. 22–28).

## Übrige Workflows — Node-/npm-Prüfung

Volltextsuche nach `setup-node|npm ci|npm run|npx |node-version` über `.github/`:

| Datei | Befund |
|-------|--------|
| `staging-ci.yml` | `setup-node` (Z. 230), `npm ci` (Z. 115, 236), `npx` (Z. 118, 253) |
| `release.yml` | `setup-node` (Z. 34), `npm ci` (Z. 45), `npm run release` (Z. 135) |
| `pr-staging-ci.yml` | **kein** `setup-node`, **aber** `npm ci` (Z. 125) und `npx playwright` (Z. 128) im Job `build-and-test` — läuft mit Runner-Default-Node. Hinweis: Die Anforderung beschreibt `pr-staging-ci.yml` als „weder setup-node noch npm"; tatsächlich nutzt der Workflow `npm ci`/`npx`, nur ohne eigenes Node-Setup. Nach der `engines.node`-Anhebung kann auch hier `EBADENGINE` auftreten (Warnung, kein Abbruch). |
| `monthly-architecture-security-review.yml` | keine Treffer |
| `security-scan.yml` | keine Treffer |
| `staging-to-main-promotion.yml` | keine Treffer |
| `sync-staging-with-main.yml` | keine Treffer |
| `verify-pr-source.yml` | keine Treffer |
| `weekly-nuget-review.yml` | keine Treffer |
| `yearly-compliance-review.yml` | keine Treffer |
