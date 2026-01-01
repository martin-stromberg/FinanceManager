using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    /// <summary>
    /// List view model showing postings filtered for a specific contact. Enables range filtering and provides an export URL helper.
    /// </summary>
    public sealed class ContactPostingsListViewModel : BasePostingsListViewModel
    {
        /// <summary>
        /// Identifier of the contact whose postings are displayed.
        /// </summary>
        private readonly Guid _contactId;

        /// <summary>
        /// Initializes a new instance of <see cref="ContactPostingsListViewModel"/>.
        /// </summary>
        /// <param name="services">Service provider used to resolve dependencies required by the base class.</param>
        /// <param name="contactId">Identifier of the contact whose postings should be listed.</param>
        public ContactPostingsListViewModel(IServiceProvider services, Guid contactId) : base(services)
        {
            _contactId = contactId;
            AllowRangeFiltering = true;
        }

        /// <summary>
        /// Queries a page of postings for the configured contact using the provided API client and filter criteria.
        /// Implementations should return a read-only list of PostingServiceDto or <c>null</c> when the query failed.
        /// </summary>
        /// <param name="api">API client to use for the query. Implementations must not dispose this instance.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to request.</param>
        /// <param name="search">Search term to filter postings.</param>
        /// <param name="from">Optional start date for date range filtering.</param>
        /// <param name="to">Optional end date for date range filtering.</param>
        /// <returns>A task that resolves to a read-only list of <see cref="PostingServiceDto"/> or <c>null</c> when the query failed.</returns>
        protected override async Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to)
        {
            try
            {
                var list = await api.Postings_GetContactAsync(_contactId, skip, take, search, from, to);
                return list?.ToList();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds an export URL for the contact postings in the requested format.
        /// </summary>
        /// <param name="format">Export format identifier (for example "csv" or "xlsx").</param>
        /// <returns>A relative URL that can be used to download the export for the contact postings.</returns>
        public override string GetExportUrl(string format)
        {
            var basePath = $"/api/postings/contact/{_contactId}/export";
            return BuildExportUrl(basePath, format);
        }
    }
}
