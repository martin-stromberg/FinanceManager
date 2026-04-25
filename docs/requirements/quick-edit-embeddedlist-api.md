# API Specification — QuickEdit Batch Update

Purpose: Define the backend API for atomic batch updates of statement draft entries used by the QuickEdit EmbeddedList feature.

Endpoint
--------
POST /api/statement-drafts/{draftId}/entries/batch-update

- Auth: Bearer JWT. Caller must own draft or have write permission.
- Content-Type: application/json
- Response: JSON

Security
--------
- Enforce ownership/permission server-side. Return 403 when caller is not allowed to modify the draft.
- Log attempts (userId, draftId, changedCount) at Information level, do not log sensitive full-text (no full file contents).

Request DTO (C#)
----------------
```csharp
public sealed class BatchUpdateRequestDto
{
    public List<EntryUpdateDto> Updates { get; set; } = new();
}

public sealed class EntryUpdateDto
{
    public Guid EntryId { get; set; }
    // Dictionary of field key -> new value. Only supported keys will be considered.
    public Dictionary<string, object?> Fields { get; set; } = new();
}
```

Supported field keys (example)
- "BookingDate" — ISO-8601 date (date or date-time) string (e.g. "2026-04-01" or "2026-04-01T00:00:00Z")
- "Subject" — string
- "RecipientName" — string
- "Amount" — decimal (number)
- "Status" — string or integer corresponding to StatementDraftEntryStatus

Server should ignore unknown field keys and return validation error for malformed values.

Success Response (200)
----------------------
- When all updates validate and are applied atomically, return 200 with updated draft snapshot.

Example C# DTO
```csharp
public sealed class BatchUpdateSuccessResponseDto
{
    public bool Success { get; set; } = true;
    // Updated draft snapshot (re-use existing DTO)
    public StatementDraftDetailDto? UpdatedDraft { get; set; }
}
```

Validation Failure (400)
------------------------
- If any row or field fails server-side validation, the entire batch MUST NOT be applied. Return 400 with per-row error details.

Error DTO
```csharp
public sealed class BatchUpdateErrorResponseDto
{
    public bool Success { get; set; } = false;
    public List<EntryErrorDto> Errors { get; set; } = new();
}

public sealed class EntryErrorDto
{
    public Guid EntryId { get; set; }
    public List<FieldErrorDto> FieldErrors { get; set; } = new();
}

public sealed class FieldErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty; // localizable key optional
}
```

Permission Denied
-----------------
- 403 Forbidden with standard ProblemDetails when the caller is not allowed to modify the draft.

Server Errors
-------------
- 500 ProblemDetails for unexpected failures. Ensure transaction rollback.

Behavior & Transactional Requirements
-------------------------------------
1. Validate every requested update:
   - Entry exists and belongs to the given draft
   - Entry is mutable (business rule: e.g., not AlreadyBooked)
   - Fields have correct formats and business constraints (e.g., Amount non-empty; BookingDate sensible; Status allowed)
2. If any validation fails, return 400 with per-entry field errors; do not persist any changes.
3. If all validations pass, apply changes in a single DB transaction and return updated draft snapshot (200).
4. Log attempt: Information-level with user id, draftId, changedCount, result.

Example Request JSON
--------------------
```json
{
  "updates": [
    {
      "entryId": "d7b9f760-0c6f-4b1d-9e0b-1a2b3c4d5e6f",
      "fields": {
        "BookingDate": "2026-04-01",
        "RecipientName": "ACME GmbH",
        "Amount": 139.00
      }
    },
    {
      "entryId": "c6a5f123-1234-4abc-9e0b-111213141516",
      "fields": {
        "Subject": "Correction",
        "Amount": 200.50
      }
    }
  ]
}
```

Example Error Response (400)
----------------------------
```json
{
  "success": false,
  "errors": [
    {
      "entryId": "d7b9f760-0c6f-4b1d-9e0b-1a2b3c4d5e6f",
      "fieldErrors": [
        { "field": "Amount", "message": "Amount must be greater than zero" }
      ]
    },
    {
      "entryId": "c6a5f123-1234-4abc-9e0b-111213141516",
      "fieldErrors": [
        { "field": "BookingDate", "message": "Invalid date format" }
      ]
    }
  ]
}
```

Controller Skeleton (C#)
------------------------
```csharp
[ApiController]
[Route("api/statement-drafts/{draftId:guid}/entries")]
public sealed class StatementDraftEntriesController : ControllerBase
{
    private readonly IStatementDraftService _service;
    private readonly ILogger<StatementDraftEntriesController> _logger;

    public StatementDraftEntriesController(IStatementDraftService service, ILogger<StatementDraftEntriesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("batch-update")]
    public async Task<IActionResult> BatchUpdate(Guid draftId, [FromBody] BatchUpdateRequestDto req, CancellationToken ct)
    {
        // Ownership/permission check inside service
        var result = await _service.ApplyBatchEntryUpdatesAsync(draftId, req, User, ct);
        if (!result.Success)
            return BadRequest(result.ErrorResponse);
        return Ok(result.SuccessResponse);
    }
}
```

Service Behavior
----------------
- `ApplyBatchEntryUpdatesAsync` should:
  1. Validate permission
  2. Load current draft and involved entries (single query)
  3. Validate each update (format + business rules)
  4. If any validation errors -> return ErrorResponseDto
  5. Begin DB transaction
  6. Apply updates to entities, update modification metadata
  7. Commit transaction
  8. Return SuccessResponse with updated draft snapshot

Notes
-----
- Keep DTO contract stable; prefer explicit typed request DTOs if the field set is small or create typed `EntryUpdateDto` with nullable typed properties instead of `Dictionary<string,object?>` to get stronger validation and model binding. The dictionary approach is more flexible but requires careful parsing and type checks server-side.
- Use localization keys for validation messages; return localized message or key depending on API policy.

References
----------
- Related requirement: FR-6, FR-7
- Implementation tasks: T1, T6, T7
