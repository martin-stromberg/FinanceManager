# Datenmodell und API

## Bestehende Ribbon-Modelle

`FinanceManager.Web/ViewModels/Common/RibbonModels.cs` definiert die zentrale Datenstruktur:

- `UiRibbonAction`: Id, Label, IconSvg, Size, Disabled, Tooltip, Callback.
- `UiRibbonAction.FileCallback`: optionaler Upload-Callback fuer Import-Aktionen.
- `UiRibbonAction.Hidden`: init-only Flag, mit dem Aktionen vollstaendig aus dem Ribbon entfernt werden.
- `UiRibbonTab`: Titel, Liste von Aktionen, Sortierung.
- `UiRibbonRegister`: Registerart und Liste von Tabs.
- `UiRibbonItem` und `UiRibbonGroup`: Legacy-Kompatibilitaetsmodelle.

Eine Eigenschaft fuer mobile Shortcuts existiert noch nicht. Da `UiRibbonAction` ein positional record ist und bereits init-only Zusatzfelder fuer nicht-konstruktorbasierte Optionen nutzt, passt eine neue init-only Eigenschaft gut in das bestehende Muster:

```csharp
public bool MobileShortcut { get; init; }
```

Damit bleiben vorhandene Konstruktoraufrufe binaer/typisch quellkompatibel und ViewModels koennen gezielt einzelne Aktionen markieren:

```csharp
new UiRibbonAction(...){ MobileShortcut = true }
```

## Provider-Schnittstelle

`IRibbonProvider` liegt in `FinanceManager.Web/ViewModels/ViewModelBase.cs` und liefert:

- `GetRibbonRegisters(IStringLocalizer localizer)`
- `GetActiveTab<TTabEnum>()`
- `SetActiveTab<TTabEnum>(TTabEnum id)`

Die Schnittstelle muss fuer die Shortcut-Anforderung voraussichtlich nicht erweitert werden, weil die Shortcut-Auswahl ueber `UiRibbonAction` transportiert werden kann.

## Basisklassen

Es gibt zwei Basisklassen mit Ribbon-Logik:

- `FinanceManager.Web/ViewModels/ViewModelBase.cs`
- `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

Beide aggregieren Ribbon-Register aus lokalen Definitionen bzw. Child-ViewModels. Die Shortcut-Logik sollte moeglichst im Datenmodell und in `Ribbon.razor` liegen, damit beide Basisklassen ohne separate Anpassung weiter funktionieren.

## RibbonExtensions

`FinanceManager.Web/ViewModels/Common/RibbonExtensions.cs` wandelt `UiRibbonRegister` in Legacy-`UiRibbonGroup`/`UiRibbonItem` um. Diese Konvertierung transportiert aktuell nur Label, Icon, Size, Disabled, Action, Tooltip und Callback.

Wenn Shortcut-Informationen nur fuer `Ribbon.razor` benoetigt werden, ist hier keine zwingende Aenderung notwendig. Falls Legacy-Tests oder alte Verbraucher Shortcuts ebenfalls auswerten sollen, braucht `UiRibbonItem` eine entsprechende Eigenschaft und `ToUiRibbonGroups` muss sie uebernehmen.

## Default-Regel fuer Ein-Aktions-Tabs

Die Anforderung sagt: Tabs mit genau einer Aktion sollen diese Aktion standardmaessig als Shortcut markieren. Aus Bestandssicht gibt es zwei moegliche Orte:

- Zentral in `Ribbon.razor`: Nach Anwendung von `Hidden` je Gruppe pruefen, ob genau eine sichtbare Aktion vorhanden ist.
- Zentral in einem Helper, z. B. `RibbonExtensions`: Vorbereitete Shortcut-Informationen aus Tabs ableiten.

Die Komponente ist der pragmatischere Ort, weil sie bereits sichtbare Aktionen filtert und die mobile Gruppierung kennt. Wichtig ist, dass die Regel auf sichtbare Aktionen angewendet wird, nicht blind auf `tab.Items.Count`.

## Kompatibilitaet

Eine neue init-only Eigenschaft auf `UiRibbonAction` ist risikoarm:

- Bestehende `new UiRibbonAction(...)` Aufrufe bleiben unveraendert gueltig.
- `Hidden` und `FileCallback` zeigen bereits, dass init-only Zusatzdaten akzeptiert sind.
- Keine API-/Backend-Schnittstellen werden beruehrt.
