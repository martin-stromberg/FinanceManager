← [Zurück zur Übersicht](index.md)

# Berichtswesen — Beschreibung

## Zweck

Das Berichtswesen liefert aggregierte Finanzdaten für Dashboards und wiederverwendbare Favoritenkonfigurationen.

## Funktionsweise

Die Seite `/reports` zeigt gespeicherte Favoriten und öffnet Dashboards über `/reports/dashboard`. Technisch werden die Daten über `ReportsController` und `HomeKpisController` geliefert. Filter, Intervalle, Vergleichsoptionen und Entitätsbezüge werden in den Favoriten persistiert.

Im Report-Dashboard können Anwender Vergleichsspalten wie Vorperiode, Vorjahr und bei Dividendenanalysen die Hochrechnung aktivieren. Die Option `Hochrechnung` steht nur für reine Wertpapierberichte zur Verfügung. Sie ergänzt die Ergebnistabelle direkt nach `Betrag` um eine Spalte mit dem erwarteten Dividendenbetrag des Betrachtungszeitraums.

Die Hochrechnung verwendet bereits gebuchte Netto-Dividenden des aktuellen Zeitraums und ergänzt erwartete Netto-Dividenden aus der korrespondierenden Vorjahresperiode, sofern diese im aktuellen Zeitraum noch nicht durch eine Dividende desselben Wertpapiers bestätigt wurden. Wird ein Report als Favorit gespeichert oder aktualisiert, wird die Hochrechnungsoption zusammen mit den übrigen Dashboard-Einstellungen gespeichert und beim erneuten Öffnen wieder angewendet.

## Beispiele

- Ein Anwender speichert einen Monatsreport als Favorit.
- Ein Dashboard wird mit Vorjahresvergleich geöffnet.
- Ein Wertpapier-Dividendenreport zeigt neben dem gebuchten Betrag eine Hochrechnung für noch erwartete Dividenden.
- Home-KPIs werden auf der Startseite angezeigt.

## Einschränkungen

- Ergebnisse hängen von vorhandenen Aggregaten und Buchungsdaten ab.
- Favoriten sind benutzerbezogen.
- Die Hochrechnung ist nur für reine Wertpapierauswertungen und nicht für das Intervall `AllHistory` verfügbar.
