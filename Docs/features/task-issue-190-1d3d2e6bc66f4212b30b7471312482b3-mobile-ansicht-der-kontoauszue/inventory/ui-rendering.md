# UI- und Rendering-Inventar

## Generische Listenkomponente

`FinanceManager.Web/Components/Pages/GenericListPage.razor` rendert Desktop und Mobile parallel:

- Desktop-Tabelle: Zeilen ab ca. Zeile 38, Records ab ca. Zeile 53.
- Mobile Karten: ab ca. Zeile 131.
- Mobile Zellenauswahl: `GetMobileCells` ab ca. Zeile 303.
- Limit: `GetMobileCells` bricht nach vier sichtbaren Zellen ab.

Folge fuer Kontoauszugseintraege: Die Spaltenreihenfolge ist Symbol, Datum, Betrag, Empfaenger, Betreff, Sparplan, Wertpapier, Status. Auf Mobile erscheinen deshalb typischerweise nur Symbol, Datum, Betrag und Empfaenger. Falls kein Empfaenger vorhanden ist, rutscht Betreff nach. Sparplan, Wertpapier und Status erscheinen nur, wenn vorherige Zellen leer genug sind.

## Mobile Styles

`FinanceManager.Web/wwwroot/css/app.css`:

- `.generic-list-mobile-cards` ist standardmaessig ausgeblendet und wird im `@media (max-width: 900px)` sichtbar.
- `.generic-list-mobile-row` ist eine vertikale Label/Wert-Zeile.
- `.generic-list-mobile-value` nutzt `word-break: break-word`.
- `.fm-table tr.muted-row` und `.fm-table tr.muted-row td` sind fuer Tabellenzeilen definiert.

Es fehlt eine Regel fuer `.generic-list-mobile-card.muted-row`. Die Klasse wird zwar in der Razor-Komponente gesetzt, hat ausserhalb der Tabelle aber keine eigene Abschwaechung. Dadurch ist die mobile Unterscheidung gebuchter Eintraege weniger verlaesslich als Desktop.

## Kontoauszugs-Detailstyles

`FinanceManager.Web/wwwroot/css/app.StatementDraftDetail.css`:

- `.draft-entries-container` erlaubt horizontales Scrollen.
- `.fm-table.wide` setzt `min-width: 1100px`.
- `.row-booked` und `.row-announced` definieren `opacity: .55`, werden in der generischen Liste aber nicht verwendet.
- `.entity-display` und `.entity-symbol` sind fuer Kontakt/Sparplan-Anzeigen vorbereitet, spielen in der generischen mobilen Karte aktuell keine Rolle.

## Dateinamen

Die mobile generische Kartenanzeige kann Textwerte umbrechen. Auf der Detailkarte wird der Dateiname aber ueber `StatementDraftCardViewModel` als normales CardField ausgegeben. Ob der Dateiname dort umbrechen kann, haengt von den generischen Card-Styles ab. Fuer die Akzeptanzkriterien sollte gezielt abgesichert werden:

- Dateiwerte duerfen `min-width` nicht erzwingen.
- Lange Dateinamen brauchen `overflow-wrap: anywhere` oder gleichwertig.
- Die Seite darf bei schmalen Viewports nicht horizontal scrollen.

## Layout-Luecke

Die Anforderung "Datum und Betrag zweispaltig anzeigen" laesst sich mit der aktuellen generischen mobilen Kartenstruktur nicht direkt ausdruecken, weil jede Zelle als eigene vertikale Zeile gerendert wird. Fuer Kontoauszugseintraege braucht es eine spezifische mobile Zeile oder eine Erweiterung des Listenrenderers fuer gruppierte mobile Felder.
