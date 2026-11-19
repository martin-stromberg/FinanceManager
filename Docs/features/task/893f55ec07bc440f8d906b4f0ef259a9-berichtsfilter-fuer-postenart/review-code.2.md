# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### FinanceManager.Web/Components/Pages/ReportDashboard.razor (Filterdialog) / FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs — **Einheitlicher Stil / Primitive Obsession**

Der Branch typisiert die Bank-Postenart-Checkboxen auf `BankPostingType`-Enum-Member um (`IsBankPostingTypeSelectedTemp`/`ToggleTempBankPostingType` nehmen jetzt das Enum entgegen, `ReportDashboardViewModel.cs` Z. 1255–1266; razor Z. 602–605). Der unmittelbar darunter liegende, strukturell identische Security-SubType-Block verwendet weiterhin magische Int-Literale `0`–`4` und die `int`-typisierten Geschwister-Methoden `IsSecuritySubTypeSelectedTemp(int)`/`ToggleTempSecuritySubType(int, bool)` (razor Z. 614–618, ViewModel Z. 1237–1250). Zwei gleichartige Checkbox-APIs in derselben Klasse haben damit jetzt unterschiedliche Typisierung; die stille Fehlzuordnung bei Enum-Erweiterung, die für die Bank-Arten behoben wurde, besteht für die Subtypes unverändert.
Empfehlung: Die Security-SubType-API analog auf `SecurityPostingSubType` umstellen und die Literale durch Enum-Member ersetzen (das Enum ist im razor bereits über `@using FinanceManager.Shared.Dtos.Securities` erreichbar).

### FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs (`LoadSavingsPlanEventsAsync`, Z. 123–178) — **Undokumentierte Ausnahmesemantik**

Der Loader für verknüpfte Sparplan-Quellposten wendet den `BankPostingTypes`-Filter bewusst nicht auf die Bankposten der verknüpften Konten an (Scope-Ausschluss, per `QueryAsync_SavingsPlanProjection_LinkedSource_IgnoresBankPostingTypes` gepinnt). Weder der XML-Doc der Methode noch die Aufrufstelle in `ReportAggregationService` (Z. 786–791) erwähnen diesen Ausschluss. Das Drilldown-Pendant `ReportPostingsQueryService.LoadLinkedSavingsPlanPostingsAsync` dokumentiert exakt denselben Ausschluss dagegen explizit (Z. 220–223). Ohne den Hinweis liegt eine Nachjustier-Falle vor: Ein späterer Bearbeiter könnte die „fehlende" Filterweitergabe als Defekt „korrigieren".
Empfehlung: Kurzen Kommentar bzw. XML-Doc-Satz in `LoadSavingsPlanEventsAsync` ergänzen, der den bewussten Filterausschluss analog zum Drilldown-Pendant festhält.

### FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs (`KpiTile_WithBankPostingTypeFavorite_ShowsFilteredTotal`, Z. 221–223) — **Schwache Assertion**

Der Test sichert das gefilterte Kachel-Total über `tileText.Should().NotContain("70")` ab — eine negative Substring-Prüfung auf dem gesamten Kacheltext statt einer exakten Betragsassertion. Sie schlägt auch fehl, wenn künftig ein unrelated „70" (Betragsformatierung, Badge, Vergleichswert) im Kacheltext auftaucht, ohne dass das Filterverhalten gebrochen wäre.
Empfehlung: Auf das Betrags-Element der Kachel eingrenzen bzw. den exakt gerenderten Betrag (z. B. `Contain("100")` bereits vorhanden, Negativseite gegen das konkrete Wertelement statt des gesamten `InnerText`) assertieren.

## Geprüfte Dateien

- `FinanceManager.Infrastructure/Reports/ReportAggregationService.cs`
- `FinanceManager.Infrastructure/Reports/ReportProjectionEventLoader.cs`
- `FinanceManager.Infrastructure/Reports/ReportPostingsQueryService.cs`
- `FinanceManager.Web/Controllers/ReportsController.cs`
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/Components/Shared/HomeKpiGrid.razor`
- `FinanceManager.Tests/Reports/ReportAggregationBankPostingTypeProjectionTests.cs`
- `FinanceManager.Tests/Reports/ReportPostingsQueryServiceTests_LinkedSourceBankPostingTypes.cs`
- `FinanceManager.Tests/ViewModels/ReportDashboardViewModelBankPostingTypeTests.cs`
- `FinanceManager.Tests.E2E/Tests/Reports/ReportBankPostingTypeFilterPlaywrightTests.cs`
- `FinanceManager.Tests.E2E/Helpers/AccountsApiSeedHelper.cs`
- `FinanceManager.Tests.E2E/Helpers/ReportDashboardGateway.cs`
