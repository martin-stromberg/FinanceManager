# Testergebnisse

- `dotnet build FinanceManager.sln`: 0 Fehler (nur pre-existing Warnungen NU1510/xUnit-Analyzer).
- `dotnet test FinanceManager.Tests` (Filter Securities/SecurityPrice/MassImport/GoldenCross/TrendReversal/Backfill/ServiceCollectionExtensions/Notification): **327 erfolgreich, 0 fehlgeschlagen**.
- Darin enthalten: `SecurityPriceErrorRecoveryTests` (3), `IngSecurityPriceImportServiceTests` (10), `ServiceCollectionExtensionsTests` (1) — alle grün.

## Angepasste Tests

- `SecurityPriceErrorRecoveryTests`: Pass-Service im Test-DI registriert; `UpsertDailyPricesAsync`-Mocks persistieren real in die In-Memory-DB; Assertion des Backfill-Tests auf persistierten Endzustand statt `CreateAsync`-Aufrufzahl umgestellt.
- `IngSecurityPriceImportServiceTests`: Dep-Typ `ISecurityPriceUpdateService`, Verifikation auf `StoreAndEvaluateAsync`.
- `ServiceCollectionExtensionsTests`: Web-Ports (`IPriceProvider`, `INotificationWriter`, `IGoldenCrossLocalizer`, `ITrendReversalLocalizer`) gemockt; Auflösbarkeit von `ISecurityPriceUpdateService` assertiert.

## Fehlgeschlagene Tests

Keine.
