# Projektplan – Berichte erweitern

## Übersicht
Erweiterung des Report-Dashboards um korrekte postenbezogene Hochrechnungs-Unterdrückung, vollständigere Overlay-Daten, konfigurierbare Diagramme und einen Bankpostenart-Filter.

## Grobe Vorgehensentscheidungen
- Unterdrückungen erhalten eine postenbezogene Referenz (`PostingId`) inklusive Betragsanzeige; bestehende Unterdrückungen ohne Postenbezug bleiben kompatibel und werden weiterhin über den bisherigen Datums-/Entitätsschlüssel angewendet.
- Das Hochrechnungs-Overlay erhält einen Kontextscope, sodass nur zur geöffneten Entität bzw. Gruppe passende ausgeblendete Erwartungen gelistet werden; nach dem Einbeziehen wird die Detaildarstellung mit den aktualisierten Berichtsdaten neu aufgebaut.
- Diagrammtyp und Diagrammposition werden als Enums in Shared-DTOs, Favoriten, API, UI und Backup ergänzt. Die Charts bleiben HTML/CSS-basiert.
- Der Bankpostenart-Filter folgt dem vorhandenen `SecuritySubTypes`-Muster über DTO, ViewModel, Filterdialog, Favoriten-Persistenz, Aggregations- und Postenabfrage.
- Die Arbeit wird in drei lieferbaren Schritten umgesetzt; jeder Schritt enthält Umsetzung, gezielte Tests und Review.

## Entwicklungsschritte

### Schritt 1: Hochrechnungs- und Posten-Overlays korrigieren
Unterdrückbare Hochrechnungsposten werden eindeutig anhand der Quellposting-ID verwaltet. Die ausgeblendete Tabelle zeigt Betrag, ist auf den geöffneten Berichtskontext gefiltert und aktualisiert sichtbare Erwartungen unmittelbar nach dem Wiedereinbeziehen. Reale Posten zeigen Beschreibung plus Verwendungszweck.

Akzeptanzkriterien:
- Zwei gleich datierte Posten derselben Entität können getrennt ausgeblendet und einbezogen werden.
- Die ausgeblendete Liste zeigt nur zum Overlay-Kontext passende Einträge inklusive Betrag.
- Nach dem Einbeziehen erscheint der Posten direkt wieder in der sichtbaren Erwartungsliste; Summen und Gesamtbetrag stimmen.
- Der Posten-Overlay-Text kombiniert `Subject`/`Description` und `RecipientName` sinnvoll statt nur eines Feldes.
- Betroffene Service-, Aggregations- und ViewModel-Tests decken Postenbezug, Kontextfilterung und Betragsanzeige ab.

Abhängigkeiten: Keine.
Betroffene Bereiche: Report-DTOs, Suppression-Domain/-Service, ProjectionCalculator, ReportsController, ReportDashboard, Tests, EF-Migration/Backup.

### Schritt 2: Diagrammtyp und -position konfigurieren
Die Berichtseinstellungen erhalten Auswahlen für Diagrammtyp (historische Summe, Aufteilung, Kreisdiagramm) und Position (oberhalb/unterhalb der Tabelle). Beide Einstellungen werden in Favoriten gespeichert und beim Laden angewendet. Das Kreisdiagramm gruppiert kleinste Werte zu „Sonstiges“ bis maximal zehn Farben.

Akzeptanzkriterien:
- Alle drei Diagrammtypen rendern die jeweiligen Entitäten korrekt; der bisherige Typ bleibt Standard.
- Die Position oberhalb/unterhalb wird unmittelbar und aus Favoriten korrekt dargestellt.
- Kreisdiagramm mit mehr als zehn Rohwerten zeigt höchstens zehn Segmente inklusive „Sonstiges“.
- Persistenz über Entity, DTOs, Controller, Backup/Restore und Migration ist vollständig.
- Lokalisierungen für Deutsch und Englisch sind gepflegt; `EnumCoverageTests` ist aktualisiert.

Abhängigkeiten: Keine.
Betroffene Bereiche: Shared-DTOs/Enums, Domain `ReportFavorite`, EF-Migration, Backup, Controller/Service, ViewModel, Razor, CSS, Resx, Tests.

### Schritt 3: Bankpostenart-Filter ergänzen
Bei Bankkonten steht im Filterdialog neben der Kontoliste eine zweite Liste der Postenarten Eingang, Ausgang, Zinsen und Steuer zur Verfügung. Die Auswahl wirkt auf Aggregation, Posten-Drilldown und Favoriten analog zum Sicherheitstyp-Filter.

Akzeptanzkriterien:
- Der Filterdialog zeigt die Bankpostenarten nur für Bank-Kontext und behandelt sie unabhängig von der Kontoauswahl.
- Aggregation und Drilldown liefern nur Bankposten der ausgewählten Typen.
- Favoriten speichern und laden die Auswahl; Backup/Restore alter und neuer Favoriten bleibt kompatibel.
- Tests decken Filterpayload, Serverfilterung und Favoriten-Persistenz ab.

Abhängigkeiten: Keine.
Betroffene Bereiche: Filter-DTOs/ViewModel/Razor, ReportFavorite, EF-Migration, Aggregations-/Postenabfrage, Backup, Tests, Resx.

## Übersichtstabelle
| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|----------------|---------------------|
| 1 | Hochrechnungs- und Posten-Overlays korrigieren | Keine | Projection, Suppression, Overlay, Drilldown, Persistenz, Tests |
| 2 | Diagrammtyp und -position konfigurieren | Keine | Favoriten, Chart-UI, DTO/API, Backup, Migration, Resx, Tests |
| 3 | Bankpostenart-Filter ergänzen | Keine | Filtermodell, Aggregation, Drilldown, Favoriten, Migration, Resx, Tests |

## Offene Punkte
Keine.
