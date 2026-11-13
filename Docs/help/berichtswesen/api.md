← [Zurück zur Übersicht](index.md)

# Berichtswesen — API

## Übersicht

Die Berichtsfunktionen liegen in `ReportsController` und `HomeKpisController`.

## Endpunkte / Methoden

### `POST /api/reports/report-aggregates`

**Beschreibung:** Liefert aggregierte Reportdaten. Das Request-Feld `CompareProjection` fordert für reine Wertpapierberichte eine Dividendenhochrechnung an. Im Ergebnis zeigt `ComparedProjection`, ob die Hochrechnung tatsächlich aktiv ist; einzelne Berichtspunkte enthalten dann `ProjectionAmount` zusätzlich zu `Amount`.

Das `Filters`-Objekt (`ReportAggregatesFiltersRequest`) akzeptiert optional vier Freitext-Filterlisten je Buchungsart:

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `BankTextFilters` | `string[]` | Nein | Textfilter für Bank-Buchungen |
| `ContactTextFilters` | `string[]` | Nein | Textfilter für Kontakt-Buchungen |
| `SavingsPlanTextFilters` | `string[]` | Nein | Textfilter für Sparplan-Buchungen |
| `SecurityTextFilters` | `string[]` | Nein | Textfilter für Wertpapier-Buchungen |

Jeder Eintrag wird getrimmt, ist maximal 500 Zeichen lang und matcht als Substring (Groß-/Kleinschreibung egal) auf `Subject` und `Description` der Buchungen; mehrere Einträge einer Liste sind ODER-verknüpft. Einträge über 500 Zeichen beim Favoriten-Speichern führen zu `400 BadRequest`.

### `GET /api/reports/report-favorites`

**Beschreibung:** Liefert gespeicherte Favoriten inklusive der Hochrechnungsoption `CompareProjection`. Das `Filters`-Objekt (`ReportFavoriteFiltersDto`) enthält die gespeicherten `*TextFilters`-Listen.

### `POST /api/reports/report-favorites`

**Beschreibung:** Legt Favorit an. `CompareProjection` speichert die Hochrechnungsoption zusammen mit den übrigen Dashboard-Einstellungen. Das `Filters`-Objekt nimmt die vier `*TextFilters`-Listen entgegen und persistiert sie am Favoriten.

### `PUT /api/reports/report-favorites/{id}`

**Beschreibung:** Aktualisiert Favorit. `CompareProjection` wird beim Aktualisieren eines Favoriten überschrieben. `Filters = null` leert alle Filter inklusive der Textfilter.

### MCP-Tools

Die Textfilter stehen auch über MCP zur Verfügung: `fm_reporting_aggregate` akzeptiert sie als Inline-Argumente `bankTextFilters`, `contactTextFilters`, `savingsPlanTextFilters`, `securityTextFilters` (String-Arrays, je max. 500 Zeichen) und wendet gespeicherte Favoriten-Filter automatisch an; `fm_reports_configuration_manage` pflegt sie im `filters`-Objekt beim Anlegen und Aktualisieren von Favoriten.

### `GET /api/home-kpis`

**Beschreibung:** Liefert Home-KPI-Konfigurationen.

### `POST /api/home-kpis`

**Beschreibung:** Legt Home-KPI an.
