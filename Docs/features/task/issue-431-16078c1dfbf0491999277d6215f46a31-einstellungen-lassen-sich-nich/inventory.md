# Bestandsaufnahme: Einstellungen lassen sich nicht speichern (Issue #431)

Analysiert wurde der Setup-Bereich (`SetupCardViewModel`, Sektions-Tabs und -ViewModels) bezogen auf die Anforderung, dass Änderungen an bestimmten Einstellungsfeldern den Ribbon-Button „Speichern" nicht aktivieren.

## Zusammenfassung

- Der Speichern-Button ist eine Ribbon-Aktion der `SetupCardViewModel`, deaktiviert über `Saving || !HasPendingChanges`. `HasPendingChanges` aggregiert `Dirty` der Sektions-VMs (profile, notifications, statements, update, returnanalysis, securitytxt, wellknown).
- Drei Defekte verhindern die Erkennung:
  1. `SetupStatementTab.razor`: `MassImportDialogPolicy` und `KnownContactAutoCreateEnabled` besitzen kein `@bind:after` — `RecomputeDirty()` wird bei Änderung nie aufgerufen.
  2. `SetupStatementsViewModel.Validate()` ruft `RecomputeDirty()` ohne `RaiseStateChanged()` auf — `Dirty` wird für `MaxEntriesPerDraft`, `MonthlySplitThreshold`, `MinEntriesPerDraft` intern gesetzt, propagiert aber nicht zur Karte; das Ribbon rendert nicht neu.
  3. `SetupCardViewModel.CreateSectionViewModel` erzeugt lazy Sektions-VMs (u. a. `returnanalysis`) ohne die `StateChanged`-Weiterleitung, die `BaseViewModel.CreateSubViewModel` einrichtet — Änderungen der Renditeanalyse-Sektion (Benchmark, Sharpe Ratio, Risikofreier Zinssatz) erreichen die Karte nie, obwohl `SetupReturnAnalysisViewModel.OnChanged()` korrekt `RaiseStateChanged()` aufruft.
- Funktionierendes Referenzmuster: Profil-, Benachrichtigungs-, security.txt-, Well-Known- und Update-Sektion rufen nach jeder Feldänderung `OnChanged()` auf, das `RecomputeDirty()` **und** `RaiseStateChanged()` ausführt; ihre VMs sind via `CreateSubViewModel` verdrahtet.
- Test-Ausgangszustand: Unit-Tests 1389/1389 grün, Integrationstests 134/134 grün, E2E siehe [tests.md](inventory/tests.md). Es gibt keinen Test, der die Dirty-Erkennung je Eingabefeld gegen die Ribbon-Aktivierung prüft.

## Details

- [Datenmodelle](inventory/models.md)
- [Logik / ViewModels](inventory/logic.md)
- [Tests](inventory/tests.md)
