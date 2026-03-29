# Security Processing (Business Rules)

This document describes business rules around securities processing in statement drafts and bookings.

Rules:
- Security-related fields (SecurityId, TransactionType, Quantity, Fee, Tax) may only be assigned when:
  - The draft has a detected account AND
  - The detected account's `SecurityProcessingEnabled` flag is true AND
  - The entry's contact equals the bank contact of the detected account.
- Automatic classification will not assign securities for drafts where the detected account does not allow security processing.
- Import of external detail files will not apply security details to drafts whose detected account has security processing disabled.
- Booking will validate these constraints and return domain validation errors:
  - `SECURITY_ACCOUNT_NOT_ALLOWED` - account disabled security processing
  - `SECURITY_INVALID_CONTACT` - contact is not bank contact
  - `SECURITY_MISSING_TXTYPE` / `SECURITY_MISSING_QUANTITY` etc.

Notes:
- Document how to enable/disable `SecurityProcessingEnabled` per account in Admin or via API.
