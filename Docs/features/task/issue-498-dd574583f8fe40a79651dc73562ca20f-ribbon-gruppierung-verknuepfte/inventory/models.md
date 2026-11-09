# Datenmodell – Ribbon-Typen

Die Ribbon-Reihenfolge wird über die `UiRibbonTab`-Liste und das optionale Feld `Sort` gesteuert. Keine persistierten Entitäten betroffen; es gibt keine DTO-/Backup-Auswirkungen.

## `UiRibbonTab`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeile 85)

`public sealed record UiRibbonTab(string Title, List<UiRibbonAction> Items, int Sort = 0);`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Title` | `string` | Gruppentitel (aus resx, z. B. `Ribbon_Group_Linked`). Dient in `Ribbon.razor` als Merge-Schlüssel über Register hinweg. |
| `Items` | `List<UiRibbonAction>` | Aktionen der Gruppe. |
| `Sort` | `int` (Default `0`) | Optionaler Sortierschlüssel; `Ribbon.razor` sortiert Gruppen via `OrderBy(g => g.Sort)` (stabile Sortierung → bei gleichem `Sort` zählt die First-Seen-Reihenfolge der Liste). Alle drei betroffenen ViewModels übergeben keinen `Sort`-Wert. |

## `UiRibbonRegister`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeile 93)

`public sealed record UiRibbonRegister(UiRibbonRegisterKind Kind, List<UiRibbonTab>? Tabs)`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Kind` | `UiRibbonRegisterKind` | Register-Art; alle drei betroffenen ViewModels nutzen `UiRibbonRegisterKind.Actions`. |
| `Tabs` | `List<UiRibbonTab>?` | Geordnete Tab-/Gruppenliste; die Reihenfolge ist für diese Anforderung maßgeblich. |
| `Title` (abgeleitet) | `string` | Titel des ersten Tabs, Kompatibilitäts-Hilfsproperty. |
| `Items` (abgeleitet) | `List<UiRibbonAction>` | Items des ersten Tabs, Kompatibilitäts-Hilfsproperty. |

## `UiRibbonAction`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 42–76)

`public sealed record UiRibbonAction(string Id, string Label, string IconSvg, UiRibbonItemSize Size, bool Disabled, string? Tooltip, Func<Task>? Callback)`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `string` | Aktions-ID (z. B. `Back`, `Reverse`, `OpenPostings`); Alias `Action`. |
| `Label` | `string` | Lokalisierte Beschriftung. |
| `IconSvg` | `string` | Inline-SVG-Icon. |
| `Size` | `UiRibbonItemSize` | `Small`/`Large`. |
| `Disabled` | `bool` | Deaktiviert-Status. |
| `Tooltip` | `string?` | Tooltip. |
| `Callback` | `Func<Task>?` | Click-Callback. |
| `FileCallback` | `Func<InputFileChangeEventArgs, Task>?` (init) | Import-Callback. |
| `MobileShortcut` | `bool` (init) | Icon-Shortcut in der mobilen Gruppenzeile. |
| `Hidden` | `bool` (init) | Blendet die Aktion komplett aus. |

Keine Änderung an diesen Typen erforderlich.
