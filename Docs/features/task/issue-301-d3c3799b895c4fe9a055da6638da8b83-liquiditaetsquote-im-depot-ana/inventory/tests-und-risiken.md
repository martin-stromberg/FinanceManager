# Tests und Risiken

## Bestehende Tests

Relevante Testdateien:

- `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportServiceTests.cs`
- `FinanceManager.Tests/Portfolio/PortfolioAnalysisReportCacheServiceTests.cs`
- `FinanceManager.Tests/Controllers/PortfolioAnalysisReportControllerTests.cs`
- `FinanceManager.Tests/Web/ViewModels/Portfolio/PortfolioAnalysisReportPageViewModelTests.cs`
- `FinanceManager.Tests/Statements/*` fuer Buchung/Statement-Draft-Verhalten
- `FinanceManager.Tests/Infrastructure/Securities/SecurityPriceService*` und `FinanceManager.Tests/Infrastructure/Postings/*` fuer bestehende Invalidierungsmuster

Die Portfolio-Service-Tests nutzen EF Core InMemory und echte FIFO-/Return-Services. Helper erzeugen aktuell Security-Postings ohne Account-Bezug. Fuer Liquiditaetsquote muessen Tests zusaetzlich Accounts und Bank-Postings mit gemeinsamer `GroupId` anlegen.

## Sinnvolle neue/angepasste Tests

- `PortfolioAnalysisReportServiceTests`: berechnet `LiquidityRatio` aus einem Bankkonto, das ueber `GroupId` mit Wertpapierbuchungen verbunden ist.
- `PortfolioAnalysisReportServiceTests`: dedupliziert mehrere Security-Postings desselben Kontos, damit `CurrentBalance` nur einmal summiert wird.
- `PortfolioAnalysisReportServiceTests`: leeres Depot oder Nenner `0` ergibt `0m`.
- `PortfolioAnalysisReportServiceTests`: konto-/benutzerfremde Buchungen beeinflussen die Quote nicht.
- `PortfolioAnalysisReportCacheServiceTests`: alte `CacheSchemaVersion` wird nach DTO-Erweiterung als Miss behandelt.
- Statement-Draft-Tests: Commit einer wertpapierrelevanten Buchung invalidiert zusaetzlich den Portfolio-Analysebericht-Cache.
- Reversal-Tests: Reversal von Gruppen mit Security-Postings invalidiert bereits den Portfolio-Cache; pruefen, ob die Bedingung fuer Liquiditaetskonto-Aenderungen ausreicht.
- UI-/Komponententest, falls im Projekt vorhanden: Cashflow-Kachel zeigt die neue KPI-Zeile und nutzt Prozentformatierung.

## Risiken

- Gemischte Konten: Die Zuordnung ueber Wertpapierbuchungsgruppen nimmt an, dass der gesamte aktuelle Kontosaldo depotbezogen ist.
- Historische Gruppen: Wird ein Konto nur einmal fuer Wertpapierbuchungen genutzt, zaehlt sein gesamter aktueller Saldo dauerhaft als Depot-Cash.
- Negative Kontosalden: Ohne fachliche Begrenzung kann die Quote negativ werden oder den Nenner reduzieren.
- Cache-Invalidierung: Kontostandsaenderungen auf einem einmal depotbezogenen Konto muessen den Portfolio-Cache invalidieren, auch wenn die konkrete neue Buchung keine Security-Buchung ist.
- DTO-Record-Erweiterung: Positional Record-Aenderungen erzeugen viele Compilerfehler in Tests/Fixtures, sind aber gut auffindbar.

## Verifikation nach Umsetzung

Empfohlene Befehle:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~PortfolioAnalysisReport"
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter "FullyQualifiedName~StatementDraft"
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj
```
