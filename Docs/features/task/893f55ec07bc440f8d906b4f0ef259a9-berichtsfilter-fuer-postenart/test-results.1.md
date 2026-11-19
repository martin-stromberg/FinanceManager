# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweis: Der E2E-Hauptlauf meldete 2 feature-fremde Timeout-Flakes (`PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack`, `Admin_SavesSettings_PersistsAcrossReload`); beide bestanden im isolierten Nachlauf (2/2, Nachweis `test-results-e2e-rerun.trx`). Keine persistierenden Fehler.

## Fehlgeschlagene Tests

Keine persistierenden Fehlgeschlagenen. Transient im E2E-Hauptlauf (beide im Nachlauf grün, feature-unabhängig):
- `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` — Timeout 30000ms beim Warten auf Navigation (Timing-Flake).
- `UpdateSetupPlaywrightTests.Admin_SavesSettings_PersistsAcrossReload` — Timeout 15000ms beim Warten auf `.setup-update-tab` (Timing-Flake).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|---|---|---|
| P1-1: Postenart im Filterdialog wählen → Hochrechnung aktivieren → nur Erwartungen der gewählten Postenart im Expected-Items-Overlay | `ReportBankPostingTypeFilterPlaywrightTests.BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` | Bestanden |
| P1-2: Gefilterten Bericht als Favorit speichern → vollständiger Reload → Postenart-Checkboxen wieder gesetzt | `ReportBankPostingTypeFilterPlaywrightTests.SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` | Bestanden |
| P2: KPI-Kachel (`HomeKpiKind.ReportFavorite`) mit `BankPostingTypes`-Favorit zeigt gefilterten Wert | `ReportBankPostingTypeFilterPlaywrightTests.KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` | Bestanden |

## Reale End-to-End-Abnahme

Durchführungsweg: Die `PlaywrightWebAppFixture` startet die echte `FinanceManager.Web.dll` als eigenen Prozess (`dotnet`, Kestrel auf `http(s)://127.0.0.1:<port>`, `ASPNETCORE_ENVIRONMENT=Development`, reale SQLite-Datei-DB via `ConnectionStrings__Default`); Playwright steuert einen echten Browser (Chromium, Channel `msedge` per `PLAYWRIGHT_BROWSER_CHANNEL`, headless). Anmeldung über die echte Login-UI (`AuthGateway`), Seeds über echte API-Aufrufe (`AccountsApiSeedHelper`, `BrowserApiHelper`), Bedienung über die echte Blazor-UI (Filterdialog, Reload, Projektions-Overlay, Favoriten-Dialog, Home-Seite). App, Browser und Persistenz liefen also real — die Durchführung erfolgte über die E2E-Infrastruktur im Rahmen des E2E-Laufs.

| Szenario | Durchgeführt über | Ergebnis |
|---|---|---|
| Postenart-Filter setzen → Bericht mit Hochrechnung → Hochrechnung zeigt nur Erwartungen der gewählten Postenart | `BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` (echte App + echter Browser + reale DB) | Bestanden |
| Filter setzen → als Favorit speichern → vollständig neu laden → Filter ist wieder gesetzt | `SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` (echte App + echter Browser + reale DB) | Bestanden |
| Favorit mit `BankPostingTypes` per API → KPI-Kachel → Home-Seite zeigt gefilterten Gesamtwert | `KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` (echte App + echter Browser + reale DB) | Bestanden |

## Zusammenfassung

Build: `dotnet build FinanceManager.sln -c Debug` (Repo-Root) → 0 Fehler, 6 Warnungen (NU1510, preexisting).

| Suite | Befehl (Repo-Root) | Exit-Code | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
|---|---|---|---|---|---|---|---|
| Unit (`FinanceManager.Tests`) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <tr> -- --filter-not-trait "Category=OsInterface" --coverage --coverage-output-format cobertura --coverage-output coverage-unittests.cobertura.xml --report-xunit-trx --report-xunit-trx-filename test-results-unittests.trx` | 0 | 2401 | 2401 | 0 | 0 | ~50 s |
| Integration (`FinanceManager.Tests.Integration`) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-integration.trx` | 0 | 176 | 176 | 0 | 0 | ~6 min 02 s |
| E2E (`FinanceManager.Tests.E2E`) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e.trx` | 2 | 176 | 174 | 2 (beide flaky, s. o.) | 0 | ~22 min 30 s |
| E2E-Nachlauf (gefiltert, nur die 2 Flakes) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --filter-method "*PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack" --filter-method "*Admin_SavesSettings_PersistsAcrossReload" --report-xunit-trx --report-xunit-trx-filename test-results-e2e-rerun.trx` | 0 | 2 | 2 | 0 | 0 | ~11 s |

`<tr>` = `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/test-results/`

- Gesamt: 2753 | Bestanden: 2751 | Fehlgeschlagen: 2 (transient, im Nachlauf grün) | Übersprungen: 0
- Unit-Suite +5 ggü. Baseline (2396 → 2401): neue Repro-/Pinning-Tests `ReportAggregationBankPostingTypeProjectionTests` (3) + `ReportDashboardViewModelBankPostingTypeTests` (2).
- E2E-Suite +3 ggü. Baseline (173 → 176): neue `ReportBankPostingTypeFilterPlaywrightTests` (3), alle grün.

## Testabdeckung

**Abdeckung:** 83,8 % (Unit-Suite, MTP `--coverage` im Cobertura-Format; `--collect:"XPlat Code Coverage"` ist unter Microsoft.Testing.Platform nicht unterstützt — stattdessen die MTP-eigene `--coverage`-Option genutzt, `Microsoft.Testing.Extensions.CodeCoverage` ist referenziert). Integration/E2E ohne Instrumentierung — Razor-UI-Pfade laufen real unter E2E, sind aber nicht in der Zeilenabdeckung enthalten.

| Datei | Abdeckung | Anmerkung |
|---|---|---|
| `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | 0,0 % | Keine Unit-Abdeckung (Razor); Feature-Pfade nur über E2E abgedeckt |
| `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` | 47,8 % | Teilabdeckung; `LoadFavoriteKpiDataAsync`-Feature-Pfad nur über E2E abgedeckt |
| `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` | 72,1 % | Feature-Pfade (Save/Update-Mapping) durch neue Tests abgedeckt; Rest = andere VM-Zweige |

Zum Vergleich (≥ 80 %, nicht gelistet): `ReportAggregationService.cs` 91,5 %, `ReportProjectionEventLoader.cs` 94,1 %.

## Fehlende Tests

- `FinanceManager.Web/Components/Pages/ReportDashboard.razor` — 0 % Zeilenabdeckung in der Unit-Suite; UI-Seite ohne Unit-/bUnit-Tests, Feature-Verhalten ausschließlich über E2E belegt.
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor` — 47,8 %; der geänderte `LoadFavoriteKpiDataAsync`-Pfad hat keinen Komponententest, Abdeckung nur über den E2E-KPI-Test.
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` — 72,1 % (< 80 %); die Feature-Zeilen sind getestet, übrige unbedeckte Zweige gehören nicht zum Feature-Diff.
- `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs` (94,1 %) und `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` (91,5 %) — keine Lücke.
