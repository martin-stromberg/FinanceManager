# Finance Manager

[![License](https://img.shields.io/github/license/martin-stromberg/FinanceManager)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

`FinanceManager` ist eine Blazor-Server-Anwendung zur Verwaltung persönlicher Finanzen.  
Sie deckt Import, Klassifizierung und Verbuchung von Kontoauszügen sowie Reporting, Budgetplanung, Sparpläne, Wertpapiermanagement und Setup-/Admin-Funktionen ab.

## Features / Highlights

- Kontoauszüge importieren, klassifizieren und verbuchen (`StatementDraftsController`)
- Konten, Sammelkonten, Kontakte, Sparpläne und Wertpapiere verwalten
- Berichte, KPI-Dashboards und Budgetauswertungen nutzen, inklusive Hochrechnung fuer Wertpapier-Dividendenreports
- Anhänge und Sicherungen (Backup/Restore) verwalten
- Responsive Web-UI für kleine Viewports (mobile Topbar, responsive Container, mobile E2E-Abdeckung)
- Einstellungs-Ribbon mit stets sichtbaren Aktionen: Backup erstellen/hochladen, Profil speichern/zurücksetzen, Benachrichtigungen und Kontoauszugs-Importregeln speichern — unabhängig davon, welche Sektion gerade aufgeklappt ist

## Installation / Setup

### Voraussetzungen

- .NET SDK 10.0

### Lokal starten

```bash
dotnet restore
dotnet build FinanceManager.sln
dotnet run --project FinanceManager.Web
```

Hinweise:
- In Development sind laut `launchSettings.json` u. a. `https://localhost:7013` und `http://localhost:5208` hinterlegt.
- Beim Start werden Migrationen/Initialisierung ausgeführt (`ApplyMigrationsAndSeed()` in `ProgramExtensions`).

## Usage

- Web-App starten: `dotnet run --project FinanceManager.Web`
- Anmelden/Registrieren über die UI
- Typischer Flow: Import (`/api/statement-drafts/upload` oder `mass-import`) → Klassifizieren → Buchen → Reporting

## Konfiguration

Wesentliche Konfigurationswerte aus `appsettings*.json` und Startup-Code:

| Parameter | Typ | Standardwert | Beschreibung |
|---|---|---|---|
| `ConnectionStrings:Default` | string | `Data Source=financemanager.db` (Fallback) | Standard-SQLite-Datenbank (Fallback in `AddInfrastructure`) |
| `Jwt:Key` | string | `""` (appsettings.json) | Signaturschlüssel für JWT |
| `Jwt:LifetimeMinutes` | int | `43200` | JWT-/Cookie-Lebensdauer in Minuten |
| `BackgroundTasks:Enabled` | bool | `true` | Aktiviert den `BackgroundTaskRunner` |
| `Workers:SecurityPriceWorker:Enabled` | bool | `true` | Aktiviert den Security-Price-Worker |
| `AlphaVantage:Quota:MaxSymbolsPerRun` | int | `8` | Begrenzung pro Abruflauf |
| `AlphaVantage:Quota:RequestsPerMinute` | int | `4` | API-Rate-Limit pro Minute |
| `FileLogging:Enabled` | bool | `false` (appsettings.json) | Aktiviert Dateilogging |
| `Identity:Lockout:MaxFailedAccessAttempts` | int | `3` | Max. Fehlversuche bis Lockout |
| `Identity:Password:RequiredLength` | int | `8` | Mindestlänge Passwort |
| `Data/KnownContacts.json` | JSON-Datei | mitgelieferte Beispiele | Programmliste bekannter Unternehmen und Alias-Muster für automatische Kontaktanlage beim Kontoauszugsimport |

```json
{
  "ConnectionStrings": {
    "Default": "Data Source=financemanager.db"
  },
  "Jwt": {
    "Key": "PLEASE_SET_A_LONG_RANDOM_SECRET",
    "LifetimeMinutes": 43200
  },
  "BackgroundTasks": {
    "Enabled": true
  },
  "Workers": {
    "SecurityPriceWorker": {
      "Enabled": true
    }
  }
}
```

## Architektur / Projektstruktur

Schichten und Projekte laut Solution:

```text
FinanceManager.Web                      # Blazor Server UI + API Controller
FinanceManager.Application              # Anwendungslogik / Services
FinanceManager.Domain                   # Domain-Modelle
FinanceManager.Infrastructure           # EF Core, Persistenz, Integrationen
FinanceManager.Shared                   # Gemeinsame DTOs / Client
FinanceManager.Shared.Dtos.Budget       # Budget-DTO-Paket
FinanceManager.Tests                    # Unit- und Komponenten-Tests (xUnit/bUnit)
FinanceManager.Tests.Integration        # Integrationstests
FinanceManager.Tests.E2E                # Playwright-End-to-End-Tests
```

Technologien: .NET 10, ASP.NET Core, Blazor Server, EF Core (SQLite), ASP.NET Identity/JWT, xUnit, bUnit, Playwright.

## API-Dokumentation

Einstiegspunkte:

- `POST /api/auth/login` – Anmeldung
- `POST /api/statement-drafts/upload` – Einzeldatei als Entwurf importieren
- `POST /api/statement-drafts/mass-import` – Massenimport analysieren/ausführen
- `POST /api/securities/{id}/prices/import` – Wertpapierkurse importieren
- `POST /api/postings/{id}/reverse` – Buchung stornieren (Reversal)

Weitere API-Dokumentation:
- `Docs/help/*/api.md`
- Controller unter `FinanceManager.Web/Controllers`

## Tests

Testprojekte und Frameworks:
- Unit/Komponente: xUnit v3, FluentAssertions, bUnit
- Integration: xUnit v3, `Microsoft.AspNetCore.Mvc.Testing`
- E2E: Playwright (`Microsoft.Playwright`) mit mobilen Sessions (`390x844`, Touch)

```bash
dotnet test FinanceManager.sln
```

## Deployment / CI/CD

- Die Release-Pipeline ist in [`.github/workflows/release.yml`](.github/workflows/release.yml) definiert.
- Ein Push auf `master` sowie ein Push eines Tags im Format `vX.Y.Z` starten den
  Workflow auf `windows-latest`. Auf `master` bestimmt Semantic Release die
  nächste Version aus Conventional Commits: `feat` erzeugt ein Minor-, `fix`
  ein Patch- und `feat!` beziehungsweise `BREAKING CHANGE` ein Major-Release.
  `docs`, `refactor` und `chore` erzeugen kein Release. Ein manueller
  `vX.Y.Z`-Tag hat Vorrang vor der automatischen Berechnung.
- Der Workflow verwendet Node 22 und das .NET-SDK `10.0.x`. Vor der
  Veröffentlichung laufen `npm ci`,
  `dotnet test FinanceManager.sln --configuration Release` und ein
  Solution-Build. Anschließend wird
  `FinanceManager.Web/FinanceManager.Web.csproj` mit .NET 10 als
  self-contained `win-x64`-Anwendung veröffentlicht.
- Der vollständige Inhalt des `publish/`-Verzeichnisses wird als
  `FinanceManager-vX.Y.Z-win-x64.zip` verpackt und als Asset am passenden
  GitHub-Release veröffentlicht. Fehler bei Versionierung, Tests, Build,
  Publish oder Paketierung verhindern die Veröffentlichung eines
  unvollständigen Releases. Ein Push ohne release-relevante Commits endet
  erfolgreich ohne neues Release. Bei der Reparatur eines unvollständigen
  Assets wird dessen Release-Tag ausgecheckt. Als vollständig gilt ein Asset
  nur mit erwartetem Namen, Upload-Status und positiver Dateigröße; die
  Reparatursuche verarbeitet alle Seiten der GitHub-Release-API.
- Produktionsnahe Konfiguration liegt in
  `FinanceManager.Web/appsettings.Production.json` (u. a. Kestrel-Endpoint
  `http://*:5003`, FileLogging aktivierbar).

## Contribution Guide

Siehe [CONTRIBUTING.md](CONTRIBUTING.md), insbesondere:
- API-Fehlerbehandlung (`ValidationProblem` vs. standardisierte `origin/code/message`-Antworten)
- Lokalisierungskonventionen für `.resx` unter `Resources/...`
- PR-Hinweise zu Ressourcenpfaden und CI-Checks

## Roadmap

Aus `Docs/features/task/issue-90-fb7b291b995c45f3b35a0bf86c8ae321-mobile-ansicht/plan.md` (Mobile Ansicht):

1. Responsive Basis/Breakpoints vereinheitlichen
2. Layout/Navigationscontainer mobilfähig machen
3. Generische Listen-/Kartenbausteine standardisieren
4. Kernseiten (Home/Reports/Budget/Setup) anpassen
5. Setup- und Securities-Tabs harmonisieren
6. Playwright-Fixture für Mobile Sessions erweitern
7. Mobile E2E-Flows ergänzen
8. Regression/Stabilisierung

## Changelog

- Laufender Änderungsverlauf: [changes.log](changes.log)
- Zusätzlich vorhanden: [CHANGELOG.md](CHANGELOG.md)

## Lizenz

MIT – siehe [LICENSE](LICENSE).

## Kontakt / Maintainer

- Repository: `martin-stromberg/FinanceManager`
- Rückfragen/Fehler: GitHub Issues im Repository verwenden.
