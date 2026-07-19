# Bestandsaufnahme

## Relevante Dateien

- `FinanceManager.Web/Components/Pages/BudgetReport.razor`: rendert den Budgetbericht und das Overlay fuer die Postenauflistung.
- `FinanceManager.Web/ViewModels/Budget/BudgetReportViewModel.cs`: laedt Berichtsdaten und Posten fuer das Overlay.
- `FinanceManager.Shared/Dtos/Budget/BudgetReportRawDataDto.cs`: enthaelt `BudgetReportPostingRawDataDto.IsValuedForBudgetPurpose`.
- `FinanceManager.Infrastructure/Budget/BudgetReportService.cs`: setzt `IsValuedForBudgetPurpose` bereits gemaess Budgetwertungsart.
- `FinanceManager.Tests.Integration/ViewModels/BudgetReportViewModelIntegrationTests.cs`: enthaelt Integrationstests fuer Budgetbericht-ViewModel und Overlay.
- `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`: belegt bereits, dass `Exakte Buchungen` passende ungewertete Posten im Rohdatenmodell kennzeichnet und zusaetzlich als unbudgetiert fuehrt.

## Ist-Zustand

Die Budgetbericht-Zahlen verwenden bereits nur Buchungen mit `IsValuedForBudgetPurpose == true`. Der Service liefert fuer `Exakte Buchungen` passende, aber nicht gewertete Buchungen mit `IsValuedForBudgetPurpose == false` in der Zweck-Rohdatenliste und zusaetzlich in `UnbudgetedPostings`.

Das UI-Overlay fuer einen Budgetzweck laedt jedoch normale `PostingServiceDto`-Eintraege ueber Kontakt-/Sparplan-Endpunkte und filtert sie lokal nach Budgetregeln. Dadurch geht die Wertungsinformation verloren. Alle passenden Posten sehen im Overlay gleich aus.

Die Overlay-Tabelle hat ein sticky `thead` mit heller Flaeche, aber keine explizit kontrastreiche Textfarbe.

## Risiken

- Wenn die Wertungsinformation nur im Service bleibt, kann das UI unbewertete passende Buchungen nicht zuverlaessig markieren.
- Eine neue API-Antwort sollte bestehende Endpunkte nicht brechen.
- Tests sollten die Anzeige- bzw. ViewModel-Klassifikation belegen, nicht nur die bestehende Zahlenlogik.

## Abweichung vom Skill

Es steht kein separates Unteragenten-Tool zur Verfuegung. Die Bestandsaufnahme wurde lokal erstellt.
