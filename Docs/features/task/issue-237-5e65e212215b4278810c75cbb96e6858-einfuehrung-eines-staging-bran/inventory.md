# Bestandsaufnahme: Einführung eines staging-Branches im CI-Prozess

Diese Bestandsaufnahme dokumentiert die vorhandene CI/CD-Infrastruktur und Branch-Struktur in Bezug auf die Anforderung, einen `staging`-Branch als Integrations- und Qualitätssicherungsstufe einzuführen.

## Zusammenfassung

| Bereich | Status | Anmerkung |
|---------|--------|-----------|
| **test.yml Workflow** | Vorhanden, unvollständig | Läuft nur auf `master` (push + pull_request). Muss um `staging` erweitert werden. |
| **release.yml Workflow** | Vorhanden | Läuft auf `master` und Tags. Keine Logik für automatisierte PRs von `staging` zu `main`. |
| **staging-Branch** | Remote existiert | `remotes/origin/staging` existiert; Trigger-Integration in Workflows fehlt. |
| **Test-Projekte** | Vorhanden | 3 Projekte definiert: FinanceManager.Tests, FinanceManager.Tests.Integration, FinanceManager.Tests.E2E. Beide Workflows nutzen diese. |
| **Branch Protection Rules** | Nicht im Repository | GitHub Branch Protection Rules müssen manuell konfiguriert oder via API automatisiert werden (nicht im Code). |
| **Automatisierte PRs (staging → main)** | Nicht vorhanden | Keine Workflow-Logik für automatisierte Promotions von `staging` zu `main`. |

## Details

- [Workflows](inventory/workflows.md)
- [Test-Struktur](inventory/tests.md)
- [Branch-Konfiguration](inventory/branches.md)
