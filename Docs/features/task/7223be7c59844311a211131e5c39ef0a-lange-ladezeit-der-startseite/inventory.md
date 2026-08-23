# Inventar: Ladezeit der Startseite

## Umfang

Die Anforderung betrifft das asynchrone Laden der monatlichen KPI auf der authentifizierten Blazor-Startseite (`/`). Die Berechnung, API-Schnittstelle und Datenquelle der KPI sind ausdruecklich nicht Bestandteil der Aenderung.

## Relevante Komponenten

| Bereich | Dateien | Relevanz |
|---|---|---|
| Startseite | [`Home.razor`](../../../../FinanceManager.Web/Components/Pages/Home.razor) | Rendermode, Authentifizierungszweig, Einbettung des KPI-Grids und State-Updates |
| KPI-Container | [`HomeKpiGrid.razor`](../../../../FinanceManager.Web/Components/Shared/HomeKpiGrid.razor) | Laedt die konfigurierten Home-KPIs und erzeugt die jeweiligen Tile-Komponenten |
| Monats-KPI | [`MonthlyBudgetKpi.razor`](../../../../FinanceManager.Web/Components/Shared/MonthlyBudgetKpi.razor) | Darstellung, Ladezustand, Fehleranzeige und API-Ladeaufruf |
| KPI-ViewModel | [`MonthlyBudgetKpiViewModel.cs`](../../../../FinanceManager.Web/ViewModels/Budget/MonthlyBudgetKpiViewModel.cs) | Haltet Ladezustand, KPI-Werte und Fehlerstatus |
| API-Client | [`ApiClient.BudgetReport.cs`](../../../../FinanceManager.Shared/ApiClient.BudgetReport.cs) | HTTP-Aufruf fuer `GET /api/budget/report/kpi-monthly` |
| Backend-Service | [`BudgetReportService.cs`](../../../../FinanceManager.Infrastructure/Budget/BudgetReportService.cs) | Berechnet und mappt die Monats-KPI; fachlich unveraendert lassen |
| Styling | [`app.HomeKpiGrid.css`](../../../../FinanceManager.Web/wwwroot/css/app.HomeKpiGrid.css) | Tile-Layout sowie vorhandene Loading-/Budget-KPI-Stile |

## Aktueller Ablauf

1. `Home.razor` initialisiert sein `HomeViewModel` und rendert bei authentifizierten Benutzern `HomeKpiGrid`.
2. `HomeKpiGrid.OnInitializedAsync` wartet auf `LoadKpisAsync`, bevor der Komponenten-Lifecycle abgeschlossen ist.
3. `LoadKpisAsync` ruft `Api.HomeKpis_ListAsync()` auf und setzt die konfigurierte KPI-Liste.
4. Fuer eine KPI vom Typ `MonthlyBudget` wird ein neues `MonthlyBudgetKpiViewModel` erzeugt und an `MonthlyBudgetKpi` uebergeben.
5. `MonthlyBudgetKpi.OnParametersSetAsync` wartet auf `ViewModel.LoadAsync(Api)`.
6. `LoadAsync` ruft `Budgets_GetMonthlyKpiAsync` auf. Erst danach werden `DataLoaded` und die Werte gesetzt.

Damit kann der KPI-Aufruf den initialen Aufbau des KPI-Grids und damit die sichtbare Startseite verzoegern. Das vorhandene ViewModel besitzt bereits einen expliziten Zustand (`DataLoaded == false`) und eine Fehleranzeige, die als Grundlage fuer die Skeleton-Darstellung dienen kann.

## Vorgesehene Aenderungsgrenzen

- Primaer betroffen: `HomeKpiGrid.razor`, `MonthlyBudgetKpi.razor` und gegebenenfalls `MonthlyBudgetKpiViewModel.cs`.
- CSS-Erweiterung nur, wenn die vorhandene Darstellung fuer den Skeleton-Zustand nicht ausreicht.
- Bestehende API-, DTO-, Controller- und Backend-Service-Vertraege beibehalten.
- Andere Home-KPI duerfen durch die Aenderung nicht blockiert werden.

## Detaildokumente

- [Startseite und Komponenten-Lifecycle](inventory/startseite-und-lifecycle.md)
- [KPI-Datenfluss und Systemgrenzen](inventory/kpi-datenfluss.md)
- [Tests und Verifikationspunkte](inventory/tests-und-verifikation.md)
