# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Hinweise zur Laufumgebung: Der erste Integrations- und E2E-Start scheiterte an einem Umgebungsproblem — Laufwerk `C:` war zu 100 % belegt (`System.IO.IOException: Es steht nicht genug Speicherplatz auf dem Datenträger` im `TestWebApplicationFactory`-Webroot-Kopiervorgang, kaskadierende Folgefehler im E2E). Beide Läufe wurden nach Umleitung von `TMP`/`TEMP` auf `D:\temp-fm` vollständig wiederholt; die unten gezählten Ergebnisse stammen aus den vollständigen Wiederholungsläufen. Der E2E-Hauptlauf meldete danach 1 feature-fremden Timeout-Flake (`PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack`); der Test bestand im isolierten Nachlauf (1/1, Nachweis `test-results-e2e-r5-rerun.trx`). Kein persistierender Fehler.

## Fehlgeschlagene Tests

Keine persistierenden Fehlgeschlagenen. Transient im E2E-Hauptlauf (im Nachlauf grün, feature-unabhängig):
- `PostingsCardLinkedActionsPlaywrightTests.PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack` — `Timeout 30000ms exceeded` beim `WaitForNavigation` nach Klick auf den Konto-Link der Postenkarte (Navigations-Timing-Flake in der Karten-Navigation, nicht im Berichtswesen).

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|---|---|---|
| P1-1: Postenart im Filterdialog wählen → Hochrechnung aktivieren → nur Erwartungen der gewählten Postenart im Expected-Items-Overlay | `ReportBankPostingTypeFilterPlaywrightTests.BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` | Bestanden |
| P1-2: Gefilterten Bericht als Favorit speichern → vollständiger Reload → Postenart-Checkboxen wieder gesetzt | `ReportBankPostingTypeFilterPlaywrightTests.SaveFavorite_ShouldPersistBankPostingTypeFilter_AcrossReload` | Bestanden |
| P2: KPI-Kachel (`HomeKpiKind.ReportFavorite`) mit `BankPostingTypes`-Favorit zeigt gefilterten Wert (`.kpi-amount`-Assertion `^100[.,]00$`) | `ReportBankPostingTypeFilterPlaywrightTests.KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` | Bestanden |

Alle 3 geplanten Szenarien sind im Hauptlauf (`test-results-e2e-r5.trx`) enthalten und bestanden — verifiziert per TRX-Auswertung (outcome `Passed` für alle drei Tests).

## Reale End-to-End-Abnahme

Durchführungsweg Iteration 5: **Dedizierte reale Abnahme** des geänderten Filterdialog-Verhaltens — nicht nur Deckung über die Suite. Aufbau: echte `FinanceManager.Web`-App als eigenständiger Prozess (`bin\Debug\net10.0\FinanceManager.Web.dll`, `ASPNETCORE_ENVIRONMENT=Development`, reale SQLite-Datei-DB unter `D:\temp-fm\fmapp\financemanager-manual.db`, `http://localhost:5199`) + echter Browser (Playwright, `msedge`-Channel, headless), gesteuert über einen temporären C#-Treiber außerhalb des Repos (`D:\temp-fm\manual-accept`, `Microsoft.Playwright` 1.64.1 — identisch zur E2E-Suite). Benutzer real über `POST /api/auth/register` registriert und per `POST /api/auth/login` im Browser-Kontext angemeldet (echtes Auth-Cookie); Konto real über `POST /api/accounts` mit Antiforgery-Token angelegt (HTTP 201).

| Szenario | Durchgeführt über | Ergebnis |
|---|---|---|
| Kind mit gesetztem Postenartfilter abwählen (Multi-Kind → Rest-Kind) → Postenart-Auswahl geleert, Bericht konsistent | Realer Browser-Durchlauf auf `/reports/dashboard?edit=true`: Kindfolge `[Bank]` → +Kontakt → `[Bank, Kontakt]` → −Bank → `[Kontakt]` → +Bank → `[Kontakt(primär), Bank]`; Filterdialog (`#FiltersOpen` → `.filter-modal`, Tabs „Kontakte \| Konten") → Konten-Tab → `Eingang` gesetzt → Übernehmen; **Verifikation der Wirksamkeit:** Dialog erneut geöffnet → `Eingang` noch gesetzt; danach `Bank` in `.multi-kind-list` abgewählt → Rest `[Kontakt]` (Primärkind unverändert, `IsMulti` false → `ClearFilters()` läuft nicht — genau der `ClearKindFilters(Bank)`-Pfad aus `OnToggleKind`, razor:1385) → Berichtsoberfläche fehlerfrei (`.report-table`/`.empty-info` sichtbar, keine `.error-msg`) → Favorit gespeichert (`#SaveAs` → Redirect `?favoriteId=`) → `GET /api/report-favorites/{id}` liefert `postingKinds:[1]` (nur Kontakt) und **`filters:null`** → die unsichtbare `SelectedBankPostingTypes`-Auswahl wurde verworfen und persistiert **nicht** im Favoriten (vor dem Fix wäre `bankPostingTypes:[1]` unsichtbar mitgespeichert worden) | Bestanden (alle Checks des Treiberlaufs PASS, `RESULT: ALL CHECKS PASSED`) |
| Fehlermeldungs-Lokalisierung (`Error_SaveFavorite`/`Error_UpdateFavorite`) | Statisch verifiziert: `ReportDashboard.razor` rendert `@Localizer[_vm!.FavoriteError!]`; beide Schlüssel in `ReportDashboard.de.resx` (`Der Favorit konnte nicht gespeichert/aktualisiert werden.`) und `.en.resx` (`The favorite could not be saved/updated.`) vorhanden (Z. 452–453). Einen echten Speicherfehler im Browser zu erzeugen (Server-Fehler beim `POST/PUT /api/report-favorites`) ist ohne provozierten Serverfehler nicht realisierbar — nicht durchgeführt; die fehlende Ressource war der eigentliche Defekt (Anzeige des Schlüsselnamens statt Text), der durch die resx-Ergänzung behoben ist | Teilweise (resx-Vorhandensein + Renderpfad verifiziert; kein echter Fehlerfall ausgelöst) |
| SQL-seitige Bankpostenart-Filterung in `LoadTextFilteredRowsAsync` / `ResolveRawFilters`-Extraktion (Refactoring) | Feature-E2E-Hauptlauf als Durchführungsnachweis: `BankPostingTypeFilter_ProjectionShowsOnlySelectedTypeExpectations` und `KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal` treiben den gefilterten Aggregatpfad über echte HTTP-Calls gegen die echte App; zusätzlich Unit-Suite mit den Postenart-Tests (`QueryAsync_BankProjection_RespectsBankPostingTypes`, `…_UnselectedTypesProduceNoExpectation`, `QueryAsync_BankPostingTypesFilter_KeepsOnlySelectedTypes`, Drilldown- und LinkedSource-Tests) | Bestanden |

Ehrlicher Hinweis: Der Treiberlauf ist ein manuell geführter Check (kein repo-persistierter Test); das DOM-/API-Protokoll stammt aus dem Konsolen-Log des Laufs (Screenshot `D:\temp-fm\manual-accept\evidence-after-deselect.png`). Die Selektor-Namen folgen `ReportDashboardGateway` (`#FiltersOpen`, `.filter-modal`, `.subtype-list`, `BankType_Inflow`).

## Zusammenfassung

Build: `dotnet build FinanceManager.sln -c Debug` (Repo-Root) → 0 Fehler, 6 Warnungen (NU1510, preexisting).

| Suite | Befehl (Repo-Root) | Exit-Code | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen | Dauer |
|---|---|---|---|---|---|---|---|
| Unit (`FinanceManager.Tests`) | `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --no-build -c Debug --results-directory <tr> -- --filter-not-trait "Category=OsInterface" --coverage --coverage-output-format cobertura --coverage-output coverage-unittests-r5.cobertura.xml --report-xunit-trx --report-xunit-trx-filename test-results-unittests-r5.trx` | 0 | 2402 | 2402 | 0 | 0 | ~55 s |
| Integration (`FinanceManager.Tests.Integration`) | `dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-integration-r5.trx` (mit `TMP/TEMP=D:\temp-fm`) | 0 | 176 | 176 | 0 | 0 | ~6 min 19 s |
| E2E (`FinanceManager.Tests.E2E`) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r5.trx` (mit `TMP/TEMP=D:\temp-fm`) | 2 | 176 | 175 | 1 (flaky, s. o.) | 0 | ~22 min 14 s |
| E2E-Nachlauf (gefiltert, nur der Flake) | `dotnet test FinanceManager.Tests.E2E/FinanceManager.Tests.E2E.csproj --no-build -c Debug --results-directory <tr> -- --filter-method "*PostingsCard_OpenAccount_NavigatesToAccountCard_WithBack" --report-xunit-trx --report-xunit-trx-filename test-results-e2e-r5-rerun.trx` | 0 | 1 | 1 | 0 | 0 | ~10 s |

`<tr>` = `Docs/features/task/893f55ec07bc440f8d906b4f0ef259a9-berichtsfilter-fuer-postenart/test-results/`

- Gesamt: 2755 | Bestanden: 2754 | Fehlgeschlagen: 1 (transient, im Nachlauf grün) | Übersprungen: 0
- Testzahlen unverändert zu Iteration 4 (Unit 2402, Integration 176, E2E 176): Iteration 5 war überwiegend Refactoring (`ResolveRawFilters`-Extraktion + `TextFilteredRowFilters`-Record + SQL-seitige Filterung in `LoadTextFilteredRowsAsync` im `ReportAggregationService`, `ToSubTypeSet`→`ToSecuritySubTypeSet` im `ReportPostingsQueryService`, Connection-Dispose im Projektions-Test) plus das neue Verhalten `ClearKindFilters`/`OnToggleKind` und die resx-Fehlerschlüssel — keine Testzahl-Änderung nötig.
- Erster Integrations-/E2E-Versuch nicht gezählt (Umgebungsfehler volles Laufwerk `C:`, s. Hinweis oben); die gezählten TRX-Artefakte stammen aus den vollständigen Wiederholungsläufen.

## Testabdeckung

**Abdeckung:** 83,8 % (Unit-Suite, MTP `--coverage` im Cobertura-Format; Nachweis `coverage-unittests-r5.cobertura.xml`). Integration/E2E ohne Instrumentierung — Razor-UI- und Controller-Pfade laufen real unter E2E bzw. der Integration-Suite, sind aber nicht in der Zeilenabdeckung enthalten.

| Datei (Iteration-5-Diff) | Abdeckung | Anmerkung |
|---|---|---|
| `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs` | 91,6 % | `ResolveRawFilters`/`TextFilteredRowFilters`/SQL-seitige `BankPostingTypes`-Filterung in `LoadTextFilteredRowsAsync` — verhaltensgleiches Refactoring; Filterpfade durch `ReportAggregationServiceTextFilterTests`, `ReportAggregationBankPostingTypeProjectionTests` und `ReportPostingsQueryServiceTests` belegt |
| `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs` | 96,8 % | `ToSubTypeSet`→`ToSecuritySubTypeSet` — reine Umbenennung; Aufrufer durch Drilldown-Tests belegt |
| `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs` | 69,4 % | `ClearKindFilters` ohne dedizierten Unit-Test, dafür durch die reale Browser-Abnahme oben belegt (Favorit enthält nach Kind-Abwahl keine `bankPostingTypes`); Fehlerschlüssel-Zuweisung (`Error_SaveFavorite`/`Error_UpdateFavorite`) auf demselben Codeweg wie die vorhandenen Favorite-Tests |
| `FinanceManager.Web/Components/Pages/ReportDashboard.razor` | 0,0 % (Unit) | Keine Unit-Abdeckung (Razor, projektweites Muster); `OnToggleKind`→`ClearKindFilters`-Aufruf durch die reale Abnahme belegt |
| `FinanceManager.Web/Resources/Components/Pages/ReportDashboard.de.resx` / `.en.resx` | n/a | `Error_SaveFavorite`/`Error_UpdateFavorite` in beiden Sprachen vorhanden (Z. 452–453); Rendern über `Localizer[_vm.FavoriteError]` |
| `FinanceManager.Tests/Reports/ReportAggregationBankPostingTypeProjectionTests.cs` | n/a (Testcode) | Connection-Dispose-Ergänzung — Testinfrastruktur |
| Referenz (unverändert in It. 5): `ReportProjectionEventLoader.cs` 94,2 %, `HomeKpiGrid.razor` 47,8 %, `ReportsController.cs` 0 % | | |

## Fehlende Tests

Nur für in Iteration 5 geänderte/neue Quelldateien bewertet:

- `ReportDashboardViewModel.cs` — `ClearKindFilters(PostingKind)` hat keinen dedizierten Unit-Test (Muster `ReportDashboardViewModelTextFilterTests` wäre direkt anwendbar: `SelectedBankPostingTypes`/`Selected*TextFilters` setzen → `ClearKindFilters` → leer + `FiltersDirty`). Stattdessen in dieser Iteration durch die reale Browser-Abnahme belegt — ein Unit-Test wäre eine sinnvolle, nicht erforderliche Ergänzung.
- `ReportDashboard.razor` — 0 % Zeilenabdeckung in der Unit-Suite (Razor-Seite ohne Unit-/bUnit-Tests; Projekt sieht dafür grundsätzlich keine Komponententests vor). `OnToggleKind`-Aufruf real abgenommen.
- resx `Error_SaveFavorite`/`Error_UpdateFavorite` — kein automatisierter Test, der das Vorhandensein der Schlüssel oder die lokalisierte Anzeige im Fehlerfall assertiert; statisch verifiziert (beide Sprachen).
- `ReportAggregationService.cs`, `ReportPostingsQueryService.cs` — > 90 % Abdeckung, rein strukturelle Änderungen auf bereits getesteten Filterpfaden; keine Lücke.
- `ReportAggregationBankPostingTypeProjectionTests.cs` — Testcode selbst geändert (Connection-Dispose); keine Abdeckung erforderlich.
