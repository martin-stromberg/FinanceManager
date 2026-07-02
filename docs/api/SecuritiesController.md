# SecuritiesController

> **Klasse:** `FinanceManager.Web.Controllers.SecuritiesController`  
> **Route-Präfix:** `/api/securities`  
> **Authentifizierung:** JWT Bearer Token (alle Endpunkte erfordern einen gültigen Token)

Verwaltet Wertpapiere (Instrumente): CRUD-Operationen, Symbol-Attachments, Kursabfragen, Zeitreihen-Aggregate, Dividenden-Aggregation, Hintergrundaufgaben für Kurs-Backfills sowie die vollständige **Renditeanalyse** (TWR, IRR, CAGR, Volatilität, Sharpe Ratio, Benchmark-Vergleich).

---

## Inhaltsverzeichnis

- [CRUD-Endpunkte](#crud-endpunkte)
  - [GET /api/securities](#get-apisecurities)
  - [GET /api/securities/count](#get-apisecuritiescount)
  - [GET /api/securities/{id}](#get-apisecuritiesid)
  - [POST /api/securities](#post-apisecurities)
  - [PUT /api/securities/{id}](#put-apisecuritiesid)
  - [POST /api/securities/{id}/archive](#post-apisecuritiesidarchive)
  - [DELETE /api/securities/{id}](#delete-apisecuritiesid)
- [Symbol-Endpunkte](#symbol-endpunkte)
  - [POST /api/securities/{id}/symbol/{attachmentId}](#post-apisecuritiesidsymbolattachmentid)
  - [DELETE /api/securities/{id}/symbol](#delete-apisecuritiesidsymbol)
  - [POST /api/securities/{id}/symbol (Upload)](#post-apisecuritiesidsymbol-upload)
- [Preis- und Zeitreihen-Endpunkte](#preis--und-zeitreihen-endpunkte)
  - [GET /api/securities/{id}/prices](#get-apisecuritiesidprices)
  - [POST /api/securities/{id}/prices/import](#post-apisecuritiesidpricesimport)
  - [GET /api/securities/{securityId}/aggregates](#get-apisecuritiesidsecurityidaggregates)
  - [GET /api/securities/dividends](#get-apisecuritiesdividends)
  - [POST /api/securities/backfill](#post-apisecuritiesbackfill)
- [Return Analysis Endpunkte](#return-analysis-endpunkte)
  - [GET /api/securities/{id}/return-summary](#get-apisecuritiesidreturn-summary)
  - [GET /api/securities/{id}/return-sparkline](#get-apisecuritiesidreturn-sparkline)
  - [GET /api/securities/{id}/return-metrics](#get-apisecuritiesidreturn-metrics)
  - [GET /api/securities/{id}/return-periodic](#get-apisecuritiesidreturn-periodic)
  - [GET /api/securities/{id}/return-cashflows](#get-apisecuritiesidreturn-cashflows)
  - [GET /api/securities/{id}/return-chart](#get-apisecuritiesidreturn-chart)
  - [GET /api/securities/{id}/return-benchmark](#get-apisecuritiesidreturn-benchmark)
  - [GET /api/securities/return-analysis/settings](#get-apisecuritiesreturn-analysissettings)
  - [PUT /api/securities/return-analysis/settings](#put-apisecuritiesreturn-analysissettings)
  - [DELETE /api/securities/{id}/return-cache](#delete-apisecuritiesidreturn-cache)
- [DTOs](#dtos)

---

## CRUD-Endpunkte

### GET /api/securities

Gibt alle Wertpapiere des angemeldeten Benutzers zurück.

**Authentifizierung:** Bearer Token *(required)*

#### Query-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `onlyActive` | `bool` | Optional | Wenn `true` (Standard), werden nur aktive Wertpapiere zurückgegeben. `false` schließt archivierte ein. |

#### Response

**200 OK**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Apple Inc.",
    "identifier": "AAPL",
    "description": "US-amerikanisches Technologieunternehmen",
    "alphaVantageCode": "AAPL",
    "currencyCode": "USD",
    "categoryId": "1b2c3d4e-5678-90ab-cdef-1234567890ab",
    "isActive": true
  }
]
```

#### curl-Beispiel
```bash
curl -X GET "https://localhost:5001/api/securities?onlyActive=true" \
  -H "Authorization: Bearer <token>"
```

---

### GET /api/securities/count

Gibt die Anzahl der Wertpapiere des angemeldeten Benutzers zurück.

**Authentifizierung:** Bearer Token *(required)*

#### Query-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `onlyActive` | `bool` | Optional | Wenn `true` (Standard), werden nur aktive Wertpapiere gezählt. |

#### Response

**200 OK**
```json
{ "count": 12 }
```

---

### GET /api/securities/{id}

Gibt ein einzelnes Wertpapier anhand seiner ID zurück.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Eindeutiger Bezeichner des Wertpapiers. |

#### Response

**200 OK**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Apple Inc.",
  "identifier": "AAPL",
  "description": "US-amerikanisches Technologieunternehmen",
  "alphaVantageCode": "AAPL",
  "currencyCode": "USD",
  "categoryId": "1b2c3d4e-5678-90ab-cdef-1234567890ab",
  "isActive": true
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Wertpapier gefunden. |
| `404 Not Found` | Wertpapier nicht gefunden oder gehört nicht dem aktuellen Benutzer. |

---

### POST /api/securities

Legt ein neues Wertpapier für den angemeldeten Benutzer an.

**Authentifizierung:** Bearer Token *(required)*

#### Request-Body (`application/json`)

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `name` | `string` | *(required)* | Anzeigename des Wertpapiers. |
| `identifier` | `string` | *(required)* | Eindeutiger Symbol-Bezeichner (z. B. ISIN, Ticker). |
| `description` | `string` | Optional | Freitext-Beschreibung. |
| `alphaVantageCode` | `string` | Optional | Code für AlphaVantage-Kursabfragen. |
| `currencyCode` | `string` | *(required)* | ISO 4217-Währungscode (z. B. `EUR`, `USD`). |
| `categoryId` | `guid` | Optional | Kategorie-ID zur Klassifizierung. |
| `parent` | `object` | Optional | Optionaler Parent-Assignment-Kontext. |

```json
{
  "name": "Microsoft Corp.",
  "identifier": "MSFT",
  "description": "US-amerikanisches Technologieunternehmen",
  "alphaVantageCode": "MSFT",
  "currencyCode": "USD",
  "categoryId": "1b2c3d4e-5678-90ab-cdef-1234567890ab"
}
```

#### Response

**201 Created** – gibt das erstellte Wertpapier zurück (gleiche Struktur wie [GET /api/securities/{id}](#get-apisecuritiesid)).

| Statuscode | Beschreibung |
|------------|--------------|
| `201 Created` | Wertpapier erfolgreich erstellt. |
| `400 Bad Request` | Validierungsfehler (ungültige Felder oder Konflikte). |

---

### PUT /api/securities/{id}

Aktualisiert ein bestehendes Wertpapier.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des zu aktualisierenden Wertpapiers. |

#### Request-Body

Gleiche Struktur wie beim [POST /api/securities](#post-apisecurities).

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Aktualisiertes Wertpapier. |
| `400 Bad Request` | Validierungsfehler. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

---

### POST /api/securities/{id}/archive

Archiviert ein Wertpapier (Soft-Delete), sodass es nicht mehr in aktiven Listen erscheint.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des zu archivierenden Wertpapiers. |

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `204 No Content` | Erfolgreich archiviert. |
| `404 Not Found` | Wertpapier nicht gefunden. |

---

### DELETE /api/securities/{id}

Löscht ein Wertpapier dauerhaft.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des zu löschenden Wertpapiers. |

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `204 No Content` | Erfolgreich gelöscht. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

---

## Symbol-Endpunkte

### POST /api/securities/{id}/symbol/{attachmentId}

Weist einem Wertpapier ein bestehendes Attachment als Symbol zu.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |
| `attachmentId` | `guid` | *(required)* | Bezeichner des Attachments, das als Symbol gesetzt werden soll. |

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `204 No Content` | Symbol erfolgreich zugewiesen. |
| `404 Not Found` | Wertpapier oder Attachment nicht gefunden. |

---

### DELETE /api/securities/{id}/symbol

Entfernt das Symbol-Attachment vom Wertpapier.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `204 No Content` | Symbol erfolgreich entfernt. |
| `404 Not Found` | Wertpapier nicht gefunden. |

---

### POST /api/securities/{id}/symbol (Upload)

Lädt eine neue Symbol-Datei hoch und weist sie dem Wertpapier direkt zu.

**Authentifizierung:** Bearer Token *(required)*  
**Content-Type:** `multipart/form-data`

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Form-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `file` | `IFormFile` | *(required)* | Hochzuladende Symbol-Datei (Bild o. ä.). |
| `categoryId` | `guid` | Optional | Kategorie-ID für das Attachment. |

#### Response

**200 OK** – gibt das erstellte `AttachmentDto` zurück.

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Symbol hochgeladen und zugewiesen. |
| `400 Bad Request` | Keine Datei übergeben oder ungültige Eingabe. |
| `500 Internal Server Error` | Unerwarteter Fehler beim Speichern. |

---

## Preis- und Zeitreihen-Endpunkte

### GET /api/securities/{id}/prices

Gibt historische Kurse (neueste zuerst, seitenweise) für ein Wertpapier zurück.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Query-Parameter

| Name | Typ | Standard | Beschreibung |
|------|-----|----------|--------------|
| `skip` | `int` | `0` | Anzahl der zu überspringenden Datensätze (Paging-Offset). |
| `take` | `int` | `50` | Seitengröße. Wird auf maximal 250 begrenzt. |

#### Response

**200 OK**
```json
[
  {
    "date": "2024-01-15",
    "closePrice": 189.50,
    "currencyCode": "USD"
  }
]
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Liste der Kursdaten. |
| `404 Not Found` | Wertpapier nicht im Besitz des Benutzers. |

---

### POST /api/securities/{id}/prices/import

Importiert Tageskurse aus einer hochgeladenen CSV-Datei für ein bestehendes Wertpapier.  
Der Endpunkt verarbeitet ING-kompatible Dateien und führt ein Upsert pro Kalendertag aus.

**UI-Kontext:** Die Importaktion wird in der Kursliste des Wertpapiers ausgelöst (`/list/securities/prices/{id}`), nicht mehr auf der Wertpapier-Detailseite.

**Authentifizierung:** ****** *(required)*

**Verwandte Feature-Dokumente:**  
- [Requirements: Wertpapierkurse ING](../requirements/wertpapierkurse-ing-requirements.md)  
- [Architecture Blueprint: Wertpapierkurse ING](../architecture/architecture-blueprint-wertpapierkurse-ing.md)  
- [Testplan: Wertpapierkurse ING](../tests/wertpapierkurse-ing-testplan.md)

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Request (`multipart/form-data`)

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `file` | Datei | *(required)* | CSV-Datei mit Kursdaten (ING-Format). |
| `provider` | `string` | Optional | Provider-Hinweis. Wenn nicht gesetzt, verwendet der Controller `ing` als Default. |

**Provider-Default und Auswahlverhalten**
- Bei fehlendem `provider` wird serverseitig `"ing"` verwendet.
- Die Service-Auswahl nutzt zusätzlich die Dateiendung (`.csv`) als Fallback.
- Ein `400` wegen nicht unterstütztem Provider tritt auf, wenn kein Import-Service passt (z. B. ungeeigneter Provider + nicht unterstützte Datei).

#### Upsert-Semantik

- `inserted`: Neuer Kurs für ein Datum wurde angelegt.
- `updated`: Bestehender Kurs wurde bei abweichendem `close` aktualisiert.
- `unchanged`: Bestehender Kurs hatte bereits denselben `close`-Wert.
- `skipped`: Zeilen wurden nicht verarbeitet (z. B. leer oder Parsing-/Validierungsfehler).
- Doppelte Datumszeilen werden auf Tagesebene dedupliziert (`last row wins`).

#### Response

**200 OK**
```json
{
  "inserted": 10,
  "updated": 2,
  "unchanged": 5,
  "skipped": 1,
  "errors": [
    { "lineNumber": 17, "message": "Invalid close value format." }
  ]
}
```

**Response-Felder**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `inserted` | `int` | Anzahl neu angelegter Tageskurse. |
| `updated` | `int` | Anzahl aktualisierter Tageskurse. |
| `unchanged` | `int` | Anzahl unveränderter Tageskurse. |
| `skipped` | `int` | Anzahl übersprungener CSV-Zeilen. |
| `errors` | `array` | Zeilenbezogene Parse-/Validierungsfehler. |

**`errors[]`-Element**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `lineNumber` | `int` | Zeilennummer in der CSV-Datei. |
| `message` | `string` | Fehlerbeschreibung für diese Zeile. |

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Import erfolgreich ausgeführt (auch bei Teilfehlern mit Fehlerliste). |
| `400 Bad Request` | Datei fehlt/leer (`Err_Invalid_File`), kein passender Import-Service (`Err_Invalid_provider`) oder keine validen Kurszeilen (`Err_Invalid_Import`). |
| `401 Unauthorized` | Kein gültiger Bearer Token. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |
| `500 Internal Server Error` | Unerwarteter Serverfehler (`Err_Unexpected`). |

**Fehler-Payload (ApiErrorDto)**
```json
{
  "origin": "API_Securities",
  "code": "Err_Invalid_File",
  "message": "A non-empty file is required.",
  "error": "Err_Invalid_File"
}
```

#### Beispiel (curl)

```bash
curl -X POST "https://localhost:5001/api/securities/3fa85f64-5717-4562-b3fc-2c963f66afa6/prices/import" \
  -H "Authorization: Bearer <token>" \
  -H "Accept: application/json" \
  -F "provider=ing" \
  -F "file=@sample.csv;type=text/csv"
```

**Beispiel-Response (200 OK)**
```json
{
  "inserted": 10,
  "updated": 2,
  "unchanged": 5,
  "skipped": 1,
  "errors": [
    {
      "lineNumber": 17,
      "message": "Invalid close value format."
    }
  ]
}
```

---

### GET /api/securities/{securityId}/aggregates

Gibt Zeitreihen-Aggregate (Buchungsbeträge nach Periode) für ein Wertpapier zurück.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `securityId` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Query-Parameter

| Name | Typ | Standard | Beschreibung |
|------|-----|----------|--------------|
| `period` | `string` | `"Month"` | Aggregationsperiode: `Month`, `Quarter`, `HalfYear`, `Year`. Groß-/Kleinschreibung irrelevant. |
| `take` | `int` | periodenabhängig | Anzahl zurückgegebener Perioden (1–200). |
| `maxYearsBack` | `int` | – | Optionale Begrenzung auf die letzten N Jahre (1–10). |

#### Response

**200 OK**
```json
[
  { "periodStart": "2024-01-01", "amount": -1250.00 },
  { "periodStart": "2023-12-01", "amount": 0.00 }
]
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Aggregierte Perioden-Daten. |
| `404 Not Found` | Keine Daten verfügbar. |

---

### GET /api/securities/dividends

Gibt quartalsweise Dividenden-Aggregate der letzten 12 Monate für alle Wertpapiere des Benutzers zurück.

**Authentifizierung:** Bearer Token *(required)*

> **Hinweis:** Die Query-Parameter `period` und `take` werden akzeptiert, aber ignoriert (für Kompatibilität beibehalten).

#### Response

**200 OK** – Liste von `AggregatePointDto` (Quartalswerte).

---

### POST /api/securities/backfill

Stellt einen Hintergrundauftrag zum rückwirkenden Befüllen von Kursdaten in die Warteschlange.

**Authentifizierung:** Bearer Token *(required)*

#### Request-Body (`application/json`)

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `securityId` | `guid` | Optional | Bezeichner des Wertpapiers (null = alle). |
| `fromDateUtc` | `DateTime` | Optional | Startdatum des Backfills (ISO 8601). |
| `toDateUtc` | `DateTime` | Optional | Enddatum des Backfills (ISO 8601). |

#### Response

**200 OK** – gibt `BackgroundTaskInfo` zurück.

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Aufgabe erfolgreich eingereiht. |
| `400 Bad Request` | Ungültige Anfrageparameter. |

---

## Return Analysis Endpunkte

Alle Endpunkte dieses Abschnitts liefern Ergebnisse der Renditeanalyse. Berechnungsergebnisse werden **1 Stunde** im Server-seitigen In-Memory-Cache zwischengespeichert (TTL). Der Cache wird automatisch invalidiert, wenn neue Buchungen oder Kursdaten für das Wertpapier vorliegen. Eine manuelle Invalidierung ist über [DELETE /api/securities/{id}/return-cache](#delete-apisecuritiesidreturn-cache) möglich.

---

### GET /api/securities/{id}/return-summary

Gibt eine kompakte Rendite-Zusammenfassung für das Dashboard-Widget auf der Wertpapier-Detailseite zurück (Feature-Referenz: FR-1).

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Response

**200 OK** – [`ReturnSummaryDto`](#returnsummarydto)

```json
{
  "investedCapital": 5000.00,
  "currentMarketValue": 6250.00,
  "totalReturnAbsolute": 1450.00,
  "totalReturnPercent": 29.00,
  "cagr": 0.1350,
  "irr": 0.1280,
  "costBasisPerShare": 125.00,
  "currentPricePerShare": 156.25,
  "netDividends": 200.00,
  "currencyCode": "EUR",
  "hasMissingPrices": false,
  "missingPricesHint": null
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Rendite-Zusammenfassung. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

#### curl-Beispiel
```bash
curl -X GET "https://localhost:5001/api/securities/3fa85f64-5717-4562-b3fc-2c963f66afa6/return-summary" \
  -H "Authorization: Bearer <token>"
```

---

### GET /api/securities/{id}/return-sparkline

Gibt Sparkline-Diagrammdaten (Mini-Chart) für ein Wertpapier zurück (Feature-Referenz: FR-1.1). Der Endpunkt ist bewusst vom Summary-Endpunkt getrennt, damit der Summary-Cache schlank bleibt.

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL (separater Cache-Eintrag)

> **Hinweis:** Gibt `404` zurück, wenn weniger als 30 Kursdatenpunkte vorhanden sind.

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Response

**200 OK** – [`SparklineDataDto`](#sparklinedatadto)

```json
{
  "points": [
    {
      "date": "2023-01-01T00:00:00Z",
      "marketValue": 5000.00,
      "investedCapital": 5000.00
    },
    {
      "date": "2024-01-01T00:00:00Z",
      "marketValue": 6250.00,
      "investedCapital": 5000.00
    }
  ]
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Sparkline-Datenpunkte. |
| `404 Not Found` | Wertpapier nicht gefunden oder weniger als 30 Kursdatenpunkte verfügbar. |

---

### GET /api/securities/{id}/return-metrics

Gibt detaillierte Rendite-Kennzahlen für den „Kennzahlen"-Tab zurück (Feature-Referenz: FR-2.1). Enthält TWR, IRR, Volatilität, MaxDrawdown, Sharpe Ratio und Gewinn/Verlust-Aufschlüsselung.

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL

> ⚠️ **Bekannter Bug BUG-1:** Die interne Hilfsmethode `BuildTwrPeriods` verwendet fälschlicherweise `start` statt `end` beim Aufbau der Periodenstruktur. Dadurch können TWR-Ergebnisse (`twr`-Feld) aktuell fehlerhaft sein. Alle anderen Kennzahlen sind nicht betroffen. Der Fix ist ausstehend.

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Response

**200 OK** – [`DetailedReturnMetricsDto`](#detailedreturnmetricsdto)

```json
{
  "grossReturn": 1650.00,
  "netReturn": 1450.00,
  "totalTaxes": 156.00,
  "totalFees": 44.00,
  "taxRate": 0.0945,
  "twr": 0.2750,
  "volatility": 0.1820,
  "maxDrawdown": -0.1230,
  "sharpeRatio": 1.42,
  "realizedGains": 300.00,
  "unrealizedGains": 1150.00,
  "irr": 0.1280,
  "dividendYieldCurrentYear": 0.0320
}
```

> **Hinweis zu `sharpeRatio`:** Der Wert ist `null`, wenn der Benutzer Sharpe Ratio in den [Einstellungen](#put-apisecuritiesreturn-analysissettings) nicht aktiviert hat oder nicht genügend Daten vorliegen.

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Detaillierte Rendite-Kennzahlen. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

#### curl-Beispiel
```bash
curl -X GET "https://localhost:5001/api/securities/3fa85f64-5717-4562-b3fc-2c963f66afa6/return-metrics" \
  -H "Authorization: Bearer <token>"
```

---

### GET /api/securities/{id}/return-periodic

Gibt periodische Renditen (jährlich + monatlich + Dividenden) für den „Zeitliche Entwicklung"-Tab zurück (Feature-Referenz: FR-2.2, FR-2.5).

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Response

**200 OK** – [`PeriodicReturnsDto`](#periodicreturnsdto)

```json
{
  "annualReturns": [
    { "year": 2024, "returnPercent": 18.50, "isYtd": true },
    { "year": 2023, "returnPercent": 12.30, "isYtd": false }
  ],
  "monthlyReturns": [
    { "year": 2024, "month": 1, "returnPercent": 3.20 },
    { "year": 2024, "month": 2, "returnPercent": null }
  ],
  "annualDividends": [
    {
      "year": 2023,
      "grossDividend": 120.00,
      "netDividend": 84.00,
      "cumulativeNet": 200.00
    }
  ]
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Periodische Renditedaten. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

---

### GET /api/securities/{id}/return-cashflows

Gibt eine chronologische Cashflow-Zeitlinie für den „Cashflows"-Tab zurück (Feature-Referenz: FR-2.3, FR-2.6). Enthält einzelne Buchungs-Cashflows sowie jährliche Zusammenfassungen.

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Response

**200 OK** – [`CashflowTimelineDto`](#cashflowtimelinedto)

```json
{
  "entries": [
    {
      "date": "2022-03-15T00:00:00Z",
      "type": "Buy",
      "amount": -2500.00,
      "description": "Kauf 20 Stück",
      "postingId": "a1b2c3d4-0000-0000-0000-000000000001"
    },
    {
      "date": "2023-06-01T00:00:00Z",
      "type": "Dividend",
      "amount": 80.00,
      "description": null,
      "postingId": "a1b2c3d4-0000-0000-0000-000000000002"
    }
  ],
  "annualSummaries": [
    {
      "year": 2023,
      "totalBuys": -2500.00,
      "totalSells": 0.00,
      "totalDividends": 80.00,
      "totalTaxes": -14.00,
      "totalFees": -9.90
    }
  ]
}
```

> **Cashflow-Typen:** `Buy`, `Sell`, `Dividend`, `Tax`, `Fee`  
> **Vorzeichen-Konvention:** Käufe und Steuern sind negativ (Abfluss), Verkäufe und Dividenden positiv (Zufluss).

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Cashflow-Zeitlinie. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

---

### GET /api/securities/{id}/return-chart

Gibt Performance-Diagrammdaten für den „Übersicht"-Tab zurück (Feature-Referenz: FR-2.4). Der Zeitraum ist über den `timeRange`-Parameter steuerbar.

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers. |

#### Query-Parameter

| Name | Typ | Standard | Beschreibung |
|------|-----|----------|--------------|
| `timeRange` | `ChartTimeRange` | `All` | Zeitbereich des Diagramms. Gültige Werte: `OneMonth`, `ThreeMonths`, `SixMonths`, `OneYear`, `ThreeYears`, `All`. |

#### Response

**200 OK** – [`PerformanceChartDataDto`](#performancechartdatadto)

```json
{
  "timeRange": "OneYear",
  "portfolioValues": [
    { "date": "2023-07-01T00:00:00Z", "value": 5200.00 },
    { "date": "2024-07-01T00:00:00Z", "value": 6250.00 }
  ],
  "investedCapitalValues": [
    { "date": "2023-07-01T00:00:00Z", "value": 5000.00 },
    { "date": "2024-07-01T00:00:00Z", "value": 5000.00 }
  ]
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Performance-Diagrammdaten. |
| `404 Not Found` | Wertpapier nicht gefunden oder nicht im Besitz des Benutzers. |

#### curl-Beispiel
```bash
curl -X GET "https://localhost:5001/api/securities/3fa85f64-5717-4562-b3fc-2c963f66afa6/return-chart?timeRange=OneYear" \
  -H "Authorization: Bearer <token>"
```

---

### GET /api/securities/{id}/return-benchmark

Gibt Benchmark-Vergleichsdaten für den „Benchmark"-Tab zurück (Feature-Referenz: FR-7). Beide Zeitreihen werden auf Basis 100 normalisiert (Einstieg = 100), um einen prozentualen Vergleich zu ermöglichen.

**Authentifizierung:** Bearer Token *(required)*  
**Caching:** 1 Stunde TTL

> **Voraussetzung:** Der Benutzer muss ein Benchmark-Wertpapier in den [Einstellungen](#put-apisecuritiesreturn-analysissettings) konfiguriert haben. Das Benchmark-Wertpapier muss demselben Benutzer gehören (Sicherheitsregel S-3).

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des zu vergleichenden Wertpapiers. |

#### Response

**200 OK** – [`BenchmarkComparisonDto`](#benchmarkcomparisondto)

```json
{
  "benchmarkSecurityId": "9c8b7a6f-1234-5678-abcd-ef0123456789",
  "benchmarkName": "MSCI World ETF",
  "securityNormalizedValues": [
    { "date": "2022-01-03T00:00:00Z", "value": 100.00 },
    { "date": "2024-07-01T00:00:00Z", "value": 125.00 }
  ],
  "benchmarkNormalizedValues": [
    { "date": "2022-01-03T00:00:00Z", "value": 100.00 },
    { "date": "2024-07-01T00:00:00Z", "value": 118.50 }
  ]
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Normalisierte Vergleichsreihen. |
| `404 Not Found` | Kein Benchmark konfiguriert, Benchmark-Daten unzureichend oder Wertpapier nicht gefunden. |

---

### GET /api/securities/return-analysis/settings

Gibt die gespeicherten Renditeanalyse-Einstellungen des aktuellen Benutzers zurück (Benchmark-Konfiguration, Sharpe-Ratio-Opt-in).

**Authentifizierung:** Bearer Token *(required)*

> **Hinweis:** Wenn noch keine Einstellungen gespeichert wurden, wird ein Standardobjekt zurückgegeben (kein Benchmark, Sharpe Ratio deaktiviert, Risikoloser Zinssatz = 0).

#### Response

**200 OK** – [`ReturnAnalysisSettingsDto`](#returnanalysissettingsdto)

```json
{
  "benchmarkSecurityId": "9c8b7a6f-1234-5678-abcd-ef0123456789",
  "benchmarkSecurityName": "MSCI World ETF",
  "showSharpeRatio": true,
  "riskFreeRate": 0.04
}
```

| Statuscode | Beschreibung |
|------------|--------------|
| `200 OK` | Aktuelle Einstellungen (immer, auch wenn Standardwerte). |

#### curl-Beispiel
```bash
curl -X GET "https://localhost:5001/api/securities/return-analysis/settings" \
  -H "Authorization: Bearer <token>"
```

---

### PUT /api/securities/return-analysis/settings

Aktualisiert die Renditeanalyse-Einstellungen des aktuellen Benutzers.

**Authentifizierung:** Bearer Token *(required)*

#### Request-Body (`application/json`) – [`ReturnAnalysisSettingsRequest`](#returnanalysissettingsrequest)

| Feld | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `benchmarkSecurityId` | `guid` | Optional | ID des Benchmark-Wertpapiers. `null` löscht den Benchmark. Das Wertpapier muss dem Benutzer gehören. |
| `showSharpeRatio` | `bool` | *(required)* | Aktiviert die Sharpe-Ratio-Anzeige in der UI. |
| `riskFreeRate` | `decimal` | *(required)* | Risikoloser Zinssatz für die Sharpe-Ratio-Berechnung (z. B. `0.04` = 4 %). Muss ≥ 0 sein. |

```json
{
  "benchmarkSecurityId": "9c8b7a6f-1234-5678-abcd-ef0123456789",
  "showSharpeRatio": true,
  "riskFreeRate": 0.04
}
```

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `204 No Content` | Einstellungen erfolgreich gespeichert. |
| `400 Bad Request` | Ungültige Eingabe (z. B. `riskFreeRate` < 0 oder Benchmark-Wertpapier gehört nicht dem Benutzer). |
| `500 Internal Server Error` | Unerwarteter Fehler. |

#### curl-Beispiel
```bash
curl -X PUT "https://localhost:5001/api/securities/return-analysis/settings" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"benchmarkSecurityId": "9c8b7a6f-1234-5678-abcd-ef0123456789", "showSharpeRatio": true, "riskFreeRate": 0.04}'
```

---

### DELETE /api/securities/{id}/return-cache

Invalidiert den serverseitigen In-Memory-Cache für die Renditeanalyse eines bestimmten Wertpapiers. Nützlich, wenn Daten manuell korrigiert wurden oder ein sofortiger Cache-Refresh ohne Ablauf der TTL gewünscht ist.

**Authentifizierung:** Bearer Token *(required)*

#### Path-Parameter

| Name | Typ | Pflicht | Beschreibung |
|------|-----|---------|--------------|
| `id` | `guid` | *(required)* | Bezeichner des Wertpapiers, dessen Cache invalidiert werden soll. |

#### Response

| Statuscode | Beschreibung |
|------------|--------------|
| `204 No Content` | Cache erfolgreich invalidiert. |

#### curl-Beispiel
```bash
curl -X DELETE "https://localhost:5001/api/securities/3fa85f64-5717-4562-b3fc-2c963f66afa6/return-cache" \
  -H "Authorization: Bearer <token>"
```

---

## DTOs

### ReturnSummaryDto

Kompakte Rendite-Zusammenfassung für das Dashboard-Widget (FR-1).

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `investedCapital` | `decimal` | Gesamtes investiertes Kapital (Summe aller Kaufbeträge). |
| `currentMarketValue` | `decimal` | Aktueller Marktwert (gehaltene Stücke × aktueller Kurs). |
| `totalReturnAbsolute` | `decimal` | Absoluter Gesamtertrag (Marktwert + Netto-Dividenden − investiertes Kapital). |
| `totalReturnPercent` | `decimal` | Gesamtertrag in Prozent. |
| `cagr` | `decimal?` | Compound Annual Growth Rate; `null` wenn Haltedauer < 1 Jahr. |
| `irr` | `decimal?` | Internal Rate of Return (persönliche Rendite); `null` wenn nicht berechenbar. |
| `costBasisPerShare` | `decimal` | Durchschnittlicher Einstandskurs (FIFO-Basis). |
| `currentPricePerShare` | `decimal` | Zuletzt verfügbarer Kurs je Stück. |
| `netDividends` | `decimal` | Erhaltene Netto-Dividenden (nach Steuern). |
| `currencyCode` | `string` | ISO 4217-Währungscode. |
| `hasMissingPrices` | `bool` | `true` wenn Kurslücken im Zeitraum vorhanden. |
| `missingPricesHint` | `string?` | Hinweistext zu fehlenden Kursdaten; `null` wenn vollständig. |

---

### SparklineDataDto

Sparkline-Daten für den Mini-Chart (FR-1.1).

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `points` | `SparklinePoint[]` | Zeitreihe von (Datum, Wert)-Paaren: Marktwert vs. investiertes Kapital. |

**SparklinePoint**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `date` | `DateTime` | Datum des Datenpunkts. |
| `marketValue` | `decimal` | Portfolio-Marktwert an diesem Datum. |
| `investedCapital` | `decimal` | Kumuliertes investiertes Kapital an diesem Datum. |

---

### DetailedReturnMetricsDto

Detaillierte Rendite-Kennzahlen für den „Kennzahlen"-Tab (FR-2.1).

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `grossReturn` | `decimal` | Bruttoertrag vor Steuern und Gebühren. |
| `netReturn` | `decimal` | Nettoertrag nach Steuern. |
| `totalTaxes` | `decimal` | Gezahlte Steuern gesamt. |
| `totalFees` | `decimal` | Gezahlte Gebühren gesamt. |
| `taxRate` | `decimal` | Steuerquote als Bruchteil des Bruttoertrags (0–1). |
| `twr` | `decimal?` | Time-Weighted Return (Modified Dietz, GIPS-konform). ⚠️ Siehe BUG-1. |
| `volatility` | `decimal?` | Annualisierte Volatilität (Std.-Abw. der Log-Renditen × √252). |
| `maxDrawdown` | `decimal?` | Maximaler Drawdown vom Höchststand (negativer Bruchteil). |
| `sharpeRatio` | `decimal?` | Sharpe Ratio; `null` wenn Opt-in nicht aktiviert oder unzureichende Daten. |
| `realizedGains` | `decimal` | Realisierte Kursgewinne (FIFO). |
| `unrealizedGains` | `decimal` | Nicht realisierte Gewinne auf aktuelle Bestände. |
| `irr` | `decimal?` | Internal Rate of Return; `null` wenn nicht berechenbar. |
| `dividendYieldCurrentYear` | `decimal` | Dividendenrendite für das laufende Kalenderjahr. |

---

### PeriodicReturnsDto

Periodische Renditen für den „Zeitliche Entwicklung"-Tab (FR-2.2, FR-2.5).

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `annualReturns` | `AnnualReturnPoint[]` | Jährliche Renditen für das Balkendiagramm. |
| `monthlyReturns` | `MonthlyReturnPoint[]` | Monatliche Renditen für die Heatmap. |
| `annualDividends` | `AnnualDividendPoint[]` | Jährliche Dividendendaten für das Dividendendiagramm. |

**AnnualReturnPoint**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `year` | `int` | Kalenderjahr. |
| `returnPercent` | `decimal` | Jahresrendite in Prozent. |
| `isYtd` | `bool` | `true` wenn es sich um den aktuellen Year-to-Date-Wert handelt. |

**MonthlyReturnPoint**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `year` | `int` | Kalenderjahr. |
| `month` | `int` | Kalendermonat (1–12). |
| `returnPercent` | `decimal?` | Monatsrendite in Prozent; `null` wenn keine Daten vorhanden. |

**AnnualDividendPoint**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `year` | `int` | Kalenderjahr. |
| `grossDividend` | `decimal` | Brutto-Dividende. |
| `netDividend` | `decimal` | Netto-Dividende nach Steuern. |
| `cumulativeNet` | `decimal` | Kumulierte Netto-Dividende bis einschließlich dieses Jahres. |

---

### CashflowTimelineDto

Cashflow-Zeitlinie für den „Cashflows"-Tab (FR-2.3, FR-2.6).

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `entries` | `CashflowEntry[]` | Chronologische Einzelbuchungen. |
| `annualSummaries` | `AnnualCashflowSummary[]` | Jährliche Cashflow-Aggregate für das Balkendiagramm. |

**CashflowEntry**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `date` | `DateTime` | Datum des Cashflows. |
| `type` | `string` | Cashflow-Typ: `Buy`, `Sell`, `Dividend`, `Tax`, `Fee`. |
| `amount` | `decimal` | Betrag (negativ = Abfluss, positiv = Zufluss). |
| `description` | `string?` | Optionale Beschreibung. |
| `postingId` | `guid` | Referenz auf die Quell-Buchung. |

**AnnualCashflowSummary**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `year` | `int` | Kalenderjahr. |
| `totalBuys` | `decimal` | Summe der Kaufbeträge (negativ). |
| `totalSells` | `decimal` | Summe der Verkaufsbeträge (positiv). |
| `totalDividends` | `decimal` | Summe der Brutto-Dividenden. |
| `totalTaxes` | `decimal` | Summe der Steuern (negativ). |
| `totalFees` | `decimal` | Summe der Gebühren (negativ). |

---

### PerformanceChartDataDto

Performance-Diagrammdaten für den „Übersicht"-Tab (FR-2.4).

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `timeRange` | `ChartTimeRange` | Gewählter Zeitbereich (Echo des Query-Parameters). |
| `portfolioValues` | `ChartPoint[]` | Marktwert-Zeitreihe des Portfolios. |
| `investedCapitalValues` | `ChartPoint[]` | Investiertes-Kapital-Zeitreihe. |

**ChartPoint**

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `date` | `DateTime` | Datum des Datenpunkts. |
| `value` | `decimal` | Wert an diesem Datum. |

**ChartTimeRange (Enum)**

| Wert | Beschreibung |
|------|--------------|
| `OneMonth` | Letzter Monat. |
| `ThreeMonths` | Letzte 3 Monate. |
| `SixMonths` | Letzte 6 Monate. |
| `OneYear` | Letztes Jahr. |
| `ThreeYears` | Letzte 3 Jahre. |
| `All` | Gesamter verfügbarer Zeitraum (Standard). |

---

### BenchmarkComparisonDto

Benchmark-Vergleichsdaten für den „Benchmark"-Tab (FR-7). Beide Zeitreihen sind auf Basis 100 normalisiert.

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `benchmarkSecurityId` | `guid` | ID des Benchmark-Wertpapiers. |
| `benchmarkName` | `string` | Anzeigename des Benchmark-Wertpapiers. |
| `securityNormalizedValues` | `ChartPoint[]` | Normalisierte Werte des Ziel-Wertpapiers (Basis 100). |
| `benchmarkNormalizedValues` | `ChartPoint[]` | Normalisierte Werte des Benchmarks (Basis 100). |

---

### ReturnAnalysisSettingsDto

Renditeanalyse-Einstellungen des Benutzers.

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `benchmarkSecurityId` | `guid?` | ID des konfigurierten Benchmark-Wertpapiers; `null` wenn nicht gesetzt. |
| `benchmarkSecurityName` | `string?` | Anzeigename des Benchmarks; `null` wenn nicht konfiguriert. |
| `showSharpeRatio` | `bool` | Gibt an, ob die Sharpe Ratio angezeigt wird. |
| `riskFreeRate` | `decimal` | Risikoloser Zinssatz für die Sharpe-Berechnung (z. B. `0.04` = 4 %). |

---

### ReturnAnalysisSettingsRequest

Request-Payload für [PUT /api/securities/return-analysis/settings](#put-apisecuritiesreturn-analysissettings).

| Feld | Typ | Pflicht | Validierung | Beschreibung |
|------|-----|---------|-------------|--------------|
| `benchmarkSecurityId` | `guid?` | Optional | – | Benchmark-ID; `null` löscht den Benchmark. |
| `showSharpeRatio` | `bool` | *(required)* | – | Aktiviert/deaktiviert die Sharpe-Ratio-Anzeige. |
| `riskFreeRate` | `decimal` | *(required)* | `>= 0` | Risikoloser Zinssatz (z. B. `0.04` = 4 %). |
