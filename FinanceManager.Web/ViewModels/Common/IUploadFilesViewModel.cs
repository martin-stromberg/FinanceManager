using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Common
{
    public interface IUploadFilesViewModel
    {
        /// <summary>
        /// Upload multiple files with a payload describing the upload kind (e.g. "statementdraft").
        /// Returns an UploadResult containing any provider-specific result details.
        /// </summary>
        Task<UploadResult?> UploadFilesAsync(string payload, IEnumerable<(Stream Stream, string FileName)> files, CancellationToken ct = default);

        // Progress / status properties for UI binding
        bool UploadInProgress { get; }
        int UploadTotal { get; }
        int UploadDone { get; }
        string? CurrentFileName { get; }
        int UploadPercent { get; }

        // Optional result/diagnostics
        bool ImportSuccess { get; }
        System.Guid? FirstDraftId { get; }
        string? LastError { get; }
    }
}
