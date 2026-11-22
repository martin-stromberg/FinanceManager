# Tests — Bestandsaufnahme und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 22.11.2026, ca. 12:43–12:50 Uhr lokale Systemzeit (UTC+1,
  aus dem Systemdatum bzw. dem Commit-Zeitstempel `+0100` abgeleitet)
- **Branch und Commit-ID:** `task/issue-574-732d6743fd3c4b219fb325d91a08f3f1-fix-pre-release-failure-in-sem`,
  Commit `bd0f6ff20f11d1718c3ddb6863b5217d4750b6f7` („Bump the test group with 1 update
  (#571)", 2026-11-22 10:19:25 +0100)
- **Uncommittete Änderungen im getesteten Stand:** Keine Produktiv-/Teständerungen; einziges
  ungetracktes Verzeichnis ist `docs/features/task/issue-574-.../` mit den Artefakten
  dieser Bestandsaufnahme (`requirement.md`, `inventory/`). Der getestete Stand
  entspricht damit fachlich dem Commit `bd0f6ff`.
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (Git Bash), Node `v24.15.0`,
  npm `11.10.0`, .NET SDK `11.0.100`. `node_modules/` war zum Laufzeitpunkt **nicht**
  installiert — für die Suite nicht erforderlich, da die Tests ausschließlich die
  Repo-Skripte mit Node-Builtins importieren (kein semantic-release-Aufruf in den Tests).
  Hinweis: Node `v24.15.0` liegt selbst außerhalb des deklarierten `engines.node: "22.x"`;
  `node --test` wertet `engines` nicht aus.
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - `npm run test:release-version` (`package.json` Z. 9):
    `node --test scripts/resolve-release-version.test.mjs scripts/generate-update-manifest.test.mjs`
    — **die für diese Anforderung relevante Suite** (testet die Release-Skripte, die in den
    betroffenen Workflows laufen).
  - `dotnet test FinanceManager.sln` (`docs/development.md` Z. 184–188) mit den Projekten
    `FinanceManager.Tests`, `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E`
    — **nicht ausgeführt**, siehe „Testlücken" unten.
  - Weitere im CI definierte Befehle (Coverage, Playwright) sind ebenfalls .NET-seitig und
    nicht im Scope.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `npm run test:release-version` | Repo-Root `D:\Repositories\softwareschmiede\732d6743-fd3c-4b21-9fb3-25d91a08f3f1` | 0 | 26 | 0 | 0 | [Log](test-results/npm-test-release-version.txt) |

Details Lauf 1 (aus `node --test`-Summary): `tests 26`, `suites 0`, `pass 26`, `fail 0`,
`cancelled 0`, `skipped 0`, `todo 0`, `duration_ms ≈ 86`. Davon 22 Tests aus
`resolve-release-version.test.mjs` und 4 aus `generate-update-manifest.test.mjs`.
Nebenbefund der Ausgabe: Zeile `Wrote update.json` stammt aus dem Test
`main falls back to current ISO timestamp...` — das Manifest wird dabei in ein Temp-
Verzeichnis geschrieben; im Repo-Root blieb **kein** `update.json` zurück (verifiziert,
`git status` zeigt keine neue Datei).

### Nachgewiesene bestehende Testfehler

Keine. Im Ausgangslauf sind alle 26 ausgeführten Tests erfolgreich; es wurde kein
Testfehler nachgewiesen.

### Testlücken und Ausführungsprobleme

- **Nicht ausgeführte .NET-Suiten:** `FinanceManager.Tests`, `FinanceManager.Tests.Integration`
  und `FinanceManager.Tests.E2E` wurden **nicht ausgeführt — nicht im Scope der Änderung**.
  Begründung: Die Anforderung betrifft ausschließlich CI-/Release-Infrastruktur
  (Workflow-YAML, `package.json`-`engines`, `docs/development.md`); es gibt keinerlei
  Berührungspunkt mit .NET-Produktivcode, Datenmodell, UI oder .NET-Tests. Ein
  vollständiger `dotnet test`-Lauf (inkl. Playwright-E2E) wäre unverhältnismäßig.
  → Für diese Suiten ist der Ausgangszustand **unbekannt**; sie dürfen später nicht als
  „preexisting fehlerfrei" oder „preexisting fehlerhaft" gewertet werden.
- **Kein automatisierbarer Workflow-Test:** Der eigentliche Fehlerpfad (Exit-Code ≠ 0 des
  `npx semantic-release`-Aufrufs im `Determine next version`-Step) ist reine
  Workflow-Shell-Logik ohne Testabdeckung im Repo. Die Verifikation des Fixes erfolgt per
  Anforderung durch einen erneuten Lauf des Pre-Release-Workflows auf `staging`.
- **Keine Build-/Setup-/Infrastrukturfehler** beim ausgeführten Lauf; `node --test` lief
  ohne installiertes `node_modules` fehlerfrei.

## Testdateien

### `scripts/resolve-release-version.test.mjs` (22 Tests)

Datei: `scripts/resolve-release-version.test.mjs` (318 Zeilen), Framework: `node:test` +
`node:assert/strict`. Testet `scripts/resolve-release-version.mjs` ausschließlich über
Dependency-Injection (`environment()`/`effects()`-Helfer) — kein Netzwerk, kein Git,
kein echter semantic-release-Aufruf.

- `accepts strict manual release tags` — `parseManualTag`/`classifyWorkflowRef` für `v2.3.4`.
- `rejects non-semantic manual tags` — `v2.3`, `release-2.3.4`, `v02.3.4` werden verworfen.
- `accepts only main for automatic releases` — nur `main` ergibt `kind: "automatic"`;
  `staging`, `master`, `develop` werfen.
- `extracts a version only when Semantic Release announces one` — `parseNextReleaseVersion`
  liefert `3.4.5` bzw. `null`.
- `extracts a prerelease version announced for the staging RC channel` — extrahiert
  `1.17.0-RC.1`/`1.16.1-RC.12` (Legacy-Verhalten, RC-Suffixe kommen heute aus dem Workflow).
- `creates a release for a manual tag without an existing release` — Outputs
  `released=true`, `reason=manual-tag`, `release_action=create`.
- `skips a manual tag whose release already has the expected asset` — `released=false`,
  `reason=existing-release`.
- `repairs a manual release whose expected asset is still in starter state` —
  `release_action=upload-existing` bei `state: "starter"`.
- `repairs a manual release whose uploaded asset has zero size` — `upload-existing` bei
  `size: 0`.
- `treats an uploaded asset with positive size as complete` — `released=false` bei
  vollständigen Assets.
- `repairs a manual release whose expected asset is missing` — `released=true`,
  `upload-existing`, Tag `v2.3.4`.
- `creates an automatic release when Semantic Release resolves a new version` —
  `released=true`, `reason=semantic-release`, `action=create`, Tag `v3.4.5`.
- `creates an automatic release even when older releases are missing expected assets` —
  neue Version hat Vorrang vor Repair.
- `repairs one automatic release whose expected asset is missing when no new release is
  pending` — Repair-Fallback ohne neue Version.
- `never repairs a prerelease, even if it is missing its expected asset and no new release
  is pending` — Regressionstest: Prereleases werden nie repariert (msTools.Updater-Vorfall).
- `skips automatic releases without releasable commits` — `released=false`,
  `reason=no-release`.
- `rejects an automatic release when its tag already exists` — `remoteTagExists` → Fehler,
  keine Outputs.
- `rejects an automatic release when its complete release already exists` — Fehler, keine
  Outputs.
- `lists GitHub releases across all pages` — `listGitHubReleases` paginiert (100 + 1).
- **`uses one repository-wide release concurrency group`** — liest
  `.github/workflows/release.yml` direkt und assertiert per Regex die Concurrency-Gruppe
  `release-${{ github.repository }}` (Workflow-Struktur-Test; relevant bei Änderungen an
  `release.yml`).
- **`checks out the release tag before repairing an existing release asset`** — liest
  `release.yml` und assertiert Step-Name, `if`-Bedingung und `git checkout --detach`
  (ebenfalls Workflow-Struktur-Test).
- `expects windows, linux and update manifest release assets` — erwartete Asset-Liste
  wird als „vollständig" bewertet.

### `scripts/generate-update-manifest.test.mjs` (4 Tests)

Datei: `scripts/generate-update-manifest.test.mjs` (99 Zeilen), Framework: `node:test`.
Testet `scripts/generate-update-manifest.mjs` mit echten Temp-Verzeichnissen
(`mkdtempSync`) — reine Dateisystem- und Hash-Logik, kein Workflow-Bezug.

- `creates update manifest with release notes and platform assets` — Manifest enthält
  win-x64/linux-x64-Assets mit SHA-256 und Download-URL.
- `builds asset URLs from an explicit tag instead of reconstructing it from version` —
  Regressionstest für RC-Tags (`v1.2.3-rc.1` statt `v${version}`).
- `rejects blank publishedAt` — Validierung wirft.
- `main falls back to current ISO timestamp when RELEASE_PUBLISHED_AT is blank` —
  `main()` setzt Default-Zeitstempel.

## Hilfsmethoden

### `resolve-release-version.test.mjs` (lokale Testhelfer)

- `environment({refType, refName})` — baut das Env-Objekt (`GITHUB_OUTPUT`,
  `GITHUB_REPOSITORY`, `GITHUB_TOKEN`, `GITHUB_REF_TYPE`, `GITHUB_REF_NAME`).
- `release(tag, assets, {prerelease})` — Fabrik für GitHub-Release-Objekte inkl.
  Asset-Normalisierung (String → `{name, state:"uploaded", size:1}`).
- `effects(overrides)` — liefert `output`-Array plus injizierbares `dependencies`-Objekt
  (Defaults: kein Release, keine Remote-Tags, Dry-Run gibt „The next release version is
  3.4.5" zurück). `runSemanticReleaseDryRun` ist damit vollständig mockbar — der
  Exit-Code-Pfad (`result.status !== 0`) ist allerdings **nicht** exportiert und in den
  Tests nicht direkt abgedeckt.
