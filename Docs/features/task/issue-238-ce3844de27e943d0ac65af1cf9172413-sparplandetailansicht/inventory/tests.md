# Tests und Testluecken

## Vorhandene Testbereiche

Es gibt mehrere relevante Testebenen:

- `FinanceManager.Tests`: Unit-/ViewModel-Tests.
- `FinanceManager.Tests.Integration`: API-Client- und Integrationsfluesse.
- `FinanceManager.Tests.E2E`: E2E-Projekt vorhanden, fuer diese Anforderung aber noch nicht als konkrete Quelle identifiziert.

## Relevante vorhandene Tests

`FinanceManager.Tests/ViewModels/SavingsPlanEditViewModelTests.cs` testet das Detail-ViewModel:

- Laden eines bestehenden Sparplans.
- Speichern/Aktualisieren.
- Erstellen.
- Delete-Fehlerfall.
- In den Mocks wird bereits `SavingsPlans_AnalyzeAsync` eingerichtet, z. B. mit `SavingsPlanAnalysisDto(..., 50m, 10m, 6)`.

`FinanceManager.Tests/ViewModels/SavingsPlansViewModelTests.cs` testet die Liste:

- Laedt Sparplaene und Kategorien.
- Mockt `SavingsPlans_AnalyzeAsync` fuer Listeneintraege.

`FinanceManager.Tests.Integration/ApiClient/ApiClientSavingsPlansTests.cs` testet den API-Fluss:

- List, Count, Create, Get, Update.
- Analyse-Endpunkt (`SavingsPlans_AnalyzeAsync`).
- Archive/Delete.

## Testluecken zur neuen Anforderung

Es fehlen spezifische Tests fuer die Detailanzeige:

- Aktueller Saldo erscheint als nicht editierbares Feld im `CardRecord`.
- Restbetrag erscheint als nicht editierbares Feld im `CardRecord`.
- Erforderlicher Monatsbetrag erscheint bei einmaligem Sparplan mit Restbetrag und zukuenftigem Zieldatum.
- Erforderlicher Monatsbetrag erscheint nicht bei wiederkehrenden Sparplaenen.
- Erforderlicher Monatsbetrag erscheint nicht bei einmaligem Sparplan ohne Restbetrag.
- Erforderlicher Monatsbetrag erscheint nicht bei Faelligkeitsdatum heute oder Vergangenheit.

## Empfohlene Unit-Tests

Primaerer Zielort:

- `FinanceManager.Tests/ViewModels/SavingsPlanEditViewModelTests.cs`

Empfohlene Faelle:

1. `LoadAsync_ShouldExposeCurrentAndRemainingAmountFields`
   - Mock `SavingsPlans_GetAsync` liefert `SavingsPlanDto` mit `currentAmount` und `remainingAmount`.
   - Assert: `CardRecord.Fields` enthaelt LabelKeys fuer aktuellen Saldo und Restbetrag mit passenden Betragswerten.

2. `LoadAsync_ShouldExposeRequiredMonthly_ForOneTimePlanWithFutureTargetAndRemainingAmount`
   - Mock DTO: `Type = OneTime`, `RemainingAmount > 0`, `TargetDate = Today.AddMonths(...)`.
   - Mock Analyse: `RequiredMonthly > 0`.
   - Assert: Feld fuer erforderlichen Monatsbetrag vorhanden.

3. `LoadAsync_ShouldNotExposeRequiredMonthly_ForRecurringPlan`
   - Mock DTO: `Type = Recurring`, `RemainingAmount > 0`, zukuenftiges Zieldatum.
   - Assert: Feld fehlt.

4. `LoadAsync_ShouldNotExposeRequiredMonthly_WhenRemainingAmountIsZero`
   - Mock DTO: `RemainingAmount = 0`.
   - Assert: Feld fehlt.

5. `LoadAsync_ShouldNotExposeRequiredMonthly_WhenTargetDateIsTodayOrPast`
   - Parametrisierbar mit `DateTime.Today` und `DateTime.Today.AddDays(-1)`.
   - Assert: Feld fehlt.

## Empfohlene Service-/Integrationstests

Optional, aber sinnvoll, falls die Monatslogik fachlich fixiert werden soll:

- `SavingsPlanService.AnalyzeAsync` fuer Zieltermin heute: `RequiredMonthly == 0`, `MonthsRemaining == 0`.
- `SavingsPlanService.AnalyzeAsync` fuer Zieltermin in Zukunft: `RequiredMonthly == remaining / fullMonthsRemaining`.
- API-Client-Test, dass `SavingsPlans_GetAsync` `CurrentAmount` und `RemainingAmount` transportiert, sobald Testdaten mit Postings vorhanden sind.

## Testausfuehrung

Nach Implementierung sind mindestens diese Tests sinnvoll:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter SavingsPlan
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter SavingsPlans
```

Bei Aenderungen an Razor-/Rendering-Verhalten sollte ergaenzend der betroffene E2E- oder UI-Testpfad geprueft werden.

