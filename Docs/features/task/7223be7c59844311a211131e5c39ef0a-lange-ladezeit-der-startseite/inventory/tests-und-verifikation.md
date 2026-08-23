# Tests und Verifikationspunkte

## Vorhandene Tests

- [`MonthlyBudgetKpiViewModelTests.cs`](../../../../../FinanceManager.Tests/ViewModels/MonthlyBudgetKpiViewModelTests.cs) prueft den Fehlerstatus bei HTTP-Fehlern und das Weiterreichen unerwarteter Exceptions.
- [`HomeKpiGridTests.cs`](../../../../../FinanceManager.Tests/Components/HomeKpiGridTests.cs) deckt die Darstellung und Interaktion des KPI-Grids ab.
- [`HomeViewModelTests.cs`](../../../../../FinanceManager.Tests/ViewModels/HomeViewModelTests.cs) prueft den Home-ViewModel-Importzustand; ein Monats-KPI-Lifecycle-Test ist dort nicht vorhanden.
- [`BudgetReportServiceAdapterTests.cs`](../../../../../FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceAdapterTests.cs) prueft die Berechnung des Backend-Service, nicht das Laden der Startseite.

## Erforderliche neue oder anzupassende Verifikation

1. Ein Komponententest muss zeigen, dass die Monats-KPI waehrend eines noch nicht abgeschlossenen Requests als Skeleton beziehungsweise Ladezustand gerendert wird.
2. Ein Test muss zeigen, dass der Abschluss des Requests die echten KPI-Werte rendert und den Skeleton-Zustand entfernt.
3. Ein Test muss sicherstellen, dass ein langsamer Monats-KPI-Request den initialen Renderpfad der uebrigen Startseite nicht blockiert.
4. Ein Fehlerfall muss den bestehenden Fehlerzustand ohne unbeobachtete Task-Exception abdecken.
5. Ein Request darf bei wiederholten Renderzyklen nicht mehrfach gestartet werden.

## Technische Verifikation

- `dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj` fuer die schnellen Unit-/Komponententests.
- Gegebenenfalls bestehende E2E-Tests der Startseite aus `FinanceManager.Tests.E2E` ausfuehren, wenn die konkrete Skeleton-Darstellung browserseitig verifiziert werden muss.
- Keine Aenderung an den Service-/Mapper-Tests erwarten, sofern die API- und Berechnungsvertraege unveraendert bleiben.
