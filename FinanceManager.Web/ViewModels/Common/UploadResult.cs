using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Generic upload result container returned by viewmodels when handling file uploads.
    /// Properties are generic; consumers can inspect StatementDraftResult for statement-draft specific information.
    /// </summary>
    public sealed class UploadResult
    {
        public StatementDraftUploadResult? StatementDraftResult { get; set; }
        public int CreatedCount { get; set; }
    }
}
