# FinanceManager API Documentation Index

Quick navigation guide for all API documentation.

## 📘 Documentation Files

### [README.md](./README.md) - Start Here
Overview of authentication, response formats, error handling, and common patterns. Read this first for a general understanding of the API.

**Best for:**
- Getting started with the API
- Understanding authentication flows
- Learning error codes and status codes
- Common patterns (pagination, filtering, dates, monetary values)

---

### [PUBLIC_API.md](./PUBLIC_API.md) - Complete API Reference
Comprehensive endpoint documentation with all resources, query parameters, request/response examples, and status codes.

**Covers:**
- **Authentication** - Login, Register, Logout endpoints
- **Accounts** - Create, Read, Update, Delete bank accounts
- **Contacts** - Manage persons, companies, and banks
- **Postings** - Query financial transactions
- **Budget Management** - Categories, purposes, rules, reports, overrides
- **Securities** - Stocks, bonds, and investment management
- **Savings Plans** - Savings goal management
- **Attachments** - File uploads
- **Reports** - Financial analytics and KPIs
- **User Management** - Account settings, preferences
- **Notifications** - User notifications
- **Background Tasks** - Async operations

**Best for:**
- Looking up endpoint details
- Finding request/response examples
- Understanding query parameters
- Debugging API issues

---

### [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md) - Architecture & Integration Guide
Technical guide for implementing API clients with best practices, error handling, and code examples.

**Covers:**
- API Architecture (three-tier layering)
- Authentication & Authorization flows
- Request/Response patterns
- Error handling strategies
- **Integration Examples:**
  - C# / .NET (HttpClient, Refit)
  - JavaScript / TypeScript (Fetch, Axios)
- **Client Best Practices:**
  - Error handling
  - Retry logic with exponential backoff
  - Request deduplication
  - Cancellation
  - Caching strategies
  - Logging & monitoring
- Security considerations
- API versioning & compatibility

**Best for:**
- Implementing a client library
- Planning API integration architecture
- Understanding authentication flows
- Learning error handling best practices
- Implementing caching and retry logic
- Security implementation

---

## 🚀 Quick Navigation by Task

### I need to...

#### **Integrate with the API**
1. Start with [README.md](./README.md) - Authentication section
2. Review [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md) - Integration Examples
3. Reference [PUBLIC_API.md](./PUBLIC_API.md) - Specific endpoints

#### **Implement error handling**
1. Read [README.md](./README.md) - Error Handling section
2. Study [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md) - Error Handling Strategy section
3. Reference [PUBLIC_API.md](./PUBLIC_API.md) - Error Codes table

#### **Build a client library**
1. Review [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md) - API Architecture section
2. Study integration examples for your language
3. Implement best practices (caching, retries, logging)
4. Reference specific endpoints in [PUBLIC_API.md](./PUBLIC_API.md)

#### **Look up a specific endpoint**
1. Go to [PUBLIC_API.md](./PUBLIC_API.md)
2. Search for the resource name (Ctrl+F / Cmd+F)
3. Find the HTTP method and full endpoint documentation

#### **Implement authentication**
1. Read [README.md](./README.md) - Authentication section
2. Review [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md) - Authentication & Authorization section
3. Check code examples for your language

#### **Handle pagination**
1. Read [README.md](./README.md) - Common Patterns section
2. Review [PUBLIC_API.md](./PUBLIC_API.md) - Pagination section
3. Check examples in [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md)

#### **Set up caching**
1. Review [ARCHITECTURE_AND_INTEGRATION.md](./ARCHITECTURE_AND_INTEGRATION.md) - Caching Strategy section
2. Check specific endpoint caching recommendations in [PUBLIC_API.md](./PUBLIC_API.md)

---

## 📊 API Resource Map

| Resource | Methods | Doc Section |
|----------|---------|-------------|
| Authentication | POST | [PUBLIC_API.md#authentication](./PUBLIC_API.md#authentication) |
| Accounts | GET, POST, PUT, DELETE | [PUBLIC_API.md#accounts](./PUBLIC_API.md#accounts) |
| Contacts | GET, POST, PUT, DELETE | [PUBLIC_API.md#contacts](./PUBLIC_API.md#contacts) |
| Postings | GET | [PUBLIC_API.md#postings](./PUBLIC_API.md#postings) |
| Budget | GET, POST, PUT, DELETE | [PUBLIC_API.md#budget-management](./PUBLIC_API.md#budget-management) |
| Securities | GET, POST, PUT, DELETE | [PUBLIC_API.md#securities](./PUBLIC_API.md#securities) |
| Savings Plans | GET, POST, PUT, DELETE | [PUBLIC_API.md#savings-plans](./PUBLIC_API.md#savings-plans) |
| Attachments | GET, POST, DELETE | [PUBLIC_API.md#attachments](./PUBLIC_API.md#attachments) |
| Reports | GET | [PUBLIC_API.md#reports](./PUBLIC_API.md#reports) |
| Notifications | GET | [PUBLIC_API.md#notifications](./PUBLIC_API.md#notifications) |
| Users | GET | [PUBLIC_API.md#user-management](./PUBLIC_API.md#user-management) |

---

## 🔑 Key Concepts

### Authentication
- JWT tokens stored in HTTP-only cookies
- Automatic handling by HTTP clients
- No manual token passing required
- See: [README.md - Authentication](./README.md#authentication)

### Authorization
- Resource isolation (users can only access their own data)
- Admin operations require admin role
- Ownership verification at service layer
- See: [ARCHITECTURE_AND_INTEGRATION.md - Authorization](./ARCHITECTURE_AND_INTEGRATION.md#authorization)

### Error Handling
- ProblemDetails format (RFC 7807)
- Detailed validation errors
- Structured error codes
- See: [README.md - Error Handling](./README.md#error-handling)

### Pagination
- `skip` and `take` query parameters
- Default page size varies by endpoint
- Maximum take usually 200-250
- See: [README.md - Pagination](./README.md#pagination)

### Status Codes
- 2xx: Success
- 4xx: Client error
- 5xx: Server error
- See: [README.md - HTTP Status Codes](./README.md#http-status-codes)

---

## 💡 Best Practices Summary

### Client Implementation
✓ Always validate input and check response codes
✓ Implement exponential backoff for retries
✓ Use pagination for list endpoints
✓ Cache frequently accessed data
✓ Log errors with trace IDs for support

### Security
✓ Use HTTPS only
✓ Let HTTP client handle cookies
✓ Don't log full tokens
✓ Validate all inputs server-side
✓ Use secure session management

### Performance
✓ Implement request deduplication
✓ Cache responses appropriately
✓ Use pagination instead of fetching all data
✓ Monitor rate limits

---

## 📚 Additional Resources

- **Source Code Examples:** See `FinanceManager.Tests.Integration*` projects
- **Source Controllers:** See `FinanceManager.Web/Controllers/*.cs` for XML documentation
- **DTOs & Models:** See `FinanceManager.Shared.Dtos.*` packages
- **Configuration:** See `FinanceManager.Web/appsettings*.json`

---

## 📝 Document Maintenance

Last Updated: March 2026
API Version: v1 (implicit)
Status: Production Ready

---

**Questions?** Check the appropriate documentation file above or review the source code integration tests for practical examples.
