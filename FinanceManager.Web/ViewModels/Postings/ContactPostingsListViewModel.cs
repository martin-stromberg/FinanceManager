using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Postings.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    public sealed class ContactPostingsListViewModel : BasePostingsListViewModel
    {
        private readonly Guid _contactId;
        public ContactPostingsListViewModel(IServiceProvider services, Guid contactId) : base(services)
        {
            _contactId = contactId;
            AllowRangeFiltering = true;
        }

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

        public override string GetExportUrl(string format)
        {
            var basePath = $"/api/postings/contact/{_contactId}/export";
            return BuildExportUrl(basePath, format);
        }
    }
}
