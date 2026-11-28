# Anforderung — Schritt 3 (#561): EUR-Gegenwert für Wertpapierpositionen

Quelle: Issue #561 + Projektplan Schritt 3.

- Neue globale Referenzentität `CurrencyExchangeRate` (benutzerübergreifender Cache — dokumentierte OwnerUserId-Ausnahme), Unique-Index `(CurrencyCode, Date, Source)`, EF-Migration via `dotnet ef`.
- `IFxRateProvider` (Frankfurter/EZB-kompatibel, „Fremdwährung je 1 EUR", konfigurierbar `Fx:*`, Timeout/Retry) + `IFxRateService` mit Persistenz-Cache, In-Flight-Dedupe, Fallback ≤ Datum, EUR-Shortcut, Offline → null.
- `fm_securities_analysis`: Opt-in `includeEurValue` → je Position `valueEur` (= openQuantity × latestPrice.close / rate zum Kursdatum; null bei fehlendem Kurs/Kursrate), `fxRate { rate, date, source }` bei Fremdwährung. Statistik: `marketValueEur` je byCurrency-Zeile + `totalMarketValueEur` (nur mit includeStatistics+includeEurValue).
- Backup: optionale Sektion `currencyExchangeRates` mit TryGetProperty-Guard, nicht in RequiredDataProperties.
- Tests: Mathematik (Division), EUR-Shortcut, Offline-null, Backup-Roundtrip, Provider-Mocks, MCP-Tests.
