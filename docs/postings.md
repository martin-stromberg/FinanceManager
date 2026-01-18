# Postings (Table `Postings`) – Functional Documentation

This document describes how the `Postings` table is used in FinanceManager, how different posting kinds are created, and what the linking fields mean.

> Scope: This describes **postings** (domain entity `FinanceManager.Domain.Postings.Posting`) as they exist in the database after booking/import. It deliberately does **not** document UI details.

## 1. Concept

A posting is a single transaction line that can belong to different “contexts”:

- a **bank account** (bank statement line)
- a **contact** (who the money was paid to / received from)
- a **savings plan** (allocation of money into a plan)
- a **security** (investment transaction)

For one real-world statement line, the system may create **multiple postings** to represent different perspectives of the same transaction (e.g., bank + contact + savings plan). These postings are typically connected via `GroupId` and/or `SourceId`.

## 2. Table / Entity shape

Domain type: `FinanceManager.Domain.Postings.Posting`

Important fields (as persisted by EF Core):

| Field | Type | Meaning |
|---|---:|---|
| `Id` | `Guid` | Primary key of the posting. |
| `SourceId` | `Guid` | Identifier of the source record that created the posting (e.g. statement entry id / import id). Multiple postings from the same source share the same `SourceId`. |
| `Kind` | `PostingKind` | Context type of the posting (Bank/Contact/SavingsPlan/Security). |
| `AccountId` | `Guid?` | Set for `Kind=Bank` postings; points to the affected bank account. |
| `ContactId` | `Guid?` | Set for `Kind=Contact` postings; points to the contact context. Can also be set for other kinds to indicate the “counterparty” contact (e.g. self-contact for savings-plan related postings). |
| `SavingsPlanId` | `Guid?` | Set for `Kind=SavingsPlan` postings; points to the plan context. |
| `SecurityId` | `Guid?` | Set for `Kind=Security` postings; points to the security context. |
| `BookingDate` | `DateTime` | Booking date of the transaction. |
| `ValutaDate` | `DateTime` | Value date; can differ from booking date.
| `Amount` | `decimal` | Signed amount. Convention: positive/negative depends on transaction direction (account perspective). |
| `Subject` | `string?` | Optional short subject (often from statement entry). |
| `RecipientName` | `string?` | Optional receiver/sender name.
| `Description` | `string?` | Optional detailed description.
| `GroupId` | `Guid` | Groups postings that represent the same real-world operation (same transaction, split, or mirrored postings). `Guid.Empty` means “not grouped”. |
| `ParentId` | `Guid?` | Points to a parent posting (used for split/derived postings). |
| `LinkedPostingId` | `Guid?` | Points to the opposite posting of a self-transfer (two-sided link). |
| `SecuritySubType` | `SecurityPostingSubType?` | Security-specific subtype.
| `Quantity` | `decimal?` | Security quantity; only relevant for security postings.

### 2.1 PostingKind

`PostingKind` is defined in `FinanceManager.Shared.Dtos.Postings.PostingKind`:

- `Bank` – posting is attached to a bank account
- `Contact` – posting is attached to a contact
- `SavingsPlan` – posting is attached to a savings plan
- `Security` – posting is attached to a security

## 3. How postings are created

### 3.1 Booking a statement draft entry (most common)

When a bank statement (CSV/PDF) is uploaded, it is parsed into statement draft entries. When a draft is **booked**, the system creates postings.

Typical outcome for one statement entry:

1. **Bank posting** (`Kind=Bank`, `AccountId=<account>`) – represents the account movement.
2. **Contact posting** (`Kind=Contact`, `ContactId=<assigned contact>`) – represents the same movement grouped for the counterparty.
3. Optional: **SavingsPlan posting** (`Kind=SavingsPlan`, `SavingsPlanId=<assigned plan>`) – if the entry was assigned to a savings plan.
4. Optional: **Security posting** (`Kind=Security`, `SecurityId=<assigned security>`) – if the entry was assigned to a security.

All postings created from the same statement entry share the same:

- `SourceId` (statement entry id)
- `GroupId` (one group per real-world transaction)

This grouping is the basis for features like:

- resolving the “origin” of a posting (bank/contact/savings plan)
- filtering mirrored postings (see §4.1)

### 3.2 Manually created draft entries

Drafts can contain manually added entries. After booking, they behave like normal statement imported entries: they create one or multiple postings and receive a `SourceId` and usually a `GroupId`.

### 3.3 Savings plan related postings (mirror pattern)

For savings plan entries, the system typically creates:

- a `Kind=SavingsPlan` posting that references `SavingsPlanId`
- a `Kind=Contact` posting that references the **self-contact** as `ContactId`

Both postings are grouped (`GroupId`), representing the fact that the plan movement is mirrored in the user’s own contact ledger.

This “mirror” is important for budget calculations:

- budgets can be defined on savings plans
- the mirrored self-contact postings should usually not appear as “unbudgeted” when the savings plan is budget-covered

### 3.4 Security postings

A statement entry can be mapped to a security transaction. The created posting(s) may additionally carry:

- `SecurityId`
- `SecuritySubType`
- `Quantity`

## 4. Relationships between postings

### 4.1 `GroupId` (same real-world transaction)

`GroupId` links postings that represent the same real-world action.

Examples:

- **Bank + Contact** postings created from the same statement line.
- **SavingsPlan + mirrored Contact** posting created from a savings plan allocated statement entry.
- Grouped postings can be looked up via `/api/postings/group/{groupId}`.

Practical meaning:

- If a savings plan posting is **budget-covered**, then contact postings in the same `GroupId` can be treated as covered mirror postings.

### 4.2 `SourceId` (same source record)

`SourceId` identifies the origin record that produced the posting, typically a statement entry. All postings created from the same statement entry share the same `SourceId`.

`SourceId` is used for:

- tracing postings back to imported data
- bulk operations or debugging

### 4.3 `LinkedPostingId` (self-transfer counterpart)

`LinkedPostingId` links two postings that are the two sides of a self-transfer.

Typical use:

- transfer between two accounts
- represent debit on one account and credit on another

### 4.4 `ParentId` (splits / derived postings)

`ParentId` is used when one posting is derived from another (e.g., split transactions).

- Parent: original posting
- Child: derived/split posting

## 5. Budget-related implications (why this matters)

Budget features typically operate on:

- contact postings (`ContactId != null`)
- and exclude postings that are already covered by purposes

However, savings plan allocations can create **mirrored self-contact contact postings**.

To avoid mismatches like:

- displayed “Unbudgeted actual” != sum of the listed unbudgeted postings

the system must treat mirror contact postings as covered **when** the corresponding savings plan posting is covered. This is usually implemented by resolving mirror postings via `GroupId`.

## 6. Known limitations / remarks

- `GroupId` may be `Guid.Empty` for legacy postings or postings created outside the normal booking pipeline.
- In that case, group-based mirror detection cannot work reliably.

