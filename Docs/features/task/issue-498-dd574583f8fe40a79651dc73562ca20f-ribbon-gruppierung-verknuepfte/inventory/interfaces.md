# Interfaces

## `IRibbonProvider`
Datei: `FinanceManager.Web/ViewModels/ViewModelBase.cs` (Zeilen 14–21)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRibbonRegisters` | `IStringLocalizer localizer` | `IReadOnlyList<UiRibbonRegister>?` | Liefert die Ribbon-Register inkl. geordneter Tab-Listen; in `BaseViewModel` implementiert (`ViewModels/Common/BaseViewModel.cs:628`) und von `Ribbon.razor` konsumiert. |

Weitere Member des Contracts (aus `Ribbon.razor` und `BaseViewModel` ersichtlich): `GetActiveTab<TTabEnum>()` und `SetActiveTab<TTabEnum>(id)` für die aktive Tab-Verwaltung — für die Reihenfolge-Änderung nicht relevant.

Gleichnamiger Contract auch in `FinanceManager.Web/ViewModels/Common/IListProvider.cs` (Zeile 118) mit `GetRibbonRegisters`.

## `IDeletableViewModel`
Datei: `FinanceManager.Web/ViewModels/Common/` (von `BankAccountCardViewModel` implementiert, `BankAccountCardViewModel.cs:16`)

Contract für löschbare ViewModels; für die Reihenfolge-Änderung nicht relevant, nur der Vollständigkeit halber erwähnt.
