← [Zurück zur Übersicht](index.md)

# Berichtswesen — Beschreibung

## Zweck

Das Berichtswesen liefert aggregierte Finanzdaten für Dashboards und wiederverwendbare Favoritenkonfigurationen.

## Funktionsweise

Die Seite `/reports` zeigt gespeicherte Favoriten und öffnet Dashboards über `/reports/dashboard`. Technisch werden die Daten über `ReportsController` und `HomeKpisController` geliefert. Filter, Intervalle und Entitätsbezüge werden in den Favoriten persistiert.

## Beispiele

- Ein Anwender speichert einen Monatsreport als Favorit.
- Ein Dashboard wird mit Vorjahresvergleich geöffnet.
- Home-KPIs werden auf der Startseite angezeigt.

## Einschränkungen

- Ergebnisse hängen von vorhandenen Aggregaten und Buchungsdaten ab.
- Favoriten sind benutzerbezogen.
