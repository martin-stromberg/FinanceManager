← [Zurück zur Übersicht](index.md)

# Wertpapiermanagement — API

## Übersicht

Die API wird über `SecuritiesController` und `SecurityCategoriesController` bereitgestellt.

## Endpunkte / Methoden

### `GET /api/securities`

**Beschreibung:** Liefert Wertpapierliste.

### `POST /api/securities`

**Beschreibung:** Legt Wertpapier an.

### `POST /api/securities/{id}/prices/import`

**Beschreibung:** Importiert Kurse für ein Wertpapier.

### `POST /api/securities/backfill`

**Beschreibung:** Startet Kurs-Nachbefüllung.

### `GET /api/securities/{id}/return-summary`

**Beschreibung:** Liefert aggregierte Renditeübersicht.

### `GET /api/securities/{id}/return-metrics`

**Beschreibung:** Liefert Kennzahlen zur Rendite.

### `GET /api/securities/{id}/return-chart`

**Beschreibung:** Liefert Zeitreihendaten für Diagramme.

### `GET /api/securities/{id}/return-benchmark`

**Beschreibung:** Liefert Benchmarkvergleich.
