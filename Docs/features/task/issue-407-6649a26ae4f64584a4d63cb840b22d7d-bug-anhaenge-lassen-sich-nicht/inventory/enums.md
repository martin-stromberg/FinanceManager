# Enums

## `ConfirmationSeverity`
Datei: `FinanceManager.Web/Services/ConfirmationSeverity.cs`

| Wert | Bedeutung |
|------|-----------|
| `Default` | Standard-Bestätigung, keine besondere Hervorhebung. |
| `Warning` | Warnung, potenziell folgenreiche Aktion. |
| `Critical` | Kritische/destruktive Aktion — wird von `AttachmentsPanel.DeleteAsync` (Zeile 406) verwendet; gerendert als CSS-Klasse `confirm-severity-critical`. |

## `EmbeddedPanelPosition` (Kontext)
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeilen 33–49)

| Wert | Bedeutung |
|------|-----------|
| `AfterRibbon` | Eingebettetes Panel hinter der Ribbon der CardPage. |
| `AfterCard` | Panel hinter dem Karteninhalt. |
| `AfterList` | Panel hinter dem Listeninhalt. |

## `BooleanSelection` (Kontext)
Datei: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs` (Zeilen 17–28)

| Wert | Bedeutung |
|------|-----------|
| `True` | Positive Auswahl in UI-Bindings. |
| `False` | Negative Auswahl in UI-Bindings. |
