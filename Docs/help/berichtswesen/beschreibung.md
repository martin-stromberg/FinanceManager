← [Zurück zur Übersicht](index.md)

# Berichtswesen — Beschreibung

## Zweck

Das Berichtswesen liefert aggregierte Finanzdaten für Dashboards und wiederverwendbare Favoritenkonfigurationen.

## Funktionsweise

Die Berichteseite zeigt gespeicherte Favoriten und öffnet daraus Dashboards. Filter, Intervalle, Vergleichsoptionen und Bezüge zu Konten, Kontakten oder Wertpapieren werden in den Favoriten gespeichert und beim erneuten Öffnen wieder angewendet.

Im Report-Dashboard können Anwender Vergleichsspalten wie Vorperiode, Vorjahr und bei Dividendenanalysen die Hochrechnung aktivieren. Die Option `Hochrechnung` steht nur für reine Wertpapierberichte zur Verfügung. Sie ergänzt die Ergebnistabelle direkt nach `Betrag` um eine Spalte mit dem erwarteten Dividendenbetrag des Betrachtungszeitraums.

Die Hochrechnung verwendet bereits gebuchte Netto-Dividenden des aktuellen Zeitraums und ergänzt erwartete Netto-Dividenden aus der korrespondierenden Vorjahresperiode, sofern diese im aktuellen Zeitraum noch nicht durch eine Dividende desselben Wertpapiers bestätigt wurden. Erwartete Dividenden werden nur für Wertpapiere berücksichtigt, die zum erwarteten Dividendentag noch einen positiven Bestand haben. Wird ein Report als Favorit gespeichert oder aktualisiert, wird die Hochrechnungsoption zusammen mit den übrigen Dashboard-Einstellungen gespeichert und beim erneuten Öffnen wieder angewendet.

Zusätzlich lassen sich im Filterdialog je ausgewählter Buchungsart beliebig viele Textfilter hinterlegen. Ein Textfilter ist ein freier Suchbegriff von bis zu 500 Zeichen; eine Buchung wird berücksichtigt, wenn der Begriff im Verwendungszweck oder in der Buchungsbeschreibung vorkommt — die Groß- und Kleinschreibung spielt keine Rolle. Mehrere Textfilter einer Buchungsart sind ODER-verknüpft, es genügt also ein einziger Treffer. Textfilter gelten zusätzlich zu den übrigen Filtern wie Entitäten, Kategorien und Buchungsuntertypen. Beim Speichern als Favorit werden die Textfilter mitabgelegt; KPI-Kacheln der Startseite, die auf einem solchen Favoriten beruhen, zeigen automatisch die gefilterten Werte.

Zu jedem angezeigten Betrag lassen sich die zugrunde liegenden Einzelposten direkt im Bericht einsehen. Jede Betragszelle der Spalten `Betrag`, `Vorperiode` und `Vorjahr` trägt einen Symbolbutton, der ein Overlay mit den Posten öffnet, aus denen sich der Zellwert zusammensetzt — mit Datum, Herkunft, Beschreibung und Betrag je Posten sowie einer Summenzeile. Der Button steht an Entitäts-, Kategorie- und Summenzeilen gleichermaßen zur Verfügung. Die Postenliste folgt der Berichtsauswahl exakt: Vergleichsspalten liefern die Posten der jeweiligen Vergleichsperiode, und alle aktiven Filter (Entitäten, Kategorien, Buchungsuntertypen, Textfilter) wirken identisch auf die aufgelisteten Posten. Die Delta-Spalten, die Hochrechnungsspalte und Zellen ohne Wert bieten keine Posteneinsicht.

## Beispiele

- Ein Anwender speichert einen Monatsreport als Favorit.
- Ein Dashboard wird mit Vorjahresvergleich geöffnet.
- Ein Wertpapier-Dividendenreport zeigt neben dem gebuchten Betrag eine Hochrechnung für noch erwartete Dividenden.
- Ein Bankbericht wird mit den Textfiltern „Zins“ und „Abschluss“ auf Zins- und Abschlussbuchungen eingegrenzt.
- Ein Anwender öffnet per Symbolbutton die Postenliste einer Vorperioden-Zelle und prüft die einzelnen Buchungen der Vergleichsperiode.
- Home-KPIs werden auf der Startseite angezeigt.

## Einschränkungen

- Ergebnisse hängen von vorhandenen Aggregaten und Buchungsdaten ab.
- Favoriten sind benutzerbezogen.
- Die Hochrechnung ist nur für reine Wertpapierauswertungen und nicht für das Intervall `AllHistory` verfügbar.
- Textfilter durchsuchen ausschließlich Verwendungszweck und Buchungsbeschreibung — nicht den Empfängernamen; reguläre Ausdrücke werden nicht unterstützt.
- Die Posteneinsicht ist serverseitig auf 5000 Posten je Zelle begrenzt; bei einer Kürzung weist ein Hinweis im Overlay darauf hin, die angezeigte Summe bleibt dennoch exakt.
- Das Posten-Overlay dient ausschließlich der Einsicht — Bearbeiten, Löschen oder Exportieren der Posten ist nicht möglich.
