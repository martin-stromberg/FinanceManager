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

### 2. Ladezustand bei Navigation, Formularen und Aktionen

Beim Auswählen eines internen Links erscheint sofort eine schmale Ladeleiste. Sie bewegt sich sichtbar über den oberen Seitenrand. Auf mobilen Displays befindet sie sich direkt unterhalb der mobilen Topbar.

Auch beim Absenden eines Formulars oder bei länger laufenden Aktionen innerhalb einer Seite kann die Ladeleiste erscheinen, wenn dadurch Inhalte geladen oder neu berechnet werden. Nach dem Seitenwechsel oder dem Abschluss des Vorgangs wird sie ausgeblendet. Bei mehreren schnellen Klicks bleibt es bei einer einzigen Ladeleiste, die neu gestartet und farblich aktualisiert wird.

### 3. Seite auswählen und Inhalte bedienen

Nach dem Seitenwechsel stehen Listen, Karten und Berichte in mobiler Darstellung bereit.  
Tabellen sind so eingebettet, dass bei Bedarf nur der Tabellenbereich horizontal scrollt.

> **Hinweis:** Auf sehr schmalen Displays können breite Tabellen weiterhin horizontales Scrollen innerhalb des Tabellencontainers erfordern.

### 4. Aktionen ausführen

Aktionsleisten (Ribbon), Dialoge und Formulare bleiben verfügbar und werden auf kleinen Breiten umgebrochen bzw. gestapelt.  
Das gilt u. a. für Home, Berichte, Setup und Wertpapier-Performance.

Bei geschlossenen Ribbon-Gruppen können rechts im Gruppen-Header zusätzliche Symbol-Schaltflächen erscheinen. Diese Shortcuts führen die jeweilige Aktion direkt aus, ohne die Gruppe zu öffnen. Wird die Gruppe aufgeklappt, verschwinden die Header-Shortcuts und alle Aktionen stehen wie gewohnt in der geöffneten Gruppe zur Verfügung.

### 5. Verhalten bei abgelaufener Anmeldung

Wenn die Anmeldung während einer längeren Inaktivität abläuft und anschließend geschützte Inhalte geladen werden, erkennt die Anwendung den fehlenden Anmeldestatus. Die Anwendung öffnet dann automatisch die Login-Seite, anstatt eine geschützte Seite dauerhaft leer oder veraltet anzuzeigen.

Nach erfolgreicher erneuter Anmeldung kehrt die Anwendung zu der ursprünglich angeforderten Seite zurück. Dabei bleiben auch die ausgewählte Ansicht sowie vorhandene Filter oder andere Angaben in der Adresse erhalten. Wird die Login-Seite direkt geöffnet, führt die Anmeldung weiterhin zur Startseite.

> **Hinweis:** Eine Anmeldung wird nicht automatisch verlängert. Bei abgelaufener Sitzung ist eine erneute Eingabe der Zugangsdaten erforderlich.

## Ergebnis

Kernabläufe (Anmeldung, Navigation, Rückkehr nach abgelaufener Sitzung, Favoriten/Reporting, Import) bleiben auch im mobilen Viewport nutzbar und wurden zusätzlich per E2E geprüft.

## Barrierefreiheit

- Der mobile Menü-Trigger ist über `aria-label="Menu"` gekennzeichnet.
- Diagramme und Tabellen verwenden in mehreren Bereichen zusätzliche `aria-label`-Attribute.

