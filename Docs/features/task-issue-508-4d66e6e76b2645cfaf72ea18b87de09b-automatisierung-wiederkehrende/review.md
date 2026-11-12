# Plan-Review

Status: **Vollständig umgesetzt**

## Geprüfte Planelemente

| Planelement | Status | Nachweis |
|-------------|--------|----------|
| Workflow monatliches Architektur-/Sicherheitsreview | [x] | `.github/workflows/monthly-architecture-security-review.yml`, Cron `0 5 1 * *` |
| Workflow wöchentliche NuGet-Prüfung | [x] | `.github/workflows/weekly-nuget-review.yml`, Cron `0 5 * * 1` |
| Workflow jährliches Compliance-Review | [x] | `.github/workflows/yearly-compliance-review.yml`, Cron `0 5 1 1 *` |
| Duplikatprüfung via Titel-Präfix | [x] | `gh issue list --state open --search 'in:title "<Präfix>"'` + `if: steps.check.outputs.open == '0'` |
| `@softwarewerkstatt`-Erwähnung im Issue | [x] | Erste Zeile des Issue-Bodys in allen drei Workflows |
| `permissions: issues: write`, `GH_TOKEN` | [x] | Job-Level `env`/`permissions` in allen drei Workflows |
| `workflow_dispatch` für manuelle Auslösung | [x] | in allen drei Workflows |
| `concurrency`-Gruppe | [x] | je Workflow eine eigene Gruppe |
| Dokumentation in CI-CD.md | [x] | Abschnitt „5. Review-Automatisierung" |

## Offene Aufgaben

– keine –
