# Finance Manager

[![Tests](https://img.shields.io/github/actions/workflow/status/martin-stromberg/FinanceManager/test.yml?label=Tests)](https://github.com/martin-stromberg/FinanceManager/actions)
[![Release](https://img.shields.io/github/actions/workflow/status/martin-stromberg/FinanceManager/release.yml?label=Release)](https://github.com/martin-stromberg/FinanceManager/actions)
[![License](https://img.shields.io/github/license/martin-stromberg/FinanceManager)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)

`FinanceManager` ist eine Blazor-Server-Anwendung zur Verwaltung persönlicher Finanzen.  
Sie deckt Import, Klassifizierung und Verbuchung von Kontoauszügen sowie Reporting, Budgetplanung, Sparpläne, Wertpapiermanagement und Setup-/Admin-Funktionen ab.

## Features / Highlights

- Kontoauszüge importieren, klassifizieren und verbuchen (`StatementDraftsController`), inklusive mobiler Kontoauszugsansicht mit lesbarer Kartenstruktur, zweispaltigem Datum/Betrag, abgeschwächten gebuchten Einträgen sowie Kontakt-, Sparplan- und Wertpapierinformationen
- Kontoauszugsentwürfe im Massenänderungsmodus bearbeiten, Zeilen zum Löschen vormerken und neue Zeilen ergänzen
- Konten, Sammelkonten, Kontakte, Sparpläne und Wertpapiere verwalten, inklusive sichtbarer SVG-Symbole für Kontakte
- Berichte, KPI-Dashboards und Budgetauswertungen nutzen, inklusive bestandsgepruefter Hochrechnung fuer Wertpapier-Dividendenreports
- Anhänge und Sicherungen (Backup/Restore) verwalten
- Responsive Web-UI für kleine Viewports (mobile Topbar, responsive Container, mobile Ribbon-Shortcuts, mobile E2E-Abdeckung)
- Einstellungs-Ribbon mit stets sichtbaren Aktionen: Backup erstellen/hochladen, Profil speichern/zurücksetzen, Benachrichtigungen, Kontoauszugs-Importregeln und Update-Einstellungen speichern sowie Update-Prüfung, Installation und Lock-Reset auslösen — unabhängig davon, welche Sektion gerade aufgeklappt ist
- Versionsinformation im Programmmenü (Footer) angezeigt — aktuelle Versionnummer oder Fallback `"Version unbekannt"`
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
- Typischer Flow: Import (`/api/statement-drafts/upload` oder `mass-import`) → Klassifizieren → optional im Massenänderungsmodus nachbearbeiten → Buchen → Reporting

### Help-Dokumentation und Sicherheit

- Help ist unter `/help` verfügbar; die Markdown-Quellen liegen unter `Docs/help/`.
- Help-Markdown wird über einen Whitelist-Renderer ausgegeben. Rohes HTML, Skripte,
  Inline-Handler und unsichere Linkziele sind kein unterstütztes Format.
- Für Help-Seiten und Help-Assets gilt eine restriktive CSP. Der Build erzeugt
  `FinanceManager.Web/wwwroot/help/help-assets.sha256`; Änderungen unter
  `Docs/help/` erfordern daher einen neuen Build vor dem Deployment.

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
| `Updates:Enabled` | bool | `false` | Aktiviert die automatische Suche nach Self-Update-Releases (steuert die externe `msTools.Updater`-Bibliothek) |
| `Updates:SourceType` | string | `Github` | Update-Quelle: `Github` oder `LocalFolder` |
| `Updates:RepositoryOwner` / `Updates:RepositoryName` | string | `martin-stromberg` / `FinanceManager` | GitHub-Repository (nur bei `SourceType: Github`) |
| `Updates:LocalFolderPath` | string? | `null` | Lokales Quellverzeichnis (nur bei `SourceType: LocalFolder`; Fallback: `{WorkingDirectory}/source`) |
| `Updates:EnableAutomaticDownload` | bool | `true` | Download nach erfolgreicher Versionsprüfung |
| `Updates:EnableAutomaticInstallation` | bool | `false` | Installation nach erfolgreichem Download |
| `Updates:ManifestAssetName` | string | `update.json` | Release-Asset mit Update-Metadaten |
| `Updates:WorkingDirectory` | string | `updates` | Betriebsverzeichnis fuer Pending-Paket, Status, Lock, Staging und Skripte |
| `Updates:ServiceName` | string? | leer | Optionaler Service-Override fuer die aktuelle Plattform; in der Admin-UI mit Windows-/Linux-Service-Autocomplete |
| `Updates:ExecutablePath` | string? | leer | Windows-Fallback, wenn kein Service gesteuert wird; muss absolut im aktuellen Anwendungsverzeichnis liegen; nicht mehr ueber die Admin-UI editierbar |
| `Updates:HealthTimeoutSeconds` | int | `120` | Wartezeit der Setup-UI bis zur Wiedererreichbarkeit von `/health`, serverseitig auf 10..600 begrenzt; nicht mehr ueber die Admin-UI editierbar |
| `Updates:MaxAssetBytes` | long | `536870912` | Maximale Groesse eines Update-ZIP-Assets |
| `Updates:HostedServicesEnabled` | bool | `true` | Aktiviert `AutoUpdateCheckerService` und `AutoUpdateSchedulerService` |
| `Updates:SourceCheck:Interval` | int | `360` | Prüfintervall in Minuten (neue Syntax; Legacy-Alias: `CheckIntervalMinutes`) |
| `Updates:SourceCheck:TimeRanges` | Array | `[]` | Zeitfenster für Prüfungen mit `DayOfWeek`, `StartTime`, `EndTime`; leer = immer erlaubt |
| `Updates:StopHostAfterScriptStart` | bool | `false` | Host nach erfolgreichem Update-Skriptstart beenden |
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

external/msTools.Updater/v0.2.0         # Geprueftes externes Updater-Release fuer den Testlauf vor NuGet
```

**Technologien:** .NET 10, ASP.NET Core, Blazor Server, EF Core (SQLite), ASP.NET Identity/JWT, xUnit, bUnit, Playwright.

### Self-Update-System

Das Self-Update-System wird aus dem externen Release-Artefakt `msTools.Updater` eingebunden. Die fruehere lokale Bibliothek `SoftwareSchmiede.AutoUpdate` und ihr Testprojekt sind nicht mehr Teil der Solution.

Bis zur geplanten NuGet-Veröffentlichung liegt der geprüfte Release `v0.2.0` aus `martin-stromberg/msTools.Updater` unter [`external/msTools.Updater/v0.2.0/`](external/msTools.Updater/v0.2.0/). Dort sind das originale `release.zip`, `SHA256SUMS.txt`, eine Herkunfts-README und die entpackte `lib/msTools.Updater.dll` abgelegt; der dokumentierte SHA-256 des ZIPs ist `adf4e64e18345ac8ef30e8c626c639489b3eb84accae0f2f5ab61b59e8ea029c`.

`FinanceManager.Web` referenziert die entpackte DLL direkt und kopiert sie in Build- und Publish-Ausgaben. Die Integration erfolgt weiterhin über den FinanceManager-Adapter (`UpdateOrchestratorAdapter`); Controller, DTOs, Admin-UI und REST-API bleiben dadurch aus Anwendersicht unverändert.

## API-Dokumentation

Einstiegspunkte:

- `POST /api/auth/login` – Anmeldung
- `POST /api/statement-drafts/upload` – Einzeldatei als Entwurf importieren
- `POST /api/statement-drafts/mass-import` – Massenimport analysieren/ausführen
- `POST /api/setup/backups/upload` – ZIP-Backup hochladen; akzeptiert nur valide ZIP/NDJSON-Backups innerhalb der konfigurierten `Backups:Security`-Limits
- `POST /api/setup/backups/{id}/apply` – Backup synchron wiederherstellen; destruktiv und nur mit `BackupRestoreRequestDto`, dessen `confirmationText` exakt dem gespeicherten Dateinamen entspricht
- `POST /api/setup/backups/{id}/apply/start` – destruktiven Restore als Hintergrundtask starten; verwendet dieselbe serverseitige Dateinamen-Bestaetigung
- `GET /api/setup/update/status`, `GET|PUT /api/setup/update/settings` und `GET /api/setup/update/services` – Self-Update-Status, Admin-Einstellungen und Service-Autocomplete; nur Rolle `Admin`
- `POST /api/setup/update/check` – GitHub-Release-Manifest abrufen, passendes Paket laden und Hash/ZIP validieren
- `POST /api/setup/update/schedule` – geplante Installationszeit fuer ein vorbereitetes Update speichern
- `POST /api/setup/update/install/start` – vorbereitetes Update nach Downtime-Bestaetigung installieren; erstellt Lock und startet ein externes Update-Skript
- `POST /api/setup/update/lock/reset` – verwaisten Update-Lock administrativ zuruecksetzen, sofern dieser Prozess keine laufende Installation kennt und der Lock aelter als das Health-Timeout ist
- `GET /api/background-tasks/active` – aktive und wartende Background-Tasks fuer authentifizierte Nutzer abrufen; das UI startet das Polling nur bei erkannter Anmeldung und beendet es nach einem `401 Unauthorized`
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

- **Branch-Workflow:** `staging` ist der Integrations- und Qualitätssicherungsbranch, `master` bleibt der ausschließliche Release-Branch. Feature- und Hotfix-PRs richten sich gegen `staging`. Der Test-Workflow [`test.yml`](.github/workflows/test.yml) läuft auf `push` und `pull_request` für beide Branches. Nach erfolgreichem Lauf auf `staging` erstellt [`staging-to-master.yml`](.github/workflows/staging-to-master.yml) automatisch einen Draft-PR von `staging` nach `master`, der manuell durch einen Maintainer gemergt werden muss. Siehe [CONTRIBUTING.md](CONTRIBUTING.md#branch-workflow-staging--master) für Details.
- `test.yml` erzwingt zusätzlich einen Line-Coverage-Schwellwert von 70 % (`FinanceManager.Tests` und `FinanceManager.Tests.Integration`, gemessen via `--collect:"XPlat Code Coverage"` und `reportgenerator`) sowie automatisierte Dependency-Updates über [`dependabot.yml`](.github/dependabot.yml) (NuGet, npm, GitHub Actions) als Quality Gates vor einem Merge auf `staging`/`master`.
- Branch-Protection-Regeln für `staging` und `master` (Pflicht-Status-Checks, mindestens 1 Approval, kein Direct-Push, `master` nur aus `staging`) werden in den GitHub-Repository-Einstellungen konfiguriert, nicht im Repository-Code.
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
  self-contained `win-x64`- und `linux-x64`-Anwendung veröffentlicht.
- Die vollständigen Inhalte der runtime-spezifischen Publish-Verzeichnisse
  werden als `FinanceManager-vX.Y.Z-win-x64.zip` und
  `FinanceManager-vX.Y.Z-linux-x64.zip` verpackt. Zusaetzlich erzeugt der
  Workflow `update.json` mit Plattform, Runtime, Asset-URL, Dateigroesse,
  SHA-256 und Release Notes. Alle drei Assets werden am passenden
  GitHub-Release veröffentlicht. Fehler bei Versionierung, Tests, Build,
  Publish, Manifest oder Paketierung verhindern unvollständige Releases.
  Ein Push ohne release-relevante Commits endet erfolgreich ohne neues Release.
  Bei der Reparatur eines unvollständigen Assets wird dessen Release-Tag
  ausgecheckt; die Reparatursuche verarbeitet alle Seiten der
  GitHub-Release-API.
- Das Self-Update ist eine Admin-Funktion im Setup. Die UI zeigt Status,
  Paketmetadaten und Release Notes; die technische Update-Quelle (GitHub-Repository
  oder lokaler Ordner via `Updates:SourceType`) sowie Manifest-Asset und
  Arbeitsverzeichnis werden serverseitig über `msTools.Updater`
  konfiguriert. Sichtbare Einstellungswerte werden über den globalen
  Ribbon-Button `Speichern` persistiert; die Aktionen `Jetzt prüfen`,
  `Update installieren` und `Update-Lock zurücksetzen` liegen ebenfalls im
  Setup-Ribbon. Der Service-Name bietet Vorschläge aus Windows-Diensten oder
  Linux-systemd-Services. Vor manueller Installation verlangt die UI eine
  Downtime-Bestaetigung und wartet nach Start erst auf einen beobachteten
  Ausfall, bevor ein spaeterer `/health`-Erfolg als abgeschlossen gilt. Vor der
  Installation validiert der Server Hash, Groesse, ZIP-Pfade, Service-/EXE-Ziel
  und Lock. Eine geplante Installationszeit wird vom Scheduler minuetlich
  geprueft und startet ein bereites Update ohne erneute Benutzerbestaetigung.
  Ein Admin-Lock-Reset loescht nur vorhandene Locks, die aelter als der interne
  Health-Timeout sind, und verweigert den Reset, solange der aktuelle Prozess
  noch eine laufende Installation kennt.
- **Verbesserungen (Issue #206):** Das Update-System wurde fuer Produktionsumgebungen
  (insbesondere Linux) stabilisiert: Lock-Verwaltung ist atomarer, verwaiste Locks
  werden zuverlaessiger erkannt und bereinigt, der Service-Neustart und die
  Versionserkennung nach dem Update sind robuster, und kritische Fehlermeldungen
  sind vollstaendig lokalisiert. Die UI zeigt waehrend der Installation einen
  Fortschrittsstatus an (Installation laeuft → Warte auf Neustart). Siehe
  `Docs/help/updates/troubleshooting.md` fuer Linux-spezifische Hinweise.
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
- Branch-Workflow: PRs gegen `staging`, automatisierte Promotion nach `master`
- API-Fehlerbehandlung (`ValidationProblem` vs. standardisierte `origin/code/message`-Antworten)
- Lokalisierungskonventionen für `.resx` unter `Resources/...`
- PR-Hinweise zu Ressourcenpfaden und CI-Checks

## Roadmap

### Aktuelle / In Bearbeitung

**Issue #224 – Update-Einstellungen vereinheitlichen** ✓ Abgeschlossen
- Technische Update-Konfiguration aus der Admin-UI entfernt und serverseitig normalisiert
- Update-Einstellungen an das globale Setup-Speicherpattern angebunden
- Update-Aktionen in das Setup-Ribbon verschoben
- Service-Name mit plattformspezifischem Autocomplete für Windows und Linux ergänzt
- Update-Statuswerte lokalisiert

### Geplant

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
