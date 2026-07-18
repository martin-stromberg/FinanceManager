# Bestandsaufnahme

## Kurzfazit

Die Anforderung betrifft den Budgetbericht und wird voraussichtlich nicht im ViewModel, sondern in der Aggregation der API-Antwort behoben.

Der Hauptfehler liegt in `FinanceManager.Web/Controllers/BudgetReportsController.cs`: Kategoriezeilen berechnen ihr Budget nur aus Regeln, die direkt an der Kategorie haengen. Budgets aus Detailpositionen bzw. Budget-Zwecken innerhalb derselben Kategorie werden dort nicht zum Kategorie-Budget addiert. Der Istwert der Kategorie wird dagegen aus den Zweck-Postings aggregiert. Dadurch entsteht genau die in der Anforderung beschriebene inkonsistente Grundlage.

Zusaetzlich ist die Abweichungsformel an mehreren Stellen dupliziert. Die sichtbaren DTO-Werte aus der API nutzen ueberwiegend `Budget - Ist`; das erwartete Beispiel verlangt fachlich `Ist - Budget`.

## Detaildokumente

- [Datenfluss Budgetbericht](inventory/datenfluss-budgetbericht.md)
- [Berechnungsstellen](inventory/berechnungsstellen.md)
- [Test- und Risikoanalyse](inventory/tests-und-risiken.md)

## Relevante Dateien

| Bereich | Datei | Bedeutung |
|---------|-------|-----------|
| API-Aggregation | `FinanceManager.Web/Controllers/BudgetReportsController.cs` | Baut `BudgetReportDto` fuer die UI. Kritisch fuer Kategorie-Budget und Delta. |
| Rohdaten-Service | `FinanceManager.Infrastructure/Budget/BudgetReportService.cs` | Ermittelt Budget-Rohdaten, Zweck-Zuordnung und Posting-Allokation. |
| Raw DTOs | `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs` | Enthaelt Kategorie- und Zweck-Budgetwerte getrennt nach Rohdatenebene. |
| API DTOs | `FinanceManager.Shared/Dtos/Budget/BudgetReportDtos.cs` | Sichtbare `Budget`, `Actual`, `Delta`, `DeltaPct`-Werte fuer UI. |
| UI | `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs` | Uebernimmt DTO-Werte weitgehend unveraendert. |
| UI-Rendering | `FinanceManager.Web/Components/Pages/BudgetReport.razor` | Rendert Kategorie- und Zweckzeilen, berechnet einzelne Summenzeilen lokal. |
| Export | `FinanceManager.Web/Services/BudgetReportExportService.cs` | Baut Monatsuebersicht und aktuellen Monat separat; nutzt eigene Budget-/Delta-Formeln. |
| Unit Tests | `FinanceManager.Tests/Budget/BudgetReportServiceTests.cs` | Deckt Rohdaten und Budget-Allokation ab. |
| Integration Tests | `FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs` | Deckt UI-ViewModel-Werte fuer Budgetbericht ab, aber bislang nicht den fehlerhaften Kategorie-Budgetwert. |

## Wahrscheinlicher Aenderungsumfang

- Kategorie-Budget in `BudgetReportsController.GetAsync` muss direkte Kategorie-Regeln und Budget-Regeln der zugeordneten Zwecke konsistent zusammenfassen.
- Kategorie-Delta, Zweck-Delta, Perioden-Delta, Summenzeilen und Export-Deltas muessen auf die fachlich geforderte Richtung `Ist - Budget` umgestellt werden, sofern diese Anzeigewege zur Abweichung gehoeren.
- Tests sollten mindestens den Akzeptanzfall `Unterhaltung & Aktivitaeten` modellieren: Kategorie mit drei Detailpositionen, Budgets nur an Zwecken/Kontakten, Kategorie-Budget `-40`, Kategorie-Ist `-30`, Kategorie-Abweichung `10`.

## Offene Punkte fuer die Planung

- Klaeren, ob die negierte Abweichung nur fuer die Detailtabelle des Budgetberichts gilt oder auch fuer Monatsuebersicht, Export und Summenzeilen.
- Klaeren, ob Kategorie-Budget bei gemischten Regeln als Summe aus direkten Kategorie-Regeln plus Zweck-Regeln gelten soll. Die Anforderung spricht von "relevante Summe"; das spricht fuer Addition, muss aber in der Planung explizit festgelegt werden.
