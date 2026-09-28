← [Zurück zur Übersicht](index.md)

# Benutzeroberfläche — Ablauf für Anwender

## Voraussetzungen

- Der Anwender ist angemeldet und hat Zugriff auf die gewünschten Bereiche.
- Für die Schritte zur mobilen Ansicht wird die Anwendung auf einem kleinen Display bzw. schmalen Browserfenster geöffnet.

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

### 5. Verhalten bei Anmeldung und aktiver Nutzung

Solange Sie aktiv in der Anwendung navigieren, klicken, tippen oder Eingaben bearbeiten, hält die Anwendung die Anmeldung im Hintergrund aufrecht. Dafür wird bei Bedarf ein stiller Serverkontakt ausgeführt; Sie sehen dabei keine zusätzliche Meldung und die aktuelle Seite wird nicht neu geladen.

Wenn die Anmeldung während einer längeren Inaktivität abläuft und anschließend geschützte Inhalte geladen werden, erkennt die Anwendung den fehlenden Anmeldestatus. Die Anwendung öffnet dann automatisch die Login-Seite, anstatt eine geschützte Seite dauerhaft leer oder veraltet anzuzeigen.

Nach erfolgreicher erneuter Anmeldung kehrt die Anwendung zu der ursprünglich angeforderten Seite zurück. Dabei bleiben auch die ausgewählte Ansicht sowie vorhandene Filter oder andere Angaben in der Adresse erhalten. Wird die Login-Seite direkt geöffnet, führt die Anmeldung weiterhin zur Startseite.

> **Hinweis:** Bei längerer Inaktivität oder einer serverseitig ungültig gewordenen Anmeldung bleibt eine erneute Eingabe der Zugangsdaten erforderlich.

### 6. Kritische Aktionen bestätigen

Bei Aktionen wie dem Löschen eines Eintrags — zum Beispiel eines Anhangs über die Löschen-Schaltfläche in der geöffneten Anhangsliste — erscheint ein Bestätigungsdialog wie „Löschen bestätigen". Der Dialog liegt im Vordergrund, auch wenn bereits ein Bereich geöffnet ist, und bleibt dadurch sichtbar und bedienbar.

- Mit „Bestätigen" wird die Aktion ausgeführt.
- Mit „Abbrechen", dem Schließen-Symbol oder einem Klick auf die abgedunkelte Fläche neben dem Dialog wird sie verworfen. Der geöffnete Bereich dahinter bleibt erhalten und wird nicht geschlossen.

> **Hinweis:** Erscheint keine Nachfrage, ist die Einstellung „Bestätigungsdialoge anzeigen" unter „Einrichtung" → „Profil" deaktiviert — die Aktion wird dann sofort ausgeführt.

## Ergebnis

Kernabläufe (Anmeldung, Navigation, Rückkehr nach abgelaufener Sitzung, Favoriten/Reporting, Import) bleiben auch im mobilen Viewport nutzbar und wurden zusätzlich per E2E geprüft.

## Barrierefreiheit

- Der mobile Menü-Trigger ist über `aria-label="Menu"` gekennzeichnet.
- Diagramme und Tabellen verwenden in mehreren Bereichen zusätzliche `aria-label`-Attribute.

