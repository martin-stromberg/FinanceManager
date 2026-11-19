# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweis: Der E2E-Hauptlauf meldete 1 feature-fremden Timeout-Flake (`PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack`); der Test bestand im isolierten Nachlauf (1/1, Nachweis `test-results-e2e-r3-rerun.trx`). Derselbe Test flakte bereits in Iteration 1 und 2 mit identischer Timeout-Ursache (Warten auf Navigation, 30 s). Kein persistierender Fehler.

Zusätzlich dokumentiert: `--logger "console;verbosity=normal"` (VSTest-Syntax) wird vom Test-Runner dieses Projekts nicht unterstützt — alle Suiten laufen über Microsoft.Testing.Platform (xUnit v3, siehe `global.json` → `"runner": "Microsoft.Testing.Platform"`); `dotnet test` lehnt `--logger` mit Exit-Code 5 ab („Unbekannte Option"). Die dokumentierten CI-Befehle (inkl. TRX via `--report-xunit-trx`) wurden stattdessen wie in den Voriterationen verwendet.

## Fehlgeschlagene Tests

Keine persistierenden Fehlgeschlagenen. Transient im E2E-Hauptlauf (im Nachlauf grün, feature-unabhängig):
- `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` — Timeout 30000ms beim Warten auf Navigation (Timing-Flake, bereits in Iteration 1 und 2 geflackt).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|---|---|---|
| P1-1: Postenart im Filterdialog wählen → Hochrechnung aktivieren → nur Erwartungen der gewählten Postenart im Expected-Items-Overlay | `ReportBankPostingTypeFilterPlaywrightTests.BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` | Bestanden |
| P1-2: Gefilterten Bericht als Favorit speichern → vollständiger Reload → Postenart-Checkboxen wieder gesetzt | `ReportBankPostingTypeFilterPlaywrightTests.SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` | Bestanden |
| P2: KPI-Kachel (`HomeKpiKind.ReportFavorite`) mit `BankPostingTypes`-Favorit zeigt gefilterten Wert (inkl. der in Iteration 3 präzisierten `.kpi-amount`-Assertion `^100[.,]00$`) | `ReportBankPostingTypeFilterPlaywrightTests.KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` | Bestanden |

Alle 3 geplanten Szenarien sind im Hauptlauf (`test-results-e2e-r3.trx`) enthalten und bestanden — verifiziert per TRX-Auswertung (outcome `Passed` für alle drei Tests).

## Reale End-to-End-Abnahme

Durchführungsweg Iteration 3: Echte `FinanceManager.Web.dll` als eigener Prozess (`dotnet`, Kestrel `http://127.0.0.1:56410`, `ASPNETCORE_ENVIRONMENT=Development`, reale SQLite-Datei-DB `fm-acceptance-r3.db` via `ConnectionStrings__Default`, `Auth__AllowSelfRegistration=true`, `Api__BaseAddress` auf die eigene URL). Benutzer über die echte Registrierungs-API (`POST /api/auth/register`, Auth-Cookie), Wertpapier über `POST /api/securities` (HTTP 201), Konto über `POST /api/accounts` — alles echte API-Aufrufe mit Cookie-Auth + Antiforgery-Header. Browser-Abnahme über einen temporären Playwright-Treiber (eigenes Konsolenprojekt außerhalb des Repos, `Microsoft.Playwright` 1.64.1 wie die E2E-Suite, Channel `msedge`, headless) — echte App + echter Browser + reale DB:

| Szenario | Durchgeführt über | Ergebnis |
|---|---|---|
| **Iteration 3 (betroffener Anteil):** Security-SubType-Checkbox im Filterdialog togglen und anwenden | `/reports/dashboard?edit=true` → Kind-Checkbox „Security/Wertpapiere" → `#FiltersOpen` → Tab „Wertpapiere" → Checkbox „Dividende" aktivieren → Anwenden (Dialog schließt) → Dialog erneut öffnen → Tab „Wertpapiere" → „Dividende" weiterhin gesetzt, „Kauf" unverändert ungesetzt | Bestanden — der enum-typisierte `IsSecuritySubTypeSelectedTemp`/`ToggleTempSecuritySubType`-Pfad rundtrippt korrekt: Temp-→Aktiv-→Temp-Zustand über Anwenden und Wiederöffnen hinweg in der real gerenderten UI |
| Happy Path (Postenart-Filter setzen → Hochrechnung → nur Erwartungen der gewählten Postenart; Favoriten-Roundtrip; KPI-Kachel) | Verweis auf den vorliegenden E2E-Lauf: die 3 `ReportBankPostingTypeFilterPlaywrightTests`-Szenarien laufen selbst gegen echte App + echten Browser + reale DB (In-Process-Server der `PlaywrightWebAppFixture`) und bestanden | Bestanden |

Anmerkung zum Umfang: Iteration 3 änderte zusätzlich zu den Security-SubType-Checkboxen auch die Bank-Postenart-Checkboxen im selben Block (`IsBankPostingTypeSelectedTemp(1..4)` → `BankPostingType.*`-Member). Der Bank-Pfad ist durch die drei bestandenen Feature-E2E-Tests (echte Checkbox-Toggles über `BankPostingTypeCheckbox`) real abgenommen.

## Zusammenfassung

Build: `dotnet build FinanceManager.sln -c Debug` (Repo-Root) → 0 Fehler, 6 Warnungen (NU1510, preexisting).

| Suite | Befehl (Repo-Root) | Exit-Code | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
|---|---|---|---|---|---|---|---|
| Unit (`FinanceManager.Tests`) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <tr> -- --filter-not-trait "Category=OsInterface" --coverage --coverage-output-format cobertura --coverage-output coverage-unittests-r3.cobertura.xml --report-xunit-trx --report-xunit-trx-filename test-results-unittests-r3.trx` | 0 | 2402 | 2402 | 0 | 0 | ~54 s |
| Integration (`FinanceManager.Tests.Integration`) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-integration-r3.trx` | 0 | 176 | 176 | 0 | 0 | ~7 min 10 s |
| E2E (`FinanceManager.Tests.E2E`) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r3.trx` | 2 | 176 | 175 | 1 (flaky, s. o.) | 0 | ~22 min 1 s |
| E2E-Nachlauf (gefiltert, nur der Flake) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --filter-method "*PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack" --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r3-rerun.trx` | 0 | 1 | 1 | 0 | 0 | ~10 s |

`<tr>` = `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/test-results/`

- Gesamt: 2755 | Bestanden: 2754 | Fehlgeschlagen: 1 (transient, im Nachlauf grün) | Übersprungen: 0
- Testzahlen unverändert zu Iteration 2 (Unit 2402, Integration 176, E2E 176): Iteration 3 war ein Refactoring (magische Ints → Enum-Member) plus Kommentar und Assertion-Präzisierung — keine neuen Tests nötig, keine entfernt.

## Testabdeckung

**Abdeckung:** 83,8 % (Unit-Suite, MTP `--coverage` im Cobertura-Format; Nachweis `coverage-unittests-r3.cobertura.xml`). Integration/E2E ohne Instrumentierung — Razor-UI- und Controller-Pfade laufen real unter E2E bzw. der Integration-Suite, sind aber nicht in der Zeilenabdeckung enthalten.

| Datei (Iteration-3-Diff) | Abdeckung | Anmerkung |
|---|---|---|
| `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | 0,0 % (Unit) | Keine Unit-Abdeckung (Razor); die umgestellten Checkbox-Bindings sind durch die bestandenen Feature-E2E-Tests (Bank-Typen) und die reale Abnahme (Security-SubType-Toggle + Wiederöffnen) belegt |
| `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` | 71,2 % | Feature-Pfade (`ToFavoriteFilters` inkl. `BankPostingTypes`, enum-typisierte Temp-Methoden) durch `ReportDashboardViewModelBankPostingTypeTests` abgedeckt; Rest = andere VM-Zweige |
| `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs` | 93,8 % | Iteration-3-Änderung war nur ein Kommentar (Scope-Ausschluss); keine neue Logik |
| `FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs` | — | Testdatei (Assertion-Präzisierung), selbst der Nachweis |
| Referenz Feature-Dateien (unverändert in It. 3): `ReportPostingsQueryService.cs` 96,8 %, `ReportAggregationService.cs` 91,4 %, `HomeKpiGrid.razor` 46,9 %, `ReportsController.cs` 0,0 % (Unit; Integration-Suite deckt echte HTTP-Aufrufe ab) | | |

## Fehlende Tests

Nur für in Iteration 3 geänderte/neue Quelldateien bewertet:

- `ReportDashboard.razor` — 0 % Zeilenabdeckung in der Unit-Suite (Razor-Seite ohne Unit-/bUnit-Tests); die geänderten Security-SubType-Checkboxen sind nur über E2E/reale Abnahme belegt — das Projekt sieht für Razor-Seiten grundsätzlich keine Komponententests vor, die reale Abnahme des konkret geänderten Verhaltens erfolgte in dieser Iteration.
- `ReportDashboardViewModel.cs` — 71,2 % (< 80 %); die enum-typisierten Methoden `IsSecuritySubTypeSelectedTemp`/`ToggleTempSecuritySubType` haben keinen eigenen Unit-Test (analoge Bank-Varianten sind über `ReportDashboardViewModelBankPostingTypeTests` indirekt belegt; beide sind dünne Temp-Set-Wrapper). Nicht als Feature-Lücke gewertet — das UI-Verhalten ist durch die reale Abnahme abgesichert; ein analoger VM-Test wäre eine mögliche, nicht erforderliche Ergänzung.
- `ReportProjectionEventLoader.cs` — Kommentar-Only-Änderung, keine neue Testpflicht.
- `ReportBankPostingTypeFilterPlaywrightTests.cs` — Test-Infrastruktur, keine Produktiv-Abdeckung erforderlich.
