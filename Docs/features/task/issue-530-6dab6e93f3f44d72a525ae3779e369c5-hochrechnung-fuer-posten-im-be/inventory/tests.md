# Tests – Hochrechnung (CompareProjection)

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-15, Läufe zwischen ca. 19:50 und 20:50 Uhr +0100 (CET)
- Branch und Commit-ID: `task/issue-530-6dab6e93f3f44d72a525ae3779e369c5-hochrechnung-fuer-posten-im-be`, Commit `a0f87d3e019bf99d854cb42889a49c67ff31fe3c` (2026-11-15 15:08:50 +0100, „Bump KeyDerivation … (konsolidiert) (#520)")
- Uncommittete Änderungen im getesteten Stand: nur untracked Feature-Artefakte unter `Docs/features/task/issue-530-…/` (requirement.md, todo.md, diese Inventory-Dateien inkl. der abgelegten trx-Reports). Keine Änderungen an Produktiv- oder Testcode.
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.403 (`dotnet --version`), Target `net10.0`, xunit.v3 4.0.3 unter Microsoft.Testing.Platform; E2E via Microsoft.Playwright 1.64.1, Browser-Channel `msedge` (Standard), headless.
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` (Job `build-and-test`, Gates 4–6) und `CI-CD.md` (§ Coverage). CI führt drei Suiten aus: `FinanceManager.Tests` (Unit, blockierend, mit `--filter-not-trait "Category=OsInterface"` und Coverage), `FinanceManager.Tests.Integration` (blockierend), `FinanceManager.Tests.E2E` (best-effort/nicht blockierend). Build vorab: `dotnet build FinanceManager.sln -c Debug` → 0 Fehler, 64 Warnungen (xunit-Analyser-Regeln, bereits vorhanden; entsprechen der `WarningsNotAsErrors`-Liste der CI).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build | `dotnet build FinanceManager.sln -c Debug` | Repo-Root | 0 | – (0 Fehler, 64 Warnungen) | – | – | Konsolenausgabe (nicht persistiert) |
| Unit | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory …/inventory/test-results/unit -- --filter-not-trait "Category=OsInterface" --report-xunit-trx --report-xunit-trx-filename baseline-unit.trx` | Repo-Root | 0 | 2283 | 0 | 0 | [baseline-unit.trx](test-results/unit/baseline-unit.trx) |
| Integration | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory …/inventory/test-results/integration -- --report-xunit-trx --report-xunit-trx-filename baseline-integration.trx` | Repo-Root | 0 | 172 | 0 | 0 | [baseline-integration.trx](test-results/integration/baseline-integration.trx) |
| E2E (gefiltert) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory …/inventory/test-results/e2e -- --filter-class "*Report*" --report-xunit-trx --report-xunit-trx-filename baseline-e2e-reports.trx` | Repo-Root | 0 | 22 | 0 | 0 | [baseline-e2e-reports.trx](test-results/e2e/baseline-e2e-reports.trx) |
| E2E (vollständig) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory …/inventory/test-results/e2e -- --report-xunit-trx --report-xunit-trx-filename baseline-e2e-all.trx` | Repo-Root | 0 | 155 | 0 | 0 | [baseline-e2e-all.trx](test-results/e2e/baseline-e2e-all.trx) |

### Nachgewiesene bestehende Testfehler

Keine. Alle drei Suiten laufen im Ausgangsstand vollständig grün (2283 + 172 + 155 Tests, 0 Fehlschläge, 0 übersprungene).

### Testlücken und Ausführungsprobleme

- Der Unit-Lauf nutzt denselben Trait-Filter wie die CI (`--filter-not-trait "Category=OsInterface"`); damit ausgefilterte Tests wurden nicht ausgeführt (Umfang: unbekannt, in CI identisch behandelt).
- Die E2E-Suite ist in der CI bewusst „best-effort"; lokal lief sie vollständig und fehlerfrei.
- Keine Build-, Setup- oder Infrastrukturfehler aufgetreten.

## Testklassen

### `ReportAggregationProjectionTests` — `FinanceManager.Tests/Reports/ReportAggregationProjectionTests.cs` (25 `[Fact]`, 1160 Zeilen)

Deckt den gesamten heutigen Projektionsumfang für Security ab (SQLite-InMemory-`AppDbContext`):

- `QueryAsync_SecurityDividendProjection_AddsUnconfirmedPriorYearNetDividend` — Erwartete Netto-Dividende (nach Fee/Tax) fließt in `ProjectionAmount`/`ProjectionExpectedDividends`; gebuchte Dividende = bestätigt (`ProjectionAmount == Amount`, keine Details).
- `QueryAsync_SecurityDividendProjection_MatchesPriorYearEventsIndividually` — Zwei Vorjahres-Events werden einzeln gegen aktuelle Buchungen gematcht.
- `QueryAsync_SecurityDividendProjection_TreatsEarlierCurrentYearDividendAsConfirmed` — Früher gebuchte aktuelle Dividende bestätigt die Jahres-Erwartung.
- `QueryAsync_SecurityDividendProjection_MonthlyPatternDoesNotExpectMissedPastMonth` — Monatsmuster: vergangene, verpasste Monate bekommen keine Erwartung.
- `QueryAsync_SecurityDividendProjection_MonthlyPatternIgnoresCorrectionPairsForFutureExpectations` — Korrekturpaare verzerren das Muster nicht.
- `QueryAsync_SecurityDividendProjection_MonthlyPatternIgnoresManyPriorYearCorrectionRows` — Realdaten-artige Korrekturzeilen (Gladstone-Szenario).
- `QueryAsync_SecurityDividendProjection_QuarterlyPatternMatchesWithinQuarterAndDropsElapsedQuarter` — Quartalsmatch; abgelaufenes Quartal ohne Erwartung.
- `QueryAsync_SecurityDividendProjection_QuarterlyPatternExpectsCurrentOpenQuarter` — Offenes Quartal erhält Erwartung.
- `QueryAsync_SecurityDividendProjection_IrregularPatternDoesNotExpectMoreWhenCurrentYearHasPayment` — Irregular: bei bereits gebuchter aktueller Zahlung keine weitere Erwartung.
- `QueryAsync_SecurityDividendProjection_IrregularPatternCautiouslyExpectsWhenCurrentYearHasNoPayment` — Irregular: ohne aktuelle Buchung nur vorsichtige Erwartung.
- `QueryAsync_SecurityDividendProjection_UsesValutaDateAndBookingFallback` — `UseValutaDate` steuert Event-Datum inkl. MinValue-Fallback.
- **`QueryAsync_ProjectionIsIgnored_ForNonSecurityKind`** — assertiert `ComparedProjection == false` und `ProjectionAmount == null` für `PostingKind.Bank`: **dieser Test kodiert die heutige Beschränkung und wird durch die Anforderung fachlich geändert werden müssen.**
- **`QueryAsync_ProjectionIsIgnored_ForMultiKindAndInvalidSecuritySubtype`** — Multi-Kind-Auswahl und Subtyp-Filter ohne Dividend ⇒ keine Projektion: **der Multi-Kind-Teil kodiert ebenfalls die heutige Beschränkung** (Subtyp-Teil bleibt vermutlich gültig).
- `QueryAsync_SecurityDividendProjection_AggregatesCategoryAndTypeRows_WhenIncludeCategory` — Kategorie-Rollup der Projektion.
- `QueryAsync_SecurityDividendProjection_DoesNotExpectDividend_WhenHoldingIsFullySold` — Komplettverkauf stoppt Erwartung.
- `QueryAsync_SecurityDividendProjection_YearIntervalLoadsTradesUntilExpectedDividendDate` — Year-Intervall: Trades bis Erwartungsdatum laden.
- `QueryAsync_SecurityDividendProjection_TreatsPositiveSellQuantityAsDisposition_WhenNotReversal` — Positives Sell-Quantity zählt als Abgang.
- `QueryAsync_SecurityDividendProjection_ExpectsDividend_WhenSellWasReversed` — Stornierter Verkauf stellt Bestand/Erwartung wieder her.
- `QueryAsync_SecurityDividendProjection_ExpectsDividend_WhenHoldingRemainsAfterPartialSell` — Teilverkauf lässt Erwartung bestehen.
- `QueryAsync_SecurityDividendProjection_KeepsBookedDividend_WhenFutureExpectationHasNoHolding` — Gebuchtes Ytd bleibt, keine neue Erwartung nach Verkauf.
- `QueryAsync_SecurityDividendProjection_CategoryExcludesSoldSecurityExpectations` — Kategorie-Details nur für gehaltene Wertpapiere.
- `QueryAsync_SecurityDividendProjection_IgnoresOtherUsersHoldings` — Besitzerisolation der Bestandsprüfung.
- `QueryAsync_SecurityDividendProjection_UsesValutaDateForHoldingCheck` — Valuta-Datum steuert `HasPositiveHolding`.
- `QueryAsync_SecurityDividendProjection_DoesNotUseTradeWithoutQuantityAsHolding` — Trade ohne Quantity ≠ Bestand.
- `QueryAsync_SecurityDividendProjection_HandlesYtdCutoffAndQuarterInterval` — Ytd- und Quarter-Fenster des Vorjahresabgleichs.

### `ReportDashboardViewModelTests` — `FinanceManager.Tests/ViewModels/ReportDashboardViewModelTests.cs` (Moq-`IApiClient`)

- `LoadAsync_ReturnsPoints` — `CompareProjection` wird nur bei Bedarf weitergegeben.
- `SaveUpdateDelete_Favorites_Roundtrip` — `CompareProjection` in Create-/Update-Requests.
- `ProjectionColumn_IsVisibleAndTotalsProjection_WhenServerComparedProjection` (Z. 181) — `CanCompareProjection`/`ShowProjectionColumn`/Totals bei Security-Auswahl.
- **`ReloadAsync_DisablesProjection_ForNonSecuritySelection`** (Z. 215) — Bank-Auswahl ⇒ `CanCompareProjection == false`, VM setzt Flag zurück: **kodiert die heutige UI-Beschränkung.**
- **`ReloadAsync_DisablesProjection_ForMultiKindSelection`** (Z. 243) — dito für Multi-Auswahl.
- `ReloadAsync_DisablesProjection_ForAllHistory` (Z. 270) — AllHistory bleibt ausgeschlossen (soll auch künftig gelten).

### `HomeKpiGridTests` — `FinanceManager.Tests/Components/HomeKpiGridTests.cs`

- `ReportFavoriteKpi_ForwardsCompareProjectionToAggregateRequest` (Z. 43) — KPI-Kachel reicht `fav.CompareProjection` unverändert weiter.

### `ReportPostingsQueryServiceTests` — `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests.cs`

Netto-Dividenden-Drill-Down (`compareProjection`-Parameter im Query-Helper Z. 51–53; netted-path-Tests ab Z. 691, Textfilter-Anker Z. 721, Subtyp-Key-Ablehnung Z. 923). Keine eigenen Projektionsfälle — nur Fenster-/Selektionsparität.

### Weitere Report-Tests (Kontext, ohne Projektionsbezug)

- `ReportAggregationServiceTests` / `ReportAggregationServiceAdditionalTests` / `ReportAggregationServiceMultiKindTests` / `ReportAggregationServiceSavingsPlanSourceTests` / `ReportAggregationServiceTextFilterTests` / `ReportAggregationServiceSecurityMixedSubTypesTests` / `ReportAggregation_ValutaBookingTests` / `ContactPostingsValutaDateTests` — Standard-, Multi-Kind-, Linked-Source-, Textfilter- und Datumsbasis-Verhalten (zusammen mit den übrigen Report-Tests 140 `[Fact]`/`[Theory]` im Ordner `FinanceManager.Tests/Reports/`).
- `SecurityDividendNetYtdSimpleTests` / `SecurityDividendsYtdScenarioTests` — Ytd-Netto-Dividendenpfad.
- `ReportFavoriteServiceTests` / `ReportFavoriteBackupTests` / `ReportFavoriteServiceFilterTests` — Favoritenpersistenz inkl. `CompareProjection` und Backup.
- E2E: `FinanceManager.Tests.E2E/Tests/Reports/ReportingFlowPlaywrightTests.cs`, `ReportTextFilterPlaywrightTests.cs`, `ReportPostingsOverlayPlaywrightTests.cs` — Dashboard-Flows; kein projektionsspezifischer E2E-Test vorhanden.

## Hilfsmethoden

### `ReportAggregationProjectionTests` (privat in der Testklasse)
- `CreateDb()` — SQLite-InMemory-`AppDbContext` (`EnsureCreated`).
- `AddUserAsync(db, name)` — `Domain.Users.User` anlegen.
- `AddSecurity(db, ownerUserId, name, categoryId)` — `Domain.Securities.Security` mit Zufalls-ISIN.
- `AddDividendGroup(db, security, bookingDate, dividend, fee, tax, valutaDate)` — Dividend(+Fee/Tax)-Postinggruppe mit gemeinsamer `GroupId` (`SetGroup`), Subtypes gesetzt.
- `AddTrade(db, security, bookingDate, subType, quantity, valutaDate)` — Buy/Sell-Posting mit `Quantity`; `SetReversalFor` für Storno-Szenarien.

### `ReportAggregationServiceTests` (privat in der Testklasse)
- `CreateDb()` — identisches InMemory-Pattern.
- `SeedAllKindsAsync(db, ownerUserId, analysisMonth, monthsBack)` (Z. 601) — legt `Account`, `Contact`, `SavingsPlan`, `Security` samt Postings/Aggregaten über alle vier Arten an (Vorlage für artübergreifende Seed-Daten).
- `AddMonthlyAggregates` / `AddAgg` (lokale Funktionen) — Aggregat-/Posting-Helfer.

### `ReportPostingsQueryServiceTests`
- Query-Helfer (Z. 51–53) mit `compareProjection`-Parameter zum Bau von `ReportPostingsQuery`.
