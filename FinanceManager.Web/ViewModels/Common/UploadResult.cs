using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Generic upload result container returned by view models when handling file uploads.
    /// </summary>
    /// <remarks>
    /// The class is intentionally generic: consumers can inspect the <see cref="StatementDraftResult"/>
    /// for statement-draft specific information while the <see cref="CreatedCount"/> provides a
    /// simple integer count of created entities across the uploaded files.
    /// </remarks>
    public sealed class UploadResult
    {
        /// <summary>
        /// Statement-draft specific result returned by the import endpoint. May be <c>null</c> when the upload
        /// did not target statement drafts or when the endpoint returned no detailed result.
        /// </summary>
        public StatementDraftUploadResult? StatementDraftResult { get; set; }

        /// <summary>
        /// Number of created domain entities (for example number of created drafts) produced by the upload operation.
        /// </summary>
        public int CreatedCount { get; set; }
    }
}
