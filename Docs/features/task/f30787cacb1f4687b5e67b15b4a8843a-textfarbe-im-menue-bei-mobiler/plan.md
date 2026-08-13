# Umsetzungsplan - Textfarbe im mobilen Ribbon-Menue

## Ziel

Im mobilen Ribbon-Menue sollen bei aktivem Dark Mode alle sichtbaren Header- und Menue-Texte sowie `currentColor`-basierte Icons hell und kontrastreich dargestellt werden. Light Mode und Desktop-Ribbon bleiben unveraendert.

## Ausgangspunkt

Die relevante Aenderungsstelle ist `FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css` im vorhandenen Block `@media (max-width: 900px)`.

Die mobilen Menueeintraege `.fm-ribbon-mobile-menu-item` sind dort bereits hell gesetzt. Nicht explizit abgedeckt sind die Button-Elemente im mobilen Gruppenkopf:

- `.fm-ribbon-mobile-group-title-toggle`
- `.fm-ribbon-mobile-group-toggle`
- `.fm-ribbon-mobile-shortcut`

Da diese Elemente Buttons sind und Icons teilweise `currentColor` verwenden, soll die Textfarbe direkt auf diesen Buttons gesetzt werden.

## Umsetzungsschritte

1. `FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css` im mobilen Breakpoint erweitern.
2. Eine gemeinsame Regel fuer die mobilen Header-Buttons ergaenzen:
   - `.fm-ribbon-mobile-group-title-toggle`
   - `.fm-ribbon-mobile-group-toggle`
   - `.fm-ribbon-mobile-shortcut`
3. In dieser Regel eine helle Farbe setzen, bevorzugt konsistent zur bestehenden Header-Farbe:
   - `color: #f0f0f0;`
4. Hover- und Focus-Zustaende der gleichen Button-Gruppe absichern, damit die Farbe dort nicht auf Browser- oder Basis-Styles zurueckfaellt:
   - `:hover:not(:disabled)`
   - `:focus-visible`
   - Farbe: `#fff`
5. Fokusdarstellung nur bei Bedarf angleichen:
   - Wenn noch keine sichtbare Fokusmarkierung fuer die betroffenen Buttons greift, `outline: 2px solid var(--accent);` und `outline-offset: 2px;` fuer `:focus-visible` setzen.
6. Keine Aenderungen an `FinanceManager.Web/Components/Shared/Ribbon.razor` vornehmen, sofern beim Umsetzen keine abweichende Ursache sichtbar wird.

## Vorgeschlagene CSS-Struktur

Die neue Regel soll innerhalb des bestehenden mobilen Dark-Mode-Blocks platziert werden, direkt nach `.fm-ribbon-mobile-group-header`, damit der Zusammenhang klar bleibt:

```css
@media (max-width: 900px) {
    .fm-ribbon-mobile-group-header {
        color: #f0f0f0;
    }

    .fm-ribbon-mobile-group-title-toggle,
    .fm-ribbon-mobile-group-toggle,
    .fm-ribbon-mobile-shortcut {
        color: #f0f0f0;
    }

    .fm-ribbon-mobile-group-title-toggle:hover:not(:disabled),
    .fm-ribbon-mobile-group-toggle:hover:not(:disabled),
    .fm-ribbon-mobile-shortcut:hover:not(:disabled),
    .fm-ribbon-mobile-group-title-toggle:focus-visible,
    .fm-ribbon-mobile-group-toggle:focus-visible,
    .fm-ribbon-mobile-shortcut:focus-visible {
        color: #fff;
    }
}
```

Falls beim Implementieren eine fehlende Fokusmarkierung auffaellt, wird der `:focus-visible`-Block um eine Outline erweitert.

## Tests und Pruefung

Automatisiert:

1. `dotnet test FinanceManager.Tests`
2. Falls kein bestehender Test CSS-Dateien sinnvoll prueft, keinen breiten Browser-Test einfuehren. Eine reine bUnit-Pruefung reicht fuer berechnete CSS-Farben nicht aus.

Manuell oder browserbasiert:

1. Anwendung starten.
2. Viewport auf maximal 900 px Breite setzen.
3. Dark Mode verwenden.
4. Ribbon pruefen:
   - Gruppentitel im mobilen Header ist hell lesbar.
   - Hamburger-Icon ist hell sichtbar.
   - Shortcut-Icons sind hell sichtbar.
   - Aufgeklappte Menueeintraege bleiben hell lesbar.
5. Desktop-Viewport pruefen:
   - Keine sichtbare Veraenderung der Desktop-Ribbon-Farben.
6. Light Mode pruefen, falls in der Umgebung umschaltbar:
   - Keine Verschlechterung der mobilen Menuefarben.

## Risiken und Gegenmassnahmen

| Risiko | Gegenmassnahme |
|--------|----------------|
| Desktop-Dark-Mode wird unbeabsichtigt veraendert | Regeln ausschliesslich innerhalb `@media (max-width: 900px)` ergaenzen. |
| Light Mode wird beeinflusst | Nur `theme.Dark.Ribbon.css` bearbeiten; keine Basisregeln in `ribbon.css` aendern. |
| Nicht alle mobilen Header-Inhalte werden hell | Alle Button-Selektoren des mobilen Headers gemeinsam abdecken. |
| Hover oder Fokus faellt wieder auf dunkle Farbe zurueck | Hover- und Focus-Zustaende explizit fuer dieselben Selektoren setzen. |

## Akzeptanzpruefung

Die Anforderung gilt als umgesetzt, wenn:

- mobile Ribbon-Header-Texte im Dark Mode nicht schwarz dargestellt werden,
- mobile Header-Icons, die `currentColor` verwenden, hell sichtbar sind,
- mobile Menueeintraege weiterhin hell und lesbar bleiben,
- Light Mode und Desktop-Ansicht durch die Aenderung nicht sichtbar verschlechtert werden,
- die automatisierten Tests mindestens `FinanceManager.Tests` erfolgreich durchlaufen oder ein nachvollziehbarer Grund dokumentiert ist, falls sie nicht ausgefuehrt werden konnten.

## Offene Punkte

Keine.
