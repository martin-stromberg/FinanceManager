# Anforderung – Berichte erweitern

## Ziel
Das konfigurierbare Berichts-Dashboard soll bei Hochrechnungen, Drill-down-Informationen, Diagrammdarstellung und Bankposten-Filtern erweitert und bestehende Fehler beim Ausblenden einzelner Erwartungsposten behoben werden.

## Fachliche Anforderungen
1. Die Tabelle ausgeblendeter Erwartungen im Hochrechnungs-Overlay zeigt zusätzlich den Betrag.
2. Nach dem Wiedereinbeziehen einer ausgeblendeten Erwartung erscheint der Posten unmittelbar wieder in der Liste der Hochrechnungsposten, ohne dass das Overlay geschlossen und neu geöffnet werden muss.
3. Die Tabelle ausgeblendeter Erwartungen ist auf den gerade geöffneten Berichtskontext begrenzt. Beim Öffnen der Hochrechnungsposten eines Kontos dürfen keine ausgeblendeten Erwartungen anderer Konten angezeigt werden.
4. Das Ausblenden von Hochrechnungsposten wirkt postenbezogen. Mehrere Buchungen derselben Entität am selben Datum müssen getrennt ausblend- und wieder einbeziehbar sein.
5. In der Liste realer Posten einer Berichtszelle wird die Beschreibung aus Posten-Beschreibung und Verwendungszweck zusammengesetzt.
6. In den Berichtseinstellungen ist ein Diagrammtyp wählbar:
   - Balkendiagramm (historische Summe): bisherige Periodendarstellung.
   - Balkendiagramm (Aufteilung): die jeweiligen Entitäten stehen nebeneinander mit ihrem Betrag.
   - Kreisdiagramm: die jeweiligen Entitäten werden als Segmente gezeigt; mehr als neun Einzelwerte werden zu einer Sonstiges-Gruppe zusammengefasst, sodass maximal zehn Farben entstehen.
7. In den Berichtseinstellungen ist die Diagrammposition wählbar: oberhalb oder unterhalb der Tabelle.
8. Bei Bankkonten kann zusätzlich zur Konto-Auswahl nach Postenart gefiltert werden: Eingang, Ausgang, Zinsen, Steuer. Das Verhalten entspricht der vorhandenen zweiten Filterliste für Wertpapierpostenarten.

## Betroffene Bereiche
- `FinanceManager.Web/Components/Pages/ReportDashboard.razor`
- `FinanceManager.Web/ViewModels/Reports/ReportDashboardViewModel.cs`
- Report-DTOs und API-Client (`FinanceManager.Shared`)
- `ReportsController`, Report-Services und EF-Core-Modell (`FinanceManager.Infrastructure`, `FinanceManager.Domain`)
- Report-Ressourcen Deutsch/Englisch und Dashboard-CSS
- Report-, ViewModel-, Service-, Backup- und E2E-Tests

## Akzeptanzkriterien
- Eine ausgeblendete Erwartung zeigt Betrag und bleibt dem zugehörigen Entitätskontext zugeordnet.
- Unmittelbar nach „Einbeziehen“ aktualisieren sich ausgeblendete Tabelle, sichtbare Erwartungsliste und Summen.
- Zwei gleich datierte Posten derselben Entität können getrennt ausgeblendet werden; das jeweils andere Exemplar bleibt sichtbar.
- Reale Posten zeigen Beschreibung plus Verwendungszweck.
- Die drei Diagrammtypen und beide Diagrammpositionen sind in der Oberfläche wählbar, werden in Favoriten gespeichert und beim Laden wiederhergestellt.
- Das Kreisdiagramm fasst kleinste Werte zu „Sonstiges“ zusammen und verwendet höchstens zehn Farben.
- Die Bankpostenart-Auswahl filtert Aggregation und Posten-Drilldown und wird in Favoriten persistiert.
- Neue persistierte Felder sind in Backup/Restore berücksichtigt; alte Backups bleiben lesbar.

## Explizite Nicht-Ziele
- Keine Änderung an Budget- oder Portfolio-Berichten außerhalb des gemeinsamen Report-Dashboards.
- Keine Änderung der fachlichen Berechnungslogik der Hochrechnung jenseits der postenbezogenen Unterdrückung und Anzeige.
- Kein neues Diagramm-Framework; die Darstellung nutzt vorhandene HTML/CSS-Mittel.

## Offene Fragen
Keine.
