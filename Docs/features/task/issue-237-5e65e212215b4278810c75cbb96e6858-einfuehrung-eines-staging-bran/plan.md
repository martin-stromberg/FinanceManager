# Umsetzungsplan: Einführung eines staging-Branches im CI-Prozess

## Übersicht

Der CI/CD-Prozess wird um einen `staging`-Branch als Integrations- und Qualitätssicherungsstufe erweitert. Entwicklungs-Pull-Requests werden gegen `staging` erstellt und getestet, während `master` als ausschließlicher Release-Branch bestehen bleibt. Dies etabliert einen zweistufigen Workflow, der Feature-Qualität von der Stabilität der Gesamtintegration entkoppelt. Die Umsetzung umfasst die Anpassung von GitHub Actions Workflows, die Konfiguration von Branch Protection Rules sowie die Dokumentation des neuen Prozesses für das Team.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Branch-Namenskonvention | `staging` (statt `develop`, `pre-release`, etc.) | Explizit in der Anforderung vorgegeben; bereits als Remote-Branch vorhanden; kurz und prägnant; weit verbreitet in der Industrie. |
| Automatisierte PRs (staging → master) | Separater Workflow `staging-to-master.yml` mit manueller Freigabe | Entkoppelt Staging-Tests von Release-Promotion; PR wird automatisch erstellt, muss aber von Maintainer gemergt werden; bietet Kontrolle vor Release ohne zu blockieren. |
| Quality Gates | Code-Coverage-Schwellwerte und Security-Scans direkt in dieser Umsetzung | Frühes Feedback auf Feature-Ebene; reduziert Risiko defekter Code; Coverage-Schwellwerte und Dependabot-Integration implementieren. |
| Branch-Protection (master) | Strict — nur PRs aus `staging` erlaubt | Erzwingt zweistufigen Workflow; verhindert accidental Direct-Push; ensures all changes durchlaufen Integrationsbranch. |
| Review-Requirement | Mindestens 1 Approval erforderlich | Balanciert Code-Quality mit Development-Velocity; Approval über GitHub Team-Berechtigungen gesteuert. |
| Version-Bumping | Nur bei Merge zu `master` | Verhindert mehrfach gebumpte Versionen in `staging`; klare Trennlinie zwischen Pre-Release und Release. |
| Hotfix-Prozess | Hotfixes gehen über `staging` (kein Direct-Push zu `master`) | Konsistente Branch-Struktur; alle Code-Änderungen durchlaufen Test-Workflow mit Quality Gates; niedrigeres Risiko. |
| Branch-Protection-Rules | Manuell via GitHub Web-UI oder API (nicht im Repository-Code) | GitHub Settings sind Infrastruktur-Konfiguration, nicht Anwendungscode; ermöglicht Wiederverwendung und Automatisierung über IaC-Tools. |
| Workflow-Trigger | Both `push` und `pull_request` für beide Branches (`master` und `staging`) | Testet sowohl eingehende PRs als auch Merges; Konsistenz mit bestehendem test.yml-Muster; Quality Gates auf beiden Events. |
| Repository-Branch | `master` (keine Umbenennung zu `main`) | Bestandscode nutzt `master`; Umbenennung ist separates Projekt; Konsistenz mit aktueller Git-Konvention des Repositories. |

## Programmabläufe

### Entwicklungs-PR-Workflow

1. Entwickler erstellt Feature-Branch basierend auf `staging`
2. Entwickler eröffnet PR gegen `staging` (nicht gegen `master`)
3. GitHub Actions `test.yml` wird automatisch ausgelöst
4. Workflow führt Unit-Tests, Integrationstests, E2E-Tests, Linting und Build-Validierung durch
5. Bei erfolgreichen Checks kann der PR gemergt werden
6. Merge zu `staging` löst erneut `test.yml` aus (als Push-Event)
7. Nach erfolgreicher Integration in `staging` kann (optional) ein automatisiertes PR von `staging` → `master` erstellt werden

Beteiligte Komponenten: `master`, `staging`, GitHub PR-System, `.github/workflows/test.yml`

### Automatische Promotion von staging zu master

1. Neuer Workflow `staging-to-master.yml` wird bei `push` zu `staging` ausgelöst (nach erfolgreichen test.yml Checks)
2. Workflow prüft, ob die neueste Staging-Commit bereits in `master` integriert ist
3. Falls nicht: Erstellt automatisch ein PR von `staging` → `master` mit Label (z. B. `automated-promotion`)
4. PR wird mit aussagekräftiger Beschreibung versehen (z. B. „Automated promotion from staging to master")
5. PR wird im Draft-Status erstellt oder gekennzeichnet, damit es nicht versehentlich automatisch gemergt wird
6. PR benötigt **manuellen Review und Merge durch Maintainer** (Branch-Protection-Rules erzwingen mindestens 1 Approval)
7. Nach erfolgreichem Merge zu `master` durch Maintainer: `release.yml` wird ausgelöst
8. Versionsbump (Semantic Release) und Release-Publikation erfolgen nur auf `master`

Beteiligte Komponenten: `master`, `staging`, `.github/workflows/staging-to-master.yml`, `.github/workflows/test.yml`, `.github/workflows/release.yml`

### Hotfix-Prozess

1. Hotfix wird aus `master` in einen separaten Branch (`hotfix/*`) ausgecheckt
2. Hotfix-PR wird gegen `staging` erstellt (nicht direkt gegen `master`)
3. Hotfix durchläuft gleichen Test-Workflow wie normale Features
4. Nach Merge zu `staging`: Automatisierte Promotion zu `master` (über `staging-to-master.yml`)
5. Release erfolgt wie üblich über `release.yml`

Beteiligte Komponenten: `master`, `staging`, Test-Workflow, Release-Workflow

## Neue Klassen

Keine — diese Anforderung betrifft ausschließlich CI/CD-Infrastruktur und Git-Workflows, nicht die Anwendungslogik.

## Änderungen an bestehenden Klassen

Keine — diese Anforderung betrifft ausschließlich CI/CD-Infrastruktur und Git-Workflows, nicht die Anwendungslogik.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — diese Anforderung betrifft ausschließlich CI/CD-Infrastruktur und Git-Workflows.

## Konfigurationsänderungen

Keine Änderungen an `appsettings.json` oder Anwendungs-Konfigurationsklassen erforderlich. Die Konfigurationsänderungen erfolgen auf der Ebene von GitHub Actions und Branch-Settings:

| Eintrag | Typ | Ort | Zweck |
|---------|-----|-----|-------|
| Branch-Trigger `staging` | Workflow-YAML | `.github/workflows/test.yml` | Führt Tests bei Push und PR gegen `staging` aus |
| Automatisierte Promotion | Neuer Workflow | `.github/workflows/staging-to-master.yml` | Erstellt PR von `staging` → `master` |
| Branch-Protection für `staging` | GitHub Settings | GitHub Repository-Settings | Erzwingt Checks vor Merge, blockiert Direct-Push |
| Branch-Protection für `master` | GitHub Settings | GitHub Repository-Settings | Erzwingt Checks vor Merge, blockiert Direct-Push, nur PRs aus `staging` erlaubt |

## Seiteneffekte und Risiken

- **Bestehende offene PRs gegen `master`:** Müssen geschlossen oder zu PRs gegen `staging` umgeleitet werden. Nicht abgefangene PRs könnten weiterhin gegen `master` gehen und die Branch-Protection-Rules verletzen.
- **Lokale Entwickler-Setup:** Developer müssen ihre lokalen Workflows anpassen (forking from `staging`, pushing to `staging`). Erfordert Dokumentation und evtl. Team-Kommunikation.
- **Erste Staging-Branch-Erstellung:** Remote-`staging` existiert bereits; lokales Setup muss diese verfolgbar machen (z. B. `git checkout --track origin/staging` oder `git pull`).
- **Release-Workflow:** Der `release.yml` läuft weiterhin nur auf `master`; `staging` erhält keine automatischen Versionsbumps. Dies ist gewünscht und verhindert Versionskonflikte, erfordert aber klare Dokumentation.
- **CI-Pipeline-Verdoppelung:** Test-Workflow läuft doppelt (push + PR), was zu doppeltem CI/CD-Verbrauch führt. Dies ist Standard-GitHub-Verhalten und akzeptabel.

## Umsetzungsreihenfolge

1. **Anpassung `.github/workflows/test.yml`: Branch-Trigger und Quality Gates erweitern**
   - Voraussetzungen: Keine
   - Beschreibung: 
     - `on.push.branches` und `on.pull_request.branches` um `staging` erweitern
     - Coverage-Schwellwert implementieren: `dotnet test --collect:"XPlat Code Coverage"` mit Schwellwert (z. B. 70%) für beide Branches
     - Dependabot-Integration aktivieren (falls noch nicht vorhanden) für automatisierte Dependency-Scans
     - Beide Quality Gates müssen bestanden sein, bevor Merge auf `staging`/`master` erlaubt ist

2. **Erstellung `.github/workflows/staging-to-master.yml`**
   - Voraussetzungen: `test.yml` muss für `staging` konfiguriert sein, einschließlich Quality Gates
   - Beschreibung: 
     - Neuer Workflow, der bei erfolgreichem Push zu `staging` (und bestandenen test.yml Checks einschließlich Coverage und Security) automatisch ein PR von `staging` → `master` erstellt
     - Verwendung einer GitHub Action wie `peter-evans/create-pull-request` oder `gh pr create` Befehl
     - PR wird mit Label (`automated-promotion`) und aussagekräftiger Beschreibung versehen
     - PR wird im Draft-Status erstellt oder mit Hinweis, dass manuelle Freigabe erforderlich ist

3. **Branch Protection Rules für `staging` konfigurieren (GitHub Web-UI oder API)**
   - Voraussetzungen: `staging`-Branch existiert (bereits vorhanden), test.yml muss für `staging` konfiguriert sein
   - Beschreibung: Manuell via GitHub UI oder automatisiert via API/Terraform:
     - Require status checks: `test.yml` (Unit, Integration, E2E, Build, Lint, Coverage, Security-Scans müssen erfolgreich sein)
     - Require PR reviews: Mindestens 1 Approval erforderlich
     - Dismiss stale PR approvals: Aktivieren (nach neuen Commits wird Approval invalidiert)
     - Restrict who can push to matching branches: Nur via PR, Direct-Push blockieren

4. **Branch Protection Rules für `master` konfigurieren (GitHub Web-UI oder API)**
   - Voraussetzungen: Keine
   - Beschreibung: Manuell via GitHub UI oder automatisiert via API/Terraform:
     - Require status checks: `test.yml` (Unit, Integration, E2E, Build, Lint, Coverage, Security-Scans müssen erfolgreich sein)
     - Require PR reviews: Mindestens 1 Approval erforderlich
     - Require branches to be up to date before merging: Aktivieren
     - Restrict who can push to matching branches: Nur via PR, Direct-Push blockieren
     - **Strict Mode:** Allowlist `staging` als einziger zulässiger Quell-Branch für PRs zu `master` (Restrict pull request target branch to only allow merges from staging)

5. **Team-Dokumentation aktualisieren**
   - Voraussetzungen: Alle vorherigen Schritte erledigt
   - Beschreibung: Dokumentieren Sie den neuen Workflow für das Team (z. B. in README.md oder CONTRIBUTING.md):
     - PRs gehen gegen `staging`, nicht `master`
     - `staging` ist der Integrationsbranch, `master` ist der Release-Branch
     - Hotfixes gehen über `staging` (kein Direct-Push zu `master`)
     - Automatisierte PRs von `staging` → `master` werden von Workflow erstellt, benötigen aber manuelles Merge durch Maintainer
     - Versionsbumps erfolgen nur auf `master`
     - Mindestens 1 Code-Review (Approval) erforderlich für alle PRs

6. **Migration bestehender offener PRs**
   - Voraussetzungen: Workflow-Konfiguration abgeschlossen, Branch-Protection-Rules aktiv
   - Beschreibung: Alle offenen PRs gegen `master` überprüfen:
     - Geschlossene/veraltete PRs schließen
     - Aktive PRs: Zielzweig zu `staging` ändern (über GitHub Web-UI: "Edit" auf PR) oder PR schließen und neue gegen `staging` eröffnen
     - Entwickler über neuen Workflow benachrichtigen

7. **Verifikation und Aktualisierung lokaler Setups**
   - Voraussetzungen: Alle Konfig-Schritte abgeschlossen, Branch-Protection-Rules aktiv
   - Beschreibung: 
     - Verifizieren Sie lokal, dass `staging` korrekt verfolgbar ist: `git checkout --track origin/staging`
     - Testen Sie manuell: Feature-Branch → PR gegen `staging` → Workflow-Ausführung prüfen (einschließlich Coverage und Security Checks)
     - Merge zu `staging` → verifizieren, dass automatisierter PR gegen `master` erstellt wird
     - Merge zu `master` → verifizieren, dass `release.yml` ausgelöst wird
     - Optional: Git-Hooks oder lokale Dokumentation aktualisieren (z. B. `DEVELOPMENT.md`)

## Tests

### Neue Tests

Keine neuen Test-Klassen oder Test-Methoden erforderlich. Die bestehenden Unit-, Integrations- und E2E-Tests (`FinanceManager.Tests`, `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E`) werden unverändert auf beiden Branches (`staging` und `master`) ausgeführt.

### Betroffene bestehende Tests

Keine — die Test-Suiten selbst ändern sich nicht; sie werden nur auf zusätzlichen Branches ausgeführt.

### E2E-Tests (Pflicht)

Keine neuen E2E-Tests erforderlich, da diese Anforderung die Anwendungslogik nicht ändert. Die bestehenden E2E-Tests decken bereits alle Benutzer-Interaktionen ab und werden unverändert auf beiden Branches ausgeführt.

**Jedoch:** Die Workflow-Änderungen sollten manuell verifiziert werden:
- Manuelles Testen: Feature-Branch → PR gegen `staging` → Workflow-Ausführung prüfen → Merge → Automatisierte PR gegen `master` prüfen

## Offene Punkte

Keine. Alle fünf offenen Punkte wurden geklärt und sind in den Plan eingearbeitet:

1. ✓ **Automatisierte PRs von `staging` → `master`:** Manuelle Freigabe — PR wird automatisch erstellt, muss aber von Maintainer gemergt werden (implementiert in `staging-to-master.yml` mit Draft-Status)
2. ✓ **Quality Gates:** Vollständige Gates in dieser Umsetzung — Coverage-Schwellwerte und Dependabot-Security-Scans werden direkt in test.yml integriert
3. ✓ **Branch-Protection für `master`:** Strict Mode — nur PRs aus `staging` erlaubt
4. ✓ **Approval-Requirement:** Mindestens 1 Approval erforderlich für alle PRs (konfiguriert via Branch-Protection-Rules)
5. ✓ **Repository-Branch:** Bleibt bei `master`, keine Umbenennung zu `main`

## Hinweise zur Implementierung

- **Workflow-Syntax:** Achten Sie auf korrekte YAML-Indentation in `.github/workflows/test.yml` und beim Erstellen von `staging-to-master.yml`.
- **GitHub API Token:** Der `staging-to-master.yml`-Workflow benötigt einen Token mit `repo` und `workflow` Permissions (üblicherweise `GITHUB_TOKEN` mit entsprechenden Scopes).
- **Basis-Branch bei Erstellung:** Stellen Sie sicher, dass neue PRs von `staging` → `master` den korrekten Basis-Branch setzen.
- **Erste `staging`-Branch-Erstellung:** Der Branch existiert bereits remote; keine zusätzliche Erstellung erforderlich. Entwickler müssen ihn nur lokal auschecken/tracken.
- **Testing des Workflows:** Testen Sie den neuen `staging-to-master.yml`-Workflow mit einem Feature-Branch vor Produktiveinführung (z. B. auf Test-Branch mergen, PR erzeugen, verifizieren).
