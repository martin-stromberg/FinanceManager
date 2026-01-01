using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    /// <summary>
    /// View model for listing postings belonging to a specific account.
    /// Provides paging, search and date-range support and exposes export URL generation.
    /// </summary>
    public sealed class AccountPostingsListViewModel : BasePostingsListViewModel
    {
        private readonly Guid _accountId;

        /// <summary>
        /// Initializes a new instance of <see cref="AccountPostingsListViewModel"/>.
        /// </summary>
        /// <param name="services">Service provider used to resolve required services such as the API client.</param>
        /// <param name="accountId">Identifier of the account whose postings will be listed.</param>
        public AccountPostingsListViewModel(IServiceProvider services, Guid accountId) : base(services)
        {
            _accountId = accountId;
            // allow range filtering by default
            AllowRangeFiltering = true;
        }

        /// <summary>
        /// Queries a single page of postings for the configured account using the provided API client.
        /// </summary>
        /// <param name="api">API client used to fetch postings. Must not be <c>null</c>.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for the page.</param>
        /// <param name="search">Search string to filter postings (may be empty).</param>
        /// <param name="from">Optional start date for date-range filtering.</param>
        /// <param name="to">Optional end date for date-range filtering.</param>
        /// <returns>
        /// A task that resolves to a read-only list of <see cref="PostingServiceDto"/> when the query succeeds;
        /// or <c>null</c> when the operation fails or an exception is caught.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="api"/> is <c>null</c>.</exception>
        protected override async Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to)
        {
            ArgumentNullException.ThrowIfNull(api);
            try
            {
                var list = await api.Postings_GetAccountAsync(_accountId, skip, take, search, from, to);
                return list?.ToList();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the export URL for the current account's postings in the requested format.
        /// </summary>
        /// <param name="format">Export format identifier (e.g. "csv", "xlsx").</param>
        /// <returns>A relative URL that can be used to download the postings export.</returns>
        public override string GetExportUrl(string format)
        {
            var basePath = $"/api/postings/account/{_accountId}/export";
            return BuildExportUrl(basePath, format);
        }
    }
}
