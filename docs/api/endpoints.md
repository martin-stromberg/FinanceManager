# FinanceManager API Endpoints

Comprehensive documentation of all REST API endpoints organized by functional area.

## Table of Contents

- [Authentication](#authentication)
- [User Management](#user-management)
- [User Settings](#user-settings)
- [Accounts](#accounts)
- [Postings (Transactions)](#postings-transactions)
- [Budget Management](#budget-management)
  - [Budget Categories](#budget-categories)
  - [Budget Purposes](#budget-purposes)
  - [Budget Rules](#budget-rules)
  - [Budget Overrides](#budget-overrides)
  - [Budget Reports](#budget-reports)
- [Contacts](#contacts)
- [Contact Categories](#contact-categories)
- [Securities](#securities)
- [Security Categories](#security-categories)
- [Savings Plans](#savings-plans)
- [Savings Plan Categories](#savings-plan-categories)
- [Attachments](#attachments)
- [Reports](#reports)
- [Statement Drafts](#statement-drafts)
- [Notifications](#notifications)
- [Background Tasks](#background-tasks)
- [Backups](#backups)
- [Home KPIs](#home-kpis)
- [Meta Holiday Providers](#meta-holiday-providers)
- [Admin](#admin)

---

## Authentication

Public endpoints for user authentication and session management.

### POST /api/auth/login

Authenticates a user with username and password, returns JWT token via HttpOnly cookie.

**Authentication:** None (public)

**Request Body:**
```typescript
{
  username: string           // Required: Username (email or username)
  password: string           // Required: Password
  preferredLanguage?: string // Optional: Preferred language code (e.g., "de", "en")
  timeZoneId?: string        // Optional: User's timezone (e.g., "Europe/Berlin")
}
```

**Response (HTTP 200):**
```typescript
{
  username: string      // Authenticated username
  isAdmin: boolean      // Whether user has admin privileges
  expiresUtc: datetime  // UTC timestamp when token expires
}
```

**Status Codes:**
- `200 OK` - Login successful, JWT set in cookie
- `400 Bad Request` - Invalid request parameters
- `401 Unauthorized` - Invalid credentials
- `429 Too Many Requests` - Too many login attempts

**Example:**
```bash
curl -X POST https://api.example.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "john@example.com",
    "password": "securepassword123",
    "preferredLanguage": "de"
  }'
```

---

### POST /api/auth/register

Registers a new user account and returns JWT token via HttpOnly cookie.

**Authentication:** None (public)

**Request Body:**
```typescript
{
  username: string           // Required: Username (email or username)
  password: string           // Required: Password (min 8 chars, must contain uppercase, lowercase, digit, special char)
  preferredLanguage?: string // Optional: Preferred language code
  timeZoneId?: string        // Optional: User's timezone
}
```

**Response (HTTP 200):**
```typescript
{
  username: string      // Registered username
  isAdmin: boolean      // User admin status (false for new users)
  expiresUtc: datetime  // UTC timestamp when token expires
}
```

**Status Codes:**
- `200 OK` - Registration successful
- `400 Bad Request` - Invalid request (password too weak, etc.)
- `409 Conflict` - Username already exists
- `429 Too Many Requests` - Too many registration attempts

---

### POST /api/auth/logout

Logs out the current user by clearing the authentication cookie.

**Authentication:** Optional (works with or without valid token)

**Request Body:** None

**Response (HTTP 200):**
```json
{}
```

**Status Codes:**
- `200 OK` - Logout successful

---

## User Management

### GET /api/users/exists

Checks if any user exists in the system (useful for initialization check).

**Authentication:** None (public)

**Query Parameters:** None

**Response (HTTP 200):**
```typescript
{
  anyUsersExist: boolean // True if at least one user exists in system
}
```

**Status Codes:**
- `200 OK` - Check completed
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Unexpected error

---

### POST /api/users/demo-data

Creates demo/sample data for the current authenticated user (for testing/exploration).

**Authentication:** Required (JWT)

**Request Body:** None

**Response (HTTP 202):**
```typescript
{
  taskId: guid   // Task ID for tracking the async operation
  status: string // Initial status (e.g., "Pending")
}
```

**Status Codes:**
- `202 Accepted` - Demo data generation started
- `500 Internal Server Error` - Unexpected error

---

## User Settings

### GET /api/user-settings

Gets current user's settings and preferences.

**Authentication:** Required (JWT)

**Query Parameters:** None

**Response (HTTP 200):**
```typescript
{
  userId: guid
  language: string        // Language code (e.g., "de", "en")
  timeZone: string        // Timezone identifier
  dateFormat: string      // Date format preference
  currencyCode: string    // Default currency (e.g., "EUR")
  decimalPlaces: number   // Decimal places for amounts
  theme: string           // UI theme preference
}
```

**Status Codes:**
- `200 OK` - Settings retrieved
- `404 Not Found` - Settings not found
- `500 Internal Server Error` - Unexpected error

---

### PUT /api/user-settings

Updates current user's settings.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  language?: string        // Language code
  timeZone?: string        // Timezone identifier
  dateFormat?: string      // Date format preference
  currencyCode?: string    // Default currency
  theme?: string           // UI theme preference
}
```

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Settings updated
- `400 Bad Request` - Invalid settings values
- `500 Internal Server Error` - Unexpected error

---

### POST /api/user-settings/import-configuration

Saves import configuration for statement import templates.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  templateName: string   // Template identifier
  configuration: object  // Import settings
}
```

**Response (HTTP 200):**
```typescript
{
  templateName: string
  configuration: object
  savedAt: datetime
}
```

**Status Codes:**
- `200 OK` - Configuration saved
- `400 Bad Request` - Invalid configuration
- `500 Internal Server Error` - Unexpected error

---

## Accounts

Bank account management - create, read, update, delete accounts and manage symbols.

### GET /api/accounts

Lists all accounts for the current user with optional pagination and filtering.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Default | Max | Description |
|-----------|------|---------|-----|-------------|
| `skip` | integer | 0 | - | Number of items to skip |
| `take` | integer | 100 | 200 | Items per page |
| `bankContactId` | guid | null | - | Filter by bank contact |

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string                     // Account name (e.g., "Main Checking")
    type: enum                       // Account type: "Depot", "Checking", "Savings", "Cash"
    iban?: string                    // Optional IBAN
    bankContactId: guid              // Associated bank contact
    symbolAttachmentId?: guid        // Optional account symbol/icon
    savingsPlanExpectation?: decimal // Expected savings per month
    securityProcessingEnabled: boolean // Whether securities are tracked
    createdAt: datetime
    modifiedAt?: datetime
  }
  // ... more accounts
]
```

**Status Codes:**
- `200 OK` - Accounts list returned
- `500 Internal Server Error` - Unexpected error

**Example:**
```bash
curl -X GET https://api.example.com/api/accounts?skip=0&take=50
```

---

### GET /api/accounts/{id}

Gets a single account by ID.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Account identifier |

**Response (HTTP 200):**
```typescript
{
  id: guid
  userId: guid
  name: string
  type: enum
  iban?: string
  bankContactId: guid
  symbolAttachmentId?: guid
  savingsPlanExpectation?: decimal
  securityProcessingEnabled: boolean
  createdAt: datetime
  modifiedAt?: datetime
}
```

**Status Codes:**
- `200 OK` - Account found
- `404 Not Found` - Account not found or not owned by user
- `500 Internal Server Error` - Unexpected error

---

### POST /api/accounts

Creates a new account for the current user.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string                      // Required: Account name
  type: enum                        // Required: Account type
  iban?: string                     // Optional: IBAN
  bankContactId?: guid              // Optional: Existing bank contact ID
  newBankContactName?: string       // Optional: Create new bank contact with this name
  savingsPlanExpectation?: decimal  // Optional: Expected monthly savings
  securityProcessingEnabled: boolean // Whether to enable securities tracking
  symbolAttachmentId?: guid         // Optional: Attachment ID for account symbol
}
```

Note: Either `bankContactId` or `newBankContactName` must be provided, or a contact will be auto-created.

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full account object (see GET response)
}
```

**Status Codes:**
- `201 Created` - Account created
- `400 Bad Request` - Invalid input parameters
- `500 Internal Server Error` - Unexpected error

---

### PUT /api/accounts/{id}

Updates an existing account.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Account identifier |

**Request Body:**
```typescript
{
  name: string                      // Required: Updated name
  iban?: string                     // Optional: Updated IBAN
  bankContactId?: guid              // Optional: New bank contact
  newBankContactName?: string       // Optional: Create new bank contact
  savingsPlanExpectation?: decimal  // Optional: Updated savings expectation
  securityProcessingEnabled: boolean // Updated securities processing flag
  symbolAttachmentId?: guid         // Optional: Updated symbol attachment
}
```

**Response (HTTP 200):**
```typescript
{
  id: guid
  // ... updated account object
}
```

**Status Codes:**
- `200 OK` - Account updated
- `400 Bad Request` - Invalid input parameters
- `404 Not Found` - Account not found
- `500 Internal Server Error` - Unexpected error

---

### DELETE /api/accounts/{id}

Deletes an account.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Account identifier |

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Account deleted
- `404 Not Found` - Account not found
- `500 Internal Server Error` - Unexpected error

---

### POST /api/accounts/{id}/symbol/{attachmentId}

Assigns an existing attachment as the account's symbol/icon.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Account identifier |
| `attachmentId` | guid | Attachment identifier to assign as symbol |

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Symbol assigned
- `404 Not Found` - Account or attachment not found
- `400 Bad Request` - Invalid IDs
- `500 Internal Server Error` - Unexpected error

---

### DELETE /api/accounts/{id}/symbol

Clears the account's symbol/icon.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Account identifier |

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Symbol cleared
- `404 Not Found` - Account not found
- `500 Internal Server Error` - Unexpected error

---

## Postings (Transactions)

Manages individual posting entries (transactions) within accounts.

### GET /api/postings

Lists postings for the current user with advanced filtering and pagination.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `skip` | integer | 0 | Items to skip |
| `take` | integer | 100 | Items per page (max 200) |
| `accountId` | guid | null | Filter by account |
| `from` | date | null | Start date (ISO 8601) |
| `to` | date | null | End date (ISO 8601) |
| `search` | string | null | Text search in posting text/memo |
| `sortBy` | string | "Date" | Sort field: "Date", "Amount", "Name" |
| `ascending` | boolean | false | Sort direction |

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    accountId: guid
    contactId?: guid
    date: datetime
    valueDate?: datetime
    text: string                  // Transaction description
    amount: decimal               // Amount (can be negative)
    currencyCode: string          // Currency code
    memo?: string                 // Additional notes
    attachmentCount: number       // Number of attached files
    budgetCategoryId?: guid       // Assigned budget category
    securityId?: guid             // If security transaction
    createdAt: datetime
    modifiedAt?: datetime
  }
  // ... more postings
]
```

**Status Codes:**
- `200 OK` - Postings list returned
- `400 Bad Request` - Invalid filter parameters
- `500 Internal Server Error` - Unexpected error

---

### GET /api/postings/{id}

Gets a single posting by ID.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Posting identifier |

**Response (HTTP 200):**
```typescript
{
  id: guid
  accountId: guid
  contactId?: guid
  date: datetime
  valueDate?: datetime
  text: string
  amount: decimal
  currencyCode: string
  memo?: string
  attachmentCount: number
  budgetCategoryId?: guid
  securityId?: guid
  createdAt: datetime
  modifiedAt?: datetime
}
```

**Status Codes:**
- `200 OK` - Posting found
- `404 Not Found` - Posting not found
- `500 Internal Server Error` - Unexpected error

---

### POST /api/postings

Creates a new posting entry.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  accountId: guid               // Required: Account to add posting to
  contactId?: guid              // Optional: Related contact
  date: datetime                // Required: Transaction date
  valueDate?: datetime          // Optional: Value date (for bank transfers)
  text: string                  // Required: Transaction description
  amount: decimal               // Required: Amount (negative for withdrawals)
  currencyCode: string          // Required: Currency (e.g., "EUR")
  memo?: string                 // Optional: Additional notes
  budgetCategoryId?: guid       // Optional: Budget category assignment
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full posting object
}
```

**Status Codes:**
- `201 Created` - Posting created
- `400 Bad Request` - Invalid input
- `404 Not Found` - Account not found
- `500 Internal Server Error` - Unexpected error

---

### PUT /api/postings/{id}

Updates an existing posting.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Posting identifier |

**Request Body:** Same as POST /api/postings

**Response (HTTP 200):**
```typescript
{
  id: guid
  // ... updated posting object
}
```

**Status Codes:**
- `200 OK` - Posting updated
- `400 Bad Request` - Invalid input
- `404 Not Found` - Posting not found
- `500 Internal Server Error` - Unexpected error

---

### DELETE /api/postings/{id}

Deletes a posting.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Posting identifier |

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Posting deleted
- `404 Not Found` - Posting not found
- `500 Internal Server Error` - Unexpected error

---

### PUT /api/postings/{id}/budget-category

Updates the budget category assignment for a posting.

**Authentication:** Required (JWT)

**Path Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | guid | Posting identifier |

**Request Body:**
```typescript
{
  budgetCategoryId?: guid // Optional: Category ID (null to clear)
}
```

**Response (HTTP 200):**
```typescript
{
  id: guid
  budgetCategoryId?: guid
  // ... other posting fields
}
```

**Status Codes:**
- `200 OK` - Category assignment updated
- `404 Not Found` - Posting or category not found
- `500 Internal Server Error` - Unexpected error

---

## Budget Management

### Budget Categories

#### GET /api/budget/categories

Lists budget categories for the current user.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | date | Optional: Start date filter |
| `to` | date | Optional: End date filter |

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string                 // Category name
    parent?: BudgetCategoryDto   // Optional: Parent category
    type: enum                   // Category type
    color?: string               // Optional: HEX color code
    icon?: string                // Optional: Icon identifier
    createdAt: datetime
  }
  // ... more categories
]
```

**Status Codes:**
- `200 OK` - Categories list returned
- `500 Internal Server Error` - Unexpected error

---

#### GET /api/budget/categories/{id}

Gets a single budget category.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  id: guid
  userId: guid
  name: string
  parent?: BudgetCategoryDto
  type: enum
  color?: string
  icon?: string
  createdAt: datetime
}
```

**Status Codes:**
- `200 OK` - Category found
- `404 Not Found` - Category not found
- `500 Internal Server Error` - Unexpected error

---

#### POST /api/budget/categories

Creates a new budget category.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string                      // Required: Category name
  color?: string                    // Optional: HEX color code
  icon?: string                     // Optional: Icon identifier
  parent?: {
    kind: string                    // Optional: Parent relationship type
    id: guid                        // Optional: Parent category ID
  }
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full category object
}
```

**Status Codes:**
- `201 Created` - Category created
- `400 Bad Request` - Invalid input
- `500 Internal Server Error` - Unexpected error

---

#### PUT /api/budget/categories/{id}

Updates a budget category.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string        // Required: Updated name
  color?: string      // Optional: Updated color
  icon?: string       // Optional: Updated icon
}
```

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Category updated
- `404 Not Found` - Category not found
- `400 Bad Request` - Invalid input
- `500 Internal Server Error` - Unexpected error

---

#### DELETE /api/budget/categories/{id}

Deletes a budget category.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Category deleted
- `404 Not Found` - Category not found
- `500 Internal Server Error` - Unexpected error

---

### Budget Purposes

#### GET /api/budget/purposes

Lists budget purposes.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string
    description?: string
    createdAt: datetime
  }
  // ... more purposes
]
```

**Status Codes:**
- `200 OK` - Purposes list returned
- `500 Internal Server Error` - Unexpected error

---

#### GET /api/budget/purposes/{id}

Gets a single budget purpose.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  id: guid
  userId: guid
  name: string
  description?: string
  createdAt: datetime
}
```

**Status Codes:**
- `200 OK` - Purpose found
- `404 Not Found` - Purpose not found

---

#### POST /api/budget/purposes

Creates a new budget purpose.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string              // Required: Purpose name
  description?: string      // Optional: Description
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full purpose object
}
```

**Status Codes:**
- `201 Created` - Purpose created
- `400 Bad Request` - Invalid input

---

#### PUT /api/budget/purposes/{id}

Updates a budget purpose.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string        // Required: Updated name
  description?: string // Optional: Updated description
}
```

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Purpose updated
- `404 Not Found` - Purpose not found

---

#### DELETE /api/budget/purposes/{id}

Deletes a budget purpose.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Purpose deleted
- `404 Not Found` - Purpose not found

---

### Budget Rules

#### GET /api/budget/rules

Lists budget rules.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string
    categoryId: guid
    monthlyLimit: decimal
    isActive: boolean
    createdAt: datetime
  }
  // ... more rules
]
```

**Status Codes:**
- `200 OK` - Rules list returned

---

#### GET /api/budget/rules/{id}

Gets a single budget rule.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  id: guid
  userId: guid
  name: string
  categoryId: guid
  monthlyLimit: decimal
  isActive: boolean
  createdAt: datetime
}
```

**Status Codes:**
- `200 OK` - Rule found
- `404 Not Found` - Rule not found

---

#### POST /api/budget/rules

Creates a new budget rule.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string           // Required: Rule name
  categoryId: guid       // Required: Budget category ID
  monthlyLimit: decimal  // Required: Monthly spending limit
  isActive: boolean      // Required: Whether rule is active
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full rule object
}
```

**Status Codes:**
- `201 Created` - Rule created

---

#### PUT /api/budget/rules/{id}

Updates a budget rule.

**Authentication:** Required (JWT)

**Request Body:** Same structure as POST

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Rule updated
- `404 Not Found` - Rule not found

---

#### DELETE /api/budget/rules/{id}

Deletes a budget rule.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Rule deleted
- `404 Not Found` - Rule not found

---

### Budget Overrides

#### GET /api/budget/overrides

Lists budget overrides.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    categoryId: guid
    month: integer        // Month (1-12)
    year: integer
    amount: decimal
    createdAt: datetime
  }
  // ... more overrides
]
```

**Status Codes:**
- `200 OK` - Overrides list returned

---

#### POST /api/budget/overrides

Creates a budget override for a specific month.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  categoryId: guid
  month: integer        // 1-12
  year: integer
  amount: decimal
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full override object
}
```

**Status Codes:**
- `201 Created` - Override created

---

#### PUT /api/budget/overrides/{id}

Updates a budget override.

**Authentication:** Required (JWT)

**Request Body:** Same structure as POST

**Response (HTTP 204):** No Content

---

#### DELETE /api/budget/overrides/{id}

Deletes a budget override.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

### Budget Reports

#### GET /api/budget/report/summary

Gets budget summary for the current period.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | date | Start date |
| `to` | date | End date |
| `purpose` | guid | Optional: Filter by purpose |

**Response (HTTP 200):**
```typescript
{
  totalBudget: decimal
  totalSpent: decimal
  remaining: decimal
  percentageUsed: number
  byCategory: [
    {
      categoryId: guid
      categoryName: string
      budget: decimal
      spent: decimal
      remaining: decimal
    }
    // ... more categories
  ]
}
```

**Status Codes:**
- `200 OK` - Summary returned

---

#### GET /api/budget/report/monthly

Gets monthly budget reports for the given period.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `year` | integer | Year (4 digits) |
| `month` | integer | Month (1-12) |

**Response (HTTP 200):**
```typescript
{
  year: integer
  month: integer
  categories: [
    {
      categoryId: guid
      categoryName: string
      budget: decimal
      spent: decimal
      remaining: decimal
      percentageUsed: number
    }
    // ... more categories
  ]
  totals: {
    budget: decimal
    spent: decimal
    remaining: decimal
  }
}
```

**Status Codes:**
- `200 OK` - Monthly report returned

---

## Contacts

Manage contacts (individuals, organizations, banks) associated with postings and accounts.

### GET /api/contacts

Lists contacts for the current user.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Items to skip (default 0) |
| `take` | integer | Items per page (default 50, max 200) |
| `type` | enum | Filter by contact type |
| `all` | boolean | Return all contacts (ignores paging) |
| `q` | string | Search query (name, email, phone) |

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string
    type: enum                   // "Person", "Company", "Bank"
    email?: string
    phone?: string
    address?: string
    symbolAttachmentId?: guid
    createdAt: datetime
  }
  // ... more contacts
]
```

**Status Codes:**
- `200 OK` - Contacts list returned
- `500 Internal Server Error` - Unexpected error

---

### GET /api/contacts/{id}

Gets a single contact.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  id: guid
  userId: guid
  name: string
  type: enum
  email?: string
  phone?: string
  address?: string
  symbolAttachmentId?: guid
  createdAt: datetime
}
```

**Status Codes:**
- `200 OK` - Contact found
- `404 Not Found` - Contact not found

---

### POST /api/contacts

Creates a new contact.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string              // Required: Contact name
  type: enum                // Required: Type
  email?: string            // Optional: Email address
  phone?: string            // Optional: Phone number
  address?: string          // Optional: Address
  symbolAttachmentId?: guid // Optional: Symbol/icon
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full contact object
}
```

**Status Codes:**
- `201 Created` - Contact created
- `400 Bad Request` - Invalid input

---

### PUT /api/contacts/{id}

Updates a contact.

**Authentication:** Required (JWT)

**Request Body:** Same structure as POST

**Response (HTTP 200):**
```typescript
{
  id: guid
  // ... updated contact object
}
```

**Status Codes:**
- `200 OK` - Contact updated
- `404 Not Found` - Contact not found

---

### DELETE /api/contacts/{id}

Deletes a contact.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

**Status Codes:**
- `204 No Content` - Contact deleted
- `404 Not Found` - Contact not found

---

### POST /api/contacts/{id}/symbol/{attachmentId}

Assigns a symbol to a contact.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

### DELETE /api/contacts/{id}/symbol

Removes a contact's symbol.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

### GET /api/contacts/count

Gets the total count of contacts for the current user.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  count: number
}
```

---

### PUT /api/contacts/{id}/merge

Merges another contact into this one.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  sourceContactId: guid  // Contact to merge from
}
```

**Response (HTTP 200):**
```typescript
{
  id: guid
  // ... merged contact object
}
```

---

## Contact Categories

### GET /api/contact-categories

Lists contact categories.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string
    description?: string
    color?: string
  }
  // ... more categories
]
```

---

### POST /api/contact-categories

Creates a contact category.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string           // Required
  description?: string   // Optional
  color?: string         // Optional: HEX color code
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full category object
}
```

---

### PUT /api/contact-categories/{id}

Updates a contact category.

**Authentication:** Required (JWT)

**Request Body:** Same as POST

**Response (HTTP 204):** No Content

---

### DELETE /api/contact-categories/{id}

Deletes a contact category.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

## Securities

Manage investment securities/stocks.

### GET /api/securities

Lists securities for the current user.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Items to skip (default 0) |
| `take` | integer | Items per page (default 100, max 200) |
| `search` | string | Search by ISIN or name |

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    isin: string                    // ISIN code
    name: string
    securityType: enum              // "Stock", "ETF", "Bond", etc.
    currencyCode: string
    currentPrice?: decimal
    lastPriceUpdate?: datetime
    categoryId?: guid
    symbolAttachmentId?: guid
    createdAt: datetime
  }
  // ... more securities
]
```

---

### GET /api/securities/{id}

Gets a single security.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  id: guid
  userId: guid
  isin: string
  name: string
  securityType: enum
  currencyCode: string
  currentPrice?: decimal
  lastPriceUpdate?: datetime
  categoryId?: guid
  symbolAttachmentId?: guid
  createdAt: datetime
}
```

---

### POST /api/securities

Creates a new security.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  isin: string              // Required: ISIN code
  name: string              // Required: Security name
  securityType: enum        // Required: Type
  currencyCode: string      // Required: Currency
  categoryId?: guid         // Optional: Category
  symbolAttachmentId?: guid // Optional: Symbol
}
```

**Response (HTTP 201):**
```typescript
{
  id: guid
  // ... full security object
}
```

---

### PUT /api/securities/{id}

Updates a security.

**Authentication:** Required (JWT)

**Request Body:** Same structure as POST

**Response (HTTP 204):** No Content

---

### DELETE /api/securities/{id}

Deletes a security.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

### GET /api/securities/{id}/prices

Gets price history for a security.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | date | Start date |
| `to` | date | End date |

**Response (HTTP 200):**
```typescript
[
  {
    date: datetime
    price: decimal
    currency: string
  }
  // ... more prices
]
```

---

### PUT /api/securities/{id}/price

Updates the current price for a security.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  price: decimal
  date?: datetime
}
```

**Response (HTTP 200):**
```typescript
{
  id: guid
  currentPrice: decimal
  lastPriceUpdate: datetime
}
```

---

## Security Categories

### GET /api/security-categories

Lists security categories.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    name: string
  }
  // ... more categories
]
```

---

### POST /api/security-categories

Creates a security category.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string
}
```

---

### PUT /api/security-categories/{id}

Updates a security category.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  name: string
}
```

---

### DELETE /api/security-categories/{id}

Deletes a security category.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

## Savings Plans

### GET /api/savings-plans

Lists savings plans.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    userId: guid
    accountId: guid
    name: string
    targetAmount: decimal
    currentAmount: decimal
    targetDate?: datetime
    isActive: boolean
    createdAt: datetime
  }
  // ... more plans
]
```

---

### GET /api/savings-plans/{id}

Gets a single savings plan.

**Authentication:** Required (JWT)

---

### POST /api/savings-plans

Creates a savings plan.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  accountId: guid
  name: string
  targetAmount: decimal
  targetDate?: datetime
}
```

---

### PUT /api/savings-plans/{id}

Updates a savings plan.

**Authentication:** Required (JWT)

---

### DELETE /api/savings-plans/{id}

Deletes a savings plan.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

## Savings Plan Categories

### GET /api/savings-plan-categories

Lists savings plan categories.

**Authentication:** Required (JWT)

---

### POST /api/savings-plan-categories

Creates a category.

**Authentication:** Required (JWT)

---

### PUT /api/savings-plan-categories/{id}

Updates a category.

**Authentication:** Required (JWT)

---

### DELETE /api/savings-plan-categories/{id}

Deletes a category.

**Authentication:** Required (JWT)

---

## Attachments

### GET /api/attachments

Lists attachments for an entity.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `entityKind` | string | Entity type (e.g., "posting", "account") |
| `entityId` | guid | Entity identifier |
| `skip` | integer | Items to skip |
| `take` | integer | Items per page |

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    entityKind: string
    entityId: guid
    fileName: string
    fileSize: number
    fileType: string
    uploadedAt: datetime
    url?: string
  }
  // ... more attachments
]
```

---

### POST /api/attachments/upload

Uploads a file attachment.

**Authentication:** Required (JWT)

**Request Body:** Multipart form data

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | File to upload |
| `entityKind` | string | Entity type |
| `entityId` | guid | Entity ID |
| `description` | string | Optional description |

**Response (HTTP 201):**
```typescript
{
  id: guid
  fileName: string
  fileSize: number
  uploadedAt: datetime
}
```

---

### DELETE /api/attachments/{id}

Deletes an attachment.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

### GET /api/attachments/{id}/download

Downloads an attachment file.

**Authentication:** Required (JWT)

**Response (HTTP 200):** File content with appropriate content type

---

## Reports

### GET /api/reports/portfolio

Gets portfolio summary/analysis.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  totalValue: decimal
  totalCost: decimal
  unrealizedGain: decimal
  gainPercentage: number
  byAssetClass: [
    {
      assetClass: string
      value: decimal
      percentage: number
    }
    // ... more asset classes
  ]
}
```

---

### GET /api/reports/account-overview

Gets overview of all accounts.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    accountId: guid
    accountName: string
    balance: decimal
    currency: string
    lastTransactionDate?: datetime
  }
  // ... more accounts
]
```

---

### GET /api/reports/cash-flow

Gets cash flow analysis for a period.

**Authentication:** Required (JWT)

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `from` | date | Start date |
| `to` | date | End date |

**Response (HTTP 200):**
```typescript
{
  income: decimal
  expenses: decimal
  netFlow: decimal
  byCategory: [
    {
      categoryName: string
      amount: decimal
    }
    // ... more categories
  ]
}
```

---

## Statement Drafts

### GET /api/statement-drafts

Lists pending statement import drafts.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    fileName: string
    importType: string
    postingCount: number
    status: enum              // "Draft", "Ready", "Error"
    uploadedAt: datetime
  }
  // ... more drafts
]
```

---

### POST /api/statement-drafts/upload

Uploads a statement file for import preview.

**Authentication:** Required (JWT)

**Request Body:** Multipart form data with statement file

**Response (HTTP 201):**
```typescript
{
  id: guid
  fileName: string
  postingCount: number
  preview: [
    {
      date: datetime
      text: string
      amount: decimal
    }
    // ... preview rows
  ]
}
```

---

### POST /api/statement-drafts/{id}/import

Confirms and imports a statement draft.

**Authentication:** Required (JWT)

**Request Body:**
```typescript
{
  accountId: guid
  importStrategy?: string  // Optional: import strategy
}
```

**Response (HTTP 202):** Accepted
```typescript
{
  taskId: guid
  status: "Pending"
}
```

---

### DELETE /api/statement-drafts/{id}

Deletes a statement draft without importing.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

## Notifications

### GET /api/notifications

Lists active notifications for the current user.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    type: string
    title: string
    message: string
    priority: enum                // "Low", "Medium", "High"
    isRead: boolean
    validFrom: datetime
    validTo?: datetime
    createdAt: datetime
  }
  // ... more notifications
]
```

---

### POST /api/notifications/{id}/dismiss

Dismisses a notification.

**Authentication:** Required (JWT)

**Response (HTTP 204):** No Content

---

### GET /api/notifications/count

Gets count of unread notifications.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  count: number
}
```

---

## Background Tasks

### GET /api/background-tasks

Lists background tasks for the current user.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    taskType: string
    status: enum              // "Pending", "Running", "Completed", "Failed"
    progress: number          // 0-100
    createdAt: datetime
    completedAt?: datetime
  }
  // ... more tasks
]
```

---

### GET /api/background-tasks/{id}

Gets status of a specific background task.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  id: guid
  taskType: string
  status: enum
  progress: number
  result?: object
  error?: string
  createdAt: datetime
  completedAt?: datetime
}
```

---

## Backups

### POST /api/backups/create

Initiates a backup of user data.

**Authentication:** Required (JWT)

**Response (HTTP 202):** Accepted
```typescript
{
  taskId: guid
  status: "Pending"
}
```

---

### GET /api/backups

Lists available backups.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    createdAt: datetime
    size: number
    fileCount: number
  }
  // ... more backups
]
```

---

### POST /api/backups/{id}/restore

Restores data from a backup.

**Authentication:** Required (JWT)

**Response (HTTP 202):** Accepted
```typescript
{
  taskId: guid
  status: "Pending"
}
```

---

## Home KPIs

### GET /api/home/kpis

Gets key performance indicators for the home dashboard.

**Authentication:** Required (JWT)

**Response (HTTP 200):**
```typescript
{
  totalAssets: decimal
  monthlyIncome: decimal
  monthlyExpenses: decimal
  savingsRate: number
  netWorth: decimal
  topExpenseCategory: {
    name: string
    amount: decimal
  }
}
```

---

## Meta Holiday Providers

### GET /api/meta/holiday-providers

Lists available holiday data providers.

**Authentication:** Optional

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    name: string
    description?: string
    supportedCountries: string[]
  }
  // ... more providers
]
```

---

### GET /api/meta/holidays

Gets holidays for a specific provider and date range.

**Authentication:** Optional

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `provider` | string | Provider identifier |
| `country` | string | Country code (ISO 3166-1 alpha-2) |
| `year` | integer | Year |

---

## Admin

Administrative endpoints (requires admin privileges).

### GET /api/admin/users

Lists all users in the system.

**Authentication:** Required (JWT + Admin)

**Response (HTTP 200):**
```typescript
[
  {
    id: guid
    username: string
    email?: string
    createdAt: datetime
    lastLogin?: datetime
    isActive: boolean
    isAdmin: boolean
  }
  // ... more users
]
```

---

### GET /api/admin/users/{id}

Gets a single user's admin details.

**Authentication:** Required (JWT + Admin)

---

### PUT /api/admin/users/{id}/password

Resets a user's password.

**Authentication:** Required (JWT + Admin)

**Request Body:**
```typescript
{
  newPassword: string  // Required: New password
}
```

**Response (HTTP 204):** No Content

---

### PUT /api/admin/users/{id}/unlock

Unlocks a user account.

**Authentication:** Required (JWT + Admin)

**Response (HTTP 204):** No Content

---

### GET /api/admin/system-status

Gets system status and statistics.

**Authentication:** Required (JWT + Admin)

**Response (HTTP 200):**
```typescript
{
  version: string
  uptime: number         // Milliseconds
  userCount: number
  postingCount: number
  databaseSize: number   // Bytes
}
```

---

