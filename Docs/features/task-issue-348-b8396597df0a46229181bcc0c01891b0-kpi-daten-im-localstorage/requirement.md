# Anforderung: KPI-Daten im LocalStorage

## Aufgaben-ID
b8396597-df0a-4622-9181-bcc0c01891b0

## Branch
task/issue-348-b8396597df0a46229181bcc0c01891b0-kpi-daten-im-localstorage

## Ursprüngliche Kundenanforderung

Beim Aufruf der Startseite werden aktuell zunächst leere KPI-Kacheln angezeigt, bis die Statistiken vom Server geladen sind.

### Muss-Kriterien

1. **Profileinstellung**: Der Anwender kann in seinem Profil die Option "KPI-Daten der Startseite im LocalStorage zwischenspeichern" aktivieren/deaktivieren.
2. **Caching bei Aktivierung**: Ist die Einstellung aktiv, werden nach dem erfolgreichen Laden der Startseiten-KPI-Daten diese im Browser-LocalStorage gespeichert.
3. **Sofortige Anzeige**: Beim nächsten Aufruf der Startseite werden die gespeicherten KPI-Daten sofort in den Kacheln angezeigt, während im Hintergrund eine Aktualisierung über das Backend erfolgt.
4. **Löschen bei Deaktivierung**: Deaktiviert der Anwender die Funktion, so werden direkt alle bisherigen KPI-Daten aus dem LocalStorage entfernt.

### Abgrenzung

- Es wird ein clientseitiger Cache im Browser-LocalStorage umgesetzt, kein serverseitiger Cache.
- Die Funktion gilt für die Startseite und ihre sichtbaren KPI-Kacheln (KPI-Liste, Monatsbudget, einfache Zahlen-KPIs, ggf. Aggregate). Berichts-Favoriten-Kacheln sind zunächst kein Fokus, falls der zeitliche Rahmen es nicht zulässt.
