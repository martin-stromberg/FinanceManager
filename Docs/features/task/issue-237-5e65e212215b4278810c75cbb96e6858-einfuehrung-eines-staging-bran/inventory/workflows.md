# Bestandsaufnahme: GitHub Actions Workflows

## test.yml

**Datei:** `.github/workflows/test.yml`

### Trigger-Konfiguration
```yaml
on:
  push:
    branches: [master]
  pull_request:
    branches: [master]
```

**Status:** Nur `master` ist konfiguriert. Der `staging`-Branch ist **nicht** enthalten.

### Test-Umgebung
- **Runner:** `windows-latest`
- **Timeout:** 60 Minuten
- **.NET Version:** 10.0.x

### Durchgeführte Schritte

1. **Dependency-Installation:**
   - npm ci (für Dependencies)
   - npx playwright install --with-deps (Playwright Browser)
   - dotnet restore

2. **Build (Debug-Konfiguration):**
   - FinanceManager.Tests
   - FinanceManager.Tests.E2E
   - FinanceManager.Tests.Integration

3. **Testausführung:**
   - `FinanceManager.Tests` – Regular Unit Tests (Filter: `Category!=OsInterface`)
   - `FinanceManager.Tests.Integration` – Integration Tests
   - `FinanceManager.Tests.E2E` – E2E Tests (continue-on-error: true)

4. **Artifact-Upload:**
   - Test-Results (TRX-Format) für Regular Tests und Integration Tests
   - E2E Test Results
   - Aufbewahrung: 14 Tage

### Abhängigkeiten
- Tests laufen unabhängig für jeden Trigger (push oder pull_request)
- Concurrency-Gruppe: `test-${{ github.workflow }}-${{ github.ref }}` – cancel-in-progress: true

---

## release.yml

**Datei:** `.github/workflows/release.yml`

### Trigger-Konfiguration
```yaml
on:
  push:
    branches: [master]
    tags: ["v*"]
```

**Status:** Nur `master` und Tags sind konfiguriert. Der `staging`-Branch ist **nicht** enthalten (und sollte laut Anforderung auch nicht sein).

### Release-Prozess

#### Vorbedingungen
- Semantic Release prüft, ob eine neue Version erforderlich ist (script: `resolve-release-version.mjs`)
- Nur wenn `released == 'true'`, werden weitere Steps ausgeführt

#### Durchgeführte Schritte (wenn Release erforderlich)

1. **Dependency-Setup:**
   - Node.js 22
   - .NET 10.0.x
   - npm ci

2. **Test-Gate:**
   - FinanceManager.Tests (Release-Konfiguration)
   - FinanceManager.Tests.Integration (Release-Konfiguration)
   - Beide müssen bestehen, bevor der Build fortgesetzt wird

3. **Build (Release-Konfiguration):**
   - dotnet build FinanceManager.sln

4. **Publish (Self-Contained):**
   - Zwei Runtimes: win-x64, linux-x64
   - Generiert `release-metadata.json` für jede Runtime
   - Output: `publish/{runtime}/`

5. **Packaging:**
   - Erstellt ZIP-Archive: `FinanceManager-v{version}-{runtime}.zip`
   - Validiert Archive nicht leer

6. **Release-Notes & Manifest:**
   - Generiert Release-Notes via `gh release view`
   - Erstellt `update.json` Manifest (via `generate-update-manifest.mjs`)

7. **GitHub Release erstellen/aktualisieren:**
   - Für automatische Releases (Commit-Message triggert)
   - Für manuelle Releases (Tags)
   - Für existierende Releases (Asset-Upload)

### Release-Aktionen
- **create** + **automatic:** Automatisierte Release (npm run release)
- **create** + **manual:** Release via Tag (gh release create)
- **upload-existing:** Asset-Upload zu existierendem Release

### Dependencies
- Semantic Release Script (`scripts/resolve-release-version.mjs`)
- Release Script (`scripts/generate-update-manifest.mjs`)
- GitHub Token (GITHUB_TOKEN) erforderlich

---

## Beobachtungen

| Aspekt | Befund |
|--------|--------|
| **Workflow-Isolation** | test.yml und release.yml sind vollständig unabhängig; keine Abhängigkeiten zwischen ihnen. |
| **Test-Abdeckung** | Both workflows nutzen dieselben 3 Test-Projekte, aber zu unterschiedlichen Zeiten (test.yml: Debug, release.yml: Release). |
| **Branch-Namen** | Hardcodiert auf `master` in beiden Workflows. Keine Variablen oder Konfigurationsfiles. |
| **Concurrency-Handling** | test.yml: cancel-in-progress für schnelle Feedback. release.yml: cancel-in-progress: false (verhindert parallele Releases). |
| **Error-Handling** | E2E Tests: continue-on-error: true. Andere Tests sind erforderlich. |
| **Artifact-Management** | test.yml speichert TRX-Results; release.yml erstellt ZIP und JSON für Distribution. |
