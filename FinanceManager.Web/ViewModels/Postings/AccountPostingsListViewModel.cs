using FinanceManager.Shared;

namespace FinanceManager.Web.ViewModels.Postings
{
    public sealed class AccountPostingsListViewModel : BasePostingsListViewModel
    {
        private readonly Guid _accountId;
        public AccountPostingsListViewModel(IServiceProvider services, Guid accountId) : base(services)
        {
            _accountId = accountId;
            // allow range filtering by default
            AllowRangeFiltering = true;
        }

        protected override async Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to)
        {
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
    }
}
