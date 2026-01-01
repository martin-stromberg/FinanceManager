using System.Threading.Tasks;
using System;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Interface for view models that support deletion of the represented entity.
    /// Implementations should perform the deletion when <see cref="DeleteAsync"/> is invoked
    /// and expose a last error message when operations fail.
    /// </summary>
    public interface IDeletableViewModel
    {
        /// <summary>
        /// Deletes the underlying entity represented by the view model.
        /// </summary>
        /// <returns>
        /// A task that resolves to <c>true</c> when the deletion succeeded; otherwise <c>false</c>.
        /// Implementations should set <see cref="LastError"/> when returning <c>false</c> to provide a user-facing error message.
        /// </returns>
        /// <exception cref="OperationCanceledException">May be thrown if the operation is canceled by the caller (optional).</exception>
        Task<bool> DeleteAsync();

        /// <summary>
        /// Last human-readable error message produced by the most recent operation on the view model, or <c>null</c> when no error occurred.
        /// </summary>
        string? LastError { get; }
    }
}
