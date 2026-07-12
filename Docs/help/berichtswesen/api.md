← [Zurück zur Übersicht](index.md)

# Berichtswesen — API

## Übersicht

Die Berichtsfunktionen liegen in `ReportsController` und `HomeKpisController`.

## Endpunkte / Methoden

### `POST /api/reports/report-aggregates`

**Beschreibung:** Liefert aggregierte Reportdaten. Das Request-Feld `CompareProjection` fordert für reine Wertpapierberichte eine Dividendenhochrechnung an. Im Ergebnis zeigt `ComparedProjection`, ob die Hochrechnung tatsächlich aktiv ist; einzelne Berichtspunkte enthalten dann `ProjectionAmount` zusätzlich zu `Amount`.

### `GET /api/reports/report-favorites`

**Beschreibung:** Liefert gespeicherte Favoriten inklusive der Hochrechnungsoption `CompareProjection`.

### `POST /api/reports/report-favorites`

**Beschreibung:** Legt Favorit an. `CompareProjection` speichert die Hochrechnungsoption zusammen mit den übrigen Dashboard-Einstellungen.

### `PUT /api/reports/report-favorites/{id}`

**Beschreibung:** Aktualisiert Favorit. `CompareProjection` wird beim Aktualisieren eines Favoriten überschrieben.

### `GET /api/home-kpis`

**Beschreibung:** Liefert Home-KPI-Konfigurationen.

### `POST /api/home-kpis`

**Beschreibung:** Legt Home-KPI an.
