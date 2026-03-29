# Import & Classification Flow

Mermaid diagram and notes describing how uploaded statement files are parsed, split into drafts, classified and enriched.

```mermaid
flowchart TD
  A[Upload file] --> B[Select Parser]
  B --> C[Parse Movements]
  C --> D[Create Drafts] 
  D --> E[Classify Header (detect account)]
  E --> F[Classify Entries (contacts, savings, securities)]
  F --> G[Persist Drafts]
  G --> H[Optional: AddStatementDetails (fees/taxes) -> enrich matching entries]
```

Notes:
- Parsers include CSV, PDF and Backup JSON readers.
- Classification uses alias patterns and security identifier matching; respects account flags (e.g., SecurityProcessingEnabled).
- Minimum entries per draft merge logic is applied for monthly grouping.
