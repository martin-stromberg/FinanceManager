← [Zurück zur Übersicht](index.md)

# Mobile Ansicht (Responsive Web-UI) — Ablauf für Anwender

## Voraussetzungen

- Die Anwendung wird auf einem kleinen Display bzw. schmalen Browserfenster geöffnet.
- Der Anwender ist angemeldet und hat Zugriff auf die gewünschten Bereiche.

## Schritt-für-Schritt-Anleitung

### 1. Navigation öffnen

Auf kleinen Displays wird oben eine mobile Leiste angezeigt.  
Über das Menü-Symbol (`aria-label="Menu"`) kann die Navigation ein- und ausgeblendet werden.

> **Hinweis:** Beim Öffnen der Navigation erscheint eine Overlay-Fläche, über die das Menü wieder geschlossen werden kann.

### 2. Seite auswählen und Inhalte bedienen

Nach dem Seitenwechsel stehen Listen, Karten und Berichte in mobiler Darstellung bereit.  
Tabellen sind so eingebettet, dass bei Bedarf nur der Tabellenbereich horizontal scrollt.

> **Hinweis:** Auf sehr schmalen Displays können breite Tabellen weiterhin horizontales Scrollen innerhalb des Tabellencontainers erfordern.

### 3. Aktionen ausführen

Aktionsleisten (Ribbon), Dialoge und Formulare bleiben verfügbar und werden auf kleinen Breiten umgebrochen bzw. gestapelt.  
Das gilt u. a. für Home, Berichte, Setup und Wertpapier-Performance.

## Ergebnis

Kernabläufe (Anmeldung, Navigation, Favoriten/Reporting, Import) bleiben auch im mobilen Viewport nutzbar und wurden zusätzlich per E2E geprüft.

## Barrierefreiheit

- Der mobile Menü-Trigger ist über `aria-label="Menu"` gekennzeichnet.
- Diagramme und Tabellen verwenden in mehreren Bereichen zusätzliche `aria-label`-Attribute.

