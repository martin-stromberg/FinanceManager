# Übersetzte Anforderung: Einstellungen lassen sich nicht speichern (Issue #431)

## Fachliche Zusammenfassung

Auf der Einstellungsseite (Setup-Card, `SetupCardViewModel` mit Sektionen in `SetupSections.razor`) ist der Ribbon-Button „Speichern" nur aktiv, wenn ungespeicherte Änderungen vorliegen (`SetupCardViewModel.HasPendingChanges` aggregiert das `Dirty`-Flag der Sektions-ViewModels). Diese Änderungserkennung funktioniert für mehrere Eingabefelder nicht: Änderungen werden nicht erkannt bzw. lösen keine UI-Aktualisierung aus, sodass der Speichern-Button deaktiviert bleibt. Der Fehler soll behoben und durch einen Test abgesichert werden, der für alle Einstellungsfelder prüft, ob eine Veränderung den Speichern-Button aktiviert und ob das Zurücksetzen auf den gespeicherten Wert ihn wieder deaktiviert.

## Betroffene Felder (aus der Kundenanforderung)

- **Sektion „Kontoauszüge"** (`SetupStatementTab.razor` / `SetupStatementsViewModel`):
  - Maximale Einträge (`MaxEntriesPerDraft`)
  - Schwelle für monatliche Aufteilung (`MonthlySplitThreshold`)
  - Minimale Einträge (`MinEntriesPerDraft`)
  - Massenimport-Dialog (`MassImportDialogPolicy`)
  - Bekannte Kontakte automatisch anlegen (`KnownContactAutoCreateEnabled`)
- **Sektion „Renditeanalyse"** (`SetupReturnAnalysisTab.razor` / `SetupReturnAnalysisViewModel`):
  - Benchmark-Wertpapier (`SelectedBenchmarkSecurityId`)
  - Sharpe Ratio anzeigen (`ShowSharpeRatio`)
  - Risikofreier Zinssatz (`RiskFreeRate`)

## Betroffene Klassen und Komponenten

- `FinanceManager.Web/Components/Pages/Setup/SetupStatementTab.razor` — Eingabefelder der Sektion „Kontoauszüge"
- `FinanceManager.Web/Components/Pages/Setup/SetupReturnAnalysisTab.razor` — Eingabefelder der Sektion „Renditeanalyse"
- `FinanceManager.Web/ViewModels/Setup/SetupStatementsViewModel.cs` — `Dirty`, `Validate()`, `RecomputeDirty()`
- `FinanceManager.Web/ViewModels/Setup/SetupReturnAnalysisViewModel.cs` — `Dirty`, `OnChanged()`, `RecomputeDirty()`
- `FinanceManager.Web/ViewModels/Setup/SetupCardViewModel.cs` — `HasPendingChanges`, `CreateSectionViewModel`, `SaveAllAsync`, Ribbon-Action „Save"
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` — `CreateSubViewModel` (verdrahtet `StateChanged`-Weiterleitung ans Eltern-ViewModel)
- Tests: `FinanceManager.Tests` (ViewModel-Tests, ggf. Komponententests)

## Beobachtete Ursachenhypothesen (aus erster Code-Sicht, als Annahmen gekennzeichnet)

1. `SetupStatementTab.razor`: Die Felder `MassImportDialogPolicy` (Select) und `KnownContactAutoCreateEnabled` (Checkbox) haben kein `@bind:after`, sodass weder `Validate()` noch `RecomputeDirty()` aufgerufen wird — `Dirty` bleibt `false`.
2. `SetupStatementsViewModel.Validate()` ruft zwar `RecomputeDirty()` auf, aber kein `RaiseStateChanged()` — eine `Dirty`-Änderung durch `MaxEntriesPerDraft`, `MonthlySplitThreshold` oder `MinEntriesPerDraft` wird nicht ans Eltern-ViewModel propagiert; das Ribbon wird nicht neu gerendert.
3. `SetupCardViewModel.CreateSectionViewModel` erzeugt nicht voreingestellte Sektions-ViewModels (u. a. `returnanalysis`) per `ActivatorUtilities.CreateInstance` ohne die `StateChanged`-Weiterleitung, die `CreateSubViewModel` einrichtet — `Dirty`-Änderungen der Renditeanalyse-Sektion erreichen das Setup-Card-ViewModel nie.

## Implementierungsansatz

- Dirty-Erkennung und `StateChanged`-Propagation so vereinheitlichen, dass jede Feldänderung in jeder sichtbaren Setup-Sektion `HasPendingChanges` der Karte aktiviert und der Ribbon-Button „Speichern" aktiviert wird; Rückkehr zum gespeicherten Wert deaktiviert ihn wieder.
- Vorab einen Test erstellen, der iterativ über alle Eingabefelder der Einstellungssektionen prüft: Änderung → Speichern aktiv; Zurücksetzen auf gespeicherten Wert → Speichern inaktiv. Der Test soll möglichst dynamisch sein (z. B. Felder/Sektionen reflektiv oder über die Sektionsdefinitionen enumerieren), damit neu hinzukommende Eingabefelder automatisch berücksichtigt werden.

## Konfiguration

Keine zusätzliche Konfiguration erforderlich.

## Offene Fragen

- Ebene des geforderten dynamischen Tests: ViewModel-/Komponententest (bUnit/xUnit in `FinanceManager.Tests`) oder E2E-Test (`FinanceManager.Tests.E2E`)? Annahme: Komponenten-/ViewModel-Ebene, da der Speichern-Button über `HasPendingChanges` im ViewModel gesteuert wird; E2E nur ergänzend, falls die Infrastruktur es hergibt.
