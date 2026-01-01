using System;
using System.Threading.Tasks;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Interface implemented by card view models that can be initialized with an identifier and
    /// accept optional navigation / prefill hints from the caller.
    /// </summary>
    public interface ICardInitializable
    {
        /// <summary>
        /// Initialize the view model for the specified entity identifier.
        /// Implementations should load any necessary data and update view model state.
        /// </summary>
        /// <param name="id">Identifier of the entity to initialize the card for. When <see cref="Guid.Empty"/> implementations may prepare a new entity for creation.</param>
        /// <returns>A task that completes when initialization has finished.</returns>
        Task InitializeAsync(Guid id);

        /// <summary>
        /// Supplies an optional prefill payload that callers can provide when opening a new card (create flow).
        /// Implementations may ignore <c>null</c> values.
        /// </summary>
        /// <param name="prefill">Arbitrary prefill string (for example an initial name) or <c>null</c>.</param>
        void SetInitValue(string? prefill);

        /// <summary>
        /// Supplies an optional back navigation URL that the view model can expose to the UI for "Back" navigation.
        /// </summary>
        /// <param name="backUrl">Relative or absolute URL to navigate back to, or <c>null</c> to clear any previously set back URL.</param>
        void SetBackNavigation(string? backUrl);
    }
}
