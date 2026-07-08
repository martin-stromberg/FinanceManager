# Interfaces – Verloren gegangene Ribbon-Aktionen in den Einstellungen

## `IRibbonProvider`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (implementiert von `BaseViewModel`)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetRibbonRegisters(IStringLocalizer localizer)` | `localizer`: Lokalisierungsdienst | `IReadOnlyList<UiRibbonRegister>?` | Liefert aggregierte Ribbon-Register des ViewModels und aller aktiven Kind-ViewModels. Wird vom Ribbon-Renderer aufgerufen, um alle anzuzeigenden Aktionen zu erhalten. |

`BaseViewModel` implementiert dieses Interface und stellt sicher, dass `GetRibbonRegisters()` sowohl die eigenen Definitionen (`GetRibbonRegisterDefinition()`) als auch jene aller in `_childViewModels` eingetragenen Kind-ViewModels aggregiert. Nur ViewModels, die via `CreateSubViewModel<T>()` erzeugt wurden, nehmen an dieser Aggregation teil.
