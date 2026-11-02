# Plan-Review

Status: **Vollständig umgesetzt**

| Planelement | Umsetzung |
|-------------|-----------|
| Port-Verschiebung | `IPriceProvider.cs` per `git mv` nach `FinanceManager.Application/Securities/`; `RequestLimitExceededException` in eigene Datei dort ausgelagert; Usings in `AlphaVantage.cs`, `AlphaVantagePriceProvider.cs`, `ProgramExtensions.cs` angepasst |
| Pass-Service | `ISecurityPriceUpdateService` + Request/Result-Typen in `FinanceManager.Application/Securities/`; `SecurityPriceUpdateService` in `FinanceManager.Infrastructure/Securities/`; scoped registriert |
| Worker | Komposition entfernt; ruft `RefreshFromProviderAsync`; Rate-Limit `break`, Generikum loggt |
| Backfill-Executor | Ruft `RefreshFromProviderAsync`; `ProviderError` → Fortschritt mit Fehlertext; Rate-Limit rethrow |
| ING-Import | `StoreAndEvaluateAsync` statt `UpsertDailyPricesAsync` |
| Tests | alle drei betroffenen Dateien angepasst und grün |
| Doku | `business-rules.md`, `ablauf-technisch.md`, `RELEASE_NOTES.md` aktualisiert |

## Offene Aufgaben

Keine.
