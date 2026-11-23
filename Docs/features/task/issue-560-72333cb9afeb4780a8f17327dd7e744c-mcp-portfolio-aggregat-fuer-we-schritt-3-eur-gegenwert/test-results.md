# Testergebnisse — Schritt 3 (#561): EUR-Gegenwert

## Ausgeführte Tests (alle bestanden)

- `dotnet build FinanceManager.sln` — 0 Fehler.
- `dotnet test --filter "FullyQualifiedName~FmSecuritiesAnalysis|FullyQualifiedName~FxRate"` — **38/38**.
- Erweiterter Lauf inkl. `FrankfurterFx`/`CurrencyExchangeRate` — **41/41**.
- Alle `~Backup`-Tests — **68/68** (inkl. neuer Roundtrip `CurrencyExchangeRateBackupRestoreTests`).
- `sh .githooks/pre-commit` — alle Checks grün.

## Neue Tests

- `FmSecuritiesAnalysisEurValueTests` (9): EUR 1:1 + `fxRate:null`, Divisions-Mathematik
  (`valueEur = qty × close / ratePerEur`), fehlender Kurs/Kursrate → `valueEur:null`,
  Flag aus → Felder `null` ohne FX-Aufruf, Dedupe je (Währung, Kursdatum),
  `marketValueEur`/`totalMarketValueEur` nur mit beiden Flags, `null` bei
  unvollständiger Umrechnung.
- `FxRateServiceTests` (6): EUR-Shortcut ohne Provider, persistierter Kurs ohne Abruf,
  Lazy-Fetch + Persistenz, Provider-Fehler → gespeicherter Fallback → `null`.
- `FrankfurterFxRateProviderTests` (5): Frankfurter-JSON-Shape (`/latest` + datiert),
  ein Retry, `Enabled=false`-Killswitch, Fehler → `null`.
- `CurrencyExchangeRateBackupRestoreTests` (3): Export-Sektion, deduplizierter Import
  (Unique-Key, Normalisierung, `CreatedUtc`), Alt-Backup ohne Sektion importierbar.

## Entscheidungen

- `Source = "Frankfurter"` (API-Ursprung; Daten = EZB-Referenzkurse), `"Fixed"` für
  synthetische EUR-Rate.
- `fxRate.date` = Datum des verwendeten Kurses (≤ Kursdatum; Fallback kann älter/
  frühest-stored sein — dokumentiert).
- `valueEur`/`fxRate` immer emittiert (`null` bei Flag aus), Statistik-EUR-Felder nur
  bei `includeStatistics && includeEurValue`.

## Abnahme

Kein UI-Anteil → kein Screenshot-/Usability-Nachweis. Akzeptanz über Unit-/Provider-/
Backup-Roundtrip-Tests; Migration via `dotnet ef migrations add` erzeugt.
