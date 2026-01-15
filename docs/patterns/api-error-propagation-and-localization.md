# Pattern: API Error Propagation & Localization (Controllers ? ApiClient ? UI)

This document describes the standard way to implement **API ? Client ? UI** error handling with **user-facing localization**.

The goal is:

- Controllers return **predictable payloads**.
- `ApiClient` extracts error information into `LastErrorCode` and `LastError`.
- ViewModels call `SetError(...)` to map error codes to localized resources.
- UI shows localized messages (fallback to server-provided message).

---

## Overview: two patterns

### Pattern 1: Framework validation (ModelState / DataAnnotations)

- Controllers return `ValidationProblem(ModelState)`.
- Response is `ValidationProblemDetails` containing an `errors` object.
- `ApiClient` aggregates `errors` via `SetRFCStyleError(...)` into `LastError`.

### Pattern 2: Custom API errors (Origin + Code + Message)

For all application/domain errors intended for user display, the API returns a standardized error payload:

- `origin`: identifies the endpoint/feature area (e.g. `API_BudgetRule`)
- `code`: stable machine-readable error code
- `message`: localized to the user’s language (resolved by server localizer)

The Blazor client sends `Accept-Language` so the server localizer can resolve messages correctly.

---

## Pattern 1 details: ValidationProblemDetails

### Server

- Use:
  - `if (!ModelState.IsValid) { return ValidationProblem(ModelState); }`

### Client (`ApiClient`)

- `EnsureSuccessOrSetErrorAsync(...)` detects an `errors` JSON object.
- `SetRFCStyleError(...)` flattens errors into a single string:
  - `FieldA: msg1; FieldB: msg2; ...`

Notes:

- Validation messages are text-based. If they must be localized, use localized validators / localized DataAnnotations on the server.

---

## Pattern 2 details: Origin + Code + Message

### Contract

The JSON payload must contain:

- `origin` (string)
- `code` (string)
- `message` (string)

### Code schema

Codes must be consistent and stable.

**Formal input errors (HTTP 400)**

- `ArgumentException` ? `Err_Invalid_{ParamName}`
- `ArgumentOutOfRangeException` ? `Err_OutOfRange_{ParamName}`

`ParamName` must be the name of the invalid property/parameter.

**Domain rule violations / invalid target state (typically HTTP 409)**

- `DomainValidationException` ?
  - `Err_Conflict_{DomainRule}` or
  - `Err_InvalidState_{DomainRule}`

**Not found (HTTP 404)**

- `Err_NotFound_{Entity}`

**Not allowed (HTTP 403)**

- `Err_NotAllowed_{Action}`

**Unexpected (HTTP 500)**

- `Err_Unexpected`

### Localization

The server resolves `message` using `IStringLocalizer`.

Lookup key:

- `{origin}_{code}`

Examples:

- `API_BudgetRule_Err_Invalid_BudgetCategoryId`
- `API_BudgetRule_Err_Conflict_CategoryAndPurposeRules`

Fallback:

- If the resource key is not found, return the exception’s original message as `message`.

---

## `ApiClient` behavior (FinanceManager.Shared)

**File**: `FinanceManager.Shared/ApiClient.cs`

### Extracted fields

On non-success responses, `ApiClient` best-effort parses JSON and sets:

- `LastErrorCode` from JSON property `error` (legacy) or the custom error payload’s `code`
- `LastError` from JSON properties:
  - `message`
  - `title` / `detail` (ProblemDetails)
  - aggregated RFC validation `errors`

Finally it calls `resp.EnsureSuccessStatusCode()` which throws `HttpRequestException`.

---

## ViewModel behavior (Blazor UI)

**File**: `FinanceManager.Web/ViewModels/Common/BaseViewModel.cs`

ViewModels should call:

- `SetError(ApiClient.LastErrorCode, ApiClient.LastError)`

The UI shows:

- localized message if `LastErrorCode` maps to a resource entry
- otherwise the fallback `LastError` (server-provided message / problem title / aggregated validation errors)

---

## HTTP status semantics

- `400 BadRequest`: formal input errors
- `404 NotFound`: entity not found
- `409 Conflict`: domain rule violation / invalid target state
- `403 Forbidden`: not allowed
- `500 InternalServerError`: unexpected failures

---

## Known inconsistencies (current state)

- Some endpoints return `ApiErrorDto(ex.Message)` (no error code).
- Some endpoints return `new ApiErrorDto(nameof(ArgumentException), ex.Message)` (not a stable resource code).
- Some endpoints return anonymous objects `{ error, message }` instead of a single standardized DTO.
- Some `Problem(...)` responses use localized messages, others hardcode `"Unexpected error"`.

These should be unified so that UI translation can rely on stable keys.
