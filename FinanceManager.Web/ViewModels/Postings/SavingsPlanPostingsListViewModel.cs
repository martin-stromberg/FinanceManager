using FinanceManager.Shared;

namespace FinanceManager.Web.ViewModels.Postings
{
    public sealed class SavingsPlanPostingsListViewModel : BasePostingsListViewModel
    {
        private readonly Guid _planId;
        public SavingsPlanPostingsListViewModel(IServiceProvider services, Guid planId) : base(services)
        {
            _planId = planId;
            AllowRangeFiltering = true;
        }

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
    }
}
