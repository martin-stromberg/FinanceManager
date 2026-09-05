# Bestandsaufnahme: Summen in der Bankübersicht

## Zusammenfassung

Die Bankübersicht ist keine eigene Seitenkomponente, sondern wird über die generische Listenroute `/list/accounts` gerendert. Die fachliche Liste liegt in `BankAccountListViewModel`; die API liefert aktuell paginierte `AccountDto`-Objekte mit dem aktuellen Kontosaldo. Die gewünschte Infokachel ist damit eine Erweiterung des generischen Kontenflusses oder eine kontenspezifische Erweiterung der generischen Listenseite.

Für die Anforderungen FA-2 bis FA-6 müssen die Werte auf derselben Kontenmenge wie die Tabelle basieren. Der bestehende API-Aufruf ist paginiert und enthält weder Bankkontaktname noch historische Salden. Jahres- und Monatsveränderungen müssen deshalb aus Bank-Postings beziehungsweise den vorhandenen zeitperiodischen Aggregaten abgeleitet oder durch einen neuen kontenspezifischen Statistik-Endpunkt bereitgestellt werden.

## Relevante Detaildokumente

- [UI, Routing und bestehende Listenlogik](inventory/ui-routing-list.md)
- [Datenmodell, Berechnung und API](inventory/data-calculation-api.md)
- [Wiederverwendung, Lokalisierung und Tests](inventory/reuse-localization-tests.md)

## Betroffene Anforderungen

| Anforderung | Relevante Bereiche |
|---|---|
| FA-1 | Generische Listenansicht, Konten-ViewModel, Layout/CSS |
| FA-2 | `AccountDto.CurrentBalance`, Kontenliste, Statistikmodell/API |
| FA-3/FA-4 | Bank-Postings mit `BookingDate`/`ValutaDate`, Aggregatlogik, Statistikmodell/API |
| FA-5 | `AccountType`, Kontenstatistik, vorhandene `DonutChart`-Komponente |
| FA-6 | `BankContactId`, Kontaktauflösung, Kontenstatistik, `DonutChart`-Komponente |
| FA-7 | Gemeinsame Kontenabfrage und einheitliche Filter-/Paging-Strategie |

## Bestehende Lücken und Risiken

- Die Tabelle lädt in Seiten zu 50 Elementen und filtert die Suche derzeit clientseitig innerhalb der geladenen Seite. Eine Statistik darf nicht versehentlich nur die erste Seite aggregieren.
- `AccountDto` enthält keine Bankkontaktbezeichnung; für die Gruppierung nach Bankkontakt ist eine Join-/Aggregationserweiterung oder ein zusätzlicher Lookup nötig.
- `CurrentBalance` ist der aktuelle Stand. Eine historische Veränderung ist nicht aus dem DTO allein bestimmbar.
- Es ist fachlich zu klären, ob Jahres-/Monatsveränderung nach Buchungsdatum oder Valutadatum berechnet wird und ob Vorjahres-/Vormonatsstichtände gemeint sind.
- Für Null- und negative Salden muss die Diagrammdarstellung festgelegt werden; `DonutChart` rendert nur positive Slice-Werte.
