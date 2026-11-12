# Umsetzungsplan – Review-Automatisierung (Issue #508)

## Neue Workflows (je Prüfung eine Datei)

### 1. `.github/workflows/monthly-architecture-security-review.yml`
- Cron `0 5 1 * *` (1. des Monats, 05:00 UTC) + `workflow_dispatch`
- Titel-Präfix: `Architektur- & Sicherheitsreview`
- Issue-Titel: `Architektur- & Sicherheitsreview – YYYY-MM`
- Body: `@softwarewerkstatt` + Checkliste lt. Anforderung

### 2. `.github/workflows/weekly-nuget-review.yml`
- Cron `0 5 * * 1` (montags, 05:00 UTC) + `workflow_dispatch`
- Titel-Präfix: `NuGet-Paketprüfung`
- Issue-Titel: `NuGet-Paketprüfung – KW NN/YYYY`
- Body: `@softwarewerkstatt` + Checkliste lt. Anforderung

### 3. `.github/workflows/yearly-compliance-review.yml`
- Cron `0 5 1 1 *` (1. Januar, 05:00 UTC) + `workflow_dispatch`
- Titel-Präfix: `Compliance- & Copyright-Review`
- Issue-Titel: `Compliance- & Copyright-Review – YYYY`
- Body: `@softwarewerkstatt` + Checkliste lt. Anforderung

## Gemeinsames Muster je Workflow

1. Step „Vorhandenes offenes Issue prüfen": `gh issue list --state open --search 'in:title "<Präfix>"'`; Anzahl > 0 → Abbruch (kein Duplikat).
2. Step „Issue anlegen" (nur wenn keins offen): `gh issue create --title … --body …` mit `@softwarewerkstatt`-Erwähnung.
3. `permissions: issues: write`; `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}`; `concurrency`-Gruppe je Workflow; `ubuntu-latest`, `bash`.

## Dokumentation

- `CI-CD.md`: Abschnitt „Workflows" um die drei neuen Workflows ergänzen (Auslöser, Duplikatlogik, Berechtigungen).
- `README.md`: kein Abschnitt zu CI-Workflows vorhanden → nur prüfen; bei vorhandener Verweisstruktur kurzen Hinweis ergänzen, sonst entfällt 9b.

## Verifikation

- YAML-Syntax der drei Dateien validieren.
- `gh workflow list`/PR-Checks beobachten.

## Offene Punkte

– keine –
