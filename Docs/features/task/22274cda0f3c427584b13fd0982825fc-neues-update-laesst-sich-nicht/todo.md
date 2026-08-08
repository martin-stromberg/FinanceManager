# Aufgabenliste – Anforderungsbearbeitung

Branch: `task/22274cda0f3c427584b13fd0982825fc-neues-update-laesst-sich-nicht`

Hinweis: Zweiter Lifecycle-Durchlauf auf diesem Branch. Erster Durchlauf (Post-Installation
Lock-Cleanup-Validierung) ist bereits committed (`dd5e040`..`7816aa9`). Dieser Durchlauf behebt die
eigentliche Root Cause: Statusanzeige (`GetStatusAsync()`, gecachter Snapshot) und Reset-Prüfung
(`GetLockCreatedAtAsync()`, Live-Read) können dauerhaft auseinanderlaufen, weil nichts sie automatisch
synchronisiert.

| Status | Schritt | Beschreibung | Artefakt |
|--------|---------|--------------|----------|
| [x] | 1 | Branch-Name ermitteln | – |
| [x] | 2 | Verzeichnisstruktur vorbereiten | `Docs/features/{branchname}/` |
| [x] | – | Einstiegspunkt ermitteln (Schritt 3, neue Anforderung) | – |
| [x] | 3 | Anforderung übersetzen (Unteragent) | `requirement.md` |
| [x] | 4 | Bestandsaufnahme (Unteragent) | `inventory.md`, `inventory/` |
| [x] | 5 | Umsetzungsplanung (Unteragent) | `plan.md` |
| [x] | 5a | Offene Punkte prüfen und ggf. Planung wiederholen | `plan.md` (aktualisiert) |
| [x] | 5b | Planungscommit | – |
| [x] | 6 | Implementierung (Unteragent) | Codeänderungen |
| [x] | 7 | Plan-Review (Unteragent, bedingt) | `review.md` |
| [x] | 8 | Code-Review (Unteragent) | `review-code.md` |
| [x] | 8b | Tests ausführen (Unteragent) | `test-results.md` |
| [x] | – | Iteration oder Abschluss entscheiden | – |
| [x] | 8a | Folgeaufgaben dokumentieren (bei Schleifenabbruch) | `continue.md` |
| [x] | 9 | Dokumentation erstellen | `Docs/help/updates/ablauf-technisch.md`, `troubleshooting.md` |
| [x] | 9b | README aktualisieren (geprüft, keine Änderung nötig — öffentliche API/Vertrag unverändert) | `README.md` |
| [ ] | 10 | Nacharbeiten abschließen (offene Punkte aus `continue.md`) | `continue-done.md` |
| [ ] | – | Feature-Verzeichnis löschen | – |
| [ ] | – | Commit durchführen | – |
