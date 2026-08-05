# UI und ViewModels

## Detailroute und Rendering

Die Sparplandetailansicht laeuft ueber die generische Kartenroute:

- `FinanceManager.Web/Components/Pages/CardPage.razor`
- Route: `/card/savings-plans/{id}`
- Aufloesung des ViewModels ueber `CardViewModelResolver.Resolve(kind, sub)`
- Rendering ueber `GenericCardPage`

`GenericCardPage.razor` rendert ab Zeile 34 alle `Provider.CardRecord.Fields`. Das bedeutet: Neue Detailkennzahlen koennen ohne neue Razor-Seite als zusaetzliche `CardField`s im ViewModel erscheinen.

## Sparplan-Detail-ViewModel

`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlanCardViewModel.cs` ist der zentrale Erweiterungspunkt.

Wichtige Stellen:

- Zeile 46: Property `Analysis` ist vorhanden.
- Zeile 102: `LoadAsync(Guid id)` laedt den Sparplan.
- Zeile 119: `ApiClient.SavingsPlans_GetAsync(id)` liefert `SavingsPlanDto`.
- Zeile 147: `LoadAnalysisAsync()` laedt `SavingsPlanAnalysisDto`, wird aber nicht automatisch in `LoadAsync` aufgerufen.
- Zeile 339: `BuildCardRecordAsync(SavingsPlanDto? dto)` baut die sichtbaren Kartenfelder.
- Zeilen 420 bis 428: Derzeit werden Zielbetrag, Zieldatum, Vertragsnummer und Symbol hinzugefuegt; `CurrentAmount`, `RemainingAmount` und `RequiredMonthly` fehlen.
- Zeilen 613 bis 614: Ribbon-Aktion "Recalculate" ruft Analyse nachtraeglich ab.

## Bestehende Listenanzeige als Referenz

`FinanceManager.Web/ViewModels/SavingsPlans/SavingsPlansListViewModel.cs` zeigt, dass die Kennzahlen in der UI bereits etabliert sind:

- Zeile 103: Analyse wird pro Listeneintrag geladen.
- Zeile 144: Spalte `List_Th_Balance`.
- Zeile 145: Spalte `List_Th_Remaining`.
- Zeile 197: Anzeige von `AccumulatedAmount` als Saldo.
- Zeile 203: Berechnung Restbetrag als `TargetAmount - AccumulatedAmount`.

Die Detailansicht sollte fachlich konsistent zur Liste sein. Allerdings kann sie fuer Saldo und Restbetrag direkt `SavingsPlanDto.CurrentAmount` und `SavingsPlanDto.RemainingAmount` verwenden, weil `GetAsync` diese Werte bereits berechnet.

## Lokalisierung

Vorhandene Ressourcen:

- `FinanceManager.Web/Resources/Components/Pages/SavingsPlanEdit.de.resx`
- `FinanceManager.Web/Resources/Components/Pages/SavingsPlanEdit.en.resx`

Dort existieren bereits Analyse-Labels:

- `Analysis_Accumulated`
- `Analysis_RequiredMonthly`
- `Analysis_MonthsRemaining`

Die generische CardField-Anzeige verwendet LabelKeys, die in der Karten-/Pages-Lokalisierung aufgeloest werden. Bestehende CardKeys im ViewModel haben das Muster:

- `Card_Caption_SavingsPlan_Name`
- `Card_Caption_SavingsPlan_TargetAmount`
- `Card_Caption_SavingsPlan_TargetDate`

Fuer neue Felder sollten entsprechend neue Keys ergaenzt werden, z. B.:

- `Card_Caption_SavingsPlan_CurrentAmount`
- `Card_Caption_SavingsPlan_RemainingAmount`
- `Card_Caption_SavingsPlan_RequiredMonthly`

## Empfohlener UI-Ansatz

Die Kennzahlen sollten als nicht editierbare `CardFieldKind.Currency`-Felder in die bestehende Karte aufgenommen werden:

- Aktueller Saldo: immer bei bestehendem Sparplan anzeigen.
- Restbetrag: immer bei bestehendem Sparplan anzeigen; Wert kann `0` sein.
- Durchschnittlicher Monatsbetrag: nur wenn `dto.Type == SavingsPlanType.OneTime`, `dto.RemainingAmount > 0`, `dto.TargetDate?.Date > DateTime.Today` und Analyse `RequiredMonthly > 0`.

Damit bleiben Anlage/Bearbeitung von Sparplaenen unveraendert, wie in den Nicht-Zielen gefordert.

