# Bestandsaufnahme - Textfarbe im mobilen Ribbon-Menue

## Zusammenfassung

Die Anforderung betrifft ausschliesslich die Darstellung des Ribbon-Menues in mobilen Viewports im Dark Mode. Die relevante Implementierung ist zentral in der Blazor-Komponente `FinanceManager.Web/Components/Shared/Ribbon.razor` und den global eingebundenen Styles `FinanceManager.Web/wwwroot/css/ribbon.css` sowie `FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css`.

Die wahrscheinlich betroffene Stelle ist der mobile Ribbon-Header: `theme.Dark.Ribbon.css` setzt zwar `color: #f0f0f0` auf `.fm-ribbon-mobile-group-header`, die darin enthaltenen Buttons `.fm-ribbon-mobile-group-title-toggle`, `.fm-ribbon-mobile-group-toggle` und `.fm-ribbon-mobile-shortcut` erhalten aber keine explizite Textfarbe. Browser-Button-Defaults oder bestehende Button-Regeln koennen daher schwarze Schrift bzw. schwarze `currentColor`-Icons erzeugen. Die aufgeklappten Menueeintraege `.fm-ribbon-mobile-menu-item` sind im Dark-Theme bereits explizit hell gesetzt.

## Detaildokumente

- [UI-Struktur](inventory/ui-struktur.md)
- [CSS- und Theme-Analyse](inventory/css-theme-analyse.md)
- [Test- und Pruefansatz](inventory/test-und-pruefansatz.md)

## Relevante Dateien

| Datei | Bedeutung |
|-------|-----------|
| `FinanceManager.Web/Components/Shared/Ribbon.razor` | Rendert Desktop- und Mobile-Ribbon, inklusive mobiler Gruppenkopfzeile, Shortcuts und Menueeintraege. |
| `FinanceManager.Web/wwwroot/css/ribbon.css` | Allgemeines Ribbon-Layout; enthaelt den mobilen Breakpoint `@media (max-width: 900px)`. |
| `FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css` | Dark-Theme-Overrides fuer Ribbon; enthaelt mobile Dark-Mode-Regeln ab dem gleichen Breakpoint. |
| `FinanceManager.Web/Components/App.razor` | Bindet `ribbon.css` vor `theme.Dark.Ribbon.css` ein; die Dark-Styles koennen Basiseigenschaften ueberschreiben. |
| `FinanceManager.Web/Components/Layout/MainLayout.razor` | Umgibt den App-Inhalt mit `.app-shell dark-mode`; bestaetigt Dark-Mode-Kontext im Layout. |
| `FinanceManager.Tests/Components/RibbonTests.cs` | Vorhandene bUnit-Tests fuer mobiles Ribbon-Verhalten, jedoch ohne CSS-/Kontrastpruefung. |

## Ist-Zustand

- Mobile Darstellung wird ueber `@media (max-width: 900px)` in `ribbon.css` aktiviert.
- Desktop-Gruppen werden mobil ausgeblendet; stattdessen wird pro Ribbon-Gruppe ein `.fm-ribbon-mobile-group-panel` gerendert.
- Die mobile Headerzeile enthaelt:
  - `.fm-ribbon-mobile-group-title-toggle` mit `.fm-ribbon-mobile-group-title`
  - optional `.fm-ribbon-mobile-shortcut`-Buttons
  - `.fm-ribbon-mobile-group-toggle` mit Hamburger-SVG, dessen Pfad `fill="currentColor"` nutzt
- `theme.Dark.Ribbon.css` setzt im mobilen Bereich:
  - helle Farbe fuer `.fm-ribbon-mobile-group-header`
  - helle Farbe fuer `.fm-ribbon-mobile-menu-item`
  - keine explizite Farbe fuer die mobilen Header-Buttons und Shortcuts

## Wahrscheinliche Ursache

Die Dark-Mode-Farbe wird auf einem Container gesetzt, aber interaktive Button-Elemente im mobilen Header verlassen sich auf Vererbung. Button-Controls sind fuer `color` nicht in allen Browser-/CSS-Kombinationen verlaesslich durch den Container abgedeckt, insbesondere wenn andere Button-Regeln oder User-Agent-Styles greifen. Da das Hamburger-Icon `currentColor` verwendet, betrifft eine falsche Button-Farbe auch das Icon.

## Begrenzung der Aenderungsflaeche

Die passende Aenderungsstelle ist `FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css` innerhalb des vorhandenen mobilen Breakpoints. Eine strukturelle Aenderung an `Ribbon.razor` ist fuer die Anforderung voraussichtlich nicht erforderlich.

Die Aenderung sollte nur Dark-Mode-Styles des mobilen Ribbon-Headers und der mobilen Menueelemente betreffen. Light Mode und Desktop-Ribbon sollten unveraendert bleiben.

## Risiken

- Eine zu breite Regel auf `.fm-ribbon button` koennte Desktop-Buttons oder Light-Mode-Styling beeinflussen.
- Eine Regel ohne mobilen Breakpoint koennte Desktop-Dark-Mode visuell veraendern.
- Eine Regel nur fuer `.fm-ribbon-mobile-menu-item` wuerde vermutlich nicht alle sichtbaren mobilen Texte erfassen, da Gruppentitel und Shortcut-/Toggle-Icons im Header eigene Button-Elemente sind.

## Empfohlene naechste Schritte

1. In `theme.Dark.Ribbon.css` innerhalb `@media (max-width: 900px)` explizite helle `color`-Regeln fuer `.fm-ribbon-mobile-group-title-toggle`, `.fm-ribbon-mobile-group-toggle` und `.fm-ribbon-mobile-shortcut` setzen.
2. Bei Hover/Focus bei Bedarf ebenfalls helle Farbe sicherstellen.
3. Einen fokussierten Test oder eine visuelle Pruefung fuer mobile Dark-Mode-Farben ergaenzen, soweit im Projekt praktikabel.
