# Enums – Verloren gegangene Ribbon-Aktionen in den Einstellungen

## `UiRibbonItemSize`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs`

| Wert | Bedeutung |
|------|-----------|
| `Small` | Kleines Ribbon-Element für sekundäre Aktionen. |
| `Large` | Großes Ribbon-Element für primäre Aktionen. |

---

## `UiRibbonRegisterKind`
Datei: `FinanceManager.Web/ViewModels/Common/RibbonModels.cs`

| Wert | Bedeutung |
|------|-----------|
| `QuickAccess` | Register für die Schnellzugriff-Leiste. |
| `Actions` | Register mit den Haupt-Aktionen der aktuellen Ansicht. |
| `LinkedInfo` | Register für verknüpfte Informations-Aktionen. |
| `Reports` | Register für Berichts-Aktionen. |
| `Custom` | Erweiterungspunkt für anwendungsspezifische Register. |

---

## `EmbeddedPanelPosition`
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

| Wert | Bedeutung |
|------|-----------|
| `AfterRibbon` | Eingebettetes Panel wird nach dem Ribbon (Aktionsleiste) der Card-Seite dargestellt. `SetupCardViewModel` verwendet diesen Wert. |
| `AfterCard` | Eingebettetes Panel wird nach dem Hauptinhalt der Card-Seite dargestellt. |
