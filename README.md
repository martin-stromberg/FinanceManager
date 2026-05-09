# FinanceManager

Blazor-Server-Webanwendung (.NET 10) zur Verwaltung persönlicher Finanzen: Kontoauszug-Import, Klassifikation, Buchungen, Wertpapierpreise, Reporting und KPI-Dashboard.

## Projektname
- **FinanceManager**

## Features
- Kontoauszüge (CSV/PDF) importieren, klassifizieren und buchen
- Sparpläne und Wertpapiertransaktionen verwalten
- Berichte/KPI-Dashboard sowie Exporte (CSV/XLSX)
- Anhänge und Backups verwalten
- Robustes Security-Price-Handling (Fehlerklassifikation, Retry, Logging ohne ApiKey-Leakage)
- **Backfill-Fehlerbenachrichtigung (neu):** `SecurityPricesBackfillExecutor` erstellt bei `PriceProviderException` für alle **nicht** `RateLimit`/`TransientNetwork`-Fehler eine Benutzerbenachrichtigung (`Kursabruf fehlgeschlagen`, Trigger `security:error:{securityId}`), analog zum Worker-Verhalten

Weiterführende Doku:
- [Docs/security-price-backfill-notification-planning-overview.md](./Docs/security-price-backfill-notification-planning-overview.md)
- [Docs/requirements/security-price-backfill-notification-alignment.md](./Docs/requirements/security-price-backfill-notification-alignment.md)
- [Docs/architecture/security-price-backfill-notification-alignment.md](./Docs/architecture/security-price-backfill-notification-alignment.md)
- [Docs/architecture/security-price-backfill-notification-erm.md](./Docs/architecture/security-price-backfill-notification-erm.md)
- [Docs/improvements/security-price-backfill-notification-review.md](./Docs/improvements/security-price-backfill-notification-review.md)

## Installation
Voraussetzungen:
- .NET 10 SDK
- optional SQLite/SQL Server (produktive Umgebung)

Schnellstart lokal:
```bash
git clone <repo-url>
cd FinanceManager
dotnet restore
dotnet build
cd FinanceManager.Web
dotnet run
```

Details für Linux/IIS:
- [Docs/install.md](./Docs/install.md)

## Usage
1. Registrieren / Anmelden
2. Konto anlegen
3. Kontoauszug importieren
4. Entwürfe prüfen und klassifizieren
5. Buchung durchführen und Reporting nutzen

API und fachliche Nutzung:
- [docs/api/README.md](./docs/api/README.md)
- [docs/api/SecuritiesController.md](./docs/api/SecuritiesController.md)
- [docs/business/overview.md](./docs/business/overview.md)
- [docs/business/features/F007-wertpapierpreise.md](./docs/business/features/F007-wertpapierpreise.md)
- [Docs/business/features/F017-backfill-fehlerbenachrichtigung.md](./Docs/business/features/F017-backfill-fehlerbenachrichtigung.md)

## Konfiguration
- Keine Secrets im Repo; verwende Umgebungsvariablen/Secret-Manager
- Pflicht in Produktion: `Jwt__Key`
- Optional für Kursabruf: AlphaVantage-Schlüssel (benutzer-/adminbasiert)
- URL-Override über `ASPNETCORE_URLS` oder `dotnet run --urls "..."`

Siehe:
- [Docs/install.md](./Docs/install.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)

## Architektur
Projektstruktur (Auszug):
- `FinanceManager.Web` (UI, API, Background-Services)
- `FinanceManager.Application`, `FinanceManager.Domain`, `FinanceManager.Infrastructure`
- `FinanceManager.Tests*` (Unit-/Integrations-Tests)

Architektur-/Flow-Doku:
- [docs/api/INDEX.md](./docs/api/INDEX.md)
- [docs/api/ARCHITECTURE_AND_INTEGRATION.md](./docs/api/ARCHITECTURE_AND_INTEGRATION.md)
- [docs/flows/README.md](./docs/flows/README.md)
- [docs/flows/security-price-worker.md](./docs/flows/security-price-worker.md)
- [docs/Prozessbeschreibungen.md](./docs/Prozessbeschreibungen.md)

## Contribution
- Bitte Richtlinien aus [CONTRIBUTING.md](./CONTRIBUTING.md) beachten
- Vor PR: Build, Tests und Formatierung ausführen (`dotnet format`)
- Commit-Konvention: Conventional Commits (`feat:`, `fix:`, `docs:` …)

## Tests
Gesamte Testsuite:
```bash
dotnet test
```

Relevante Tests zur Backfill-Fehlerbenachrichtigung:
- [FinanceManager.Tests/Web/Services/SecurityPricesBackfillExecutorNotificationTests.cs](./FinanceManager.Tests/Web/Services/SecurityPricesBackfillExecutorNotificationTests.cs)
- [FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs](./FinanceManager.Tests/Web/Services/SecurityPriceWorkerErrorHandlingTests.cs)
- [FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs](./FinanceManager.Tests/Web/Services/PriceProviderErrorClassExtensionsTests.cs)

Offene Testlücken:
- [Docs/tests/backfill-fehlerbenachrichtigung-testluecken.md](./Docs/tests/backfill-fehlerbenachrichtigung-testluecken.md)

## Deployment
- Kein separates Migrationspaket für das Backfill-Feature erforderlich (code-only)
- Datenbankmigrationen bei Modelländerungen via `dotnet ef ...` ausführen
- Produktive Installations-/Betriebshinweise:
  - [Docs/install.md](./Docs/install.md)
  - [docs/postings.md](./docs/postings.md)

## Lizenz
- MIT, siehe [LICENSE](./LICENSE)

## Kontakt
- Issues/Feature-Requests über GitHub Issues
- Für größere Änderungen bitte vorab Design-/Architektur-Diskussion starten

## Roadmap
- Anforderungen/Status:
  - [docs/Anforderungskatalog.md](./docs/Anforderungskatalog.md)
  - [docs/Anforderungsstatus.md](./docs/Anforderungsstatus.md)
- Verbesserungen:
  - [Docs/improvements/security-price-backfill-notification-review.md](./Docs/improvements/security-price-backfill-notification-review.md)

## Changelog
- Aktuelle Feature-/Lifecycle-Dokumentation:
  - [Docs/lifecycle-report-alphavantage-error-handling-and-logging.md](./Docs/lifecycle-report-alphavantage-error-handling-and-logging.md)
  - [docs/documentation-plan.md](./docs/documentation-plan.md)
