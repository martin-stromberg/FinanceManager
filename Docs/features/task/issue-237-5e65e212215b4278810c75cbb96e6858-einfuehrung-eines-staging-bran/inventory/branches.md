# Bestandsaufnahme: Branch-Konfiguration

## Lokale Branches

```
master            – Hauptbranch (existiert lokal)
task/...          – Aktueller Feature-Branch (existiert lokal)
```

## Remote Branches

### Primäre Branches
- **remotes/origin/master** – Haupt-Branch (Integration & Release)
- **remotes/origin/staging** – Staging-Branch **existiert bereits auf Remote**

### Weitere Feature-Branches
- Zahlreiche Feature- und Bugfix-Branches (z.B. `task/issue-*`, `remotes/origin/79-upgrade-auf-net-10`, etc.)

---

## Branch-Status

| Branch | Lokal | Remote | Trigger in test.yml | Trigger in release.yml | Zweck |
|--------|-------|--------|---------------------|------------------------|--------|
| **master** | Ja | Ja | **Ja** (push + pull_request) | **Ja** (push + tags) | Haupt-Integration & Release |
| **staging** | Nein | **Ja** | **Nein** | **Nein** | Integrations-/QA-Branch (geplant) |

---

## Branch-Protection-Regeln

**Status:** Nicht im Repository konfiguriert.

Die Anforderung nennt folgende geplante Regeln:

### Für `staging`
- PRs müssen vor Merge alle Checks bestehen (von `test.yml`)
- Direktes Pushing auf `staging` blockieren (nur über PR erlaubt)

### Für `main` (oder `master`)
- PRs müssen alle Checks bestehen
- Direktes Pushing auf `main` blockieren
- Nur PRs aus `staging` sind zulässig (geplant)

**Implementierung:** Solche Regeln werden typischerweise über:
1. GitHub UI (Settings → Branches → Branch protection rules)
2. GitHub API (Infrastruktur-Code, z.B. Terraform/IaC)
3. GitHub CLI (`gh api`)

**Keine Konfiguration im Repository vorhanden** – müssen manual oder extern konfiguriert werden.

---

## Workflow-Integration pro Branch

### test.yml Execution Path

**Aktuell:**
```
master: push → test.yml ausführen
master: pull_request → test.yml ausführen
staging: (kein Trigger) → test.yml wird NICHT ausgeführt
```

**Geplant:**
```
master: push → test.yml ausführen
master: pull_request → test.yml ausführen
staging: push → test.yml ausführen
staging: pull_request → test.yml ausführen
```

### release.yml Execution Path

**Aktuell & geplant (unverändert):**
```
master: push → release.yml ausführen (prüft Semantic Release)
tags: v* → release.yml ausführen
staging: (kein Trigger) → release.yml wird nicht ausgeführt
```

---

## Abhängigkeiten & Koordination

| Aspekt | Befund |
|--------|--------|
| **Staging-Existenz** | Remote-Branch existiert bereits; nur Workflow-Integration fehlt. |
| **Concurrency** | test.yml nutzt cancel-in-progress für schnelles Feedback. release.yml nutzt cancel-in-progress: false. |
| **Git-Konfiguration** | Keine Branch-Schutzregeln im Code definiert; müssen extern verwaltet werden. |
| **Checkout-Behavior** | Workflows nutzen actions/checkout@v4 ohne explizite Branch-Angabe; verwenden implizit den Trigger-Branch. |
| **Rollout-Strategie** | Staging-Branch auf Remote vorhanden, aber nicht aktiviert. Einfache Erweiterung der Trigger-Definitionen ausreichend. |

---

## Offene Punkte (aus Anforderung)

1. **Branch-Namenskonvention:** `staging` scheint festgelegt zu sein (Remote-Branch existiert). Keine Alternative sichtbar.
2. **master vs. main:** Repository nutzt `master` (nicht `main`). Workflows konfigurieren `master`.
3. **Automatisierte PRs (staging → master):** Nicht vorhanden. Müsste als separater Workflow oder Step implementiert werden.
4. **Versionsmanagement (Semantic Release):** Erfolgt nur beim Merge zu `master`, nicht zu `staging`. Zu bestätigen.
5. **Hotfix-Prozess:** Nicht dokumentiert. Müssen definiert werden.
