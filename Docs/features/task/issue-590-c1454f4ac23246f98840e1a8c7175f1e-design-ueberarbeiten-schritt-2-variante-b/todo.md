# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten-schritt-2-variante-b`

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `docs/features/task/issue-590-...-variante-b/` |
| [x] | – | Einstiegspunkt ermitteln | – |
| [ ] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` |
| [ ] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` |
| [ ] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [ ] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [ ] | 5b | Planungscommit | – |
| [ ] | 6 | Implementierung (Unteragent) | Codeänderungen |
| [ ] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [ ] | 8 | Code-Review (Unteragent) | `review-code.md` |
| [ ] | 8b | Tests ausführen (Unteragent) | `test-results.md` |
| [ ] | – | Iteration oder Abschluss entscheiden | – |
| [ ] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [ ] | 9 | Dokumentation erstellen (Unteragent) | `Docs/help/` |
| [ ] | 9b | README aktualisieren (Unteragent) | `README.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |

## Abweichungen vom Skill-Ablauf

- Keine Unteragenten verfügbar: Alle Schritte (Übersetzung, Inventar, Planung,
  Implementierung, Reviews, Tests, Dokumentation) werden vom Hauptagenten selbst
  ausgeführt (erlaubte Abweichung gemäß `SKILL.md`/`lifecycle.md`).
- Bestandsaufnahme wird **nicht neu erhoben**: Das Entwurfsphasen-Feature-Verzeichnis
  `docs/features/task-issue-590-c1454f4ac23246f98840e1a8c7175f1e-design-ueberarbeiten/`
  enthält requirement/inventory/plan mit vollständiger UI-Bestandsaufnahme;
  zusätzlich liegen `Docs/design/inventory/` (Ist-Struktur aller Ansichten) und
  `Docs/design/proposals/variantB/` (verbindliche Design-Vorlage) vor.
  `inventory.md` verlinkt diese Eingaben.
- Kein PR/Merge in diesem Lauf — der Projekt-Koordinator erstellt den PR gegen
  `staging`. Abschluss = Lifecycle-Commit + `git push -u origin`.
