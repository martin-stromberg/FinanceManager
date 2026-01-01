namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Provides information whether a ViewModel can accept symbol attachments and validates uploaded files.
    /// Implementations are expected to validate the uploaded stream, file name and content type and
    /// return an attachment id when the upload is accepted.
    /// </summary>
    public interface ISymbolAssignableCard
    {
        /// <summary>
        /// Validates an uploaded symbol file. Implementations should inspect the provided <paramref name="stream"/>, 
        /// <paramref name="fileName"/> and <paramref name="contentType"/> and return an attachment identifier when the
        /// file is valid and can be stored. Return <c>null</c> when the file is not acceptable.
        /// </summary>
        /// <param name="stream">Stream containing the uploaded file data. The stream position may be at the start of the content.</param>
        /// <param name="fileName">Original filename provided by the client.</param>
        /// <param name="contentType">MIME content type of the uploaded file.</param>
        /// <returns>
        /// A task that resolves to the assigned attachment <see cref="System.Guid"/> when validation succeeded and the file was stored;
        /// or <c>null</c> when the file is invalid or rejected.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="fileName"/> is <c>null</c>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation is cancelled by an ambient cancellation token.</exception>
        Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType);
    }
}
