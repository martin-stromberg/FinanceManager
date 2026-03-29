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

If you want I can:
- expand DTO schemas with formal property types and nullable annotations,
- generate a machine-readable JSON Schema or OpenAPI components snippet for the models above,
- or add the remaining domain DTOs (Postings, SavingsPlan, Security) to this file.