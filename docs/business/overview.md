# Business Overview

This document provides a high-level description of the main functional areas of FinanceManager.

## Functional Areas

- Scanning & Import
  - Upload bank statement files (CSV, PDF, backup JSON).
  - Parsers extract movements and create statement drafts.
  - Drafts can be split, assigned to accounts and enriched via details files.

- Classification & Auto-Matching
  - Heuristics match recipients to contacts, savings plans and securities.
  - Auto-assign occurs on import and on explicit classification endpoints.
  - Validation checks domain rules (savings plan eligibility, security constraints).

- Booking
  - Convert draft entries into postings (bank, contact, savings plan, security).
  - Supports partial booking and split-drafts grouping logic.
  - Booking is transaction-safe: a draft-level guard prevents parallel double processing, and repeated bookings return deterministic conflict/processed responses instead of creating duplicates.
  - **Budgetwirkung bei Buchung (neu):** Bei Kontakt-/Sparplanzuordnung sowie beim finalen Buchen können Budget-Hinweise und eine Abschluss-Summary mit Vorher/Nachher/Delta zurückgegeben werden.

- Attachments & Symbols
  - Upload attachments and assign them to drafts, entries or postings.
  - Symbol resolution: account -> bank contact -> contact category.

- Security (Securities)
  - Security postings (Buy/Sell/Dividend + Fee/Tax) are created only when the account allows security processing and the entry's contact is the account's bank contact.
  - **Return Analysis (Renditeanalyse):** A dedicated performance analysis is available for each security at `/securities/{id}/performance`. It provides TWR, IRR, CAGR, volatility, max drawdown, realized/unrealized gains, cashflow timeline, and optional benchmark comparison and Sharpe Ratio. A compact summary widget is embedded on the security detail page. See [`docs/business/features/F017-renditeanalyse.md`](./features/F017-renditeanalyse.md) for the user-facing documentation and [`docs/business/features/F017-renditeanalyse-domain.md`](./features/F017-renditeanalyse-domain.md) for business rules and domain logic.

- Savings Plans
  - Savings plans allow users to define targets or recurring contributions that draft entries can be assigned to.
  - Plans can be one-time or recurring and have properties like `TargetAmount`, `TargetDate`, `Interval` and `ContractNumber`.
  - Classification will attempt to auto-assign matching plans for `Self` transfers; booking creates `SavingsPlan` postings and may advance recurring plan dates.
  - See `docs/business/savings-plans.md` for full business rules and edge cases.

- Backups & Restore
  - Backup DTOs capture entity state; restore must preserve sensible defaults (backwards compatibility).

## Important Domain Rules
- Savings plans can only be attached to Self contacts and may be constrained per account.
- Security entries require the bank contact and may be disabled per account (SecurityProcessingEnabled).

## Cross-References (detailed docs)

- Booking flow and validation: `docs/flows/statement-draft-booking.md`
- Import & classification internals: `docs/flows/import-classification.md`
- Posting aggregates & reporting: `docs/flows/posting-aggregates.md`
- Split / UploadGroup handling: `docs/flows/split-uploadgroup.md`
- Savings plans (detailed rules): `docs/business/savings-plans.md`
- Budgetwirkung bei Buchung: `docs/business/features/F018-budgetwirkung-buchung.md`
- Security processing rules: `docs/business/security-processing.md`
 - Budget planning: `docs/business/budget-planning.md`
 - Reporting & KPIs (start page tiles): `docs/business/reporting-kpis.md`
 - Administration: `docs/business/administration.md`
- API models and OpenAPI: `docs/api/models.md`, `docs/api/openapi.yaml`
- API endpoints overview and controller docs: `docs/api/`

For more details consult the linked documents. If a missing detailed doc is required, open an issue or request it here and it will be produced.
