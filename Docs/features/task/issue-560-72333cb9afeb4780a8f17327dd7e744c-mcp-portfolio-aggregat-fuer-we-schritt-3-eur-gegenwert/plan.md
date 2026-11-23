# Plan — Schritt 3 (#561)

1. Domain `CurrencyExchangeRate` + EF-Config + Migration `AddCurrencyExchangeRates`.
2. Application: `IFxRateService`, `IFxRateProvider`, DTO `FxRate`; Infrastructure `FxRateService` (Cache-Lookup, Lazy-Fill mit Dedupe, Fallback, MaxAge-Option); Web `FrankfurterFxRateProvider` + Registrierung + appsettings `Fx:*`.
3. Tool: `includeEurValue`-Parameter, `valueEur`/`fxRate` je Position, `marketValueEur`/`totalMarketValueEur` in statistics; Descriptor-Doku.
4. Backup: Export/Import `currencyExchangeRates` optional.
5. Tests + Doku (api.md, business-rules.md, beschreibung.md, Docs/planung/mcp-integration/datenmodell-erweiterungen.md falls vorhanden) + Reviews + Commit/Merge.
