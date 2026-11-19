# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweis: Der E2E-Hauptlauf meldete 3 feature-fremde Timeout-Flakes (`PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack`, `Admin_TriggersCheck_ShowsAvailableUpdate`, `Admin_SavesSettings_PersistsAcrossReload`); alle drei bestanden im isolierten Nachlauf (3/3, Nachweis `test-results-e2e-r2-rerun.trx`). Zwei davon flakten bereits in Iteration 1 (`test-results.1.md`), der dritte teilt dieselbe Timeout-Ursache (`.setup-update-tab`). Keine persistierenden Fehler.

## Fehlgeschlagene Tests

Keine persistierenden Fehlgeschlagenen. Transient im E2E-Hauptlauf (alle im Nachlauf grün, feature-unabhängig):
- `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` — Timeout 30000ms beim Warten auf Navigation (Timing-Flake, bereits in Iteration 1 geflackt).
- `UpdateSetupPlaywrightTests.Admin_TriggersCheck_ShowsAvailableUpdate` — Timeout 15000ms beim Warten auf `.setup-update-tab` (Timing-Flake).
- `UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — Timeout 15000ms beim Warten auf `.setup-update-tab` (Timing-Flake, bereits in Iteration 1 geflackt).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|---|---|---|
| P1-1: Postenart im Filterdialog wählen → Hochrechnung aktivieren → nur Erwartungen der gewählten Postenart im Expected-Items-Overlay | `ReportBankPostingTypeFilterPlaywrightTests.BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` | Bestanden |
| P1-2: Gefilterten Bericht als Favorit speichern → vollständiger Reload → Postenart-Checkboxen wieder gesetzt | `ReportBankPostingTypeFilterPlaywrightTests.SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` | Bestanden |
| P2: KPI-Kachel (`HomeKpiKind.ReportFavorite`) mit `BankPostingTypes`-Favorit zeigt gefilterten Wert | `ReportBankPostingTypeFilterPlaywrightTests.KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` | Bestanden |

## Reale End-to-End-Abnahme

Durchführungsweg Iteration 2: Echte `FinanceManager.Web.dll` als eigener Prozess (`dotnet`, Kestrel `http://127.0.0.1:56380`, `ASPNETCORE_ENVIRONMENT=Development`, reale SQLite-Datei-DB `fm-acceptance-r2.db` via `ConnectionStrings__Default`, `Auth__AllowSelfRegistration=true`). Benutzer über die echte Registrierungs-API (`POST /api/auth/register`, JWT-Cookie), Konto über `POST /api/accounts`, Sparplan über `POST /api/savings-plans`, Kontoverknüpfung über `POST /api/savings-plans/{id}/linked-accounts/{accountId}` (HTTP 204) — alles echte API-Aufrufe. Bankposten (-100, `PostingType=Outflow`, Nov-2026) direkt in die SQLite-DB geschrieben (gleiches Seeding-Muster wie `AccountsApiSeedHelper.CreateAccountWithBalanceAsync`, inkl. explizitem `PostingType`). Abnahme über echte `POST /api/report-postings`-Aufrufe mit JWT-Bearer-Token (realer HTTP-Pfad: Controller → `ReportPostingsQueryService` → EF → SQLite):

| Szenario | Durchgeführt über | Ergebnis |
|---|---|---|
| **Iteration 2:** Drilldown/Posteneinsicht einer verknüpften Sparplan-Zelle mit gesetztem Postenartfilter zeigt ungefilterte Quell-Bankposten | `POST /api/report-postings` mit `GroupKey=SavingsPlan:{planId}`, `PostingKind=SavingsPlan`, `SavingsPlanPostingSource=LinkedAccountPostings`, `Filters.BankPostingTypes=[Interest]` (echte App + reale DB) | Bestanden — der Outflow-Quellposten (-100, `entityName="ACC Vertragskonto"`) wird trotz nicht ausgewählter Postenart gelistet (`totalCount=1`, `totalAmount=-100`) |
| **Iteration 2, Kontrast:** Direkte Bank-Zelle desselben Kontos bleibt gefiltert | `POST /api/report-postings` mit `GroupKey=Account:{accountId}`, `PostingKind=Bank`, `Filters.BankPostingTypes=[Interest]` | Bestanden — leere Trefferliste (`totalCount=0`); mit `BankPostingTypes=[Outflow]` erscheint der Posten korrekt (`totalCount=1`) |
| **Iteration 2, Kontrast:** Summenzeile (Multi-Kind) zeigt den verknüpften Quellposten ungefiltert | `POST /api/report-postings` mit `GroupKey=null`, `PostingKinds=[Bank,SavingsPlan]`, `Filters.BankPostingTypes=[Interest]` | Bestanden — `totalCount=1`, `totalAmount=-100` |
| Iteration 2, UI-Pfad des Drilldowns (Overlay öffnen, verknüpfte Quellposten rendern) | `PostingsOverlay_LinkedSavingsPlanSource_TypeAndTotalRows` (Playwright: echte App + echter Browser + reale DB, im E2E-Hauptlauf) | Bestanden |
| Postenart-Filter setzen → Bericht mit Hochrechnung → nur Erwartungen der gewählten Postenart (Happy Path, unverändert zu Iteration 1) | `BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` (echte App + echter Browser + reale DB) | Bestanden |
| Filter setzen → als Favorit speichern → vollständig neu laden → Filter wieder gesetzt (Happy Path, unverändert zu Iteration 1) | `SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` (echte App + echter Browser + reale DB) | Bestanden |
| Favorit mit `BankPostingTypes` per API → KPI-Kachel → Home-Seite zeigt gefilterten Gesamtwert (Happy Path, unverändert zu Iteration 1) | `KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` (echte App + echter Browser + reale DB) | Bestanden |

## Zusammenfassung

Build: `dotnet build FinanceManager.sln -c Debug` (Repo-Root) → 0 Fehler, 6 Warnungen (NU1510, preexisting).

| Suite | Befehl (Repo-Root) | Exit-Code | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
|---|---|---|---|---|---|---|---|
| Unit (`FinanceManager.Tests`) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <tr> -- --filter-not-trait "Category=OsInterface" --coverage --coverage-output-format cobertura --coverage-output coverage-unittests-r2.cobertura.xml --report-xunit-trx --report-xunit-trx-filename test-results-unittests-r2.trx` | 0 | 2402 | 2402 | 0 | 0 | ~52 s |
| Integration (`FinanceManager.Tests.Integration`) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-integration-r2.trx` | 0 | 176 | 176 | 0 | 0 | ~5 min 45 s |
| E2E (`FinanceManager.Tests.E2E`) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r2.trx` | 2 | 176 | 173 | 3 (alle flaky, s. o.) | 0 | ~22 min 41 s |
| E2E-Nachlauf (gefiltert, nur die 3 Flakes) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --filter-method "*PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack" --filter-method "*Admin_TriggersCheck_ShowsAvailableUpdate" --filter-method "*Admin_SavesSettings_PersistsAcrossReload" --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r2-rerun.trx` | 0 | 3 | 3 | 0 | 0 | ~14 s |

`<tr>` = `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/test-results/`

- Gesamt: 2754 | Bestanden: 2751 | Fehlgeschlagen: 3 (transient, im Nachlauf grün) | Übersprungen: 0
- Unit-Suite +1 ggü. Iteration 1 (2401 → 2402): neuer Pinning-Test `ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes.QueryAsync_LinkedSavingsPlanSource_IgnoresBankPostingTypes` (bestanden).

## Testabdeckung

**Abdeckung:** 83,8 % (Unit-Suite, MTP `--coverage` im Cobertura-Format; Nachweis `coverage-unittests-r2.cobertura.xml`). Integration/E2E ohne Instrumentierung — Razor-UI- und Controller-Pfade laufen real unter E2E bzw. der Integration-Suite (`ApiClientReportsTests`, `ApiClientReportPostingsTests`), sind aber nicht in der Zeilenabdeckung enthalten.

| Datei (Iteration-2-Diff) | Abdeckung | Anmerkung |
|---|---|---|
| `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` | 96,6 % | Geänderter Linked-Source-Pfad durch neuen Test `QueryAsync_LinkedSavingsPlanSource_IgnoresBankPostingTypes` plus realer API-Abnahme belegt |
| `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs` | 94,1 % | Keine Lücke |
| `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` | 91,5 % | Keine Lücke |
| `FinanceManager.Web/Controllers/ReportsController.cs` | 0,0 % (Unit) | Controller werden nicht von der Unit-Suite, sondern von der Integration-Suite über echte HTTP-Aufrufe abgedeckt (`Reports_Aggregates_And_Favorites_Flow`, `Reports_Favorites_SavingsPlanPostingSource_Roundtrips`, `ReportPostings_*` — alle grün); Iteration-2-Änderung war eine vereinfachende DTO-Durchreichung ohne neue Logik |
| `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` | 71,5 % | Feature-Pfade (`ToFavoriteFilters` inkl. `BankPostingTypes`, Enum-Signaturen) durch `ReportDashboardViewModelBankPostingTypeTests` abgedeckt; Rest = andere VM-Zweige |
| `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | 0,0 % | Keine Unit-Abdeckung (Razor); Enum-Member-Umstellung rendert durch die bestandenen E2E-Filter-Tests belegt (`BankPostingTypeCheckbox`-Gateway) |
| `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` | 47,8 % | Teilabdeckung; `LoadFavoriteKpiDataAsync`-Feature-Pfad über den E2E-KPI-Test belegt |
| `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`, `ReportDashboardGateway.cs` | — | Test-Infrastruktur, keine Produktiv-Abdeckung erforderlich |

## Fehlende Tests

- `FinanceManager.Web/Controllers/ReportsController.cs` — 0 % in der Unit-Suite; Controller-Ebene bewusst nur über die Integration-Suite (`ApiClientReportsTests`/`ApiClientReportPostingsTests`, echte HTTP-Aufrufe inkl. Favoriten-Create/Update und Postings-Query) abgedeckt — kein Unit-Testzugang vorgesehen, kein Feature-Mangel.
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` — 0 % Zeilenabdeckung in der Unit-Suite; UI-Seite ohne Unit-/bUnit-Tests, Feature-Verhalten ausschließlich über E2E belegt.
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` — 47,8 %; der geänderte `LoadFavoriteKpiDataAsync`-Pfad hat keinen Komponententest, Abdeckung nur über den E2E-KPI-Test.
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` — 71,5 % (< 80 %); die Feature-Zeilen sind getestet, übrige unbedeckte Zweige gehören nicht zum Feature-Diff.
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` (96,6 %), `ReportProjectionEventLoader.cs` (94,1 %), `ReportAggregationService.cs` (91,5 %) — keine Lücke.
