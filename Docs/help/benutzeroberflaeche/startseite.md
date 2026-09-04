← [Zurück zur Übersicht](index.md)

# Startseite und KPI-Kacheln

## Übersicht

Die Startseite zeigt im oberen Bereich konfigurierbare KPI-Kacheln an. Jede Kachel stellt eine ausgewählte Kennzahl dar, zum Beispiel:

- Monatliches Budget (Einnahmen, Ausgaben, Soll-/Ist-Ergebnis)
- Aktuelle Anzahl von Kontakten, Sparplänen, Wertpapieren oder offenen Kontoauszugsentwürfen
- Konfigurierte Favoritenberichte

## KPI-Daten im Browser-LocalStorage zwischenspeichern

In den Profileinstellungen (`Setup → Profil`) kann die Option *Startseiten-KPIs zwischenspeichern* aktiviert werden.

### Wirkung

- Wenn die Option **aktiv** ist:
  - Werden abgerufene KPI-Daten der Startseite nach dem erfolgreichen Laden im `localStorage` des Browsers gespeichert.
  - Das betrifft vorgegebene Kacheln (Budget, Anzahlen) ebenso wie auf Berichts-Favoriten basierende Kacheln.
  - Beim nächsten Aufruf der Startseite werden die gespeicherten Werte sofort in den Kacheln angezeigt.
  - Im Hintergrund werden die Daten trotzdem neu vom Server abgerufen und die Anzeige nach erfolgreicher Aktualisierung ersetzt.
  - Die zwischengespeicherten Daten sind pro Benutzer und Anwendung durch das Präfix `fm.kpi.*` voneinander getrennt.
- Wenn die Option **deaktiviert** wird:
  - Werden alle bereits gespeicherten KPI-Einträge für den aktuellen Benutzer sofort aus dem `localStorage` entfernt.
  - Bei künftigen Seitenaufrufen findet kein Lesen oder Schreiben des Caches statt.

### Datenschutz-Hinweis

Die Zwischenspeicherung erfolgt ausschließlich clientseitig im Browser des Anwenders. Die Daten werden nicht an den Server übertragen oder dort gespeichert. Sie bleiben im `localStorage`, bis die Funktion in den Profileinstellungen deaktiviert oder der Browser-Speicher manuell geleert wird. Weitere Details finden sich auf der Seite `/legal`.

### Fehlerverhalten

- Fehler beim Lesen oder Schreiben des `localStorage` (z. B. durch gesperrte Browser-Funktionen) verhindern nicht den normalen API-Abruf der KPI-Daten.
- Ist ein gespeicherter Eintrag beschädigt oder unlesbar, wird er ignoriert und die Kachel lädt die Werte wie gewohnt vom Server.
