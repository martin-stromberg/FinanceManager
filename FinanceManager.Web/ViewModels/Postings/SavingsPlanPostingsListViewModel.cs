using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    /// <summary>
    /// List view model that loads postings related to a specific savings plan.
    /// Inherits common posting list behavior from <see cref="BasePostingsListViewModel"/> and
    /// supplies the savings plan id for queries.
    /// </summary>
    public sealed class SavingsPlanPostingsListViewModel : BasePostingsListViewModel
    {
        private readonly Guid _planId;

        /// <summary>
        /// Initializes a new instance of <see cref="SavingsPlanPostingsListViewModel"/>.
        /// </summary>
        /// <param name="services">Service provider used to resolve dependencies required by the base class.</param>
        /// <param name="planId">The identifier of the savings plan for which postings should be listed.</param>
        public SavingsPlanPostingsListViewModel(IServiceProvider services, Guid planId) : base(services)
        {
            _planId = planId;
            AllowRangeFiltering = true;
        }

        /// <summary>
        /// Queries a page of postings for the configured savings plan using the supplied API client.
        /// </summary>
        /// <param name="api">API client used to fetch postings.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for paging.</param>
        /// <param name="search">Optional search term to filter postings (subject, recipient, description, etc.).</param>
        /// <param name="from">Optional inclusive start date filter for booking date.</param>
        /// <param name="to">Optional inclusive end date filter for booking date.</param>
        /// <returns>
        /// A read-only list of <see cref="PostingServiceDto"/> instances when the query succeeds; otherwise <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Exceptions thrown by the underlying API are swallowed and <c>null</c> is returned to indicate a failure to load data.
        /// The base class handles null results appropriately.
        /// </remarks>
        protected override async Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to)
        {
            try
            {
                var list = await api.Postings_GetSavingsPlanAsync(_planId, skip, take, from, to, search);
                return list?.ToList();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds an export URL for the postings of the current savings plan in the requested format.
        /// </summary>
        /// <param name="format">The export format (e.g. "csv", "xlsx").</param>
        /// <returns>Absolute or relative URL that can be used to download the exported postings.</returns>
        public override string GetExportUrl(string format)
        {
            var basePath = $"/api/postings/savings-plan/{_planId}/export";
            return BuildExportUrl(basePath, format);
        }
    }
}
