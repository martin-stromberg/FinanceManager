# Bestandsaufnahme

## Schichten

`Application → Domain/Shared`, `Infrastructure → Domain + Application`, `Web → alle`.

## Fundstellen (Aufrufer des Kurs-Pflegevorgangs)

| Datei | Rolle | Bisherige Komposition |
|-------|-------|----------------------|
| `FinanceManager.Web/Services/SecurityPriceWorker.cs` | Stündlicher BackgroundService | Auswahl + Fenster selbst; `IPriceProvider` → `ClearPriceErrorAsync` → `db.SecurityPrices.Add`/`SaveChanges` → `GoldenCross`/`TrendReversal`; Fehler: `entity.SetPriceError` + `INotificationWriter` |
| `FinanceManager.Web/Services/SecurityPricesBackfillExecutor.cs` | Manueller `IBackgroundTaskExecutor` | `GetLatestDateAsync` → `IPriceProvider` → `ClearPriceErrorAsync` → `CreateAsync` pro Kurs; Fehler: `SetPriceErrorAsync`, keine Notification, keine Signale |
| `FinanceManager.Infrastructure/Securities/IngSecurityPriceImportService.cs` | CSV-Import (Controller + MassImportOrchestrator) | Parse → `UpsertDailyPricesAsync`; keine Evaluierung |

## Beteiligte Services/Ports

- `ISecurityPriceService` (Application) / `SecurityPriceService` (Infrastructure): `CreateAsync`, `ListAsync`, `GetLatestDateAsync`, `UpsertDailyPricesAsync` (inkl. `close < 0`-Validierung + `IPortfolioAnalysisReportCacheService`-Invalidierung), `SetPriceErrorAsync`, `ClearPriceErrorAsync`
- `IPriceProvider` + `RequestLimitExceededException`: bisher in `FinanceManager.Web.Services` — Port, wird nach `FinanceManager.Application.Securities` verschoben (Implementierung `AlphaVantagePriceProvider` verbleibt in Web)
- `INotificationWriter` (Application) / `NotificationWriter` (Web-Registrierung)
- `IGoldenCrossService` + `ITrendReversalService` (Application) / Impl. in Infrastructure; transitiv abhängig von `IGoldenCrossLocalizer`/`ITrendReversalLocalizer` (Web-Implementierungen)
- `IAlphaVantageKeyResolver` (Web): Run-Level-Vorbedingung, verbleibt in den Aufrufern

## Positives Muster im Repo

`IMassImportOrchestrator.ProcessAsync` (Application/Infrastructure): ein Einstieg, der intern analysiert/parst/importiert — Controller orchestriert nichts selbst.

## Tests

- `FinanceManager.Tests/Web/SecurityPriceErrorRecoveryTests.cs`: Reflection auf private `RunOnceAsync`, Spy zählt Einzelschritte (→ Befund 3, Issue #474)
- `FinanceManager.Tests/Infrastructure/Securities/IngSecurityPriceImportServiceTests.cs`: konstruiert Import-Service direkt
- `FinanceManager.Tests/Infrastructure/ServiceCollectionExtensionsTests.cs`: DI-Smoke-Test auf `AddInfrastructure` — Web-Ports müssen dort gemockt werden
