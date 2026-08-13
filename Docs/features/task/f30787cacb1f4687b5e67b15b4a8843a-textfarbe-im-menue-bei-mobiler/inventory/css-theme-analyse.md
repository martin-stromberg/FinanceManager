# CSS- und Theme-Analyse

## Einbindung

`FinanceManager.Web/Components/App.razor` bindet die Styles in folgender Reihenfolge ein:

- `css/app.css`
- `css/ribbon.css`
- danach `css/theme.Dark.css`
- danach `css/theme.Dark.Ribbon.css`

Damit kann `theme.Dark.Ribbon.css` die allgemeinen Ribbon-Regeln gezielt ueberschreiben.

`FinanceManager.Web/Components/Layout/MainLayout.razor` setzt `.app-shell dark-mode`; `App.razor` setzt am `body` ebenfalls `dark-mode`. Die Ribbon-Dark-Datei arbeitet aktuell jedoch ueber unpraefixte `.fm-ribbon...`-Selektoren und ist generell eingebunden.

## Allgemeines Ribbon-CSS

`FinanceManager.Web/wwwroot/css/ribbon.css` enthaelt ab Zeile 161 den mobilen Breakpoint `@media (max-width: 900px)`.

Relevante mobile Basisregeln:

- `.fm-ribbon-group-desktop` wird ausgeblendet.
- `.fm-ribbon-mobile-group-panel` wird angezeigt.
- `.fm-ribbon-mobile-group-header` definiert Layout, aber keine Textfarbe.
- `.fm-ribbon-mobile-group-title-toggle` und `.fm-ribbon-mobile-group-toggle` setzen `border: 0`, `background: transparent`, `font: inherit`, aber keine `color`.
- `.fm-ribbon-mobile-shortcut` setzt `background: transparent`, aber keine `color`.
- `.fm-ribbon-mobile-menu-item` setzt `background: transparent`, aber keine `color`.

## Dark-Ribbon-CSS

`FinanceManager.Web/wwwroot/css/theme.Dark.Ribbon.css` enthaelt ab Zeile 66 mobile Dark-Mode-Regeln.

Vorhandene Dark-Mode-Regeln:

- `.fm-ribbon-mobile-group-panel`: dunkler Hintergrund und Rahmen.
- `.fm-ribbon-mobile-group-header`: `color: #f0f0f0`.
- `.fm-ribbon-mobile-menu-item`: `color: #ddd`, dunkler Hintergrund, dunkler Rahmen.
- `.fm-ribbon-mobile-menu-item:hover:not(:disabled)`: `color: #fff`.
- `.fm-ribbon-mobile-menu-item:disabled`: reduzierte Opazitaet.

Nicht abgedeckt:

- `.fm-ribbon-mobile-group-title-toggle`
- `.fm-ribbon-mobile-group-toggle`
- `.fm-ribbon-mobile-shortcut`
- Hover-/Focus-Farbe fuer mobile Header-Buttons und Shortcuts

## Bewertung

Die aufgeklappten Menueeintraege sind bereits abgesichert. Die Anforderung spricht aber von allen sichtbaren Texten im mobilen Ribbon-Menue. Dazu gehoert auch der Gruppentitel im geschlossenen mobilen Gruppenheader. Da dieser Text innerhalb eines Buttons liegt, sollte die Dark-Farbe auf den Button selbst gesetzt werden.

Eine gezielte Loesung sollte innerhalb des vorhandenen mobilen Dark-Mode-Breakpoints erfolgen, z. B. fuer:

- `.fm-ribbon-mobile-group-title-toggle`
- `.fm-ribbon-mobile-group-toggle`
- `.fm-ribbon-mobile-shortcut`

Optional sollte eine gemeinsame Regel fuer deren `:hover:not(:disabled)` und `:focus-visible` sicherstellen, dass die Farbe nicht wieder auf schwarz zurueckfaellt.
