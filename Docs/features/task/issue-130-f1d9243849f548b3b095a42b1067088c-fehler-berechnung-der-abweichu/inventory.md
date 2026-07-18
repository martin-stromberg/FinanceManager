# Bestandsaufnahme - Budgetwertungsart fuer Budgetzwecke

## Ausfuehrung

Die Bestandsaufnahme wurde lokal ausgefuehrt, weil in dieser Umgebung kein separates Unteragent-Werkzeug verfuegbar ist.

## Relevante Bereiche

- Domain: `FinanceManager.Domain/Budget/BudgetPurpose.cs`
  - Budgetzwecke enthalten bisher Name, Beschreibung, SourceType, SourceId und optionale Kategorie.
  - Es gibt noch keine Wertungsart.
  - Backup-DTOs muessen bei neuer persistenter Eigenschaft kompatibel erweitert werden.
- DTO/API: `FinanceManager.Shared/Dtos/Budget/*Purpose*.cs`, `IBudgetPurposeService`, `BudgetPurposeService`, `BudgetPurposesController`
  - Create/Update/Get/List/Overview tragen die Budgetzweckdaten.
  - Neue Eigenschaft muss in DTO, Requests und Service-Methoden mit Default `Exakte Buchungen` ergaenzt werden.
- Persistenz: `FinanceManager.Infrastructure/AppDbContext.cs`, EF-Migrationen
  - `BudgetPurpose` wird direkt im AppDbContext konfiguriert.
  - Migration mit nicht-nullbarer Enum-Spalte und Default ist erforderlich.
- Berichtsdaten: `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`
  - `BuildUncategorizedPurposeDtosAsync` allokiert Zweckposten nach Budgetregeln und Vorzeichen.
  - Aktuell werden uebrig gebliebene passende Posten als unbudgeted hinzugefuegt und aus der Zweckauflistung entfernt.
  - Fuer `Gesamtbudget` muss die Allokation alle passenden Posten saldieren.
  - Fuer `Exakte Buchungen` muss das bisherige Vorzeichenverhalten bleiben; nicht gewertete passende Posten sollen beim Zweck separat sichtbar bleiben und zusaetzlich in der nicht-budgetierten Liste erscheinen.
- Aggregation/UI/Export:
  - `BudgetReportsController` und `BudgetReportExportService` berechnen Istwerte aus Raw-Postings.
  - Die Raw-Daten muessen daher klar unterscheiden, ob ein Posting im Zweck-Istwert gewertet wird.
  - `BudgetPurposeCardViewModel` ist die passende Stelle fuer die sichtbare/bearbeitbare Wertungsart.
- Tests:
  - Bestehende fokussierte Budgetbericht-Integrationstests liegen in `FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs`.
  - API/CRUD-Tests fuer Budgetzwecke liegen in `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetsTests.cs`.

## Risiken

- Erweiterung von Record-Konstruktoren kann viele Aufrufstellen betreffen. Default-Parameter am Ende reduzieren den Umbau.
- Nicht gewertete passende Posten duerfen nicht doppelt in Summen eingehen, obwohl sie doppelt sichtbar sein sollen.
- Cache-/RawData-Serialisierung muss neue Felder mit Defaultwerten tolerieren.
- Direkte Kategorie-Regeln bleiben Gesamtbudget-artig und duerfen nicht vom Zweckverhalten eingeschraenkt werden.
