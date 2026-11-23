# Aufgabenliste – Projektbearbeitung

Branch: `task/issue-560-72333cb9afeb4780a8f17327dd7e744c-mcp-portfolio-aggregat-fuer-we`

| Status | Phase | Beschreibung | Artefakt |
|--------|-------|--------------|----------|
| [x] | 1 | Basisbranch und Projektzustand bestimmen | – |
| [x] | 2 | Verzeichnis/Tracking anlegen | `todo.md` |
| [x] | – | Einstiegspunkt ermitteln | – |
| [x] | 3 | Anforderung aufbereiten | `requirement.md` |
| [x] | 3 | Bestandsaufnahme | `inventory.md`, `inventory/` |
| [x] | 4 | Projektplan erstellen | `project-plan.md` |
| [x] | 4 | Offene Punkte klären | `project-plan.md` (aktualisiert) |
| [x] | 4 | Planprüfung | `project-plan-check.md` |
| [x] | 5 | Planungscommit | – |
| [x] | 5 | Entwicklungsschritte-Tracking | `steps.md`, `status.html` |
| [x] | 6 | Entwicklungsschleife (Schritte 1–4) | `acceptance-schritt-{n}.md` |
| [x] | 7 | Aufräumen und Abschlusscommit | PR #587 gegen staging |

## Hinweise zur Ausführung

- Auftrag: GitHub-Issue-Kommentar — Issues #560, #561, #562 und #578 gemeinsam umsetzen, ein gemeinsamer PR gegen `staging`, Statuskommentare je Issue, Screenshots als Beleg.
- Abweichung zur Skill-Vorgabe: Schreibfähige Unteragenten stehen in dieser Umgebung nicht zur Verfügung; inhaltliche Arbeitsaufträge werden vom Hauptagenten ausgeführt. Nur-Lese-Untersuchungen (Recherche, Reviews) werden an Explore-Subagenten delegiert.
- Anwender-Interaktion erfolgt nicht interaktiv, sondern über Issue-Kommentare; offene Punkte werden mit dokumentierten Annahmen entschieden.

## Abschlussstatus

- Gemeinsamer PR: https://github.com/martin-stromberg/FinanceManager/pull/587 (gegen `staging`, `Closes #560/#561/#562/#578`).
- Statuskommentare auf allen vier Issues gesetzt (Umsetzung, Tests, PR-Verweis).
- Screenshots des granularen Genehmigungsmodus (Wizard + Grant-Matrix + Bestätigungsdialog): `Docs/screenshots/mcp-*.png`, Playwright-Skript `scripts/screenshots/mcp-granular-shots.js`; Belege dem SCM-Anhang des Ereignisses beigelegt.
- CI-Nachbesserung: xUnit1051-Verstöße in `FxRateServiceTests`/`McpAccessServiceTests` behoben (`TestContext.Current.CancellationToken`).
