# BudgetRulesController

Pfad: `FinanceManager.Web.Controllers.BudgetRulesController`  
Route-Basis: `/api/budget/rules`

## Übersicht

Der Controller verwaltet Budget-Regeln und ist der API-Einstiegspunkt für `PurposePattern` und `UseRegex`.  
Die Felder steuern, welche Buchungstexte bei Budgetzuordnung als Treffer gelten.

## HTTP-Methode & Pfad

- `GET /api/budget/rules/{id}`
- `GET /api/budget/rules/by-purpose/{budgetPurposeId}`
- `GET /api/budget/rules/by-category/{budgetCategoryId}`
- `POST /api/budget/rules`
- `PUT /api/budget/rules/{id}`
- `DELETE /api/budget/rules/{id}`

## Authentifizierung

Bearer Token erforderlich (`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`).

## Request

### Header

- `Authorization: Bearer <token>`
- `Content-Type: application/json` (für `POST`/`PUT`)

### Body (`POST /api/budget/rules`)

```json
{
  "budgetPurposeId": "11111111-1111-1111-1111-111111111111",
  "budgetCategoryId": null,
  "amount": -60.0,
  "interval": "Monthly",
  "customIntervalMonths": null,
  "startDate": "2026-01-01",
  "endDate": null,
  "purposePattern": "ST\\d{10}",
  "useRegex": true
}
```

### Body (`PUT /api/budget/rules/{id}`)

```json
{
  "amount": -60.0,
  "interval": "Monthly",
  "customIntervalMonths": null,
  "startDate": "2026-01-01",
  "endDate": null,
  "purposePattern": "vertragsnummer abc123",
  "useRegex": false
}
```

### DTO-Änderungen (relevant)

- `BudgetRuleCreateRequest.PurposePattern` *(optional, max 500)*
- `BudgetRuleCreateRequest.UseRegex` *(optional, default false)*
- `BudgetRuleUpdateRequest.PurposePattern` *(optional, max 500)*
- `BudgetRuleUpdateRequest.UseRegex` *(optional, default false)*
- `BudgetRuleDto.PurposePattern`
- `BudgetRuleDto.UseRegex`

## Response

### Erfolgsfall

- `POST`: `201 Created` + `BudgetRuleDto`
- `PUT`: `204 NoContent`
- `GET`: `200 OK` + `BudgetRuleDto`/Liste
- `DELETE`: `204 NoContent`

Beispiel `BudgetRuleDto`:

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "ownerUserId": "33333333-3333-3333-3333-333333333333",
  "budgetPurposeId": "11111111-1111-1111-1111-111111111111",
  "budgetCategoryId": null,
  "amount": -60.0,
  "interval": "Monthly",
  "customIntervalMonths": null,
  "startDate": "2026-01-01",
  "endDate": null,
  "purposePattern": "ST\\d{10}",
  "useRegex": true
}
```

### Fehlerfälle

- `400 BadRequest` bei ungültigem Request (z. B. falsche Kombination `budgetPurposeId`/`budgetCategoryId`)
- `400 BadRequest` als `ValidationProblem` bei ungültigem Regex
- `404 NotFound` bei nicht vorhandener Rule
- `500 InternalServerError` bei unerwartetem Fehler

Regex-Validierungsfehler (`ValidationProblem`, Status 400):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PurposePattern": [
      "Ungültiger regulärer Ausdruck: Invalid regular expression"
    ]
  }
}
```

## Validierung (PurposePattern / UseRegex)

- `PurposePattern` wird getrimmt; leer/whitespace setzt Pattern intern auf `null` und `UseRegex=false`.
- Maximale Länge: 500 Zeichen.
- Wenn `UseRegex=true`, erfolgt **nur syntaktische Prüfung** durch `new Regex(trimmedPattern)`.
- Kein semantischer Match-Test bei Create/Update.
- Bei `ArgumentException` mit Parameter `pattern`/`PurposePattern` liefert der Controller `ValidationProblem(ModelState)` mit HTTP 400.

## Matching-Verhalten auf API-Ebene

Die hier gespeicherten Felder wirken in:

- [BudgetReportsController](./BudgetReportsController.md): Zuordnung von Buchungen in Berichten (`actual` vs `unbudgeted`).
- [StatementDraftsController](./StatementDraftsController.md): BudgetImpact-Hinweise und `BudgetImpactSummary` beim Buchen.

## Beispiel (curl)

### Regel mit Regex anlegen

```bash
curl -X POST "https://localhost:5001/api/budget/rules" \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d "{\"budgetPurposeId\":\"11111111-1111-1111-1111-111111111111\",\"budgetCategoryId\":null,\"amount\":-60.0,\"interval\":\"Monthly\",\"customIntervalMonths\":null,\"startDate\":\"2026-01-01\",\"endDate\":null,\"purposePattern\":\"ST\\\\d{10}\",\"useRegex\":true}"
```

### Antwort (201)

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "ownerUserId": "33333333-3333-3333-3333-333333333333",
  "budgetPurposeId": "11111111-1111-1111-1111-111111111111",
  "budgetCategoryId": null,
  "amount": -60.0,
  "interval": "Monthly",
  "customIntervalMonths": null,
  "startDate": "2026-01-01",
  "endDate": null,
  "purposePattern": "ST\\d{10}",
  "useRegex": true
}
```

## Referenzen

- Migration: `FinanceManager.Infrastructure/Migrations/20260604172812_202606041500_AddBudgetRulePurposePattern.cs`
- Tests:
  - `FinanceManager.Tests/Controllers/BudgetRulesControllerTests.cs`
  - `FinanceManager.Tests/Budget/BudgetCrudServicesTests.cs`