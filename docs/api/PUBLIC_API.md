# FinanceManager Public API Documentation

## Overview

The FinanceManager API is a REST API built with ASP.NET Core that provides comprehensive financial management functionality including accounts, contacts, postings, budgets, securities, savings plans, and more.

**Base URL:** `https://api.financemanager.local/api`

**Authentication:** JWT Bearer token (set via `FinanceManager.Auth` HTTP-only cookie)

**API Format:** JSON

---

## Table of Contents

1. [Authentication](#authentication)
2. [API Endpoints by Resource](#api-endpoints-by-resource)
3. [Common Response Formats](#common-response-formats)
4. [Error Handling](#error-handling)

---

## Authentication

### Login

Register a new user account or authenticate with existing credentials.

**Endpoint:** `POST /auth/login`

**Request Body:**
```json
{
  "username": "string",
  "password": "string",
  "preferredLanguage": "string (optional, e.g., 'de', 'en')",
  "timeZoneId": "string (optional)"
}
```

**Response (200 OK):**
```json
{
  "username": "string",
  "isAdmin": "boolean",
  "expiresUtc": "datetime"
}
```

**Status Codes:**
- `200 OK` - Authentication successful
- `400 Bad Request` - Invalid request format
- `401 Unauthorized` - Invalid credentials

---

### Register

Register a new user account and automatically authenticate.

**Endpoint:** `POST /auth/register`

**Request Body:**
```json
{
  "username": "string",
  "password": "string",
  "preferredLanguage": "string (optional)",
  "timeZoneId": "string (optional)"
}
```

**Response (200 OK):**
```json
{
  "username": "string",
  "isAdmin": "boolean",
  "expiresUtc": "datetime"
}
```

**Status Codes:**
- `200 OK` - Registration successful
- `400 Bad Request` - Invalid request format
- `409 Conflict` - User already exists

---

### Logout

Clear authentication cookie and invalidate session.

**Endpoint:** `POST /auth/logout`

**Authentication:** Required (JWT Bearer)

**Response (200 OK):** Empty

**Status Codes:**
- `200 OK` - Logout successful

---

## API Endpoints by Resource

### Accounts

Manage bank accounts owned by the current user.

#### List Accounts

**Endpoint:** `GET /accounts`

**Authentication:** Required

**Query Parameters:**
- `skip` (integer, default: 0) - Number of items to skip for paging
- `take` (integer, default: 100, max: 200) - Maximum number of items to return
- `bankContactId` (uuid, optional) - Filter accounts by bank contact ID

**Response (200 OK):**
```json
[
  {
    "id": "uuid",
    "name": "string",
    "type": "string (Checking|Savings|MoneyMarket|CreditCard|Brokerage)",
    "iban": "string (optional)",
    "bankContactId": "uuid",
    "bankContact": {
      "id": "uuid",
      "name": "string",
      "type": "string"
    },
    "symbolAttachmentId": "uuid (optional)",
    "savingsPlanExpectation": "decimal (optional)",
    "securityProcessingEnabled": "boolean"
  }
]
```

**Status Codes:**
- `200 OK` - Success
- `500 Internal Server Error` - Unexpected server error

---

#### Get Account

**Endpoint:** `GET /accounts/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Account identifier

**Response (200 OK):**
```json
{
  "id": "uuid",
  "name": "string",
  "type": "string",
  "iban": "string (optional)",
  "bankContactId": "uuid",
  "bankContact": {
    "id": "uuid",
    "name": "string"
  },
  "symbolAttachmentId": "uuid (optional)",
  "savingsPlanExpectation": "decimal (optional)",
  "securityProcessingEnabled": "boolean"
}
```

**Status Codes:**
- `200 OK` - Account found
- `404 Not Found` - Account not found or not owned by current user

---

#### Create Account

Create a new account for the current user. Either provide an existing bank contact ID, a new bank contact name, or neither (auto-creates bank contact).

**Endpoint:** `POST /accounts`

**Authentication:** Required

**Request Body:**
```json
{
  "name": "string (required, max 200 chars)",
  "type": "string (required: Checking|Savings|MoneyMarket|CreditCard|Brokerage)",
  "iban": "string (optional, max 34 chars)",
  "bankContactId": "uuid (optional)",
  "newBankContactName": "string (optional)",
  "savingsPlanExpectation": "decimal (optional)",
  "securityProcessingEnabled": "boolean (optional, default: false)",
  "symbolAttachmentId": "uuid (optional)"
}
```

**Response (201 Created):**
```json
{
  "id": "uuid",
  "name": "string",
  "type": "string",
  "iban": "string (optional)",
  "bankContactId": "uuid",
  "bankContact": {
    "id": "uuid",
    "name": "string"
  },
  "symbolAttachmentId": "uuid (optional)",
  "savingsPlanExpectation": "decimal (optional)",
  "securityProcessingEnabled": "boolean"
}
```

**Status Codes:**
- `201 Created` - Account created successfully
- `400 Bad Request` - Invalid input
- `500 Internal Server Error` - Unexpected server error

---

#### Update Account

**Endpoint:** `PUT /accounts/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Account identifier

**Request Body:**
```json
{
  "name": "string (required)",
  "iban": "string (optional)",
  "bankContactId": "uuid (optional)",
  "newBankContactName": "string (optional)",
  "savingsPlanExpectation": "decimal (optional)",
  "securityProcessingEnabled": "boolean (optional)",
  "symbolAttachmentId": "uuid (optional)"
}
```

**Response (200 OK):** Updated account DTO

**Status Codes:**
- `200 OK` - Account updated
- `400 Bad Request` - Invalid input
- `404 Not Found` - Account not found or not owned by current user

---

#### Delete Account

**Endpoint:** `DELETE /accounts/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Account identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Account deleted
- `404 Not Found` - Account not found

---

#### Set Account Symbol

Assign an existing attachment as the account's symbol/icon.

**Endpoint:** `POST /accounts/{id}/symbol/{attachmentId}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Account identifier
- `attachmentId` (uuid, required) - Attachment identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Symbol assigned
- `404 Not Found` - Account or attachment not found

---

#### Clear Account Symbol

**Endpoint:** `DELETE /accounts/{id}/symbol`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Account identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Symbol cleared
- `404 Not Found` - Account not found

---

### Contacts

Manage contacts (persons, companies, banks, etc.) for the current user.

#### List Contacts

**Endpoint:** `GET /contacts`

**Authentication:** Required

**Query Parameters:**
- `skip` (integer, default: 0) - Items to skip
- `take` (integer, default: 100) - Maximum items to return
- `type` (string, optional) - Filter by contact type (Person|Company|Bank|Other)
- `all` (boolean, optional) - If true, returns all contacts ignoring paging
- `nameFilter` (string, optional) - Substring filter on contact name
- `ct` (cancellation token) - Cancellation token

**Response (200 OK):**
```json
[
  {
    "id": "uuid",
    "name": "string",
    "type": "string",
    "email": "string (optional)",
    "phone": "string (optional)",
    "symbolAttachmentId": "uuid (optional)"
  }
]
```

**Status Codes:**
- `200 OK` - Success

---

#### Get Contact

**Endpoint:** `GET /contacts/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Contact identifier

**Response (200 OK):** Contact DTO

**Status Codes:**
- `200 OK` - Contact found
- `404 Not Found` - Contact not found

---

#### Create Contact

**Endpoint:** `POST /contacts`

**Authentication:** Required

**Request Body:**
```json
{
  "name": "string (required)",
  "type": "string (required: Person|Company|Bank|Other)",
  "categoryId": "uuid (optional)",
  "description": "string (optional)",
  "isPaymentIntermediary": "boolean (optional)",
  "parent": {
    "parentKind": "string (optional, e.g. statement-drafts/entries)",
    "parentId": "uuid (required when parent is provided)",
    "field": "string (optional, e.g. ContactId)"
  }
}
```

**Response (201 Created):** Contact DTO

**Status Codes:**
- `201 Created` - Contact created
- `400 Bad Request` - Invalid input
- `409 Conflict` - Parent assignment failed (`Err_Conflict_ParentAssignment`)

**Parent assignment contract (when `parent` is provided):**
- The server creates the contact and then tries to assign it to the requested parent context.
- For `parentKind=statement-drafts/entries` and `field=ContactId`, the created contact is assigned to the target statement draft entry.
- If assignment fails, the server attempts a rollback delete of the just-created contact and returns `409 Conflict`.

**Conflict payload example:**
```json
{
  "code": "Err_Conflict_ParentAssignment",
  "message": "Contact creation could not be completed because assignment to the selected statement entry failed."
}
```

**Idempotency note:**
- `POST /contacts` itself is not idempotent.
- Internal parent assignment is idempotent for already assigned `(parentId, contactId)` combinations.

---

#### Update Contact

**Endpoint:** `PUT /contacts/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Contact identifier

**Request Body:** Same as Create

**Response (200 OK):** Updated contact DTO

**Status Codes:**
- `200 OK` - Contact updated
- `404 Not Found` - Contact not found

---

#### Delete Contact

**Endpoint:** `DELETE /contacts/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Contact identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Contact deleted
- `404 Not Found` - Contact not found

---

### Postings

Query, filter, export, and reverse financial postings across all accounts, contacts, and securities.

#### Get Single Posting

**Endpoint:** `GET /postings/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Posting identifier

**Response (200 OK):**
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

**Status Codes:**
- `200 OK` - Posting found
- `404 Not Found` - Posting not found or not owned by current user

---

#### Validate Posting Reversal

Checks whether a posting can be reversed. Call this before showing a confirmation UI.

**Endpoint:** `GET /postings/{id}/validate-reversal`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Posting identifier

**Response (200 OK):**
```json
{
  "isValid": true,
  "errors": []
}
```

**Response (200 OK) – when reversal is not possible:**
```json
{
  "isValid": false,
  "errors": [
    "This posting has already been reversed."
  ]
}
```

**Status Codes:**
- `200 OK` - Validation result returned (always; check `isValid` field)

---

#### Reverse a Posting

Creates a counter-posting with a negated amount, effectively cancelling the original posting. All postings in the same booking group are reversed together. A new statement import is created for reconciliation.

> ⚠️ **Irreversible operation.** Once reversed, neither the original nor the reversal posting can be reversed again.

**Endpoint:** `POST /postings/{id}/reverse`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - ID of the posting to reverse

**Request Body:** None

**Response (200 OK):**
```json
{
  "reversedPostingIds": [
    "a1b2c3d4-0000-0000-0000-000000000001"
  ],
  "createdReversalIds": [
    "bbbbbbbb-0000-0000-0000-000000000001"
  ],
  "statementImportId": "cccccccc-0000-0000-0000-000000000001"
}
```

**Status Codes:**
- `200 OK` - Reversal successful
- `400 Bad Request` - Posting not found or cannot be reversed (e.g. it is already a reversal posting itself)
- `403 Forbidden` - Current user is not authorized to reverse this posting
- `409 Conflict` - Posting has already been reversed

**Error Examples:**

`403 Forbidden`:
```json
{
  "title": "Forbidden",
  "detail": "You are not authorized to reverse this posting.",
  "status": 403
}
```

`409 Conflict`:
```json
{
  "title": "Conflict",
  "detail": "This posting has already been reversed.",
  "status": 409
}
```

`400 Bad Request`:
```json
{
  "title": "Bad Request",
  "detail": "A reversal posting cannot itself be reversed.",
  "status": 400
}
```

---

### Budget Management

Manage budget categories, purposes, rules, and overrides.

#### Budget Categories - List

**Endpoint:** `GET /budget/categories`

**Authentication:** Required

**Query Parameters:**
- `from` (date, optional) - Start date filter
- `to` (date, optional) - End date filter

**Response (200 OK):**
```json
[
  {
    "id": "uuid",
    "name": "string",
    "color": "string",
    "parent": {
      "id": "uuid",
      "name": "string"
    }
  }
]
```

**Status Codes:**
- `200 OK` - Success
- `500 Internal Server Error` - Unexpected error

---

#### Budget Categories - Get

**Endpoint:** `GET /budget/categories/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Category identifier

**Response (200 OK):** Category DTO

**Status Codes:**
- `200 OK` - Category found
- `404 Not Found` - Category not found

---

#### Budget Categories - Create

**Endpoint:** `POST /budget/categories`

**Authentication:** Required

**Request Body:**
```json
{
  "name": "string (required)",
  "parent": {
    "kind": "string (optional)",
    "id": "uuid (optional)"
  }
}
```

**Response (201 Created):** Category DTO

**Status Codes:**
- `201 Created` - Category created
- `400 Bad Request` - Invalid input

---

#### Budget Categories - Update

**Endpoint:** `PUT /budget/categories/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Category identifier

**Request Body:**
```json
{
  "name": "string (required)"
}
```

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Category updated
- `404 Not Found` - Category not found

---

#### Budget Categories - Delete

**Endpoint:** `DELETE /budget/categories/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Category identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Category deleted
- `404 Not Found` - Category not found

---

### Securities

Manage securities (stocks, bonds, etc.) and their price history.

#### List Securities

**Endpoint:** `GET /securities`

**Authentication:** Required

**Query Parameters:**
- `skip` (integer, default: 0) - Items to skip
- `take` (integer, default: 100) - Maximum items to return

**Response (200 OK):** List of security DTOs

**Status Codes:**
- `200 OK` - Success

---

#### Get Security

**Endpoint:** `GET /securities/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Security identifier

**Response (200 OK):** Security DTO

**Status Codes:**
- `200 OK` - Security found
- `404 Not Found` - Security not found

---

#### Create Security

**Endpoint:** `POST /securities`

**Authentication:** Required

**Request Body:**
```json
{
  "name": "string (required)",
  "isin": "string (optional)",
  "symbolAttachmentId": "uuid (optional)"
}
```

**Response (201 Created):** Security DTO

**Status Codes:**
- `201 Created` - Security created
- `400 Bad Request` - Invalid input

---

#### Update Security

**Endpoint:** `PUT /securities/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Security identifier

**Request Body:**
```json
{
  "name": "string (required)",
  "isin": "string (optional)",
  "symbolAttachmentId": "uuid (optional)"
}
```

**Response (200 OK):** Updated security DTO

**Status Codes:**
- `200 OK` - Security updated
- `404 Not Found` - Security not found

---

#### Delete Security

**Endpoint:** `DELETE /securities/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Security identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Security deleted
- `404 Not Found` - Security not found

---

### Savings Plans

Manage savings plans and their categories.

#### List Savings Plans

**Endpoint:** `GET /savings-plans`

**Authentication:** Required

**Query Parameters:**
- `skip` (integer) - Items to skip
- `take` (integer) - Maximum items to return

**Response (200 OK):** List of savings plan DTOs

**Status Codes:**
- `200 OK` - Success

---

#### Get Savings Plan

**Endpoint:** `GET /savings-plans/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Savings plan identifier

**Response (200 OK):** Savings plan DTO

**Status Codes:**
- `200 OK` - Savings plan found
- `404 Not Found` - Savings plan not found

---

#### Create Savings Plan

**Endpoint:** `POST /savings-plans`

**Authentication:** Required

**Request Body:**
```json
{
  "name": "string (required)",
  "description": "string (optional)",
  "symbol": "string (optional)"
}
```

**Response (201 Created):** Savings plan DTO

**Status Codes:**
- `201 Created` - Savings plan created
- `400 Bad Request` - Invalid input

---

#### Update Savings Plan

**Endpoint:** `PUT /savings-plans/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Savings plan identifier

**Request Body:** Same as Create

**Response (200 OK):** Updated savings plan DTO

**Status Codes:**
- `200 OK` - Savings plan updated
- `404 Not Found` - Savings plan not found

---

#### Delete Savings Plan

**Endpoint:** `DELETE /savings-plans/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Savings plan identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Savings plan deleted
- `404 Not Found` - Savings plan not found

---

### Attachments

Manage file attachments and attachment categories.

#### List Attachments

**Endpoint:** `GET /attachments`

**Authentication:** Required

**Query Parameters:**
- `skip` (integer) - Items to skip
- `take` (integer) - Maximum items to return

**Response (200 OK):** List of attachment DTOs

**Status Codes:**
- `200 OK` - Success

---

#### Upload Attachment

**Endpoint:** `POST /attachments`

**Authentication:** Required

**Request:** Multipart form data with file

**Response (201 Created):** Attachment DTO

**Status Codes:**
- `201 Created` - Attachment uploaded
- `400 Bad Request` - Invalid file

---

#### Delete Attachment

**Endpoint:** `DELETE /attachments/{id}`

**Authentication:** Required

**Path Parameters:**
- `id` (uuid, required) - Attachment identifier

**Response (204 No Content):** Empty

**Status Codes:**
- `204 No Content` - Attachment deleted
- `404 Not Found` - Attachment not found

---

### Reports

Generate financial reports, KPIs, and analytics.

#### Get Home KPIs

**Endpoint:** `GET /home-kpis`

**Authentication:** Required

**Response (200 OK):**
```json
{
  "totalBalance": "decimal",
  "totalIncome": "decimal",
  "totalExpenses": "decimal",
  "savingsRate": "decimal",
  "lastUpdated": "datetime"
}
```

**Status Codes:**
- `200 OK` - Success

---

#### Get Budget Report

**Endpoint:** `GET /budget/reports`

**Authentication:** Required

**Query Parameters:**
- `from` (date) - Start date
- `to` (date) - End date
- `skip` (integer) - Items to skip
- `take` (integer) - Maximum items to return

**Response (200 OK):** List of budget report DTOs

**Status Codes:**
- `200 OK` - Success

---

### Notifications

Manage notifications and background tasks.

#### List Notifications

**Endpoint:** `GET /notifications`

**Authentication:** Required

**Query Parameters:**
- `skip` (integer) - Items to skip
- `take` (integer) - Maximum items to return

**Response (200 OK):** List of notification DTOs

**Status Codes:**
- `200 OK` - Success

---

#### Get Background Tasks

**Endpoint:** `GET /background-tasks`

**Authentication:** Required (Admin only)

**Response (200 OK):** List of background task DTOs

**Status Codes:**
- `200 OK` - Success

---

### User Management

Manage user accounts and settings.

#### Get Current User

**Endpoint:** `GET /users/me`

**Authentication:** Required

**Response (200 OK):**
```json
{
  "id": "uuid",
  "username": "string",
  "email": "string (optional)",
  "preferredLanguage": "string",
  "timeZoneId": "string",
  "isAdmin": "boolean"
}
```

**Status Codes:**
- `200 OK` - Success

---

#### Get User Settings

**Endpoint:** `GET /user-settings`

**Authentication:** Required

**Response (200 OK):** User settings DTO

**Status Codes:**
- `200 OK` - Success

---

#### Update User Settings

**Endpoint:** `PUT /user-settings`

**Authentication:** Required

**Request Body:**
```json
{
  "preferredLanguage": "string (optional)",
  "timeZoneId": "string (optional)"
}
```

**Response (200 OK):** Updated settings DTO

**Status Codes:**
- `200 OK` - Settings updated

---

---

## Common Response Formats

### Success Response (200 OK)

Endpoints return JSON data directly:

```json
{
  "id": "uuid",
  "name": "string",
  "...": "other properties"
}
```

### Error Response (4xx, 5xx)

API errors are returned in a standardized format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "fieldName": ["Error message 1", "Error message 2"]
  },
  "traceId": "string"
}
```

### Error Types

- `400 Bad Request` - Invalid request format or validation failed
- `401 Unauthorized` - Missing or invalid authentication
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource conflict (e.g., duplicate entry)
- `500 Internal Server Error` - Unexpected server error

---

## Error Handling

### Common Error Codes

| Code | HTTP Status | Description |
|------|-------------|-------------|
| `Err_InvalidCredentials` | 401 | Login credentials are incorrect |
| `Err_Conflict_UserAlreadyExists` | 409 | User with this username already exists |
| `Err_Invalid_{FieldName}` | 400 | Specific field validation error |
| `Err_NotFound` | 404 | Resource not found |
| `Err_InvalidArgument` | 400 | Invalid argument provided |

### Error Response Example

```json
{
  "code": "Err_Invalid_Name",
  "title": "Validation Error",
  "message": "Account name is required and cannot be empty",
  "traceId": "0HN1GQ7B5QLSH:00000001"
}
```

---

## Pagination

Most list endpoints support pagination through query parameters:

- `skip` (integer, default: 0) - Number of items to skip
- `take` (integer, default: 100) - Maximum items to return (usually clamped to 1-200 or 1-250)

**Example:**
```
GET /api/accounts?skip=20&take=10
```

Returns items 21-30.

---

## Rate Limiting

Rate limiting may be applied to authentication and reporting endpoints to prevent abuse. Clients should implement appropriate backoff strategies.

---

## Versioning

The API follows semantic versioning. The current version is `v1` (implicit in current URL structure). Future versions will be indicated in the URL path or header.

---

## Support & Documentation

For additional API details or integration support, refer to:

- The service DTOs in `FinanceManager.Shared.Dtos.*` packages
- XML documentation in controller source code (`FinanceManager.Web/Controllers/*.cs`)
- Integration test examples in `FinanceManager.Tests.Integration*` projects

---

**Last Updated:** March 2026  
**Maintainer:** FinanceManager Development Team
