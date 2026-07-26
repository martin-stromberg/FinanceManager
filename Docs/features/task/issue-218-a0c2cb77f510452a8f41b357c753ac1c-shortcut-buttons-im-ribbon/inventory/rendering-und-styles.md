# Rendering und Styles

## Bestehendes Rendering

`FinanceManager.Web/Components/Shared/Ribbon.razor` rendert pro sichtbarer Gruppe zwei Varianten:

- Desktop: `.fm-ribbon-group.fm-ribbon-group-desktop` mit Buttons `.fm-ribbon-btn`.
- Mobile: `.fm-ribbon-mobile-group-panel` mit Header-Button `.fm-ribbon-mobile-group-header` und Menue `.fm-ribbon-mobile-menu`.

Der mobile Header ist derzeit selbst ein `<button>` und toggelt die Gruppe:

- Header-Klick: `ToggleMobileGroup(mobileGroupId)`
- Geoeffnetes Menue: Klasse `open`
- Mobile Menueaktion: `OnMobileItemClick(item)` ruft `OnItemClick(item)` auf und setzt `_openMobileGroupId = null`

## Interne Abbildung

`BuildTabsToRender` kopiert `UiRibbonAction` in die interne Klasse `RibbonItem`. Dabei werden `Id`, `Label`, `IconSvg`, `Size`, `Disabled`, `Tooltip`, `Callback` und `FileCallback` uebernommen. `Hidden` wird vor dem Kopieren ausgewertet.

Fuer Shortcuts braucht `RibbonItem` eine zusaetzliche Eigenschaft, z. B.:

```csharp
public bool MobileShortcut { get; set; }
```

Beim Kopieren kann diese Eigenschaft aus `UiRibbonAction.MobileShortcut` kommen. Die Default-Regel fuer Gruppen mit genau einer sichtbaren Aktion kann nach dem Aufbau der Gruppen angewendet werden.

## Header-Struktur

Der bestehende mobile Header ist ein Button, in den keine weiteren Buttons verschachtelt werden sollten. Fuer Shortcut-Buttons ist daher eine kleine Strukturumstellung sinnvoll:

- aeusserer Container `.fm-ribbon-mobile-group-header`
- separater Toggle-Button fuer Titel/Hamburger
- separater Shortcut-Container mit Icon-Buttons

Alternativ koennte der Header Button bleiben und Shortcuts als sibling daneben liegen. Wichtig ist semantisch: keine Buttons in Buttons verschachteln.

## Event-Propagation

Shortcut-Klicks duerfen das Aufklappen nicht ausloesen. In Blazor ist dafuer auf dem Shortcut-Button erforderlich:

```razor
@onclick="(() => OnItemClick(item))"
@onclick:stopPropagation="true"
```

Wenn der Header nicht mehr der aeussere Button ist, bleibt `stopPropagation` trotzdem sinnvoll, weil der mobile Headerbereich optisch eine Einheit bleibt und spaetere Container-Handler nicht versehentlich ausgeloest werden.

## Icon-only Rendering

`RenderRibbonItemContent` rendert aktuell immer Icon plus Text. Fuer Shortcuts sollte eine eigene Render-Variante nur das Icon ausgeben:

- sichtbares Icon via `IconSvg`
- `aria-label` aus `Label`
- `title` aus `Tooltip` oder `Label`
- keine sichtbare `.text-inline`

Bei `FileCallback` ist besondere Vorsicht noetig: Der bestehende Mechanismus nutzt ein ueberlagertes `InputFile`. Falls Datei-Shortcuts unterstuetzt werden sollen, muss dieses Overlay auch im kompakten Icon-Button funktionieren. Falls nicht, sollten `FileCallback`-Aktionen nicht automatisch zu Shortcuts werden.

## CSS-Bestand

`FinanceManager.Web/wwwroot/css/ribbon.css` definiert ab `@media (max-width: 900px)` das mobile Verhalten:

- `.fm-ribbon-group-desktop` wird ausgeblendet.
- `.fm-ribbon-mobile-group-panel` wird eingeblendet.
- `.fm-ribbon-mobile-group-header` ist ein flexibler Header mit `justify-content: space-between`.
- `.fm-ribbon-mobile-menu` wird per `display: none` / `.open { display: flex }` geschaltet.

Fuer Shortcuts sind neue Klassen sinnvoll:

- `.fm-ribbon-mobile-group-toggle`
- `.fm-ribbon-mobile-shortcuts`
- `.fm-ribbon-mobile-shortcut`

## Layout-Anforderungen

Die Shortcuts sollen rechtsbuendig im geschlossenen Header erscheinen und den Titel sowie den Aufklappbutton nicht ueberdecken. Ein robustes Flex-Layout sollte:

- den Titel mit `min-width: 0` und `overflow: hidden` begrenzen,
- den Shortcut-Container mit `flex: 0 1 auto` und `overflow: hidden` begrenzen,
- feste Icon-Button-Groessen verwenden,
- den Hamburger/Toggler als eigenes fixes Element behalten,
- bei geoeffneter Gruppe Shortcuts nicht rendern oder per CSS ausblenden.

Rein per CSS ist eine harte Begrenzung der sichtbaren Anzahl nur eingeschraenkt steuerbar. Praktisch genuegt voraussichtlich: Container begrenzen, Buttons nicht schrumpfen lassen, Ueberlauf verstecken. JavaScript-Resize-Messung ist nur noetig, wenn eine exakte Anzahl sichtbarer Shortcuts fachlich gefordert wird.
