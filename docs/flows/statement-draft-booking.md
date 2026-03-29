# Statement Draft Booking Flow

This document describes the booking flow for a statement draft entry (from validation to postings). It contains a Mermaid flowchart and a short explanation.

## Mermaid diagram

```mermaid
flowchart TD
  A[Start: Book Request] --> B[Validate Draft & Entries]
  B --> |Errors| C[Return Errors, Stop]
  B --> |Warnings & not forced| D[Return Warnings]
  B --> |OK| E[Create Postings per Entry]
  E --> F{Is Split Parent?}
  F --> |Yes| G[Create zero-amount parent postings]
  G --> H[Book child drafts, create child postings, set parents]
  F --> |No| I[Create bank + contact postings]
  I --> J[Optional savings posting]
  I --> K[Optional security postings]
  J --> L[Update aggregates]
  K --> L
  H --> L
  L --> M[Commit draft / Remove booked entries]
  M --> N[Propagate attachments]
  N --> O[Refresh reports cache]
  O --> P[Return success]
```

## Notes
- Validation ensures account and contact constraints, savings plan and security rules.
- Security postings are only created when the detected account allows security processing and the contact equals the bank contact of the account.
- Partial booking (single entry) keeps draft open and removes only processed entry.
- Split booking creates parent zero postings and books grouped child drafts.
