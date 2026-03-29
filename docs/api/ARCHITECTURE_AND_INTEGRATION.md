# FinanceManager API Architecture & Integration Guide

## Table of Contents

1. [API Architecture](#api-architecture)
2. [Authentication & Authorization](#authentication--authorization)
3. [Request/Response Patterns](#requestresponse-patterns)
4. [Error Handling](#error-handling)
5. [Integration Examples](#integration-examples)
6. [Client Implementation Best Practices](#client-implementation-best-practices)

---

## API Architecture

### Overview

The FinanceManager API follows a **three-tier layered architecture**:

```
┌─────────────────────────────────────┐
│      Presentation Layer (Web)       │
│   API Controllers & Middleware      │
├─────────────────────────────────────┤
│    Application Layer (Services)     │
│   Business Logic & Orchestration    │
├─────────────────────────────────────┤
│    Infrastructure Layer             │
│   EF Core DbContext, External APIs  │
└─────────────────────────────────────┘
```

### Request Flow

```
Client Request
    ↓
[Controller] - Validates model, checks auth, maps to Service
    ↓
[Application Service] - Business logic, domain rules
    ↓
[Infrastructure] - Database, external APIs
    ↓
[Response Mapper] - DTO creation
    ↓
Client Response (JSON)
```

### API Controller Structure

Controllers are organized by resource domain:

- **AuthController** - Authentication (login, register, logout)
- **AccountsController** - Bank account management
- **ContactsController** - Contact/entity management
- **PostingsController** - Transaction/posting queries
- **BudgetCategoriesController** - Budget category management
- **SecuritiesController** - Security/investment management
- **SavingsPlansController** - Savings plan management
- **ReportsController** - Financial reports & analytics
- **AttachmentsController** - File upload & management
- **NotificationsController** - User notifications
- **UserSettingsController** - User account settings
- **AdminController** - Admin-only operations

---

## Authentication & Authorization

### JWT Bearer Tokens

The API uses **JWT Bearer tokens** stored in HTTP-only cookies for security:

**Token Details:**
- Format: JWT (JSON Web Token)
- Storage: HTTP-only cookie named `FinanceManager.Auth`
- Transport: Automatic cookie handling by HTTP client libraries
- Expiration: Token includes UTC expiration (`expiresUtc`)

### Authentication Flow

```
1. Client POST /auth/login
   ├─ Request: username, password, language, timezone
   ├─ Validation: Credentials verified against user store
   └─ Response: JWT token (as HTTP-only cookie) + user info

2. Client makes authenticated request (automatic cookie inclusion)
   ├─ Cookie included: FinanceManager.Auth
   ├─ Token verified: Signature & expiration validated
   └─ Request proceeds if valid

3. Client POST /auth/logout
   └─ Response: Cookie cleared on client and server
```

### Authorization

- **Default:** Most endpoints require authenticated user (JWT Bearer)
- **Resource Isolation:** Each user can only access their own data
- **Admin Operations:** Some endpoints (e.g., /admin) require `isAdmin = true` role
- **Ownership Verification:** Service layer verifies current user owns the requested resource

**Authorization Attributes:**

```csharp
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AccountsController : ControllerBase { ... }
```

---

## Request/Response Patterns

### Query Parameters

#### Pagination

All list endpoints support optional pagination:

```
GET /api/accounts?skip=0&take=50
```

- `skip` (default: 0) - Items to skip
- `take` (default: varies by endpoint, max: 200-250)

#### Filtering

Resource-specific filters:

```
GET /api/contacts?type=Bank&nameFilter=Deutsche
GET /api/budget/categories?from=2024-01-01&to=2024-12-31
```

#### Examples

```
# Get first 20 accounts
GET /api/accounts?skip=0&take=20

# Get accounts 41-60
GET /api/accounts?skip=40&take=20

# Filter contacts by bank type
GET /api/contacts?type=Bank

# Get budget categories for date range
GET /api/budget/categories?from=2024-06-01&to=2024-06-30
```

### Request Headers

Required headers for all authenticated requests:

```http
GET /api/accounts HTTP/1.1
Host: api.financemanager.local
Accept: application/json
Cookie: FinanceManager.Auth={jwt_token}
```

Most HTTP clients automatically handle cookies.

### Response Structure

#### Successful Response (2xx)

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "My Checking Account",
  "type": "Checking",
  "iban": "DE89370400440532013000",
  "bankContactId": "550e8400-e29b-41d4-a716-446655440001",
  "savingsPlanExpectation": 5000.00,
  "securityProcessingEnabled": true
}
```

#### Error Response (4xx, 5xx)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "name": ["The Name field is required."],
    "type": ["Invalid account type."]
  },
  "traceId": "0HN1GQ7B5QLSH:00000001"
}
```

---

## Error Handling

### HTTP Status Codes

| Status | Meaning | When to Retry |
|--------|---------|---------------|
| 200 | OK | ✗ |
| 201 | Created | ✗ |
| 204 | No Content | ✗ |
| 400 | Bad Request | ✗ (fix request) |
| 401 | Unauthorized | ✓ (refresh token) |
| 404 | Not Found | ✗ |
| 409 | Conflict | ✗ (resolve conflict) |
| 500 | Server Error | ✓ (exponential backoff) |
| 503 | Service Unavailable | ✓ (exponential backoff) |

### Handling Common Errors

#### 401 Unauthorized

**Cause:** Missing, invalid, or expired JWT token

**Action:**
1. Check if cookie is present
2. Try refreshing token (if refresh endpoint available)
3. If unsuccessful, redirect to login

#### 404 Not Found

**Cause:** Resource doesn't exist or belongs to another user

**Action:**
1. Verify resource ID is correct
2. Check that resource belongs to authenticated user
3. Implement graceful degradation in UI

#### 400 Bad Request

**Cause:** Invalid input or validation failure

**Action:**
1. Parse `errors` object from response
2. Display field-level error messages to user
3. Fix validation issues and retry

#### 409 Conflict

**Cause:** Resource conflict (e.g., duplicate username)

**Action:**
1. Handle business logic conflict
2. Inform user and request different input
3. Do NOT retry without changes

#### 500 Internal Server Error

**Cause:** Unexpected server-side failure

**Action:**
1. Implement exponential backoff retry strategy
2. Log error with `traceId` for support investigation
3. Display generic error message to user

---

## Integration Examples

### C# / .NET Client

#### Using HttpClient

```csharp
using var httpClient = new HttpClient { BaseAddress = new Uri("https://api.financemanager.local") };

// 1. Login
var loginRequest = new { username = "john", password = "secret123" };
var loginResponse = await httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
var authResult = await loginResponse.Content.ReadAsAsync<AuthResponse>();

// Cookie is automatically stored by HttpClientHandler

// 2. Get Accounts
var accountsResponse = await httpClient.GetAsync("/api/accounts?skip=0&take=20");
var accounts = await accountsResponse.Content.ReadAsAsync<IReadOnlyList<AccountDto>>();

// 3. Create Account
var createRequest = new
{
    name = "New Savings Account",
    type = "Savings",
    iban = "DE89370400440532013000",
    newBankContactName = "Deutsche Bank"
};
var createResponse = await httpClient.PostAsJsonAsync("/api/accounts", createRequest);
var newAccount = await createResponse.Content.ReadAsAsync<AccountDto>();

// 4. Logout
await httpClient.PostAsync("/api/auth/logout", null);
```

#### Using Refit (Declarative HTTP Client)

```csharp
[Headers("Accept: application/json")]
public interface IFinanceManagerApi
{
    [Post("/auth/login")]
    Task<AuthResponse> LoginAsync([Body] LoginRequest request);

    [Get("/accounts")]
    Task<IReadOnlyList<AccountDto>> GetAccountsAsync([Query] int skip = 0, [Query] int take = 100);

    [Get("/accounts/{id}")]
    Task<AccountDto> GetAccountAsync(Guid id);

    [Post("/accounts")]
    Task<AccountDto> CreateAccountAsync([Body] AccountCreateRequest request);

    [Put("/accounts/{id}")]
    Task<AccountDto> UpdateAccountAsync(Guid id, [Body] AccountUpdateRequest request);

    [Delete("/accounts/{id}")]
    Task LogoutAsync();
}

// Usage
var api = RestService.For<IFinanceManagerApi>("https://api.financemanager.local");
var accounts = await api.GetAccountsAsync();
```

### JavaScript / TypeScript Client

#### Using Fetch API

```javascript
const API_BASE = 'https://api.financemanager.local/api';

class FinanceManagerClient {
  async login(username, password) {
    const response = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      credentials: 'include', // Include cookies
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });
    
    if (!response.ok) throw new Error(`Login failed: ${response.status}`);
    return response.json();
  }

  async getAccounts(skip = 0, take = 100) {
    const response = await fetch(
      `${API_BASE}/accounts?skip=${skip}&take=${take}`,
      { credentials: 'include' }
    );
    
    if (!response.ok) throw new Error(`Failed to fetch accounts: ${response.status}`);
    return response.json();
  }

  async createAccount(account) {
    const response = await fetch(`${API_BASE}/accounts`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(account)
    });
    
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.detail || 'Failed to create account');
    }
    return response.json();
  }

  async logout() {
    await fetch(`${API_BASE}/auth/logout`, {
      method: 'POST',
      credentials: 'include'
    });
  }
}

// Usage
const client = new FinanceManagerClient();
await client.login('john', 'secret123');
const accounts = await client.getAccounts();
```

#### Using Axios

```javascript
import axios from 'axios';

const api = axios.create({
  baseURL: 'https://api.financemanager.local/api',
  withCredentials: true // Include cookies
});

// Login
const { data: auth } = await api.post('/auth/login', {
  username: 'john',
  password: 'secret123'
});

// Get accounts
const { data: accounts } = await api.get('/accounts', {
  params: { skip: 0, take: 100 }
});

// Create account
const { data: newAccount } = await api.post('/accounts', {
  name: 'New Account',
  type: 'Checking',
  newBankContactName: 'My Bank'
});
```

---

## Client Implementation Best Practices

### 1. Error Handling Strategy

```javascript
async function makeRequest(method, endpoint, data = null) {
  try {
    const response = await fetch(`${API_BASE}${endpoint}`, {
      method,
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: data ? JSON.stringify(data) : null
    });

    if (response.status === 401) {
      // Token expired - redirect to login
      window.location.href = '/login';
      return;
    }

    if (!response.ok) {
      const error = await response.json();
      throw new ApiError(error.detail || 'Request failed', response.status, error);
    }

    return response.status === 204 ? null : response.json();
  } catch (error) {
    console.error('API Error:', error);
    throw error;
  }
}
```

### 2. Retry Logic with Exponential Backoff

```csharp
public static async Task<T> ExecuteWithRetryAsync<T>(
    Func<Task<T>> operation,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is 500 or 503)
        {
            if (i == maxRetries - 1) throw;
            
            var delay = TimeSpan.FromSeconds(Math.Pow(2, i));
            await Task.Delay(delay);
        }
    }
    throw new InvalidOperationException("Should not reach here");
}

// Usage
var accounts = await ExecuteWithRetryAsync(() =>
    httpClient.GetAsync("/api/accounts")
);
```

### 3. Request Deduplication

Prevent duplicate requests due to network delays:

```javascript
class DedupedClient {
  constructor() {
    this.pendingRequests = new Map();
  }

  async fetch(key, requestFn) {
    if (this.pendingRequests.has(key)) {
      return this.pendingRequests.get(key);
    }

    const promise = requestFn()
      .finally(() => this.pendingRequests.delete(key));

    this.pendingRequests.set(key, promise);
    return promise;
  }
}

// Usage
const client = new DedupedClient();
const accounts = await client.fetch('accounts:list', () =>
  api.get('/accounts')
);
```

### 4. Request Cancellation

Support cancellation of in-flight requests:

```csharp
private CancellationTokenSource _cts;

public async Task GetAccountsAsync()
{
    _cts?.Cancel();
    _cts = new CancellationTokenSource();

    try
    {
        var accounts = await _httpClient.GetAsync(
            "/api/accounts",
            _cts.Token
        );
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Request cancelled");
    }
}

public void CancelPendingRequests()
{
    _cts?.Cancel();
}
```

### 5. Caching Strategy

Implement intelligent caching to reduce API calls:

```javascript
class CachedClient {
  constructor(cacheMaxAge = 5 * 60 * 1000) { // 5 minutes
    this.cache = new Map();
    this.cacheMaxAge = cacheMaxAge;
  }

  async get(endpoint, options = {}) {
    const now = Date.now();
    const cached = this.cache.get(endpoint);

    if (cached && now - cached.timestamp < this.cacheMaxAge) {
      return cached.data;
    }

    const data = await fetch(`${API_BASE}${endpoint}`, {
      credentials: 'include',
      ...options
    }).then(r => r.json());

    this.cache.set(endpoint, { data, timestamp: now });
    return data;
  }

  invalidate(endpoint) {
    this.cache.delete(endpoint);
  }

  invalidatePattern(pattern) {
    for (const key of this.cache.keys()) {
      if (key.includes(pattern)) {
        this.cache.delete(key);
      }
    }
  }
}

// Usage
const client = new CachedClient();
const accounts = await client.get('/accounts');

// After creating new account, invalidate cache
client.invalidatePattern('/accounts');
```

### 6. Logging & Monitoring

```csharp
public class LoggingHttpHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    public LoggingHttpHandler(ILogger logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("→ {Method} {Url}", request.Method, request.RequestUri);

        var response = await base.SendAsync(request, ct);

        stopwatch.Stop();
        _logger.LogInformation(
            "← {Method} {Url} → {StatusCode} ({ElapsedMs}ms)",
            request.Method, request.RequestUri, response.StatusCode, stopwatch.ElapsedMilliseconds
        );

        return response;
    }
}

// Register
httpClient.AddMessageHandler(new LoggingHttpHandler(logger));
```

---

## API Versioning & Compatibility

### Current Version

The API currently operates in **v1 (implicit)**.

### Forward Compatibility

To maintain backward compatibility:

- New endpoints are added with `/v2` prefix if breaking changes are required
- Existing endpoints remain unchanged
- Deprecated fields are marked in documentation before removal
- Client implementations should gracefully handle unknown response fields

---

## Security Considerations

### 1. HTTPS Only

Always use HTTPS in production:
```
https://api.financemanager.local/api
```

### 2. Cookie Security

JWT is stored in HTTP-only, secure cookies:
- **HttpOnly:** Prevents JavaScript access (mitigates XSS)
- **Secure:** Only transmitted over HTTPS
- **SameSite=Lax:** Prevents CSRF attacks

### 3. Input Validation

All inputs are validated server-side:
- Check response status codes and error messages
- Don't trust client-side validation alone
- Sanitize user input before display

### 4. Sensitive Data

- Never log full authentication tokens
- Avoid storing passwords in local storage
- Use secure session management

---

## Support & Resources

- **API Docs:** See `PUBLIC_API.md` for detailed endpoint documentation
- **Integration Tests:** Refer to `FinanceManager.Tests.Integration*` projects for examples
- **Status Page:** Monitor API health and incidents
- **Issues:** Report bugs and request features via issue tracker

---

**Last Updated:** March 2026
