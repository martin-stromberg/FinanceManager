using System.Collections.Generic;
using System;

namespace FinanceManager.Shared.Dtos.Statements
{
    /// <summary>
    /// Request DTO for batch updating draft entries.
    /// </summary>
    public sealed class BatchUpdateRequestDto
    {
        /// <summary>List of entry updates to apply.</summary>
        public List<EntryUpdateDto> Updates { get; set; } = new();
    }

    /// <summary>Update for a single entry.</summary>
    public sealed class EntryUpdateDto
    {
        /// <summary>Entry identifier.</summary>
        public Guid EntryId { get; set; }
        /// <summary>Field key -> value mapping for changed fields.</summary>
        public Dictionary<string, object?> Fields { get; set; } = new();
    }

    /// <summary>Success response for batch update.</summary>
    public sealed class BatchUpdateSuccessResponseDto
    {
        /// <summary>Success flag (true).</summary>
        public bool Success { get; set; } = true;
        /// <summary>Updated draft snapshot when update succeeded.</summary>
        public StatementDraftDetailDto? UpdatedDraft { get; set; }
    }

    /// <summary>Error response containing per-entry validation failures.</summary>
    public sealed class BatchUpdateErrorResponseDto
    {
        /// <summary>Success flag (false).</summary>
        public bool Success { get; set; } = false;
        /// <summary>Per-entry errors describing which fields failed validation.</summary>
        public List<EntryErrorDto> Errors { get; set; } = new();
    }

    /// <summary>Errors for a single entry.</summary>
    public sealed class EntryErrorDto
    {
        /// <summary>Entry identifier the errors belong to.</summary>
        public Guid EntryId { get; set; }
        /// <summary>Field-level errors for this entry.</summary>
        public List<FieldErrorDto> FieldErrors { get; set; } = new();
    }

    /// <summary>Field-level validation error.</summary>
    public sealed class FieldErrorDto
    {
        /// <summary>Field key.</summary>
        public string Field { get; set; } = string.Empty;
        /// <summary>Human readable message (localized by caller/service).</summary>
        public string Message { get; set; } = string.Empty;
    }
}
