# Abnahmeprüfung – Entwicklungsschritt 10

## Ergebnis

**Status:** Anforderung vollständig erfüllt

## Abweichungen

Keine.

## Hinweise

- **Prüfumfang:** `git diff c11a6267..HEAD` (Schritt 10 ist in Commit `67cf45ac` enthalten) gegen Schritt 10 in `project-plan.md` (Zeilen 179–183) sowie `requirement.md` (FA-4, AK 9). Code-Review plus Testlauf.

- **Dedizierte Regressions-Testsuite:** `FinanceManager.Tests/Budget/BudgetPurposeServiceSavingsPlanActualsTests.cs` (4 Tests, alle bestanden) deckt exakt die geforderten Invarianten ab:
  - `ListOverview_PlanPostings_ActualsKeepSignConvention` — Vorzeichenkonvention unverändert (`Sum(-p.Amount)`: negative Plan-Postings bleiben positive Kosten-Actuals).
  - `ListOverview_LinkedAccountBankPostings_NotPlanActuals` — Bankpostings verknüpfter Vertragskonten fließen **nicht** als Sparplan-Budgetactuals ein (kein Doppel-Effekt durch den Link).
  - `ListOverview_ActualReport_ExcludedFromActuals` — Istmeldung verändert die Actuals nicht.
  - `ListOverview_InformalFundingPosting_StillCountsForActuals` — die `informal`-Auswertung berührt die Budgetsicht nicht: informelle Plan-Postings zählen weiterhin als Actuals (Kostensicht wie bisher).

- **Implementierungs-Minimalismus bestätigt Unverändertheit:** Keine Budget-Query nutzt `SavingsPlanAccountLinks`; Quelle und Vorzeichen der Actuals sind unverändert. Einzige fachliche Anpassung ist der geplante, begründete Ausschluss der Saldenkorrektur: `p.SavingsPlanSubType != SavingsPlanPostingSubType.ActualReport` in `BudgetPurposeService.cs:351–357` (ListOverview-Actuals) und `BudgetImpactEvaluationService.cs:243` (Draft-/Posting-Matching) — entspricht Vorgehensentscheidung 5 („Kostensicht wie bisher"), da eine Korrekturbuchung kein Kostenfluss ist. `BudgetReportService.BuildRealizationsAsync` benötigt keine Änderung: Realizations entstehen aus Kontakt-Postings bzw. `GroupId`-Fallback; die Istmeldung hat weder Kontakt-Posting noch `GroupId` und kann dort strukturell nicht einfließen.

- **Keine ungewollte Änderung aufgedeckt:** Die Tests bestätigen den Ist-Zustand; eine Bugfix-Notwendigkeit („Deckt ein Test eine ungewollte Änderung auf, wird die Ursache behoben") trat nicht ein — die einzige nötige Ursache (Istmeldung-Ausschluss) wurde bereits in Schritt 5 implementiert und hier verifiziert.

- **Testnachweis:** `dotnet test FinanceManager.Tests` (gefiltert) → `BudgetPurposeServiceSavingsPlanActualsTests` **4/4 bestanden** (Teil des 33/33-Laufs).

- **Nicht-Ziel-Einhaltung:** Es wurde keine separate Konto-/Einnahmen-Budgetquelle modelliert, keine automatische Zuordnung von Bankpostings zu Plänen eingeführt und keine Aufteilungslogik für Sammelkonten gebaut — konsistent mit den expliziten Nicht-Zielen in `requirement.md`.
