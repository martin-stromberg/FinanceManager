# Tasks: Einführung eines staging-Branches im CI-Prozess

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Workflow-Konfiguration | `.github/workflows/test.yml` anpassen: `staging` zu `on.push.branches` hinzufügen | Offen | — |
| 2 | Workflow-Konfiguration | `.github/workflows/test.yml` anpassen: `staging` zu `on.pull_request.branches` hinzufügen | Offen | — |
| 3 | Workflow-Konfiguration | `.github/workflows/staging-to-master.yml` erstellen: Automatisierte PR-Erstellung von `staging` → `master` | Offen | — |
| 4 | Workflow-Konfiguration | Staging-to-Master-Workflow testen: Feature-Branch → PR gegen `staging` → automatische PR zu `master` verifizieren | Offen | — |
| 5 | Branch-Schutz | GitHub Branch-Protection-Rules für `staging` konfigurieren (Status Checks erforderlich, Direct-Push blockieren) | Offen | — |
| 6 | Branch-Schutz | GitHub Branch-Protection-Rules für `master` konfigurieren (Status Checks erforderlich, nur PRs erlaubt, optional: nur aus `staging`) | Offen | — |
| 7 | Dokumentation | README.md / CONTRIBUTING.md aktualisieren: Neuer Workflow-Beschreibung (PRs gegen `staging`, nicht `master`) | Offen | — |
| 8 | Dokumentation | Hotfix-Prozess dokumentieren (Hotfixes gehen über `staging`, nicht direkt zu `master`) | Offen | — |
| 9 | Dokumentation | Release-Prozess dokumentieren (Versionsbumps nur auf `master`, automatisierte PRs von `staging`) | Offen | — |
| 10 | Team-Kommunikation | Team über neuen Workflow benachrichtigen (Slack, E-Mail oder Meeting) | Offen | — |
| 11 | Migration | Offene PRs gegen `master` überprüfen und zu `staging` umleiten oder schließen | Offen | — |
| 12 | Verifikation | Lokal verifizieren: `staging`-Branch auschecken und verfolgbar machen (`git checkout --track origin/staging`) | Offen | — |
| 13 | Verifikation | Lokal verifizieren: Feature-Branch von `staging` erstellen und PR eröffnen; Test-Workflow sollte ausgelöst werden | Offen | — |
| 14 | Verifikation | Lokal verifizieren: Nach Merge zu `staging` sollte automatisiertes PR zu `master` erstellt werden | Offen | — |
