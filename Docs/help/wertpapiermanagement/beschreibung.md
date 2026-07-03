← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Beschreibung

## Zweck

Der Bereich verwaltet Wertpapierstammdaten, Kurszeitreihen und Performance-Auswertungen inklusive Benchmark-Vergleich.

## Funktionsweise

Wertpapiere und Kategorien werden über `SecuritiesController` und `SecurityCategoriesController` gepflegt. Kurse können abgerufen, importiert und nachbefüllt werden. Für die Analyse stehen Endpunkte wie `return-summary`, `return-metrics`, `return-cashflows` und `return-benchmark` bereit.

## Beispiele

- Ein Wertpapier wird mit ISIN/WKN, Währung und Kategorie angelegt.
- Historische Kurse werden importiert.
- Für ein Wertpapier wird die Performance-Ansicht über mehrere Tabs aufgerufen.

## Einschränkungen

- Fehlerhafte Kursabfragen markieren das Wertpapier als Preisfehlerzustand.
- Renditeberechnungen hängen von vorhandenen Buchungs- und Kursdaten ab.
