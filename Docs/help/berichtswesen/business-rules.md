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
