# Umsetzungsplan

## Zielbild

Ein Pass-Einstieg `ISecurityPriceUpdateService` (Application) mit Implementierung `SecurityPriceUpdateService` (Infrastructure) kapselt die Sequenz:

- `RefreshFromProviderAsync(request)`: Provider-Abruf → bei `InvalidOperationException` Fehlerflag setzen + Owner benachrichtigen (dedupliziert) → `ClearPriceErrorAsync` → Wochenendfilter → `StoreAndEvaluateAsync`
- `StoreAndEvaluateAsync(owner, securityId, items)`: `UpsertDailyPricesAsync` → `GoldenCross`/`TrendReversal` auswerten (warn-and-continue)

`RequestLimitExceededException` propagiert zum Aufrufer (Worker: Abbruch des Runs; Backfill: Task schlägt fehl).

## Schritte

1. `IPriceProvider` und `RequestLimitExceededException` von `FinanceManager.Web.Services` nach `FinanceManager.Application.Securities` verschieben (Port in die Service-Schicht); Usings in `AlphaVantage.cs`, `AlphaVantagePriceProvider.cs`, `ProgramExtensions.cs` anpassen.
2. `ISecurityPriceUpdateService` + `SecurityPriceRefreshRequest`/`SecurityPriceRefreshResult`/`SecurityPriceRefreshStatus` in Application anlegen; `SecurityPriceUpdateService` in Infrastructure implementieren; scoped in `ServiceCollectionExtensions` registrieren.
3. `SecurityPriceWorker`: behält Scope/Key-Check/Batch-Auswahl/Fenster/Quota-Pacing; pro Security `RefreshFromProviderAsync`; Zähler aus `StoreResult`.
4. `SecurityPricesBackfillExecutor`: behält Payload/Fensterlogik/Fortschritt; pro Security `RefreshFromProviderAsync`; `ProviderError` → Fehler-Fortschritt ohne `processed++`.
5. `IngSecurityPriceImportService`: `UpsertDailyPricesAsync` → `StoreAndEvaluateAsync` (gleiche Signatur/Rückgabe).
6. Tests minimal anpassen: `SecurityPriceErrorRecoveryTests` (Pass registrieren, Upsert-Mocks persistieren, Assertion auf Endzustand), `IngSecurityPriceImportServiceTests` (Dep-Typ + Methodenname), `ServiceCollectionExtensionsTests` (Web-Ports mocken, Auflösbarkeit des Passes assertieren).
7. Hilfe-Doku (`business-rules.md`, `ablauf-technisch.md`) auf den Pass umstellen; Release Notes ergänzen.

## Offene Punkte

Keine.

## Auswirkungen

- Worker-Schreibpfad nutzt jetzt Upsert: `close < 0`-Validierung, Duplikat-sicher, Portfolio-Cache-Invalidierung.
- Backfill und CSV-Import lösen nun dieselbe Signalauswertung aus; Backfill benachrichtigt den Owner bei Provider-Domainfehlern wie der Worker.
- `CreateAsync` am `ISecurityPriceService` wird produktiv nicht mehr genutzt (Interface-Bereinigung folgt in #473).
