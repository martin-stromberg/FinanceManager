# Risiken und offene Punkte

## Technische Risiken

### Buttons in Buttons

Der mobile Header ist aktuell selbst ein `<button>`. Shortcut-Buttons duerfen semantisch nicht darin verschachtelt werden. Die Umsetzung sollte die Header-Struktur deshalb aufteilen oder Shortcuts als Geschwisterelemente rendern.

### Event-Propagation

Wenn Shortcut-Klicks den Header-Toggle erreichen, klappt die Gruppe unbeabsichtigt auf. Jeder Shortcut-Button braucht `@onclick:stopPropagation="true"` und sollte denselben Callback-Pfad wie normale Ribbon-Aktionen verwenden.

### Ueberlauf auf kleinen Viewports

Die Anforderung verlangt, sichtbare Buttons auf den verfuegbaren Platz zu begrenzen. CSS kann Ueberlauf verstecken, aber keine fachlich perfekte Anzahl berechnen. Wenn exakt sichtbare Shortcut-Anzahlen benoetigt werden, waere zusaetzliche Messlogik mit JS-Interop noetig. Ohne diese Anforderung ist ein begrenzter Flex-Container wahrscheinlich ausreichend.

### FileCallback

Dateiaktionen nutzen `InputFile` als Overlay. Das kann in einem Icon-only Shortcut funktionieren, muss aber separat getestet werden. Alternativ koennen Dateiaktionen als Shortcuts ausgeschlossen werden, bis fachlich bestaetigt ist, dass Upload-Shortcuts gewuenscht sind.

### Hidden und dynamische Zustaende

Einige Aktionen wechseln dynamisch zwischen sichtbar und versteckt, z. B. QuickEdit-Aktionen. Die Shortcut-Auswahl muss bei jedem Render aus dem aktuellen Zustand neu abgeleitet werden. Das passt zum bestehenden Verhalten, weil `TabsToRender` bereits immer neu gebaut wird.

## Fachliche offene Punkte

- Welche Aktionen sollen in Mehr-Aktions-Gruppen als Shortcut markiert werden?
- Sollen deaktivierte Aktionen als deaktivierte Shortcuts sichtbar bleiben oder komplett entfallen?
- Sollen Datei-Upload-Aktionen mit `FileCallback` als Shortcut unterstuetzt werden?
- Soll es eine maximale Anzahl sichtbarer Shortcuts pro Gruppe geben, z. B. 2 oder 3?
- Soll die automatische Ein-Aktions-Regel auch fuer deaktivierte Einzelaktionen gelten?

## Empfehlung fuer Planung

Die Planung sollte eine konservative Auswahl festlegen:

- Infrastruktur zentral bauen: `UiRibbonAction.MobileShortcut`, interne `RibbonItem.MobileShortcut`, automatische Ein-Aktions-Regel.
- UI semantisch sauber umbauen: kein Button-in-Button, Icon-only Buttons mit ARIA.
- Mehr-Aktions-Gruppen nur fuer eindeutig primaere Aktionen markieren.
- Upload-Shortcuts erst aktivieren, wenn der Test fuer `FileCallback` sauber ist.
- bUnit-Tests fuer Logik und Rendering verpflichtend, E2E fuer mobile Sichtbarkeit auf repraesentativen Seiten.
