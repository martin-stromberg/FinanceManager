# Code-Review

Status: **Keine Befunde**

## Geprüfte Aspekte

- YAML-Syntax aller drei Workflow-Dateien (PyYAML-Parse erfolgreich, Trigger `schedule` + `workflow_dispatch` korrekt erkannt).
- Cron-Ausdrücke: `0 5 1 * *` (monatlich am 1.), `0 5 * * 1` (montags), `0 5 1 1 *` (1. Januar) — jeweils 05:00 UTC, zeitlich versetzt zum bestehenden `security-scan.yml` (Mo 04:00).
- Bash-Skripte der Steps: `bash -n` fehlerfrei; Heredoc-Terminatoren stehen nach YAML-Dedent an Spalte 0.
- Laufzeitverhalten mit gestubbtem `gh` simuliert: Anlage-Pfad erzeugt korrektes `gh issue create` (Titel + Body inkl. `@softwarewerkstatt` und Checkliste); vorhandenes offenes Issue (`open=1`) führt per `if:`-Bedingung zum Überspringen.
- `gh issue list --search 'in:title "…"'` gegen das echte Repository getestet (liefert `0`).
- Berechtigungen minimal (`issues: write`); kein Checkout/kein .NET-Setup nötig; `ubuntu-latest`.
- Keine Secrets, keine hartcodierten Tokens im Code.

## Befunde

– keine –
