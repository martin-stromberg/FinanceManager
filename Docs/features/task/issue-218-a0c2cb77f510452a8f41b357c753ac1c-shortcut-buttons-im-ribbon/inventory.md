# Bestandsaufnahme - Shortcut-Buttons im mobilen Ribbon

## Ausgangslage

Das Ribbon wird zentral ueber `FinanceManager.Web/Components/Shared/Ribbon.razor` gerendert. Die Daten kommen aus `UiRibbonRegister`, `UiRibbonTab` und `UiRibbonAction` in `FinanceManager.Web/ViewModels/Common/RibbonModels.cs`. Mobile Gruppen werden bereits als einklappbare Panels dargestellt, aber der mobile Gruppen-Header enthaelt bisher nur Titel und Hamburger-Symbol. Es gibt keine bestehende Eigenschaft, mit der eine Aktion als mobiler Shortcut markiert wird.

Die geforderte Erweiterung betrifft damit drei Schichten:

- Ribbon-Datenmodell: Transport einer Shortcut-Auswahl je `UiRibbonAction`.
- Ribbon-Komponente/CSS: Icon-only Shortcuts rechtsbuendig im geschlossenen mobilen Header rendern, Klicks ohne Gruppentoggle ausfuehren.
- ViewModels: vorhandene Ribbon-Aktionen bewerten und passende Shortcuts setzen.

## Detaildokumente

- [Datenmodell und API](inventory/datenmodell-und-api.md)
- [Rendering und Styles](inventory/rendering-und-styles.md)
- [ViewModel-Flaeche](inventory/viewmodel-flaeche.md)
- [Tests und Verifikation](inventory/tests-und-verifikation.md)
- [Risiken und offene Punkte](inventory/risiken-und-offene-punkte.md)

## Betroffene zentrale Dateien

| Bereich | Datei | Relevanz |
|---------|-------|----------|
| Ribbon-Modell | `FinanceManager.Web/ViewModels/Common/RibbonModels.cs` | `UiRibbonAction` braucht voraussichtlich eine neue init-only Shortcut-Markierung. |
| Ribbon-Konvertierung | `FinanceManager.Web/ViewModels/Common/RibbonExtensions.cs` | Legacy-Konvertierung zu `UiRibbonGroup` ignoriert aktuell alle Zusatzdaten ausser Basisfeldern. |
| Ribbon-Komponente | `FinanceManager.Web/Components/Shared/Ribbon.razor` | Mobile Header und interne `RibbonItem`-Abbildung muessen Shortcut-Informationen transportieren und rendern. |
| Ribbon-CSS | `FinanceManager.Web/wwwroot/css/ribbon.css` | Mobile Header-Layout, Icon-only Buttons, Ueberlaufbegrenzung und sichtbarer Zustand. |
| Tests | `FinanceManager.Tests/Components/RibbonTests.cs` | Bestehende bUnit-Tests fuer mobile Gruppen sind der direkte Erweiterungspunkt. |
| E2E | `FinanceManager.Tests.E2E/Infrastructure/PlaywrightWebAppFixture.cs` | Mobile Playwright-Session existiert bereits mit 390 x 844 Viewport. |

## Aktuelles Verhalten

- `Ribbon.razor` baut die UI bei jedem Render neu aus `Provider.GetRibbonRegisters(Localizer)`.
- Hidden-Actions werden in `BuildTabsToRender` ausgelassen.
- Desktop rendert alle sichtbaren Aktionen als `.fm-ribbon-btn`.
- Mobile rendert pro Gruppe einen Button `.fm-ribbon-mobile-group-header`; ein Klick setzt `_openMobileGroupId`.
- Mobile Menueintraege werden erst im geoeffneten `.fm-ribbon-mobile-menu.open` angezeigt.
- `OnMobileItemClick` fuehrt die bestehende Aktion aus und schliesst danach die mobile Gruppe.
- `FileCallback` wird durch ein ueberlagertes `InputFile` in `RenderRibbonItemContent` unterstuetzt.

## Voraussichtlicher Implementierungsansatz aus Bestandssicht

Die geringste strukturelle Aenderung ist eine neue init-only Eigenschaft auf `UiRibbonAction`, z. B. `MobileShortcut`. `Ribbon.razor` kann diese Eigenschaft beim Aufbau der internen `RibbonItem` uebernehmen. Zusaetzlich kann die Komponente eine Default-Regel anwenden: Wenn eine mobile Gruppe genau eine sichtbare Aktion enthaelt, gilt diese Aktion als Shortcut, auch wenn das ViewModel sie nicht explizit markiert hat.

Fuer Gruppen mit mehreren sichtbaren Aktionen sollten ViewModels gezielt `MobileShortcut = true` setzen. Die Komponente sollte dennoch `Hidden` und `Disabled` respektieren: versteckte Aktionen fallen weg, deaktivierte Shortcuts koennen deaktiviert sichtbar bleiben, weil dies dem bestehenden Ribbon-Verhalten entspricht.

## Testnahe Akzeptanzpunkte

- Geschlossene mobile Gruppe zeigt rechts neben dem Titel nur Icon-Shortcuts fuer markierte Aktionen.
- Offene mobile Gruppe zeigt keine Header-Shortcuts.
- Shortcut-Klick ruft denselben Callback wie der normale Ribbon-Button auf.
- Shortcut-Klick veraendert `_openMobileGroupId` nicht durch Header-Toggle-Propagation.
- Tabs/Gruppen mit genau einer sichtbaren Aktion erhalten automatisch einen Shortcut.
- Mehr-Aktions-Gruppen zeigen nur explizit markierte Shortcuts.
- Hidden-Actions erscheinen nicht als Shortcut.
- Deaktivierte Actions erscheinen als deaktivierter Shortcut oder werden nach fachlicher Entscheidung ausgelassen.

## Nicht direkt betroffen

Backend-Services, Datenbank, API-Controller und Domain-Modelle sind nach aktueller Bestandsaufnahme nicht betroffen. Die Aenderung ist UI-/ViewModel-seitig und nutzt vorhandene Callback-Pfade.
