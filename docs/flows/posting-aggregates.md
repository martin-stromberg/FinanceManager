# Posting Aggregates Flow

Mermaid and notes describing how posting aggregates are upserted and used in reports.

```mermaid
flowchart TD
  A[Posting created] --> B[Upsert Aggregates]
  B --> C[Update Period Buckets (month/quarter/year)]
  C --> D[Persist Aggregate Changes]
  D --> E[Report queries read aggregates]
```

Notes:
- Aggregates are created per DateKind (Booking and Valuta) across period buckets.
- Unique indexes enforce one aggregate per (kind, account/contact/plan/security, period, datekind) where applicable.
