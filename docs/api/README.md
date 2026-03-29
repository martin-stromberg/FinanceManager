# FinanceManager API Documentation

This document provides a comprehensive guide to the FinanceManager REST API. The API is built with ASP.NET Core and uses modern REST conventions with JSON payloads.

## Documentation Files

This directory contains the following documentation:

1. **[PUBLIC_API.md](./PUBLIC_API.md)** - Complete REST API reference with all endpoints, request/response examples, and error codes
2. **[ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md)** - API architecture, authentication flows, integration examples, and client best practices
3. **[README.md](./README.md)** - This file

## Table of Contents

- [Quick Start](#quick-start)
- [Base URL](#base-url)
- [Authentication](#authentication)
- [API Response Format](#api-response-format)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)
- [Common Patterns](#common-patterns)

## Quick Start

### 1. Authenticate
```bash
curl -X POST https://your-domain.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"user","password":"pass"}'
```

### 2. Make Authenticated Requests
Most HTTP clients automatically include the JWT cookie. For detailed integration examples, see [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md#integration-examples).

### 3. Complete API Reference
For all endpoints, parameters, and response formats, see [PUBLIC_API.md](./PUBLIC_API.md).

## Base URL

```
https://your-domain.com/api
```

All endpoints are prefixed with `/api/`.

## Authentication

### JWT Bearer Token via HttpOnly Cookie

FinanceManager uses **JWT tokens** stored in **HttpOnly cookies** for authentication. This approach provides:

- **XSS Protection**: Token is never accessible to JavaScript
- **CSRF Automatic**: HttpOnly cookies with SameSite=Lax setting
- **Session Security**: Tokens are encrypted and tamper-proof

#### How Authentication Works

1. **Login/Register**: Call `/api/auth/login` or `/api/auth/register`
   - Returns HTTP 200 with user info
   - Browser automatically stores JWT in HttpOnly cookie

2. **Authenticated Requests**: For all protected endpoints:
   - Browser automatically includes cookie with each request
   - No manual token handling required
   - Server validates token from cookie

3. **Logout**: Call `/api/auth/logout`
   - Clears the authentication cookie

#### Authentication Headers

Protected endpoints require the following header:

```
Authorization: Bearer <JWT_TOKEN>
```

However, when using browser-based clients, the token is automatically managed via cookies.

### Public Endpoints

The following endpoints do **NOT** require authentication:

- `GET /api/users/exists` - Check if any users exist in system
- `POST /api/auth/login` - User login
- `POST /api/auth/register` - User registration
- `POST /api/auth/logout` - User logout (recommended but not required)

### Protected Endpoints

All other endpoints require JWT authentication. Access is scoped to the current authenticated user unless otherwise noted.

## API Response Format

### Success Response

```json
{
  "id": "uuid",
  "name": "Example",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

### Error Response (ProblemDetails)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found",
  "traceId": "0HMVF0HGVJK84:00000001"
}
```

### Validation Error Response

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["The Name field is required."]
  },
  "traceId": "0HMVF0HGVJK84:00000002"
}
```

## HTTP Status Codes

| Code | Description |
|------|-------------|
| 200 | OK - Request succeeded |
| 201 | Created - Resource created successfully |
| 202 | Accepted - Request accepted for processing (async) |
| 204 | No Content - Request succeeded, no content to return |
| 400 | Bad Request - Invalid parameters or validation failed |
| 401 | Unauthorized - Authentication required or invalid |
| 403 | Forbidden - Authenticated but not authorized |
| 404 | Not Found - Resource does not exist |
| 409 | Conflict - Resource conflict (e.g., duplicate entry) |
| 429 | Too Many Requests - Rate limit exceeded |
| 499 | Client Closed Request - Client cancelled the request |
| 500 | Internal Server Error - Unexpected server error |

## Error Handling

### Error Code Format

API errors use a hierarchical code format:

```
<Origin>_<ErrorCategory>_<ErrorType>
```

Examples:
- `API_Auth_InvalidCredentials` - Login failed
- `API_Accounts_InvalidIban` - Invalid IBAN format
- `API_BudgetCategory_Conflict_NameAlreadyExists` - Category name duplicate

### Handling Validation Errors

Validation errors return HTTP 400 with detailed field errors:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["The Name field is required.", "Name must be less than 100 characters"],
    "Amount": ["The field Amount must be between 0.01 and 999999.99."]
  }
}
```

### Common Errors

| Status | Code | Meaning |
|--------|------|---------|
| 401 | `API_Auth_InvalidCredentials` | Login credentials are incorrect |
| 409 | `API_Auth_Conflict_UserAlreadyExists` | Username already taken |
| 404 | `API_Accounts_NotFound` | Account does not exist or not owned by current user |
| 404 | `API_NotFound` | Generic not found error |

## Rate Limiting

Currently, rate limiting is applied to sensitive endpoints:

- `/api/auth/login` - Max 10 attempts per 15 minutes per IP
- `/api/auth/register` - Max 3 registrations per hour per IP

Rate limit information is provided in response headers:

```
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 5
X-RateLimit-Reset: 1642296000
```

Exceeding limits returns HTTP 429 with retry-after information.

## Endpoint Categories

The FinanceManager API is organized into the following categories:

| Category | Purpose | Endpoints |
|----------|---------|-----------|
| [Authentication](./endpoints.md#authentication) | User login, registration, logout | 3 endpoints |
| [User Settings](./endpoints.md#user-settings) | Profile, notifications, import configuration | 6 endpoints |
| [Accounts](./endpoints.md#accounts) | Bank account management | 8 endpoints |
| [Postings](./endpoints.md#postings) | Transaction entries | 10+ endpoints |
| [Budget Categories](./endpoints.md#budget-categories) | Budget category CRUD | 5 endpoints |
| [Budget Purposes](./endpoints.md#budget-purposes) | Budget purpose CRUD | 5 endpoints |
| [Budget Rules](./endpoints.md#budget-rules) | Budget rule CRUD | 6 endpoints |
| [Budget Reports](./endpoints.md#budget-reports) | Budget reporting and analysis | 8+ endpoints |
| [Budget Overrides](./endpoints.md#budget-overrides) | Budget override management | 4 endpoints |
| [Contacts](./endpoints.md#contacts) | Contact CRUD and relationships | 12+ endpoints |
| [Contact Categories](./endpoints.md#contact-categories) | Contact category management | 5 endpoints |
| [Securities](./endpoints.md#securities) | Security/investment management | 15+ endpoints |
| [Security Categories](./endpoints.md#security-categories) | Security category CRUD | 5 endpoints |
| [Savings Plans](./endpoints.md#savings-plans) | Savings plan management | 10+ endpoints |
| [Savings Plan Categories](./endpoints.md#savings-plan-categories) | Savings plan category CRUD | 5 endpoints |
| [Statement Drafts](./endpoints.md#statement-drafts) | Import draft management | 8+ endpoints |
| [Reports](./endpoints.md#reports) | Portfolio and financial reports | 5+ endpoints |
| [Backups](./endpoints.md#backups) | Database backup/restore | 5 endpoints |
| [Background Tasks](./endpoints.md#background-tasks) | Async task management | 4 endpoints |
| [Attachments](./endpoints.md#attachments) | File upload and management | 6+ endpoints |
| [Admin](./endpoints.md#admin) | Administrative operations | 3+ endpoints |
| [Notifications](./endpoints.md#notifications) | User notifications | 4 endpoints |
| [Home KPIs](./endpoints.md#home-kpis) | Dashboard key performance indicators | 3+ endpoints |
| [Meta Holiday Providers](./endpoints.md#meta-holiday-providers) | Holiday data providers | 2 endpoints |

## Common Patterns

### Pagination

Paginated endpoints support the following query parameters:

| Parameter | Type | Default | Max | Description |
|-----------|------|---------|-----|-------------|
| `skip` | integer | 0 | - | Number of items to skip |
| `take` | integer | 100/50 | 200 | Maximum items to return |

Example:
```
GET /api/accounts?skip=0&take=50
```

### Filtering

Most list endpoints support filtering via query parameters:

| Parameter | Type | Description |
|-----------|------|-------------|
| `q` | string | Search query (name/text search) |
| `type` | enum | Filter by type |
| `from` | date | Start date filter (ISO 8601) |
| `to` | date | End date filter (ISO 8601) |
| `all` | boolean | Return all results (ignores paging) |

Example:
```
GET /api/contacts?q=acme&type=Company&all=true
```

### Date Formats

All dates are in ISO 8601 format:

- Full datetime: `2024-01-15T10:30:00Z`
- Date only: `2024-01-15`
- Duration: `PT1H30M` (ISO 8601 duration)

### Monetary Values

All monetary amounts use decimal strings to preserve precision:

```json
{
  "amount": "1250.99",
  "currency": "EUR"
}
```

### Entity Identifiers

All entity identifiers are UUIDs (Globally Unique Identifiers):

```
xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
```

Example:
```
550e8400-e29b-41d4-a716-446655440000
```

### Relationships

Related entities can be included via:

1. **ID Reference**: Only the ID is returned
   ```json
   { "accountId": "uuid" }
   ```

2. **Nested DTO**: Full entity data is nested
   ```json
   { "account": { "id": "uuid", "name": "My Account" } }
   ```

3. **Explicit Loading**: Some endpoints support `?include=...` parameters

### Asynchronous Operations

Some long-running operations (like imports, backups) return **HTTP 202 Accepted** with a task ID:

```json
{
  "taskId": "uuid",
  "status": "Pending"
}
```

You can poll the task status via:
```
GET /api/background-tasks/{taskId}
```

## Best Practices

### 1. Always Validate Input

- Check response status codes
- Parse error responses for detailed messages
- Use `try-catch` or equivalent error handling

### 2. Handle Rate Limiting

- Monitor `X-RateLimit-*` headers
- Implement exponential backoff for retries
- Use `Retry-After` header value

### 3. Use Pagination

- Always use pagination for list endpoints
- Start with reasonable `take` values (50-100)
- Implement client-side caching where appropriate

### 4. Secure Cookie Handling

- Enable HttpOnly and Secure flags (automatic)
- Respect SameSite=Lax cookie policy
- Clear cookies on logout

### 5. Handle Async Operations

- For long-running operations, store the task ID
- Implement polling with exponential backoff
- Provide user feedback while waiting

### 6. Error Recovery

For common errors:

| Error | Recovery |
|-------|----------|
| 401 Unauthorized | Re-authenticate (login again) |
| 404 Not Found | Verify resource ID or that it still exists |
| 409 Conflict | Check for duplicates or constraint violations |
| 429 Too Many Requests | Wait before retrying (check Retry-After header) |
| 500 Internal Server Error | Retry with exponential backoff |

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2024-01-15 | Initial API release |

---

For detailed endpoint documentation, see [endpoints.md](./endpoints.md).
For request/response models, see [models.md](./models.md).
For practical examples, see [examples.md](./examples.md).
