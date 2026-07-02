# Finance Manager

Kompakte Übersicht

`FinanceManager` ist eine Blazor Server Webanwendung (.NET 10) zur Verwaltung persönlicher Finanzen: Import und Klassifikation von Kontoauszügen, Buchungen (Bank / Kontakt / Sparplan / Wertpapier), Berichte und KPI‑Dashboard.

Kurz: Import → Klassifikation → Validierung → Buchung → Reporting.

## Features

### Für Nutzer
Kurzbeschreibung und was die Anwendung bietet (nicht‑technisch):
- Kontoauszüge (CSV/PDF) importieren und automatisch oder manuell kategorisieren
- Sparpläne verwalten (einmalig oder wiederkehrend) und Zielerreichung verfolgen
- Wertpapiertransaktionen (Kauf/Verkauf/Dividende) erfassen und Gebühren/Steuern berücksichtigen
- Wertpapierkurse per ING-CSV importieren (inkl. Upsert für neue/geänderte/unveränderte Tageskurse) über die Wertpapier-Kursseite und API
- Wertpapier-Performance-Analyse: TWR, IRR, CAGR, Sharpe Ratio, Max. Drawdown – mit Benchmark-Vergleich
- Transaktionssichere Kontoauszug-Buchung mit Single-Flight-Guard, idempotenten Wiederholungen und 409-Fehlervertrag
- Inline-Kontakterstellung aus Kontoauszugseinträgen mit automatischer Parent-Zuordnung, 409-Fehlervertrag bei Zuordnungskonflikten und Rollback-Versuch
- Budgetwirkung bei Buchung: Hinweise bei kritischen Budgets und Abschluss-Summary mit Vorher/Nachher/Delta
- Budget-Regeln mit Verwendungszweck-Pattern: optionales PurposePattern pro Regel (contains, case-insensitive) oder Regex-Matching inkl. Validierung
- Berichte und KPI‑Dashboard; Daten als CSV/XLSX exportieren
- Anhänge pro Buchung verwalten und Backups erstellen
- Buchungen stornieren: Fehlerhafte oder versehentlich erfasste Postings einfach rückgängig machen – mit automatischer Gegenbuchung und vollständiger Nachvollziehbarkeit

Schnelle Nutzung (Kurz):
1. Registrieren / Anmelden
2. Konto anlegen (Bankkontakt zuordnen)
3. Kontoauszug hochladen (Import) → Entwürfe prüfen
4. Einträge klassifizieren / fehlende Angaben ergänzen
5. Buchung durchführen → Postings werden erstellt

Hinweis: Diese README beschreibt die Entwicklungs‑ und Installationsdetails. Für eine reine Nutzer‑Installation wird eine gehostete Instanz oder eine einfache Install‑Anleitung benötigt (siehe `docs/` oder Admin/Hilfe im Webinterface).

### Für Entwickler
Kurz: welche Informationen Entwickler brauchen, um das Projekt lokal zu betreiben und weiterzuentwickeln.

## Installation

### Voraussetzungen
- .NET 10 SDK
- (optional) SQLite oder SQL Server für Produktion

### Schnellstart (lokal)

```bash
git clone <repo-url>
cd FinanceManager
dotnet restore
dotnet build
cd FinanceManager.Web
dotnet run
```

Default URLs are printed when you run the application (see the console output from `dotnet run`). Do not rely on a hardcoded port — configure `ASPNETCORE_URLS`, `launchSettings.json` or use `dotnet run --urls "http://localhost:5002;https://localhost:5003"` to override the defaults.

## Usage

- Start der Anwendung über `dotnet run` im Projekt `FinanceManager.Web`.
- Zugriff über die in der Konsole ausgegebenen URLs.
- Primärer Nutzerfluss: Importieren → Draft klassifizieren → validieren → buchen → Reports prüfen.

## Konfiguration

### Datenbankmigrationen

Änderungen am Domain‑Modell müssen gegen den korrekten DbContext migriert werden. Beispiel für `AppDbContext`:

```bash
dotnet ef migrations add 20260329_AddSomething -p FinanceManager.Infrastructure -s FinanceManager.Web --context AppDbContext --output-dir Data/Migrations
dotnet ef database update -p FinanceManager.Infrastructure -s FinanceManager.Web --context AppDbContext
```

### Tests & Qualität

```bash
dotnet test
```
- Formatierung: `dotnet format` vor PR
- CI soll Build, Tests und Formatierung prüfen

## Architektur

Schichtenmodell: `Domain` → `Application` → `Infrastructure` → `Web`

| Projekt | Inhalt |
|---------|--------|
| `FinanceManager.Domain` | Entitäten, Interfaces, Domain-Events |
| `FinanceManager.Application` | Services, Use Cases, Business-Logik |
| `FinanceManager.Infrastructure` | EF Core, Migrations, externe Integrationen |
| `FinanceManager.Shared` | Gemeinsame DTOs, Utilities |
| `FinanceManager.Shared.Dtos.Budget` | Budget-DTOs (separates Paket) |
| `FinanceManager.Web` | Blazor Server App, Controller, Razor Components |
| `FinanceManager.Analyzer` | Roslyn-Analyzer (Code-Qualitätsregeln) |
| `FinanceManager.Tests` | Unit-Tests |
| `FinanceManager.Tests.Integration` | Integrationstests |
| `FinanceManager.Tests.Integration.ApiClient` | Typisierter API-Client für Integrationstests |

Details: [`docs/architecture/`](docs/architecture/)

## Dokumentation & API

- Projektdokumentation unter `docs/` (Flows, Business Rules, API stubs)
- Installationsanleitung: `docs/install.md`
- Teil‑OpenAPI: `docs/api/openapi.yaml` (Accounts + models)
- Detaillierte Controller‑Docs in `docs/api/`
- Kontakt-Create/Assign-Vertrag: `docs/api/ContactsController.md` und `docs/flows/contact-create-auto-assign.md`
- Posting-Reversal-API: `POST /api/postings/{id}/reverse`, `GET /api/postings/{id}/validate-reversal` (Details: `docs/api/PostingsController.md`)
- [Planungsübersicht Renditeanalyse](docs/planning-renditeanalyse.md)
- [Anforderungen FA-WERT-REN-001](docs/requirements/FA-WERT-REN-001_Renditeanalyse.md)
- [Architektur-Blueprint Renditeanalyse](docs/architecture/architecture-blueprint-renditeanalyse.md)
- [Requirements: Budget-Verwendungszweck-Pattern inkl. Regex](Docs/requirements/budget-verwendungszweck.md)
- [Architektur: Budget-Verwendungszweck-Pattern inkl. Regex](Docs/architecture/budget-verwendungszweck.md)
- [Business-Dokumentation F017 Renditeanalyse](docs/business/features/F017-renditeanalyse.md)
- [Requirements: Wertpapierkurse ING-CSV-Import](Docs/requirements/wertpapierkurse-ing-requirements.md)
- [Architektur: Wertpapierkurse ING-CSV-Import](Docs/architecture/architecture-blueprint-wertpapierkurse-ing.md)
- [Tests: Wertpapierkurse ING-CSV-Import](Docs/tests/wertpapierkurse-ing-testplan.md)
- [API: SecuritiesController (`POST /api/securities/{id}/prices/import`)](Docs/api/SecuritiesController.md)
- [Business: F007 Wertpapierpreise ING-CSV-Import](Docs/business/features/F007-wertpapierpreise-ing-csv-import.md)
- [Flow: Wertpapierkurse ING-CSV-Import inkl. Upsert/API/UI](Docs/flows/security-price-import-ing.md)
- [API: AttachmentsController (inkl. Download-Stabilisierung für SQLite)](Docs/api/AttachmentsController.md)

## Entwicklungskonventionen

- Siehe `.github/copilot-instructions.md` für Coding‑Guidelines (Naming, Async, Logging, Tests).
- Branching & Commits: Conventional Commits (`feat:`, `fix:`, `docs:` ...)

## Security

- Keine Secrets im Repo. Verwende Environment Variables oder Secret Manager.
- JWTs werden via HttpOnly Cookie verwaltet; sichere Konfiguration in Produktion.

## Deployment

- Für produktiven Betrieb HTTPS erzwingen, Secret-Management aktivieren und eine persistente Datenbank (SQL Server oder SQLite mit Backup-Strategie) verwenden.
- Reverse-Proxy (z. B. Nginx/IIS) für TLS-Terminierung und Header-Härtung.
- Vor Deployment: `dotnet build` und `dotnet test` erfolgreich ausführen.

## Kontakt / Issues

- Öffne Issues für Bugs/Feature‑Requests. Für größere Änderungen bitte zuerst Design‑Discussion.

## Bekannte offene Punkte

- Hinweis: Die folgenden bekannten Testprobleme sind fachfremd und **nicht** durch das Feature „Budget-Verwendungszweck-Pattern“ verursacht.
- **BUG-1:** `BuildTwrPeriods` verwendet `start` statt `end` als Periodenbeginn → TWR-Ergebnisse können verfälscht sein (Regressionstest vorhanden)
- **Ausstehende Tests (Prio 2/3):** Periodische Renditen, Benchmark-Normalisierung, bUnit UI-Tests noch nicht implementiert
- **Posting-Reversal:** Kein Bestätigungsdialog vor der Stornierung – Buchung wird direkt rückgängig gemacht; Bug: `GetRelatedPostingsAsync` mit `GroupId == Guid.Empty` (Test mit `Skip` versehen)

## Changelog

- 2026-07: Statement Contact Auto Assignment dokumentiert (Create + Parent-Assignment, 409 Conflict `Err_Conflict_ParentAssignment`, Rollback- und Idempotenzverhalten).
- 2026-07: Wertpapierkurs-Import UI-Platzierung auf die Kursseite (`/list/securities/prices/{id}`) präzisiert; Attachment-Download stabilisiert (isolierter Read-Path via `IDbContextFactory`, Retry bei SQLite-Collation-Konflikt).
- 2026-06: Transaktionssichere Kontoauszug-Buchung mit Guard, Retry-Semantik und 409 ProblemDetails dokumentiert.
- 2026-06: Budget-Verwendungszweck-Pattern inkl. Regex ergänzt (Migration `20260604172812_202606041500_AddBudgetRulePurposePattern`, API/Matching/Tests aktualisiert).
- 2026-05: Budget-Impact-Auswertung für Statement-Buchung dokumentiert (Entry-Hinweise + Booking-Summary).
- 2025-07: Posting-Stornierung (Reversal) implementiert – Gegenbuchung mit negativem Betrag, Gruppen-Stornierung (All-or-Nothing), Statusanzeige in Posting-Listen, REST-API `POST /api/postings/{id}/reverse`.

## Lizenz

- MIT — siehe `LICENSE`.
