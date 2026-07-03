← [Zurück zur Übersicht](index.md)

# Konten und Buchungen — Beschreibung

## Zweck

Der Bereich verwaltet Bankkonten und die daraus entstehenden Buchungen. Anwender können Konten pflegen, Buchungen nach Entität abrufen und Exporte erstellen.

## Funktionsweise

Konten werden über `AccountsController` als Stammdaten gepflegt. Buchungsdaten werden über `PostingsController` für Konten, Kontakte, Sparpläne und Wertpapiere bereitgestellt. Stornierungen erfolgen über die Reversal-Funktionen und erzeugen verknüpfte Gegenbuchungen.

## Beispiele

- Ein Girokonto wird angelegt und mit einem Bankkontakt verknüpft.
- Für ein Konto wird eine Buchungsliste als CSV exportiert.
- Eine fehlerhafte Buchung wird storniert und als Reversal nachvollziehbar markiert.

## Einschränkungen

- Kontobezogene Aktionen sind benutzergebunden.
- Stornierung ist nur möglich, wenn die Validierung (`validate-reversal`) keine Sperrgründe liefert.
