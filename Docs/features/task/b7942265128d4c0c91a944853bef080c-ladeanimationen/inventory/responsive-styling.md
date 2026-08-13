# Responsive Darstellung und Styles

## Bestehende Layoutregeln

`wwwroot/css/app.css` definiert:

- `.app-shell` als Flex-Layout mit Sidebar und Content.
- `.mobile-topbar` als sticky Topbar, standardmaessig verborgen.
- Ab `@media (max-width: 900px)` eine fixe, einblendbare Sidebar und sichtbare `.mobile-topbar`.
- `.content` als relativ positionierten Inhaltsbereich.
- Z-Index-Werte von 400 bis 500 fuer Overlay, Sidebar und Topbar; die globale Ladeanzeige muss sich in dieses Stack-Verhalten einfuegen.

## Anforderungen an den neuen Balken

- Genau ein schmales horizontales Element, ohne Spinner oder zusaetzliche Ladeanzeigen.
- Desktop: am oberen Rand des Viewports beziehungsweise der Seite.
- Mobil: am unteren Rand der mobilen Menueleiste, also direkt unterhalb der sticky `.mobile-topbar`.
- Sichtbar in hellem und dunklem Theme; vorhandene Theme-Dateien koennen die Farbdarstellung ergaenzen.
- Bewegung von rechts nach links. Die Farbwahl muss pro Start zufaellig erfolgen.
- Keine Layoutverschiebung und keine Blockierung von Klicks oder Formularen.

## Bestehende JavaScript-Einbindung

`App.razor` bindet `js/financeManager.js` im Dokumentkopf ein. Das globale Skript ist damit vor den Blazor-Skripten vorhanden und kann fruehe native Klick-/Submit-Ereignisse beobachten. Eine konkrete Implementierung muss dennoch sicherstellen, dass interne Blazor-Events und native Browsernavigation nicht doppelt behandelt werden.

