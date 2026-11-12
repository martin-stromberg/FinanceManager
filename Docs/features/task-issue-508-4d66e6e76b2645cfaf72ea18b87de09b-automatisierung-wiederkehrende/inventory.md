# Bestandsaufnahme

## Relevante Bestandteile

- `.github/workflows/` — vorhandene Workflows: `pr-staging-ci.yml`, `staging-ci.yml` (Pre-Release), `release.yml`, `security-scan.yml`, `staging-to-main-promotion.yml`, `sync-staging-with-main.yml`, `verify-pr-source.yml`
- `.github/actions/security-scan/` — Composite Action für den NuGet-Vulnerability-Scan (Gate 2 der CI)
- `CI-CD.md` — zentrale Dokumentation der Pipeline; Abschnitt „Workflows" beschreibt jeden Workflow
- `security-scan.yml` — bestehender Schedule-Workflow (wöchentlich Mo 04:00 UTC), Stilvorlage: Header-Kommentar, `workflow_dispatch`, `concurrency`-Gruppe
- `Docs/maintenance/dependency-upgrade-process.md` — Pflegeprozess für Abhängigkeiten (fachlicher Kontext der NuGet-Prüfung)

## Feststellungen

- Es existiert noch kein Workflow, der Issues automatisiert anlegt.
- Die neuen Workflows sind reine Issue-Automation (kein Build/Test nötig) → `ubuntu-latest`, kein .NET-Setup.
- Duplikatprüfung sinnvoll über Titel-Präfix-Suche via `gh issue list` (GITHUB_TOKEN) — ohne Label-Abhängigkeit, da Labels ggf. nicht existieren.
- Berechtigungen: `issues: write` (+ `contents: read` nicht zwingend, da kein Checkout nötig).
