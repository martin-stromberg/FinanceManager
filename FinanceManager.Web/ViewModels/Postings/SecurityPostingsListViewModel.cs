using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    /// <summary>
    /// List view model for postings related to a specific security. Provides paging and lookup
    /// of posting service DTOs filtered by the configured security id.
    /// </summary>
    public sealed class SecurityPostingsListViewModel : BasePostingsListViewModel
    {
        /// <summary>
        /// Identifier of the security for which postings are queried.
        /// </summary>
        private readonly Guid _securityId;

        /// <summary>
        /// Initializes a new instance of <see cref="SecurityPostingsListViewModel"/> for the specified security.
        /// </summary>
        /// <param name="services">Service provider used by the base view model.</param>
        /// <param name="securityId">Identifier of the security whose postings should be listed.</param>
        public SecurityPostingsListViewModel(IServiceProvider services, Guid securityId) : base(services)
        {
            _securityId = securityId;
            AllowRangeFiltering = true;
        }

        /// <summary>
        /// Queries a page of postings for the configured security.
        /// </summary>
        /// <param name="api">API client used to perform the query.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for the page.</param>
        /// <param name="search">Search term to filter postings (may be empty).</param>
        /// <param name="from">Optional start date for range filtering.</param>
        /// <param name="to">Optional end date for range filtering.</param>
        /// <returns>
        /// A task that resolves to a read-only list of <see cref="PostingServiceDto"/> when the query succeeds,
        /// or <c>null</c> when an error occurs.
        /// </returns>
        /// <remarks>
        /// Exceptions thrown by the API client are caught and result in a <c>null</c> return value to
        /// indicate that the page could not be loaded.
        /// </remarks>
        protected override async Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to)
        {
            try
            {
                var list = await api.Postings_GetSecurityAsync(_securityId, skip, take, from, to);
                return list?.ToList();
            }
            catch
            {
                return null;
            }
        }
    }
}
