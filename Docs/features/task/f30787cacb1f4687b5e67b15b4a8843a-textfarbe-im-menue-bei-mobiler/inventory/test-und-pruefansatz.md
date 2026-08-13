# Test- und Pruefansatz

## Vorhandene Tests

`FinanceManager.Tests/Components/RibbonTests.cs` deckt die Struktur und Interaktion des mobilen Ribbon bereits ab:

- Zeile 92: `MobileGroupPanel_RendersGroupTitleAndHamburgerButton`
- Zeile 125: `MobileGroupMenu_TogglesOnHamburgerClick`
- Zeile 162: `MobileGroupMenu_ItemsRenderIconAndName`
- Zeile 202: `MobileShortcut_ExplicitAction_RendersIconOnlyInClosedHeader`
- weitere Tests fuer Shortcut-Rendering, Disabled-Status und Header-Reihenfolge

Diese Tests pruefen gerendertes Markup und Klickverhalten, aber keine berechneten CSS-Farben.

## Geeignete automatisierte Pruefung

Eine reine bUnit-Pruefung kann die konkrete Browser-Farbvererbung nicht verlaesslich validieren, weil CSS-Dateien und Computed Styles dort nicht wie im Browser ausgewertet werden. Sinnvoll sind daher:

- Beibehaltung der bestehenden bUnit-Tests als Regressionsschutz fuer Struktur und Verhalten.
- Optionaler CSS-Texttest, der sicherstellt, dass `theme.Dark.Ribbon.css` explizite mobile Regeln fuer die Header-Buttons enthaelt.
- Wenn im Projekt Playwright oder ein vergleichbarer Browser-Test vorhanden ist, ein visueller oder computed-style-basierter Test fuer einen Viewport <= 900px.

## Manuelle Pruefung

Empfohlene manuelle Checks nach Umsetzung:

1. Anwendung starten.
2. Mobile Viewport-Breite unter 900px einstellen.
3. Dark Mode aktiv lassen, da die App aktuell Dark-Styles global einbindet und `body.dark-mode` nutzt.
4. Seite mit Ribbon oeffnen.
5. Pruefen:
   - Gruppentitel im mobilen Ribbon-Header ist hell lesbar.
   - Hamburger-Icon ist hell sichtbar.
   - Shortcut-Icons sind hell sichtbar.
   - Aufgeklappte Menueeintraege und Labels sind hell lesbar.
   - Desktop-Viewport zeigt keine unbeabsichtigte Aenderung.

## Empfohlene Testausfuehrung

Nach der Implementierung sollte mindestens ausgefuehrt werden:

```powershell
dotnet test FinanceManager.Tests
```

Falls ein Browser-Test ergaenzt wird, sollte dieser zusaetzlich fuer einen mobilen Viewport ausgefuehrt werden.
