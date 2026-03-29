# Finance Manager

Kompakte Übersicht

`FinanceManager` ist eine Blazor Server Webanwendung (.NET 10) zur Verwaltung persönlicher Finanzen: Import und Klassifikation von Kontoauszügen, Buchungen (Bank / Kontakt / Sparplan / Wertpapier), Berichte und KPI‑Dashboard.

Kurz: Import → Klassifikation → Validierung → Buchung → Reporting.

## Für Nutzer
Kurzbeschreibung und was die Anwendung bietet (nicht‑technisch):
- Kontoauszüge (CSV/PDF) importieren und automatisch oder manuell kategorisieren
- Sparpläne verwalten (einmalig oder wiederkehrend) und Zielerreichung verfolgen
- Wertpapiertransaktionen (Kauf/Verkauf/Dividende) erfassen und Gebühren/Steuern berücksichtigen
- Berichte und KPI‑Dashboard; Daten als CSV/XLSX exportieren
- Anhänge pro Buchung verwalten und Backups erstellen

Schnelle Nutzung (Kurz):
1. Registrieren / Anmelden
2. Konto anlegen (Bankkontakt zuordnen)
3. Kontoauszug hochladen (Import) → Entwürfe prüfen
4. Einträge klassifizieren / fehlende Angaben ergänzen
5. Buchung durchführen → Postings werden erstellt

Hinweis: Diese README beschreibt die Entwicklungs‑ und Installationsdetails. Für eine reine Nutzer‑Installation wird eine gehostete Instanz oder eine einfache Install‑Anleitung benötigt (siehe `docs/` oder Admin/Hilfe im Webinterface).

## Für Entwickler
Kurz: welche Informationen Entwickler brauchen, um das Projekt lokal zu betreiben und weiterzuentwickeln.

Voraussetzungen
- .NET 10 SDK
- (optional) SQLite oder SQL Server für Produktion

Schnellstart (lokal)

```bash
git clone <repo-url>
cd FinanceManager
dotnet restore
dotnet build
cd FinanceManager.Web
dotnet run
```

Default URLs are printed when you run the application (see the console output from `dotnet run`). Do not rely on a hardcoded port — configure `ASPNETCORE_URLS`, `launchSettings.json` or use `dotnet run --urls "http://localhost:5002;https://localhost:5003"` to override the defaults.

Datenbankmigrationen

Änderungen am Domain‑Modell müssen gegen den korrekten DbContext migriert werden. Beispiel für `AppDbContext`:

```bash
dotnet ef migrations add 20260329_AddSomething -p FinanceManager.Infrastructure -s FinanceManager.Web --context AppDbContext --output-dir Data/Migrations
dotnet ef database update -p FinanceManager.Infrastructure -s FinanceManager.Web --context AppDbContext
```

Tests & Qualität

```bash
dotnet test
```
- Formatierung: `dotnet format` vor PR
- CI soll Build, Tests und Formatierung prüfen

Dokumentation & API

- Projektdokumentation unter `docs/` (Flows, Business Rules, API stubs)
- Installationsanleitung: `docs/install.md`
- Teil‑OpenAPI: `docs/api/openapi.yaml` (Accounts + models)
- Detaillierte Controller‑Docs in `docs/api/`

Entwicklungskonventionen

- Siehe `.github/copilot-instructions.md` für Coding‑Guidelines (Naming, Async, Logging, Tests).
- Branching & Commits: Conventional Commits (`feat:`, `fix:`, `docs:` ...)

Security

- Keine Secrets im Repo. Verwende Environment Variables oder Secret Manager.
- JWTs werden via HttpOnly Cookie verwaltet; sichere Konfiguration in Produktion.

Kontakt / Issues

- Öffne Issues für Bugs/Feature‑Requests. Für größere Änderungen bitte zuerst Design‑Discussion.

Lizenz

- MIT — siehe `LICENSE`.
