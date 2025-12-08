using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Accounts;
using FinanceManager.Web;

namespace FinanceManager.Web.ViewModels
{
    public interface IListItemNavigation
    {
        string GetNavigateUrl();
    }

    public sealed class ListFactoryResult
    {
        public IListProvider Provider { get; }

        public ListFactoryResult(IListProvider provider)
        {
            Provider = provider;
        }
    }

    public sealed class ListViewModelFactory
    {
        private readonly IServiceProvider _sp;
        private readonly IStringLocalizer<Pages> _L;
        public ListViewModelFactory(IServiceProvider sp, IStringLocalizer<Pages> localizer)
        {
            _sp = sp; _L = localizer;
        }

        public ListFactoryResult? Create(string kind)
        {
            switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "accounts":
                    var vm = ActivatorUtilities.CreateInstance<BankAccountListViewModel>(_sp);
                    return new ListFactoryResult(vm);
                default:
                    return null;
            }
        }
    }
}
