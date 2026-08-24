# UI, Styling und Navigation

## Hub

`HelpHub.razor` rendert eine Ueberschrift, Einleitung, Suchfeld, Suchergebnisbereich und Themenkarten. Karten sind Bootstrap-Grid-Elemente mit `feature-card`; die Navigation funktioniert per normalem Link.

`help-search.js` laedt `/api/help/search-index/{language}.json`, filtert Suchtreffer und rendert die Karten erneut per DOM. Die Datei enthaelt zusaetzlich eine noch nicht sichtbare Autocomplete-Logik und umfangreiche Konsolenprotokolle. Dadurch existieren zwei Renderpfade fuer die Themenliste: serverseitig in Blazor und clientseitig im Skript.

## Detailseite

`HelpPageView.razor` zeigt einen Zurueck-Link und den gerenderten Markdown-Inhalt. Lade-, Fehler- und Erfolgszustand sind vorhanden. Der aktuelle Fehlerzustand gibt den angeforderten `HelpPath` in sichtbarer Form aus, was fuer eine Endanwenderhilfe technisch wirkt.

## Styling

`wwwroot/help/css/help-page.css` definiert globale Elementselektoren sowie Help-spezifische Klassen. Es gibt Bootstrap-nahe Karten, responsive Tabellenregeln und Dark-Mode-Regeln. Die Detailueberschriften verwenden eine violette Farbpalette, waehrend der Hub primaer Bootstrap-Blau verwendet; dadurch wirkt die Help-Seite nicht vollstaendig in das bestehende Produktdesign integriert.

Die CSS-Datei hat zwar responsive Regeln fuer schmale Viewports, aber keine eigene Struktur fuer Inhaltsnavigation, Breadcrumbs, Inhaltsverzeichnis oder klar getrennte Themen-/Detailbereiche.

## Konsequenz fuer die Anforderung

Die Planung sollte einen konsistenten Navigationsrahmen mit Themenuebersicht, Detailzustand und Rueckweg definieren. Die UI sollte nur freigegebene Anwenderdokumente beschriften und verlinken. CSS-Aenderungen muessen Desktop und schmale Viewports abdecken, ohne die bestehende Help-Funktionalitaet fuer Suche und Navigation zu verlieren.
