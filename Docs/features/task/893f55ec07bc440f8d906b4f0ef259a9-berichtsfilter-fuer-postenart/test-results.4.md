# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweis: Der E2E-Hauptlauf meldete 1 feature-fremden Timeout-Flake (`AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable`); der Test bestand im isolierten Nachlauf (1/1, Nachweis `test-results-e2e-r4-rerun.trx`). Ursache war ein Playwright-Timing-Problem (`Timeout 10000ms exceeded` — „element was detached from the DOM, retrying" auf dem „Erneut laden"-Button der Kontenübersicht-Statistik), fachlich unabhängig vom Berichtsfilter. Kein persistierender Fehler.

## Fehlgeschlagene Tests

Keine persistierenden Fehlgeschlagenen. Transient im E2E-Hauptlauf (im Nachlauf grün, feature-unabhängig):
- `AccountsOverviewStatisticsPlaywrightTests.AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable` — Timeout 10000ms beim Klick auf den Statistik-Reload-Button (Element wurde während des Klicks aus dem DOM gelöst; Timing-Flake in der Kontenübersicht, nicht im Berichtswesen).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|---|---|---|
| P1-1: Postenart im Filterdialog wählen → Hochrechnung aktivieren → nur Erwartungen der gewählten Postenart im Expected-Items-Overlay | `ReportBankPostingTypeFilterPlaywrightTests.BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` | Bestanden |
| P1-2: Gefilterten Bericht als Favorit speichern → vollständiger Reload → Postenart-Checkboxen wieder gesetzt | `ReportBankPostingTypeFilterPlaywrightTests.SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` | Bestanden |
| P2: KPI-Kachel (`HomeKpiKind.ReportFavorite`) mit `BankPostingTypes`-Favorit zeigt gefilterten Wert (`.kpi-amount`-Assertion `^100[.,]00$`) | `ReportBankPostingTypeFilterPlaywrightTests.KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` | Bestanden |

Alle 3 geplanten Szenarien sind im Hauptlauf (`test-results-e2e-r4.trx`) enthalten und bestanden — verifiziert per TRX-Auswertung (outcome `Passed` für alle drei Tests).

Zusatzprüfung Null-Semantik der Favoriten (geändert in dieser Iteration: `BuildFiltersPayload()` liefert `null` statt leerem Objekt): Alle Favoriten-/Save-E2E-Tests im Hauptlauf grün — `ReportingFlowPlaywrightTests.SaveFavorite_ShouldPersistAndReload`, `SaveFavorite_ShouldPersistAndReload_OnMobileViewport` (Favorit ohne `Filters`-Property angelegt → Laden + Reload fehlerfrei), `ReportTextFilterPlaywrightTests.SaveFavorite_ShouldPersistTextFilter_AcrossReload`, `KpiTile_WithTextFilterFavorite_ShouldReloadAfterFilterChange` sowie die drei `ReportProjectionPlaywrightTests.FavoriteWithCompareProjection_*`-Favoriten-Tests.

## Reale End-to-End-Abnahme

Durchführungsweg Iteration 4: Deckung über den bestandenen E2E-Hauptlauf — die `PlaywrightWebAppFixture` fährt pro Lauf die echte `FinanceManager.Web`-App als In-Process-Kestrel-Server mit realer SQLite-Datei-DB hoch und steuert einen echten Playwright-Browser (`msedge`-Channel); API-Seeds laufen als echte HTTP-Calls (`fetch` im Browser-Kontext mit echtem Auth-Cookie). Der betroffene Anteil (Favoriten-Speicherpfad mit neuer `Filters=null`-Semantik aus `BuildFiltersPayload()`):

| Szenario | Durchgeführt über | Ergebnis |
|---|---|---|
| Favorit **mit** Postenartfilter speichern → laden → Filter wieder gesetzt | `SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload`: echter UI-Speicherpfad (`#SaveAs` → Favoritendialog → `SubmitFavoriteDialogAsync` → `_vm.BuildFiltersPayload()` → `POST /api/report-favorites`, Redirect auf `?favoriteId=`), danach `page.ReloadAsync()` und erneutes Öffnen des Filterdialogs → `Inflow` gesetzt, `Outflow` nicht gesetzt | Bestanden — derselbe Code-Pfad wie der Null-Fall, nur mit nicht-leerem Payload |
| Favorit **ohne** gesetzte Filter speichern → neu laden → kein Fehler, keine Filter gesetzt | `SaveFavorite_ShouldPersistAndReload` (Desktop + Mobile-Viewport): Favorit per echtem `POST /api/report-favorites` **ohne** `Filters`-Property angelegt (persistiert `Filters=null`), Dashboard mit `?favoriteId=` geladen (Überschrift rendert), `page.ReloadAsync()` → weiterhin fehlerfrei, keine Filter aktiv. Die Erzeugung des `Filters=null`-Payloads im UI-Speicherpfad (`BuildFiltersPayload()` → `null` bei leeren Filterlisten → `Filters = null` im Create-/Update-Request) ist auf VM-Ebene belegt (`ReportDashboardViewModelTests` — `SaveUpdateDelete_Favorites_Roundtrip`, `SaveFavorite`-Aufruf mit `filters: null`, Moq-Request-Capture); die Update-Semantik `Filters = null` leert Filter zusätzlich service-seitig (`UpdateAsync_NullFilters_ClearsTextFilters`) | Bestanden |

Ehrlicher Hinweis zum Restschnitt: Das konkrete Zusammenspiel „Favoritendialog-Speichern **ohne** jegliche Filter → `BuildFiltersPayload()` liefert `null` → `Filters=null` landet im Server-Payload" wird nicht von einem einzigen E2E-Test durchgehend per Browser getrieben; beide Teilhälften sind jedoch separat real belegt (Dialog-Speichern mit Payload via `SaveAsAsync` im Feature-Test; `Filters=null`-Favorit anlegen + laden/reloaden via `SaveFavorite_ShouldPersistAndReload`), ergänzt durch die VM-Unit-Tests mit Request-Capture. Eine dedizierte manuelle Browser-Abnahme wie in Iteration 3 entfällt, da der geänderte Speicherpfad auf beiden Hälften durch echte App + echten Browser abgesichert ist.

## Zusammenfassung

Build: `dotnet build FinanceManager.sln -c Debug` (Repo-Root) → 0 Fehler, 6 Warnungen (NU1510, preexisting).

| Suite | Befehl (Repo-Root) | Exit-Code | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
|---|---|---|---|---|---|---|---|
| Unit (`FinanceManager.Tests`) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <tr> -- --filter-not-trait "Category=OsInterface" --coverage --coverage-output-format cobertura --coverage-output coverage-unittests-r4.cobertura.xml --report-xunit-trx --report-xunit-trx-filename test-results-unittests-r4.trx` | 0 | 2402 | 2402 | 0 | 0 | ~54 s |
| Integration (`FinanceManager.Tests.Integration`) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-integration-r4.trx` | 0 | 176 | 176 | 0 | 0 | ~9 min 02 s |
| E2E (`FinanceManager.Tests.E2E`) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r4.trx` | 2 | 176 | 175 | 1 (flaky, s. o.) | 0 | ~23 min 35 s |
| E2E-Nachlauf (gefiltert, nur der Flake) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --filter-method "*AccountsOverview_StatisticsFailure_ShowsErrorAndLeavesTableUsable" --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r4-rerun.trx` | 0 | 1 | 1 | 0 | 0 | ~13 s |

`<tr>` = `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/test-results/`

- Gesamt: 2755 | Bestanden: 2754 | Fehlgeschlagen: 1 (transient, im Nachlauf grün) | Übersprungen: 0
- Testzahlen unverändert zu Iteration 3 (Unit 2402, Integration 176, E2E 176): Iteration 4 war ein Refactoring (SQL-seitiger Filter im `ReportProjectionEventLoader`, `PostingLoadFilters`-Record/Signaturkonsolidierung im `ReportPostingsQueryService`, einheitliche `HashSet<BankPostingType>`-Repräsentation im `ReportAggregationService`, `BuildFilters` → `_vm.BuildFiltersPayload()` in `ReportDashboard.razor`, `MapFilters`-Extraktion im `ReportsController`) — keine neuen Tests nötig, keine entfernt.

## Testabdeckung

**Abdeckung:** 83,8 % (Unit-Suite, MTP `--coverage` im Cobertura-Format; Nachweis `coverage-unittests-r4.cobertura.xml`). Integration/E2E ohne Instrumentierung — Razor-UI- und Controller-Pfade laufen real unter E2E bzw. der Integration-Suite, sind aber nicht in der Zeilenabdeckung enthalten.

| Datei (Iteration-4-Diff) | Abdeckung | Anmerkung |
|---|---|---|
| `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs` | 93,9 % | SQL-seitiger `BankPostingTypes`-Filter durch `ReportAggregationBankPostingTypeProjectionTests` (`QueryAsync_BankProjection_RespectsBankPostingTypes`, `…_UnselectedTypesProduceNoExpectation`) belegt |
| `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` | 96,9 % | `PostingLoadFilters`-Record/Signaturkonsolidierung — rein strukturell, Pfade durch `ReportPostingsQueryServiceTests` (u. a. `QueryAsync_BankPostingTypesFilter_KeepsOnlySelectedTypes`) abgedeckt |
| `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` | 91,4 % | `HashSet<BankPostingType>`-Repräsentation — Verhalten durch die bestehenden Postenart-/Projektions-Tests unverändert belegt |
| `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` | 71,2 % | `BuildFiltersPayload()` indirekt über `ReloadAsync`-Request-Capture-Tests (`r.Filters != null` mit gemapptem Inhalt) und die Favoriten-Tests belegt; Null-Zweig durch Aufrufe mit `filters: null` |
| `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | 0,0 % (Unit) | Keine Unit-Abdeckung (Razor); `BuildFilters` → `_vm.BuildFiltersPayload()` im Dialog-Submit-Pfad durch die bestandenen Feature-E2E-Tests (`SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` treibt den echten Save-Dialog) belegt |
| `FinanceManager.Web/Controllers/ReportsController.cs` | 0,0 % (Unit) | `MapFilters`-Extraktion — reine Methodenverschiebung, beide Endpunkte durch echte HTTP-Calls der Integration-Suite (176 grün) sowie Browser-Aufrufe der E2E-Tests belegt |
| Referenz (unverändert in It. 4): `HomeKpiGrid.razor` 46,9 % | | |

## Fehlende Tests

Nur für in Iteration 4 geänderte/neue Quelldateien bewertet:

- `ReportDashboard.razor` — 0 % Zeilenabdeckung in der Unit-Suite (Razor-Seite ohne Unit-/bUnit-Tests); der geänderte Submit-Aufruf (`_vm.BuildFiltersPayload()`) ist nur über E2E belegt — das Projekt sieht für Razor-Seiten grundsätzlich keine Komponententests vor.
- `ReportDashboardViewModel.cs` — 71,2 % (< 80 %); `BuildFiltersPayload()` hat keinen dedizierten Unit-Test für den `null`-Rückgabezweig (die `null`-Semantik ist über `SaveFavoriteAsync`/`UpdateFavoriteAsync`-Aufrufe mit `filters: null` und den `Filters=null`-Favoriten-E2E indirekt belegt). Nicht als Feature-Lücke gewertet — ein direkter `BuildFiltersPayload`-Test wäre eine mögliche, nicht erforderliche Ergänzung.
- `ReportsController.cs` — 0 % Unit-Abdeckung (projektweites Muster: Controller werden über die Integration-Suite getestet); `MapFilters` ist eine reine Extraktion ohne Verhaltensänderung — kein neuer Test erforderlich.
- `ReportProjectionEventLoader.cs`, `ReportPostingsQueryService.cs`, `ReportAggregationService.cs` — > 90 % Abdeckung, geänderte Filterpfade durch bestehende Feature-Tests belegt; keine Lücke.
