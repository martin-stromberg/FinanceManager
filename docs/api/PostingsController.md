# PostingsController

Path: `FinanceManager.Web.Controllers.PostingsController`

**Base route:** `api/postings`  
**Authentication:** JWT Bearer token required for all endpoints.

## Overview

Provides endpoints for querying, exporting and reversing financial postings across accounts, contacts, savings plans and securities. Also exposes aggregate time-series endpoints mounted under the respective resource routes.

---

## Table of Contents

1. [Get Posting by ID](#1-get-posting-by-id)
2. [Reverse a Posting](#2-reverse-a-posting)
3. [Validate Posting Reversal](#3-validate-posting-reversal)
4. [List Postings – Account](#4-list-postings--account)
5. [List Postings – Contact](#5-list-postings--contact)
6. [List Postings – Savings Plan](#6-list-postings--savings-plan)
7. [List Postings – Security](#7-list-postings--security)
8. [Get Group Links](#8-get-group-links)
9. [Export Postings – Account](#9-export-postings--account)
10. [Export Postings – Contact](#10-export-postings--contact)
11. [Export Postings – Savings Plan](#11-export-postings--savings-plan)
12. [Export Postings – Security](#12-export-postings--security)
13. [Aggregate Time Series](#13-aggregate-time-series)

---

## 1. Get Posting by ID

Returns a single posting including linked posting metadata and group bank-posting metadata.

**`GET /api/postings/{id}`**

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | uuid | ✓ | Posting identifier |

### Response (200 OK)

Returns a [`PostingServiceDto`](./models.md#postingservicedto-response).

```json
{
  "id": "a1b2c3d4-0000-0000-0000-000000000001",
  "bookingDate": "2025-03-15T00:00:00Z",
  "valutaDate": "2025-03-15T00:00:00Z",
  "amount": -250.00,
  "kind": 0,
  "accountId": "550e8400-e29b-41d4-a716-446655440000",
  "contactId": null,
  "savingsPlanId": null,
  "securityId": null,
  "sourceId": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee",
  "subject": "Monthly rent",
  "recipientName": "Landlord GmbH",
  "description": "SEPA direct debit",
  "securitySubType": null,
  "quantity": null,
  "groupId": "00000000-0000-0000-0000-000000000000",
  "linkedPostingId": null,
  "linkedPostingKind": null,
  "linkedPostingAccountId": null,
  "linkedPostingAccountSymbolAttachmentId": null,
  "linkedPostingAccountName": null,
  "bankPostingAccountId": null,
  "bankPostingAccountSymbolAttachmentId": null,
  "bankPostingAccountName": null,
  "isReversed": false,
  "isReversal": false,
  "reversedByPostingId": null,
  "reversalForPostingId": null
}
```

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Posting found and returned |
| `404 Not Found` | Posting not found or not owned by the current user |

### Example

```bash
curl -X GET https://api.financemanager.local/api/postings/a1b2c3d4-0000-0000-0000-000000000001 \
  --cookie "FinanceManager.Auth=<token>"
```

---

## 2. Reverse a Posting

Creates a counter-posting (reversal) with a negated amount for the specified posting. All postings in the same booking group are reversed together. A new statement import is created for reconciliation purposes.

**`POST /api/postings/{id}/reverse`**

> ⚠️ **This operation is irreversible.** Once a posting is reversed, neither the original nor the reversal posting can be reversed again.

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | uuid | ✓ | ID of the posting to reverse |

### Request Body

None. No request body is required.

### Response (200 OK)

Returns a [`ReversalResultDto`](./models.md#reversalresultdto-response).

```json
{
  "reversedPostingIds": [
    "a1b2c3d4-0000-0000-0000-000000000001",
    "a1b2c3d4-0000-0000-0000-000000000002"
  ],
  "createdReversalIds": [
    "bbbbbbbb-0000-0000-0000-000000000001",
    "bbbbbbbb-0000-0000-0000-000000000002"
  ],
  "statementImportId": "cccccccc-0000-0000-0000-000000000001"
}
```

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Reversal successful |
| `400 Bad Request` | Posting not found, is a reversal itself, or cannot be reversed for other business reasons |
| `403 Forbidden` | Current user is not authorized to reverse this posting |
| `409 Conflict` | Posting has already been reversed |

### Error Examples

**400 – Cannot reverse a reversal posting:**
```json
{
  "title": "Bad Request",
  "detail": "A reversal posting cannot itself be reversed.",
  "status": 400
}
```

**403 – Unauthorized:**
```json
{
  "title": "Forbidden",
  "detail": "You are not authorized to reverse this posting.",
  "status": 403
}
```

**409 – Already reversed:**
```json
{
  "title": "Conflict",
  "detail": "This posting has already been reversed.",
  "status": 409
}
```

### Example

```bash
curl -X POST https://api.financemanager.local/api/postings/a1b2c3d4-0000-0000-0000-000000000001/reverse \
  --cookie "FinanceManager.Auth=<token>"
```

---

## 3. Validate Posting Reversal

Checks whether a posting can be reversed without actually performing the reversal. Use this endpoint to pre-validate before showing the user a confirmation UI.

**`GET /api/postings/{id}/validate-reversal`**

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `id` | uuid | ✓ | ID of the posting to validate |

### Response (200 OK)

Returns a [`ReversalValidationDto`](./models.md#reversalvalidationdto-response).

**Valid (can be reversed):**
```json
{
  "isValid": true,
  "errors": []
}
```

**Invalid (cannot be reversed):**
```json
{
  "isValid": false,
  "errors": [
    "This posting has already been reversed.",
    "A reversal posting cannot itself be reversed."
  ]
}
```

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Validation result returned (always succeeds; check `isValid` field) |

### Notes

- This endpoint always returns `200 OK`. Check the `isValid` property and `errors` list to determine whether the reversal can proceed.
- If `isValid` is `false`, calling `POST .../reverse` will return `400` or `409`.

### Example

```bash
curl -X GET https://api.financemanager.local/api/postings/a1b2c3d4-0000-0000-0000-000000000001/validate-reversal \
  --cookie "FinanceManager.Auth=<token>"
```

---

## 4. List Postings – Account

Returns paginated postings for a specific bank account.

**`GET /api/postings/account/{accountId}`**

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `accountId` | uuid | ✓ | Account identifier |

### Query Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `skip` | int | – | `0` | Number of records to skip (pagination offset) |
| `take` | int | – | `50` | Number of records to return (max 250) |
| `q` | string | – | – | Full-text search query |
| `from` | datetime | – | – | Earliest booking date (inclusive) |
| `to` | datetime | – | – | Latest booking date (inclusive) |

### Response (200 OK)

Array of [`PostingServiceDto`](./models.md#postingservicedto-response).

```json
[
  {
    "id": "a1b2c3d4-0000-0000-0000-000000000001",
    "bookingDate": "2025-03-15T00:00:00Z",
    "amount": -250.00,
    "kind": 0,
    "accountId": "550e8400-e29b-41d4-a716-446655440000",
    "isReversed": false,
    "isReversal": false,
    "reversedByPostingId": null,
    "reversalForPostingId": null
  }
]
```

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Postings returned (may be empty array) |
| `404 Not Found` | Account not found or not owned by current user |

---

## 5. List Postings – Contact

Returns paginated postings associated with a contact.

**`GET /api/postings/contact/{contactId}`**

### Path & Query Parameters

Same as [List Postings – Account](#4-list-postings--account) but with `contactId` as path parameter.

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Postings returned |
| `404 Not Found` | Contact not found or not owned by current user |

---

## 6. List Postings – Savings Plan

Returns paginated postings associated with a savings plan.

**`GET /api/postings/savings-plan/{planId}`**

### Path & Query Parameters

Same as [List Postings – Account](#4-list-postings--account) but with `planId` as path parameter.

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Postings returned |
| `404 Not Found` | Savings plan not found or not owned by current user |

---

## 7. List Postings – Security

Returns paginated postings associated with a security (stock, bond, etc.).

**`GET /api/postings/security/{securityId}`**

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `securityId` | uuid | ✓ | Security identifier |

### Query Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `skip` | int | – | `0` | Pagination offset |
| `take` | int | – | `50` | Records to return (max 250) |
| `from` | datetime | – | – | Earliest date filter |
| `to` | datetime | – | – | Latest date filter |

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Postings returned |
| `404 Not Found` | Security not found or not owned by current user |

---

## 8. Get Group Links

Returns the first linked entity IDs (account, contact, savings plan, security) for all postings belonging to a given group.

**`GET /api/postings/group/{groupId}`**

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `groupId` | uuid | ✓ | Group identifier (must not be empty GUID) |

### Response (200 OK)

```json
{
  "accountId": "550e8400-e29b-41d4-a716-446655440000",
  "contactId": null,
  "savingsPlanId": null,
  "securityId": null
}
```

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Group links returned |
| `400 Bad Request` | `groupId` is the empty GUID |
| `404 Not Found` | No owned posting found in the group |

---

## 9. Export Postings – Account

Exports account postings as a CSV or XLSX file download.

**`GET /api/postings/account/{accountId}/export`**

### Path Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `accountId` | uuid | ✓ | Account identifier |

### Query Parameters

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `format` | string | – | `csv` | Export format: `csv` or `xlsx` |
| `from` | datetime | – | – | Earliest date filter |
| `to` | datetime | – | – | Latest date filter |
| `q` | string | – | – | Full-text filter |

### Response (200 OK)

Binary file download (`text/csv` or `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`).

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | File download |
| `400 Bad Request` | Invalid format or row limit exceeded (`Exports:MaxRows` config, default 50 000) |
| `404 Not Found` | Account not found or not owned by current user |

---

## 10. Export Postings – Contact

**`GET /api/postings/contact/{contactId}/export`**

Same parameters and behaviour as [Export Postings – Account](#9-export-postings--account).

---

## 11. Export Postings – Savings Plan

**`GET /api/postings/savings-plan/{planId}/export`**

Same parameters and behaviour as [Export Postings – Account](#9-export-postings--account).

---

## 12. Export Postings – Security

**`GET /api/postings/security/{securityId}/export`**

Same parameters and behaviour as [Export Postings – Account](#9-export-postings--account).

---

## 13. Aggregate Time Series

Returns aggregate (sum) data grouped by time period for charting. Endpoints are mounted under the respective resource routes.

| Endpoint | Description |
|----------|-------------|
| `GET /api/accounts/{accountId}/aggregates` | Aggregates for a single account |
| `GET /api/accounts/aggregates` | Aggregates across all accounts |
| `GET /api/contacts/{contactId}/aggregates` | Aggregates for a single contact |
| `GET /api/savings-plans/{planId}/aggregates` | Aggregates for a single savings plan |
| `GET /api/savings-plans/aggregates` | Aggregates across all savings plans |

### Query Parameters (all aggregate endpoints)

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `period` | string | – | `Month` | Grouping period: `Month`, `Quarter`, `HalfYear`, `Year` |
| `take` | int | – | `36` | Number of periods to return (max 200) |
| `maxYearsBack` | int | – | – | Limit history to N years back (1–10) |

### Response (200 OK)

```json
[
  {
    "periodStart": "2025-01-01T00:00:00Z",
    "amount": -1234.56
  },
  {
    "periodStart": "2025-02-01T00:00:00Z",
    "amount": 567.89
  }
]
```

### Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Aggregate data returned |
| `404 Not Found` | Entity not found (entity-specific endpoints only) |

---

## Notes

- All list endpoints return **at most 250 records** per request (`MaxTake = 250`). Use `skip` / `take` for pagination.
- Date parameters accept ISO 8601 (`yyyy-MM-dd`) and German notation (`dd.MM.yyyy`).
- Postings are always **user-scoped**: only postings belonging to the authenticated user's accounts, contacts, savings plans or securities are returned.
- The **reversal** endpoints (`/reverse`, `/validate-reversal`) operate on the full posting group — when one posting in a group is reversed, all postings in that group are reversed together.
