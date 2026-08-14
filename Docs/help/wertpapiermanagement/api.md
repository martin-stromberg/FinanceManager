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

## Depot-Analysebericht

Die folgenden Endpunkte werden über `PortfolioAnalysisReportController`
bereitgestellt und sind auf den authentifizierten Benutzer
(`ICurrentUserService.UserId`) beschränkt.

### `GET /api/portfolio/analysis-report`

**Beschreibung:** Liefert den depotweiten Analysebericht
(`PortfolioAnalysisReportDto`) für den aktuellen Benutzer. Nutzt den
monatlichen Cache; bei Cache-Miss wird der Bericht neu berechnet.

**Rückgabe:**

| Typ | Beschreibung |
|-----|--------------|
| `PortfolioAnalysisReportDto` | Enthält `Structure`, `Performance`, `Cashflow`, `Risk`, `GeneratedUtc`, `CacheValidUntilUtc`. |

**DTO-Struktur `PortfolioCashflowDto` (in `PortfolioAnalysisReportDto.Cashflow`):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `NetDepositsCurrentYear` | `decimal` | Netto-Einzahlungen des laufenden Jahres |
| `DividendsCurrentYear` | `decimal` | Dividenden des laufenden Jahres |
| `RealizedGainsCurrentYear` | `decimal` | FIFO-realisierte Gewinne/Verluste des laufenden Jahres |
| `LiquidityRatio` | `decimal?` | Aktueller Saldo der aus Wertpapierbuchungen abgeleiteten Verrechnungskonten geteilt durch Marktwert plus Cash-Bestand; `null`, wenn wegen negativem Cash-Bestand, fehlendem Marktwert oder nicht positivem Nenner keine belastbare Berechnung möglich ist |
| `LiquidityCashBalance` | `decimal` | Aktueller Cash-Bestand der fuer die Liquiditaetsquote abgeleiteten Verrechnungskonten |
| `LiquidityTotalMarketValue` | `decimal` | Aktueller Depot-Marktwert, der als Marktwert-Anteil in die Liquiditaetsquote eingeht |

**DTO-Struktur `PortfolioStructureDto` (in `PortfolioAnalysisReportDto.Structure`):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `TotalMarketValue` | `decimal` | Gesamtmarktwert aller Positionen |
| `InvestedCapital` | `decimal` | Gesamtinvestitionen (Summe aller FIFO-Kosten) |
| `UnrealizedGainLoss` | `decimal` | Unrealisierter Gewinn/Verlust |
| `TopPositions` | `List<PositionDto>` | Die 10 größten Positionen nach Marktwert (absteigend sortiert) |
| `AllPositions` | `List<PositionDto>` | Alle Positionen mit Marktwert, absteigend sortiert; gedeckelt auf 200 Einträge mit „und N weitere"-Hinweis bei Überschreitung |
| `CategoryAllocation` | `List<CategoryAllocationDto>` | Asset Allocation nach Kategorie |
| `RegionalDistribution` | `List<RegionalDistributionDto>` | Regionale Verteilung |
| `SectorDistribution` | `List<SectorDistributionDto>` | Sektorverteilung |
| `InvestedCapitalBreakdown` | `List<InvestedCapitalBreakdownDto>` | Pro Wertpapier die verbleibenden FIFO-Kauf-Lots (Kaufdatum, Menge, Kosten/Einheit, Gesamtkosten); gedeckelt auf 200 Einträge mit „und N weitere"-Hinweis bei Überschreitung |

**DTO-Struktur `InvestedCapitalBreakdownDto` (in `PortfolioStructureDto.InvestedCapitalBreakdown`):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `SecurityName` | `string` | Name des Wertpapiers |
| `TotalInvestedCapital` | `decimal` | Gesamtinvestitionen für dieses Wertpapier |
| `FifoLots` | `List<FifoLotDto>` | Liste der verbleibenden FIFO-Lots |

**DTO-Struktur `FifoLotDto` (in `InvestedCapitalBreakdownDto.FifoLots`):**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `PurchaseDate` | `DateTime` | Kaufdatum |
| `Quantity` | `decimal` | Menge |
| `CostPerUnit` | `decimal` | Kosten pro Einheit |
| `TotalCost` | `decimal` | Gesamtkosten für diesen Lot |

### `GET /api/portfolio/kpi-configuration`

**Beschreibung:** Liefert die gespeicherte Kachel-Konfiguration
(`PortfolioKpiConfigurationDto`) des aktuellen Benutzers, oder eine
Default-Konfiguration (`Structure`, `Performance`, `Cashflow` aktiv; `Risk`
inaktiv), wenn noch keine gespeichert wurde.

### `POST /api/portfolio/kpi-configuration`

**Beschreibung:** Speichert die Kachel-Sichtbarkeit und -Reihenfolge des
aktuellen Benutzers und invalidiert anschließend den Berichts-Cache.

**Parameter:**

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `ActiveTileIds` | `List<PortfolioTileId>` | Ja | Sichtbare Kacheln; mindestens ein Eintrag. |
| `TileOrder` | `List<PortfolioTileId>` | Ja | Anzeigereihenfolge; muss exakt die aktiven Kachel-IDs ohne Duplikate enthalten. |

**Rückgabe:**

| Typ | Beschreibung |
|-----|--------------|
| `PortfolioKpiConfigurationDto` | Die persistierte Konfiguration inkl. `UpdatedUtc`. |

**Fehler:**

| Code / Exception | Ursache |
|-------------------|---------|
| `400 Bad Request` | Keine aktive Kachel, oder `TileOrder` enthält nicht exakt die aktiven Kachel-IDs ohne Duplikate. |

### `POST /api/portfolio/cache/reset`

**Beschreibung:** Löscht den Berichts-Cache des aktuellen Benutzers manuell
(Ribbon-Button "Aktualisieren"); der nächste Aufruf von
`GET /api/portfolio/analysis-report` berechnet den Bericht neu.

**Rückgabe:** `204 No Content`.
