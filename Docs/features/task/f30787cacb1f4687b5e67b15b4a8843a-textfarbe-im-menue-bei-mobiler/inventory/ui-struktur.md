# UI-Struktur des Ribbon-Menues

## Komponente

`FinanceManager.Web/Components/Shared/Ribbon.razor` rendert das Ribbon fuer alle relevanten Oberflaechen. Die Komponente erzeugt sowohl Desktop-Elemente als auch mobile Ersatzdarstellungen.

Wichtige Stellen:

- Zeile 34: Wurzelcontainer `.fm-ribbon`
- Zeile 55: Toolbar-Container `.fm-ribbon-groups`
- Zeile 60: Desktop-Gruppe `.fm-ribbon-group fm-ribbon-group-desktop`
- Zeile 83: Mobile Gruppe `.fm-ribbon-mobile-group-panel`
- Zeile 84: Mobile Headerzeile `.fm-ribbon-mobile-group-header`
- Zeile 86: Button `.fm-ribbon-mobile-group-title-toggle`
- Zeile 90: Textspan `.fm-ribbon-mobile-group-title`
- Zeile 97: Shortcut-Container `.fm-ribbon-mobile-shortcuts`
- Zeile 104: Shortcut-Button `.fm-ribbon-mobile-shortcut`
- Zeile 115: Expand-/Hamburger-Button `.fm-ribbon-mobile-group-toggle`
- Zeile 135: Mobile Menueeintraege `.fm-ribbon-mobile-menu-item`

## Mobile Texte und Icons

In der mobilen Ansicht sind mindestens diese sichtbaren Inhalte relevant:

- Gruppentitel im Button `.fm-ribbon-mobile-group-title-toggle`
- Hamburger-Icon im Button `.fm-ribbon-mobile-group-toggle`
- Shortcut-Icons in `.fm-ribbon-mobile-shortcut`
- Aktionslabels in `.fm-ribbon-mobile-menu-item .text-inline`
- Icons in `.fm-ribbon-mobile-menu-item .icon svg`

Die Icons sind inline SVG. Das Hamburger-SVG verwendet `fill="currentColor"`, sodass seine Farbe direkt von der CSS-Eigenschaft `color` des Buttons abhaengt.

## Funktionslogik

Die mobile Logik ist zustandsarm:

- `_openMobileGroupId` steuert, ob ein mobiles Gruppenmenue geoeffnet ist.
- `ToggleMobileGroup(...)` klappt mobile Gruppen auf oder zu.
- `OnMobileItemClick(...)` fuehrt die Aktion aus und schliesst das mobile Menue.
- Einzelne sichtbare, nicht deaktivierte Actions koennen automatisch oder explizit als `MobileShortcut` gerendert werden.

Fuer die Farbkorrektur ist keine Aenderung an dieser Logik erkennbar.
