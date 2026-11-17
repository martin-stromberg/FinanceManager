# Enums

## `UiRibbonRegisterKind`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 17–29)

| Wert | Bedeutung |
|------|-----------|
| `QuickAccess` | Register im Schnellzugriffsbereich. |
| `Actions` | Register mit den Hauptaktionen der Ansicht — von allen drei betroffenen ViewModels verwendet. |
| `LinkedInfo` | Register für verknüpfte Informationen (in AGENT.md als Register-Kind für die Linked-Gruppe erwähnt; von den drei betroffenen ViewModels nicht verwendet). |
| `Reports` | Register für Berichtsaktionen. |
| `Custom` | Register für Erweiterungen. |

Hinweis: `EnumCoverageTests` (siehe AGENT.md) assertiert Enum-Mitgliederlisten; für diese Anforderung werden keine Enum-Mitglieder geändert oder hinzugefügt.

## `UiRibbonItemSize`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` (Zeilen 6–12)

| Wert | Bedeutung |
|------|-----------|
| `Small` | Kleineres Ribbon-Element. |
| `Large` | Größeres (primäres) Ribbon-Element. |
