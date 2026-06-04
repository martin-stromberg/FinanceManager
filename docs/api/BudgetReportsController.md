# BudgetReportsController

Pfad: `FinanceManager.Web.Controllers.BudgetReportsController`  
Route-Basis: `/api/budget/report`

## Übersicht

Der Controller liefert Budgetberichte, Monats-KPI und Export.  
`PurposePattern`/`UseRegex` aus Budget-Regeln beeinflussen die Zuordnung von Buchungen im Bericht.

## HTTP-Methode & Pfad

- `POST /api/budget/report`
- `GET /api/budget/report/kpi-monthly`
- `GET /api/budget/report/export`
- `POST /api/budget/report/cache/reset`

## Authentifizierung

Bearer Token erforderlich.

## Request

### Header

- `Authorization: Bearer <token>`
- `Content-Type: application/json` (für `POST /api/budget/report`)

### `POST /api/budget/report` Body

```json
{
  "asOfDate": "2026-01-31",
  "months": 12,
  "dateBasis": "BookingDate"
}
```

## Response

### Erfolgsfall

- `POST /api/budget/report`: `200 OK` mit `BudgetReportDto`
- `GET /kpi-monthly`: `200 OK` mit `MonthlyBudgetKpiDto`
- `GET /export`: `200 OK` mit Datei-Stream
- `POST /cache/reset`: `204 NoContent`

Beispiel (`POST /api/budget/report`, gekürzt):

```json
{
  "asOfDate": "2026-01-31",
  "months": 12,
  "periods": [
    {
      "year": 2026,
      "month": 1,
      "planned": -60.0,
      "actual": -60.0,
      "deviation": 0.0
    }
  ],
  "rawData": {
    "unbudgetedPostings": []
  }
}
```

### Fehlerfälle

- `400 BadRequest` bei ungültigen Parametern (z. B. `months` außerhalb 1..60)
- `500 InternalServerError` bei unerwartetem Fehler

## Matching-Verhalten (PurposePattern / UseRegex)

Das Matching wird in der Report-Ermittlung angewendet:

- Kein `PurposePattern` (oder nur whitespace) ⇒ Regel matcht ohne zusätzliche Text-Einschränkung.
- `UseRegex=false` ⇒ case-insensitive `contains` (`IndexOf(..., OrdinalIgnoreCase)`).
- `UseRegex=true` ⇒ `Regex.IsMatch` mit  
  `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant` und `TimeSpan.FromMilliseconds(200)`.
- Laufzeitfehler (`ArgumentException`, `RegexMatchTimeoutException`) werden als **kein Match** behandelt; die API antwortet weiterhin erfolgreich.

Auswirkung:

- Treffende Buchungen fließen in Purpose/Category-`actual`.
- Nicht treffende Buchungen bleiben in `unbudgetedPostings`.

## Beispiel (curl)

```bash
curl -X POST "https://localhost:5001/api/budget/report" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d "{\"asOfDate\":\"2026-01-31\",\"months\":12,\"dateBasis\":\"BookingDate\"}"
```

## Referenzen

- Abhängige Regel-API: [BudgetRulesController](./BudgetRulesController.md)
- Auswirkungen auf Draft-Buchung: [StatementDraftsController](./StatementDraftsController.md)
- Migration: `FinanceManager.Infrastructure/Migrations/20260604172812_202606041500_AddBudgetRulePurposePattern.cs`
- Tests:
  - `FinanceManager.Tests/Infrastructure/Budget/BudgetReportServiceRawDataTests.cs`
  - `FinanceManager.Tests.Integration/ApiClient/ApiClientBudgetReportUnbudgetedMirrorTests.cs`