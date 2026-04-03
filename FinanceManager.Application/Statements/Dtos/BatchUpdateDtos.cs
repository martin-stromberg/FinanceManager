using System.Collections.Generic;
using System;

namespace FinanceManager.Application.Statements.Dtos
{
    /// <summary>
    /// Request DTO for batch updating draft entries.
    /// </summary>
    public sealed class BatchUpdateRequestDto
    {
        /// <summary>
        /// List of entry updates to apply.
        /// </summary>
        public List<EntryUpdateDto> Updates { get; set; } = new();
    }

    /// <summary>
    /// Represents an update for a single draft entry.
    /// </summary>
    public sealed class EntryUpdateDto
    {
        /// <summary>
        /// Identifier of the entry to update.
        /// </summary>
        public Guid EntryId { get; set; }

        /// <summary>
        /// Mapping of field key to new value. Unknown keys are ignored by the service.
        /// </summary>
        public Dictionary<string, object?> Fields { get; set; } = new();
    }

    /// <summary>
    /// Success response returned when a batch update has been applied.
    /// </summary>
    public sealed class BatchUpdateSuccessResponseDto
    {
        /// <summary>
        /// Always true for success responses.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Optional updated draft snapshot. Type is kept generic to avoid tight coupling.
        /// </summary>
        public object? UpdatedDraft { get; set; }
    }

    /// <summary>
    /// Error response returned when validation fails for one or more entries.
    /// </summary>
    public sealed class BatchUpdateErrorResponseDto
    {
        /// <summary>
        /// Always false for error responses.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Per-entry validation errors.
        /// </summary>
        public List<EntryErrorDto> Errors { get; set; } = new();
    }

    /// <summary>
    /// Validation errors for a single entry.
    /// </summary>
    public sealed class EntryErrorDto
    {
        /// <summary>
        /// Entry identifier the errors belong to.
        /// </summary>
        public Guid EntryId { get; set; }

        /// <summary>
        /// List of field-level errors.
        /// </summary>
        public List<FieldErrorDto> FieldErrors { get; set; } = new();
    }

    /// <summary>
    /// Field-level validation error.
    /// </summary>
    public sealed class FieldErrorDto
    {
        /// <summary>
        /// Field key that failed validation.
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable error message (may be localized by the caller).
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
