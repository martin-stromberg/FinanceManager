# Finance Manager

[![License](https://img.shields.io/github/license/martin-stromberg/FinanceManager)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

`FinanceManager` ist eine Blazor-Server-Anwendung zur Verwaltung persönlicher Finanzen.  
Sie deckt Import, Klassifizierung und Verbuchung von Kontoauszügen sowie Reporting, Budgetplanung, Sparpläne, Wertpapiermanagement und Setup-/Admin-Funktionen ab.

## Features / Highlights

- Kontoauszüge importieren, klassifizieren und verbuchen (`StatementDraftsController`), inklusive mobiler Kontoauszugsansicht mit lesbarer Kartenstruktur, zweispaltigem Datum/Betrag, abgeschwächten gebuchten Einträgen sowie Kontakt-, Sparplan- und Wertpapierinformationen
- Konten, Sammelkonten, Kontakte, Sparpläne und Wertpapiere verwalten
- Berichte, KPI-Dashboards und Budgetauswertungen nutzen, inklusive bestandsgepruefter Hochrechnung fuer Wertpapier-Dividendenreports
- Anhänge und Sicherungen (Backup/Restore) verwalten
- Responsive Web-UI für kleine Viewports (mobile Topbar, responsive Container, mobile E2E-Abdeckung)
- Einstellungs-Ribbon mit stets sichtbaren Aktionen: Backup erstellen/hochladen, Profil speichern/zurücksetzen, Benachrichtigungen und Kontoauszugs-Importregeln speichern — unabhängig davon, welche Sektion gerade aufgeklappt ist
- JWT-Authentifizierung mit 30 Minuten Access-Token-Laufzeit, SecurityStamp-/Rollen-/Active-Revalidierung und DB-validiertem Refresh

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
| `Jwt:Key` | string | kein produktiver Standardwert | Signaturschluessel fuer JWT; in Produktion extern bereitstellen, nicht im Repository |
| `Jwt:Issuer` | string | `financemanager` | Erwarteter JWT-Issuer fuer Ausstellung und Validierung |
| `Jwt:Audience` | string | `financemanager` | Erwartete JWT-Audience fuer Ausstellung und Validierung |
| `Jwt:LifetimeMinutes` | int | `30` | JWT-/Cookie-Lebensdauer in Minuten |
| `DataProtection:KeysPath` | string | leer | Optionaler Pfad fuer den ASP.NET-Core-Data-Protection-Key-Ring; in produktionsnahen Deployments persistent und geschuetzt bereitstellen |
| `BackgroundTasks:Enabled` | bool | `true` | Aktiviert den `BackgroundTaskRunner` |
| `Workers:SecurityPriceWorker:Enabled` | bool | `true` | Aktiviert den Security-Price-Worker |
| `Backups:Security:MaxUploadBytes` | long | `104857600` | Maximale Uploadgroesse fuer Backup-ZIP-Dateien |
| `Backups:Security:MaxCompressedZipBytes` | long | `104857600` | Maximale komprimierte ZIP-Groesse fuer Backup-Validierung |
| `Backups:Security:MaxUncompressedNdjsonBytes` | long | `262144000` | Maximale entpackte NDJSON-Nutzlast im Backup |
| `Backups:Security:MaxZipEntries` | int | `1` | Maximal erlaubte ZIP-Entries pro Backup |
| `Backups:Security:MaxCompressionRatio` | int | `25` | Maximal erlaubtes Verhaeltnis zwischen entpackter und komprimierter Backup-Nutzlast |
| `Backups:Security:AllowedBackupVersions` | int[] | `[3]` | Erlaubte Backup-Metaversionen fuer Upload und Restore |
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
    "Key": "",
    "Issuer": "financemanager",
    "Audience": "financemanager",
    "LifetimeMinutes": 30
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
- `POST /api/setup/backups/upload` – ZIP-Backup hochladen; akzeptiert nur valide ZIP/NDJSON-Backups innerhalb der konfigurierten `Backups:Security`-Limits
- `POST /api/setup/backups/{id}/apply` – Backup synchron wiederherstellen; destruktiv und nur mit `BackupRestoreRequestDto`, dessen `confirmationText` exakt dem gespeicherten Dateinamen entspricht
- `POST /api/setup/backups/{id}/apply/start` – destruktiven Restore als Hintergrundtask starten; verwendet dieselbe serverseitige Dateinamen-Bestaetigung
- `POST /api/securities/{id}/prices/import` – Wertpapierkurse importieren
- `POST /api/postings/{id}/reverse` – Buchung stornieren (Reversal)
- `GET|POST|PUT|DELETE /api/admin/users...` – administrative Benutzerverwaltung; serverseitig auf JWT-authentifizierte Benutzer mit Rolle `Admin` beschränkt. Authentifizierte Nicht-Admins erhalten `403 Forbidden`, anonyme Aufrufe `401 Unauthorized`.

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
  Veröffentlichung laufen `npm ci`, ein Restore der Solution, die Unit- und
  Integrationstests als Release-Gate sowie ein vollständiger Solution-Build.
  Die Playwright-E2E-Tests bleiben Bestandteil der Testsuite, blockieren aber
  den Release-Publish-Pfad nicht. Anschließend wird
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
- JWT-Secrets gehoeren nicht ins Repository. Betreiber stellen produktive Werte
  ueber die .NET-Konfiguration bereit, bevorzugt als Environment-Variablen:
  `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` und `Jwt__LifetimeMinutes`.
  `Jwt__LifetimeMinutes` ist auf 30 Minuten ausgelegt; Refresh und Request-
  Authentifizierung validieren Benutzerstatus, SecurityStamp und aktuelle Rollen
  serverseitig gegen die Datenbank.
  In produktionsnahen Umgebungen (alle Umgebungen ausser `Development`) bricht
  der Start ab, wenn `Jwt__Key` fehlt, ein Platzhalter ist, weniger als 32
  UTF-8-Bytes Schluesselmaterial enthaelt oder `Jwt__Issuer`,
  `Jwt__Audience` beziehungsweise `Jwt__LifetimeMinutes` ungueltig sind.
- AlphaVantage API Keys werden vor der Persistenz mit ASP.NET Core Data
  Protection geschuetzt und nur fuer den unmittelbaren API-Aufruf entschluesselt.
  Fuer produktionsnahe Deployments muss der Data-Protection-Key-Ring erhalten
  bleiben, sonst koennen gespeicherte AlphaVantage-Keys nach Containerwechsel,
  Neuinstallation oder Deployment nicht verlaesslich gelesen werden. Setze
  dafuer `DataProtection__KeysPath` auf ein persistentes, zugriffsgeschuetztes
  Volume und sichere diesen Key-Ring gemeinsam mit der Datenbank.

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
