using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Abstraction for view models supporting multi-file uploads used by UI components.
    /// Implementations handle receiving one or more files together with a payload that describes the upload intent
    /// (for example "statementdraft").
    /// </summary>
    public interface IUploadFilesViewModel
    {
        /// <summary>
        /// Upload multiple files with a payload describing the upload kind (e.g. "statementdraft").
        /// Returns an <see cref="UploadResult"/> containing any provider-specific result details.
        /// </summary>
        /// <param name="payload">A short string describing the upload kind or additional metadata understood by the receiver.</param>
        /// <param name="files">Sequence of tuples containing the stream and original file name for each uploaded file.
        /// The caller is responsible for providing readable streams; implementations may read and/or copy the streams.</param>
        /// <param name="ct">Cancellation token used to cancel the upload operation.</param>
        /// <returns>
        /// A task that resolves to an <see cref="UploadResult"/> with details about the upload when successful,
        /// or <c>null</c> when the operation failed or was rejected.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> or <paramref name="files"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        Task<UploadResult?> UploadFilesAsync(string payload, IEnumerable<(Stream Stream, string FileName)> files, CancellationToken ct = default);

        // Progress / status properties for UI binding

        /// <summary>
        /// Indicates whether an upload operation is currently in progress.
        /// </summary>
        bool UploadInProgress { get; }

        /// <summary>
        /// Total number of files expected to be uploaded in the current batch.
        /// </summary>
        int UploadTotal { get; }

        /// <summary>
        /// Number of files already processed in the current upload batch.
        /// </summary>
        int UploadDone { get; }

        /// <summary>
        /// Name of the file currently being uploaded, or <c>null</c> when none.
        /// </summary>
        string? CurrentFileName { get; }

        /// <summary>
        /// Upload progress percentage (0-100). Implementations should compute this from <see cref="UploadDone"/> and <see cref="UploadTotal"/>.
        /// </summary>
        int UploadPercent { get; }

        // Optional result/diagnostics

        /// <summary>
        /// Indicates whether the last import produced at least one successfully created draft or was otherwise considered successful.
        /// </summary>
        bool ImportSuccess { get; }

        /// <summary>
        /// When the import created new drafts, this contains the first draft id produced by the import; otherwise <c>null</c>.
        /// </summary>
        System.Guid? FirstDraftId { get; }

        /// <summary>
        /// Last human-readable error message produced by the most recent upload operation, or <c>null</c> when no error occurred.
        /// </summary>
        string? LastError { get; }
    }
}
