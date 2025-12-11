using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Web.ViewModels.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    public sealed class SecurityPostingsListViewModel : BasePostingsListViewModel
    {
        private readonly Guid _securityId;
        public SecurityPostingsListViewModel(IServiceProvider services, Guid securityId) : base(services)
        {
            _securityId = securityId;
            AllowRangeFiltering = true;
        }

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
