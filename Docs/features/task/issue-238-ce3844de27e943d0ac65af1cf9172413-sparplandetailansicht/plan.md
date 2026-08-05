# Umsetzungsplan: Sparplandetailansicht

## Zielbild

Die bestehende Sparplandetailansicht unter `/card/savings-plans/{id}` zeigt fuer bestehende Sparplaene zusaetzlich zu den Stammdaten diese Kennzahlen:

- Aktueller Saldo aus `SavingsPlanDto.CurrentAmount`
- Restbetrag aus `SavingsPlanDto.RemainingAmount`
- Durchschnittlich benoetigter Monatsbetrag aus `SavingsPlanAnalysisDto.RequiredMonthly`, aber nur bei einmaligen Sparplaenen mit offenem Restbetrag und zukuenftigem Faelligkeitsdatum

Die Umsetzung nutzt die vorhandene generische Kartenansicht. Es werden keine neuen API-Endpunkte, DTOs oder Domain-Eigenschaften benoetigt.

## Annahmen

- `SavingsPlanService.GetAsync` ist die verbindliche Datenquelle fuer aktuellen Saldo und Restbetrag in der Detailansicht.
- `SavingsPlanService.AnalyzeAsync` bleibt die verbindliche Datenquelle fuer den erforderlichen Monatsbetrag.
- Die vorhandene Analyseberechnung mit vollen verbleibenden Kalendermonaten wird fuer diese Anforderung akzeptiert. Es wird keine kalendertagsgenaue oder anteilige Monatsberechnung eingefuehrt.
- Die neuen Kennzahlen werden nur fuer bestehende Sparplaene angezeigt. Bei `Guid.Empty` bleibt die Anlageansicht unveraendert.
- Wenn das automatische Laden der Analyse fehlschlaegt, bleibt die Detailansicht nutzbar; Saldo und Restbetrag werden weiterhin angezeigt, der Monatsbetrag wird dann nicht angezeigt.

## Betroffene Dateien

| Datei | Aenderung |
|-------|-----------|
| `FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` | Analyse beim Laden bestehender Sparplaene laden, CardRecord um nicht editierbare Kennzahlen erweitern, Recalculate-Aktion bei Bedarf CardRecord neu aufbauen lassen |
| `FinanceManager.Web/Resources/Pages.resx` | Neue Card-Caption-Keys fuer die Kennzahlen ergaenzen |
| `FinanceManager.Web/Resources/Pages.de.resx` | Deutsche Labels ergaenzen |
| `FinanceManager.Web/Resources/Pages.en.resx` | Englische Labels ergaenzen |
| `FinanceManager.Tests/ViewModels/SavingsPlanEditViewModelTests.cs` | Unit-Tests fuer Anzeige- und Nichtanzeige-Regeln der neuen Felder ergaenzen |

## Umsetzungsschritte

1. `SavingsPlanCardViewModel.LoadAsync(Guid id)` erweitern.
   - Bei `Guid.Empty` keine Analyse laden und keine Kennzahlen anzeigen.
   - Nach erfolgreichem `SavingsPlans_GetAsync(id)` und `LoadCategoriesAsync()` fuer bestehende Sparplaene `SavingsPlans_AnalyzeAsync(id)` laden.
   - Das Ergebnis in `Analysis` speichern, bevor `BuildCardRecordAsync(dto)` aufgerufen wird.
   - Fehler beim Analyseladen lokal abfangen, damit die Detailansicht nicht durch optionale Analysewerte blockiert wird.

2. `LoadAnalysisAsync(CancellationToken ct = default)` aktualisieren.
   - Nach erfolgreichem Laden der Analyse den `CardRecord` fuer den aktuell geladenen DTO-Stand neu aufbauen, sofern `_loadedDto` vorhanden ist.
   - Dadurch aktualisiert auch die vorhandene Ribbon-Aktion "Neu berechnen" die sichtbaren Kennzahlen.
   - Das bestehende Verhalten fuer Nicht-Edit-Modus beibehalten.

3. `BuildCardRecordAsync(SavingsPlanDto? dto)` um Kennzahlenfelder erweitern.
   - Nach Zielbetrag und Zieldatum oder direkt vor Vertragsnummer/Symbol nicht editierbare `CardFieldKind.Currency`-Felder einfuegen:
     - `Card_Caption_SavingsPlan_CurrentAmount` mit `amount: dto.CurrentAmount`
     - `Card_Caption_SavingsPlan_RemainingAmount` mit `amount: dto.RemainingAmount`
   - Diese beiden Felder nur einfuegen, wenn `dto` nicht `null` ist.
   - Das Monatsbetragsfeld nur einfuegen, wenn alle Bedingungen erfuellt sind:
     - `dto.Type == SavingsPlanType.OneTime`
     - `dto.RemainingAmount > 0m`
     - `dto.TargetDate?.Date > DateTime.Today`
     - `Analysis?.RequiredMonthly > 0m`
   - Das Monatsbetragsfeld als `Card_Caption_SavingsPlan_RequiredMonthly` mit `CardFieldKind.Currency` und `editable: false` anlegen.

4. Lokalisierung ergaenzen.
   - Neue Keys in `FinanceManager.Web/Resources/Pages.resx`, `.de.resx` und `.en.resx` neben den bestehenden `Card_Caption_SavingsPlan_*`-Keys eintragen.
   - Vorgeschlagene Werte:
     - `Card_Caption_SavingsPlan_CurrentAmount`: `Aktueller Saldo` / `Current balance`
     - `Card_Caption_SavingsPlan_RemainingAmount`: `Restbetrag` / `Remaining amount`
     - `Card_Caption_SavingsPlan_RequiredMonthly`: `Benoetigter Monatsbetrag` / `Required monthly amount`

5. Unit-Tests erweitern.
   - In `SavingsPlanEditViewModelTests` Hilfslogik fuer Sparplan-DTOs und Feldsuche nutzen oder einfuehren.
   - Test: `LoadAsync_ShouldExposeCurrentAndRemainingAmountFields`
     - Mock-DTO mit `CurrentAmount` und `RemainingAmount`.
     - Assert auf Felder `Card_Caption_SavingsPlan_CurrentAmount` und `Card_Caption_SavingsPlan_RemainingAmount`, `Kind == Currency`, `Editable == false`, passende `Amount`-Werte.
   - Test: `LoadAsync_ShouldExposeRequiredMonthly_ForOneTimePlanWithFutureTargetAndRemainingAmount`
     - `SavingsPlanType.OneTime`, `RemainingAmount > 0`, `TargetDate = DateTime.Today.AddMonths(...)`, Analyse `RequiredMonthly > 0`.
     - Assert: Feld `Card_Caption_SavingsPlan_RequiredMonthly` vorhanden.
   - Test: `LoadAsync_ShouldNotExposeRequiredMonthly_ForRecurringPlan`
     - Gleiche Daten, aber `SavingsPlanType.Recurring`.
     - Assert: Monatsbetragsfeld fehlt.
   - Test: `LoadAsync_ShouldNotExposeRequiredMonthly_WhenRemainingAmountIsZero`
     - Einmaliger Sparplan mit zukuenftigem Ziel, aber `RemainingAmount == 0`.
     - Assert: Monatsbetragsfeld fehlt.
   - Test: `LoadAsync_ShouldNotExposeRequiredMonthly_WhenTargetDateIsTodayOrPast`
     - Parametrisierter Test fuer `DateTime.Today` und `DateTime.Today.AddDays(-1)`.
     - Assert: Monatsbetragsfeld fehlt.

6. Bestehende Tests stabilisieren.
   - Vorhandene Tests, die bestehende Sparplaene laden, muessen bei automatischem Analyseaufruf entweder `SavingsPlans_AnalyzeAsync` mocken oder so angepasst werden, dass der abgefangene Analysefehler die Erwartung nicht stoert.
   - Falls ein Test explizit prueft, dass nur bestimmte Felder vorhanden sind, die neuen Felder beruecksichtigen.

## Validierung

Mindestens auszufuehren:

```powershell
dotnet test FinanceManager.Tests/FinanceManager.Tests.csproj --filter SavingsPlan
```

Optional, falls waehrend der Umsetzung Service- oder API-Verhalten beruehrt wird:

```powershell
dotnet test FinanceManager.Tests.Integration/FinanceManager.Tests.Integration.csproj --filter SavingsPlans
```

## Review-Kriterien

- Aktueller Saldo und Restbetrag erscheinen in der Sparplandetailansicht fuer bestehende Sparplaene.
- Beide Werte sind nicht editierbar.
- Der Monatsbetrag erscheint nur fuer einmalige Sparplaene mit offenem Restbetrag und zukuenftigem Faelligkeitsdatum.
- Der Monatsbetrag erscheint nicht fuer wiederkehrende Sparplaene.
- Der Monatsbetrag erscheint nicht bei `RemainingAmount == 0`.
- Der Monatsbetrag erscheint nicht bei Faelligkeitsdatum heute oder in der Vergangenheit.
- Die Anlage und Bearbeitung von Sparplaenen bleibt funktional unveraendert.
- Es gibt keine neuen API-Endpunkte oder veraenderten DTO-Vertraege.

## Offene Punkte

Keine.
