# Anforderungsübersetzung: Einführung eines staging-Branches im CI-Prozess

## Fachliche Zusammenfassung

Der CI/CD-Prozess wird um einen neuen `staging`-Branch erweitert, der als Integrations- und Qualitätssicherungsstufe fungiert. Entwicklungs-Pull-Requests werden zukünftig gegen `staging` (statt direkt gegen `main`/`master`) erstellt und getestet. Der `main`-Branch bleibt der ausschließliche Deployment-Branch für stabile, veröffentlichte Versionen und wird durch einen kontrollierten Promotionsprozess aus `staging` aktualisiert. Dies etabliert einen zweistufigen Workflow, der Qualität auf Feature-Ebene (PRs gegen `staging`) von der Stabilität der integrierten Gesamtheit (`staging` selbst) entkoppelt.

## Betroffene Klassen und Komponenten

### GitHub Actions Workflows
- **`.github/workflows/test.yml`** – Anpassung der Trigger und der Logik:
  - Aktuell: Läuft auf `push` zu `master` und PRs gegen `master`
  - Neu: Muss auch auf `staging`-Branch angewendet werden (sowohl für PRs als auch für Push)
  - Scope: Unit-Tests, Integrationstests, Linting, Build-Validierung

- **`.github/workflows/release.yml`** – Anpassung der Trigger und der Release-Logik:
  - Aktuell: Läuft auf `push` zu `master` und Tags
  - Neu: Soll primär auf `main` (oder `master`) bleiben, aber zusätzlich automatisierte PRs von `staging` → `main` erzeugen können
  - Scope: Versionsverwaltung (Semantic Release), Artefakt-Erzeugung (ZIP-Dateien), Release-Publikation

### Branch-Konfiguration
- **`staging`-Branch** – Neuer primärer Integrationsbranch
- **`main`/`master`-Branch** – Bleibt für Releases, wird ausschließlich aus `staging` aktualisiert

### Konfigurationsartefakte (potentiell)
- GitHub Branch Protection Rules für `staging` und `main`
- CI-Konfiguration (Trigger, Qualitäts-Gates)
- Optional: Automatisierte PR-Erstellung und Workflow-Orchestrierung

### Tests und Validierung
- Keine neuen Test-Klassen notwendig, aber bestehende Tests (`FinanceManager.Tests`, `FinanceManager.Tests.Integration`, `FinanceManager.Tests.E2E`) müssen auf beiden Branches (`staging` und `main`) ausgeführt werden

## Implementierungsansatz

### Phase 1: Workflow-Anpassung (`.github/workflows/test.yml`)
1. Trigger erweitern: `on.push.branches` und `on.pull_request.branches` um `staging` ergänzen
2. Job-Logik bleibt unverändert, läuft jedoch auf zwei Branches mit denselben Checks
3. Kein zusätzliches Job-Setup notwendig – die bestehenden Unit-, Integrations- und E2E-Tests gelten für beide Branches

### Phase 2: Release-Workflow-Anpassung (`.github/workflows/release.yml`)
1. Trigger prüfen: Soll das Release-Workflow nur auf `main` auslösen, oder auch auf `staging`?
   - **Annahme (zu klären):** Release-Workflow wird weiterhin nur auf `main` ausgelöst; `staging` erhält keinen eigenen Release-Trigger
2. Neue Gate-Logik hinzufügen: Nach erfolgreichem Merge in `staging` kann automatisch ein PR gegen `main` erzeugt werden (optional)
   - Dies könnte durch einen separaten Workflow (`staging-to-main.yml`) oder durch zusätzliche Steps im bestehenden Workflow erfolgen

### Phase 3: Branch-Protection-Rules (GitHub-UI oder Terraform/Automation)
1. Für `staging`:
   - PRs müssen vor Merge alle Checks bestehen (von `test.yml`)
   - Direktes Pushing auf `staging` blockieren (nur über PR erlaubt)
2. Für `main`:
   - PRs müssen alle Checks bestehen
   - Direktes Pushing auf `main` blockieren
   - **Annahme:** Nur PRs aus `staging` sind zulässig

### Abhängigkeiten
- Der bestehende `test.yml`-Workflow läuft für beide Branches unabhängig
- Der `release.yml`-Workflow bleibt vorerst nur auf `main`, kann aber später mit Logik für automatisierte PRs erweitert werden
- Die Staging/Main-Struktur basiert auf GitHub selbst – keine Code-Änderungen in der Anwendung sind notwendig

## Konfiguration

### Workflow-Konfiguration
- **Branch-Trigger:** `test.yml` muss `staging` in den Trigger-Branches aufnehmen
  ```yaml
  on:
    push:
      branches: [master, staging]  # oder main/staging, je nach Namenskonvention
    pull_request:
      branches: [master, staging]
  ```
- **Release-Trigger:** `release.yml` kann vorerst auf `master`/`main` bleiben, wird aber evtl. um automatisierte Promotionslogik erweitert

### Branch-Protection-Rules (GitHub API / Settings)
- Müssen manuell oder durch Infrastruktur-Code (z. B. Terraform) konfiguriert werden
- Sichern zu, dass Merges nur nach bestandenen Checks erlaubt sind

### Quality Gates (optional)
- Mindestens X% Code-Coverage für `staging` (aktuell nicht in Workflows definiert, könnte über `dotnet test --collect:"XPlat Code Coverage"` implementiert werden)
- Sicherheitsscan-Ergebnisse (aktuell nicht im Workflow, könnte über Dependabot/SAST-Tools erfolgen)

## Offene Fragen

1. **Branch-Namenskonvention:**
   - Ist `staging` der korrekte Name, oder sollte es `develop`, `pre-release` o. ä. sein?
   - Wird die aktuelle `master`-Branch zu `main` umbenannt, oder bleibt sie `master`?

2. **Automatisierte PR-Erstellung:**
   - Soll nach erfolgreichem Merge in `staging` automatisch ein PR gegen `main` erzeugt werden?
   - Wenn ja: Sollte dieser PR automatisch gemergt werden, oder benötigt er manuelle Freigabe?

3. **Versionsmanagement (Semantic Release):**
   - Erfolgt die Versionsbumps nur beim Merge zu `main`, oder auch bei Merges zu `staging`?
   - Wie wird verhindert, dass `staging` mehrfach gebumpte Versionen enthält?

4. **Quality Gates und Qualitätsmetriken:**
   - Sollen Mindest-Coverage-Anforderungen definiert werden? Falls ja, wie hoch?
   - Welche zusätzlichen Sicherheitsprüfungen (Dependency-Scans, SAST) sollen Teil des Workflows sein?

5. **Notfall-Prozesse:**
   - Wie wird bei Hotfixes verfahren? Können diese direkt in `main` committed werden, oder müssen sie über `staging` gehen?
   - Gibt es einen Rollback-Prozess, falls etwas schiefgeht?

6. **CI/CD-Tool-Spezifika:**
   - Werden GitHub Branch-Protection-Rules manuell konfiguriert, oder sollten diese durch Infrastruktur-Code (z. B. via GitHub API / Terraform) definiert werden?
   - Ist eine zusätzliche Authentifizierung oder ein Review-Requirement für PRs gegen `main` gewünscht?

7. **Timing und Koordination:**
   - Wann soll der `staging`-Branch erstmalig erstellt werden (Initial Branch Creation)?
   - Sollen bestehende offene PRs zu `master` zu `staging` umgeleitet werden, oder geschlossen?
   - Wie wird mit bestehenden Commits umgegangen: Bleibt `master` als-ist, oder wird er neu basiert?

## Implementierungsreihenfolge (Empfohlener Prozess)

1. Klärung der offenen Fragen mit dem Kunden/Produktmanagement
2. Erstellung der `staging`-Branch basierend auf aktuellem `master`/`main`
3. Anpassung von `test.yml` (Trigger erweitern)
4. Konfiguration von GitHub Branch-Protection-Rules für `staging` und `main`
5. (Optional) Implementierung eines Workflows für automatisierte PRs von `staging` → `main`
6. (Optional) Integration von Quality-Gates und erweiterten Sicherheitsprüfungen
7. Kommunikation und Dokumentation des neuen Workflows für das Team
8. Migration bestehender PRs und Workflows
