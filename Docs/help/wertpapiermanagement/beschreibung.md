← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — Beschreibung

## Zweck

Der Bereich verwaltet Wertpapierstammdaten, Kurszeitreihen und Performance-Auswertungen inklusive Benchmark-Vergleich.

## Funktionsweise

Wertpapiere und Kategorien werden über `SecuritiesController` und `SecurityCategoriesController` gepflegt. Kurse können abgerufen, importiert und nachbefüllt werden. Für die Analyse stehen Endpunkte wie `return-summary`, `return-metrics`, `return-cashflows` und `return-benchmark` bereit.

AlphaVantage-Kursabrufe verwenden den im Benutzerprofil gespeicherten API Key.
Ist kein persoenlicher Key vorhanden, kann ein von einem Administrator
freigegebener Key als Fallback genutzt werden. Gespeicherte Keys liegen in der
Datenbank verschluesselt vor und werden nur unmittelbar fuer den externen
AlphaVantage-Aufruf entschluesselt.

## Beispiele

- Ein Wertpapier wird mit ISIN/WKN, Währung und Kategorie angelegt.
- Historische Kurse werden importiert.
- Für ein Wertpapier wird die Performance-Ansicht über mehrere Tabs aufgerufen.
- Ein Benutzer ohne eigenen AlphaVantage API Key ruft Kurse ueber einen
  freigegebenen Admin-Key ab, ohne dessen Klartext sehen zu koennen.

## Einschränkungen

- Fehlerhafte Kursabfragen markieren das Wertpapier als Preisfehlerzustand.
- Renditeberechnungen hängen von vorhandenen Buchungs- und Kursdaten ab.
- AlphaVantage-Abrufe benoetigen entweder einen persoenlichen API Key oder
  einen durch einen Administrator freigegebenen Key.
