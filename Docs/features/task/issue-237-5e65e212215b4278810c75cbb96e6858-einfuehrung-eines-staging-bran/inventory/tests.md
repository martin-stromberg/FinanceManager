# Bestandsaufnahme: Test-Struktur

## Test-Projekte

Die Anforderung erwähnt 3 Test-Projekte, die auf beiden Branches (`staging` und `main`) laufen müssen:

### 1. FinanceManager.Tests
**Zweck:** Unit-Tests für die Kernlogik

**Csproj:** `FinanceManager.Tests/FinanceManager.Tests.csproj`

**Testklassen (Auswahl):**
- `FinanceManager.Tests.Accounts.AccountServiceTests`
- `FinanceManager.Tests.Auth.UserAuthServiceTests`
- `FinanceManager.Tests.Budget.BudgetCrudServicesTests`
- `FinanceManager.Tests.Controllers.AttachmentsControllerTests`
- `FinanceManager.Tests.Infrastructure.BackupServiceTests`
- `FinanceManager.Tests.Reports.HomeKpiServiceTests`
- `FinanceManager.Tests.Securities.ReturnAnalysisServiceTests`
- und viele weitere (insgesamt 154 Test-Dateien im gesamten Tests-Verzeichnis)

**Konfiguration in Workflows:**
- **test.yml:** `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --filter "Category!=OsInterface"`
  - Filtert Tests mit `Category=OsInterface` aus
  - Logger: TRX + Console
  - Ergebnis: `test-results-regular-tests.trx`
- **release.yml:** `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --configuration Release --no-restore`
  - Keine spezielle Filter
  - Release-Konfiguration (Optimierung)

### 2. FinanceManager.Tests.Integration
**Zweck:** Integrationstests für Service-Layer und Datenbankoperationen

**Csproj:** `FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj`

**Konfiguration in Workflows:**
- **test.yml:** `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug`
  - Debug-Konfiguration
  - Logger: TRX + Console
  - Ergebnis: `test-results-regular-integration.trx`
- **release.yml:** `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --configuration Release --no-restore`
  - Release-Konfiguration

### 3. FinanceManager.Tests.E2E
**Zweck:** End-to-End Tests (wahrscheinlich UI/Playwright-basiert)

**Csproj:** `FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj`

**Besonderheit:** Wird mit `continue-on-error: true` ausgeführt, da E2E-Tests instabiler sein können.

**Konfiguration in Workflows:**
- **test.yml:** `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --logger "trx;LogFileName=test-results-e2e-tests.trx" --logger "console;verbosity=normal"`
  - Debug-Konfiguration
  - continue-on-error: true
  - Ergebnis: `test-results-e2e-tests.trx`
- **release.yml:** E2E Tests werden **nicht** im Release-Workflow ausgeführt

---

## Build-Abhängigkeiten (in test.yml)

```
Build Phase:
├─ FinanceManager.Tests (Debug)
├─ FinanceManager.Tests.E2E (Debug)
└─ FinanceManager.Tests.Integration (Debug)
   └─ npm ci (Playwright)
   └─ npx playwright install --with-deps
   └─ dotnet restore
```

---

## Test-Laufzeitverhalten

| Projekt | test.yml | release.yml | Abhängigkeiten |
|---------|----------|------------|-----------------|
| FinanceManager.Tests | Ja (Debug, gefiltert) | Ja (Release) | dotnet, npm (Playwright) |
| FinanceManager.Tests.Integration | Ja (Debug) | Ja (Release) | dotnet, npm (Playwright) |
| FinanceManager.Tests.E2E | Ja (Debug, optional) | **Nein** | dotnet, npm (Playwright Browsers) |

---

## Beobachtungen

| Aspekt | Befund |
|--------|--------|
| **Test-Filter** | Nur FinanceManager.Tests nutzt einen Filter (Category!=OsInterface). Grund unklar; möglicherweise System-abhängige Tests. |
| **E2E-Handling** | E2E Tests haben continue-on-error: true, aber werden im Release-Workflow komplett übersprungen. |
| **Parallelisierung** | Alle 3 Tests in test.yml laufen sequenziell (nicht parallel). |
| **Konfigurationen** | test.yml: Debug (schneller). release.yml: Release (optimiert, mit Tests als Gate). |
| **Playwright** | Installiert in test.yml-Setup. Wird für alle 3 Projekte erwartet (auch Unit Tests). |
| **Reproduzierbarkeit** | Beide Workflows nutzen dieselbe Test-Suite; Ergebnisse sollten konsistent sein (außer Runtime-Abhängigkeiten). |

---

## Test-Artefakte

**test.yml generiert:**
- `FinanceManager.Tests/TestResults/*regular*.trx` (regelmäßige Unit Tests)
- `FinanceManager.Tests.Integration/TestResults/*regular*.trx` (regelmäßige Integration Tests)
- `FinanceManager.Tests/TestResults/*os-interface*.trx` (OS-Interface Tests - separate Uploads)
- `FinanceManager.Tests.Integration/TestResults/*os-interface*.trx`
- `FinanceManager.Tests.E2E/TestResults/*e2e*.trx`

**Aufbewahrung:** 14 Tage

**release.yml:** Generiert keine Test-Artefakte; Tests sind nur Gate für das Release.
