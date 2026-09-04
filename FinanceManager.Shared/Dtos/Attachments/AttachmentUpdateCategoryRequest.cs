namespace FinanceManager.Shared.Dtos.Attachments;

/// <summary>
/// Request payload to update only the category of an attachment.
/// </summary>
/// <param name="CategoryId">The category identifier to assign; null clears the category.</param>
public sealed record AttachmentUpdateCategoryRequest(
    Guid? CategoryId
);
