# Projekt-Todo – Umsetzung: MCP-Integration für FinanceManager

Basisbranch: `task/mcp-integration` (Working dir: `_scratch/mcp-umsetzung`)
Issues: #479–#489 (Epics), Konzept: Issue #470 / PR #478
Datum: 2026-11-04

| Status | Phase | Beschreibung |
|--------|-------|--------------|
| [x] | Branchprüfung | `task/mcp-integration` von `staging` |
| [x] | Verzeichnis/Tracking | Projektverzeichnis angelegt |
| [x] | Anforderung | `requirement.md` (inkl. Tool-Definitionen, Demodaten-Testbarkeit) |
| [x] | Bestandsaufnahme | `inventory.md` aus Planungsprojekt übernommen |
| [x] | Projektplan | `project-plan.md`: 11 Schritte = EP-01…EP-11, geprüft `vollständig` |
| [x] | Planungscommit | `plan: ...` |
| [x] | Schritte-Tracking | `steps.md` + `status.html` |
| [x] | Entwicklungsschleife | 11 Schritte, je Branch + Lifecycle + Abnahme + Merge |
| [ ] | Aufräumen + Abschlusscommit | Projektverzeichnis entfernen |
| [ ] | PR | Gegen `staging`, Checks überwachen |

## Regeln für diesen Lauf

- **Jeder Commit sofort pushen** (Basis- und Schritt-Branch) — Schutz vor
  Verlust durch Verzeichnisbereinigung.
- Neue Enums brauchen `EnumCoverageTests`-Einträge.
