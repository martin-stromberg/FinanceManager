← [Zurück zur Übersicht](index.md)

# Berichtswesen — Business Rules

## Favoriten begrenzen Zeitfenster

**Beschreibung:** Favoriten begrenzen die Anzahl betrachteter Perioden über `Take`.

**Bedingungen:**
- Eingabewert kann frei übermittelt werden.

**Verhalten:**
- Wert wird auf einen sinnvollen Bereich geklemmt.

**Umsetzung:** `ReportFavorite.SetTake` (1 bis 120).

## Mehrfachfilter werden als CSV persistiert

**Beschreibung:** Mehrfachselektionen werden dauerhaft in CSV-Feldern gespeichert.

**Bedingungen:**
- Filterlisten können leer oder befüllt sein.

**Verhalten:**
- Beim Speichern werden Listen serialisiert.
- Beim Laden werden Listen typisiert zurückgegeben.

**Umsetzung:** `ReportFavorite.SetFilters` und `ReportFavorite.GetFilters`.

## Dividendenhochrechnung nur für Wertpapierberichte

**Beschreibung:** Die Vergleichsoption `Hochrechnung` berechnet erwartete Dividenden nur für reine Wertpapierberichte.

**Bedingungen:**
- Die effektive Buchungsartenauswahl enthält ausschließlich `PostingKind.Security`.
- Das Intervall ist nicht `AllHistory`.
- Die Auswertung nutzt den Netto-Dividendenpfad mit Dividenden, Gebühren und Steuern.

**Verhalten:**
- Bei gültiger Auswahl liefert die Aggregation `ComparedProjection = true` und pro Berichtspunkt ein optionales `ProjectionAmount`.
- `ProjectionAmount` besteht aus den gebuchten Netto-Dividenden des aktuellen Betrachtungszeitraums plus erwarteten Netto-Dividenden aus der gleichen Vorjahresperiode.
- Eine Vorjahresdividende gilt als bestätigt, wenn im korrespondierenden aktuellen Periodenbucket mindestens eine Dividende desselben Wertpapiers vorhanden ist.
- Bei anderen Buchungsarten, gemischten Auswahlen oder `AllHistory` bleibt die Hochrechnung wirkungslos; `ComparedProjection = false` und `ProjectionAmount` bleibt leer.
- Kategorie- und Summenzeilen aggregieren die Hochrechnungswerte ihrer Kindzeilen.

**Umsetzung:** `ReportAggregationQuery.CompareProjection`, `ReportAggregationResult.ComparedProjection`, `ReportAggregatePointDto.ProjectionAmount`.

## Hochrechnung wird in Favoriten gespeichert

**Beschreibung:** Die Hochrechnungsoption ist Teil der benutzerspezifischen Favoritenkonfiguration.

**Bedingungen:**
- Ein Anwender speichert oder aktualisiert einen Report-Favoriten im Dashboard.

**Verhalten:**
- `CompareProjection` wird zusammen mit den übrigen Favoritenoptionen persistiert.
- Beim Laden eines Favoriten wird die Option wiederhergestellt.
- Wechselt die Auswahl im Dashboard auf eine nicht gültige Buchungsartenauswahl, wird die Option deaktiviert und nicht an die Aggregation übergeben.

**Umsetzung:** `ReportFavorite.CompareProjection` und die Favoriten-DTOs.
