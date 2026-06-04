# API Models (DTOs)

This document lists commonly used request and response DTOs referenced in the API docs. Each model shows property names, types, brief description and a JSON example.

---

## AccountDto (response)
Represents a bank account exposed by the API.

Properties
- `id` (GUID) — account identifier
- `ownerUserId` (GUID) — owner user id
- `type` (int) — enum `AccountType` (e.g. 0 = Giro/Checking)
- `name` (string) — display name
- `iban` (string?) — IBAN or null
- `currentBalance` (decimal) — current balance in account currency
- `bankContactId` (GUID) — contact id for the bank
- `symbolAttachmentId` (GUID?) — optional attachment id used as symbol
- `savingsPlanExpectation` (int) — enum `SavingsPlanExpectation`
- `securityProcessingEnabled` (bool) — whether securities processing is allowed for this account
- `createdUtc` (string, ISO 8601) — creation timestamp
- `modifiedUtc` (string?, ISO 8601) — last modified timestamp or null

Example
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "ownerUserId": "00000000-0000-0000-0000-000000000000",
  "type": 0,
  "name": "Girokonto",
  "iban": "DE00123456781234567890",
  "currentBalance": 1234.56,
  "bankContactId": "11111111-1111-1111-1111-111111111111",
  "symbolAttachmentId": null,
  "savingsPlanExpectation": 1,
  "securityProcessingEnabled": true,
  "createdUtc": "2024-01-01T12:00:00Z",
  "modifiedUtc": null
}
```

---

## AccountCreateRequest (request)
Payload to create a new account.

Properties
- `name` (string) — required
- `type` (int) — account type enum
- `iban` (string?) — optional
- `bankContactId` (GUID?) — optional existing contact id
- `newBankContactName` (string?) — optional name to create a bank contact
- `symbolAttachmentId` (GUID?) — optional attachment id
- `savingsPlanExpectation` (int) — enum value, optional
- `securityProcessingEnabled` (bool) — optional, default true

Example
```json
{
  "name": "My Checking",
  "type": 0,
  "iban": "DE89123456781234567890",
  "bankContactId": null,
  "newBankContactName": "My Bank GmbH",
  "symbolAttachmentId": null,
  "savingsPlanExpectation": 1,
  "securityProcessingEnabled": true
}
```

Notes
- Either `bankContactId` or `newBankContactName` may be provided. When both are null a bank contact will be auto-created from the account name or IBAN.

---

## AccountUpdateRequest (request)
Payload to update an existing account.

Properties
- `name` (string) — required
- `iban` (string?) — optional
- `bankContactId` (GUID?) — optional; if not provided `newBankContactName` must be set
- `newBankContactName` (string?) — optional
- `symbolAttachmentId` (GUID?) — optional to assign symbol
- `savingsPlanExpectation` (int) — optional enum
- `securityProcessingEnabled` (bool) — optional

Example
```json
{
  "name": "My Checking (Updated)",
  "iban": "DE89123456781234567890",
  "bankContactId": "11111111-1111-1111-1111-111111111111",
  "newBankContactName": null,
  "symbolAttachmentId": null,
  "savingsPlanExpectation": 1,
  "securityProcessingEnabled": false
}
```

---

## ContactDto (response)
Minimal contact representation used across endpoints.

Properties
- `id` (GUID)
- `ownerUserId` (GUID)
- `name` (string)
- `type` (int) — enum `ContactType` (e.g. Self, Person, Organization, Bank)
- `isPaymentIntermediary` (bool)
- `symbolAttachmentId` (GUID?)

Example
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "ownerUserId": "00000000-0000-0000-0000-000000000000",
  "name": "My Bank GmbH",
  "type": 2,
  "isPaymentIntermediary": false,
  "symbolAttachmentId": null
}
```

---

## AttachmentDto (response)
Represents a stored attachment.

Properties
- `id` (GUID)
- `fileName` (string)
- `contentType` (string)
- `sizeBytes` (long)
- `uploadedUtc` (string, ISO 8601)

Example
```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "fileName": "logo.png",
  "contentType": "image/png",
  "sizeBytes": 10240,
  "uploadedUtc": "2024-01-01T12:00:00Z"
}
```

---

## Draft & Entry DTOs (short)
For full request/response models used by `StatementDraftsController` see `docs/api/StatementDraftsController.md`.

`StatementDraftDto` and `StatementDraftEntryDto` contain many fields (IDs, booking/valuta dates, amounts, contactId, security fields). Use the controller docs for examples.

---

## PostingServiceDto (response)
Extended posting representation returned by all posting query endpoints.

Properties
- `id` (GUID) — unique posting identifier
- `bookingDate` (string, ISO 8601) — booking date
- `valutaDate` (string, ISO 8601) — valuta / value date
- `amount` (decimal) — posting amount (negative = debit)
- `kind` (int) — enum `PostingKind` (0 = Bank, 1 = Contact, 2 = SavingsPlan, 3 = Security)
- `accountId` (GUID?) — bank account id, or `null`
- `contactId` (GUID?) — contact id, or `null`
- `savingsPlanId` (GUID?) — savings plan id, or `null`
- `securityId` (GUID?) — security id, or `null`
- `sourceId` (GUID) — original domain source id for traceability
- `subject` (string?) — subject / title
- `recipientName` (string?) — counterparty name
- `description` (string?) — additional booking description
- `securitySubType` (int?) — enum `SecurityPostingSubType`, only set for security postings
- `quantity` (decimal?) — share quantity, only set for security postings
- `groupId` (GUID) — group id connecting related postings (empty GUID when ungrouped)
- `linkedPostingId` (GUID?) — counterpart posting id
- `linkedPostingKind` (int?) — counterpart posting kind
- `linkedPostingAccountId` (GUID?) — counterpart posting account id
- `linkedPostingAccountSymbolAttachmentId` (GUID?) — counterpart account symbol
- `linkedPostingAccountName` (string?) — counterpart account name
- `bankPostingAccountId` (GUID?) — bank posting account id for the group
- `bankPostingAccountSymbolAttachmentId` (GUID?) — bank posting account symbol
- `bankPostingAccountName` (string?) — bank posting account name
- `isReversed` (bool) — `true` when a reversal counter-posting exists for this posting *(added in feature 140)*
- `isReversal` (bool) — `true` when this posting itself is a reversal / counter-posting *(added in feature 140)*
- `reversedByPostingId` (GUID?) — ID of the reversal counter-posting; populated when `isReversed = true` *(added in feature 140)*
- `reversalForPostingId` (GUID?) — ID of the original posting this reversal cancels; populated when `isReversal = true` *(added in feature 140)*

Example
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

---

## ReversalResultDto (response)
Result of a successful `POST /api/postings/{id}/reverse` call.  
See [PostingsController – Reverse a Posting](./PostingsController.md#2-reverse-a-posting).

Properties
- `reversedPostingIds` (GUID[]) — IDs of the original postings that were reversed *(required)*
- `createdReversalIds` (GUID[]) — IDs of the newly created reversal / counter-postings *(required)*
- `statementImportId` (GUID) — ID of the statement import created for reconciliation *(required)*

Example
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

---

## ReversalValidationDto (response)
Validation result returned by `GET /api/postings/{id}/validate-reversal`.  
See [PostingsController – Validate Posting Reversal](./PostingsController.md#3-validate-posting-reversal).

Properties
- `isValid` (bool) — `true` when the reversal can proceed without errors *(required)*
- `errors` (string[]) — list of human-readable validation error messages; empty when `isValid = true` *(required)*

Example – valid
```json
{
  "isValid": true,
  "errors": []
}
```

Example – invalid
```json
{
  "isValid": false,
  "errors": [
    "This posting has already been reversed.",
    "A reversal posting cannot itself be reversed."
  ]
}
```