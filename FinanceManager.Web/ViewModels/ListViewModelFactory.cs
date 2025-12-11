using DocumentFormat.OpenXml.Office2010.Excel;
using FinanceManager.Web;
using FinanceManager.Web.ViewModels.Accounts;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System;

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

        public ListFactoryResult? Create(string kind, string subKind, string id)
        {
            switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "accounts":
                    var vm = ActivatorUtilities.CreateInstance<BankAccountListViewModel>(_sp);
                    return new ListFactoryResult(vm);
                case "postings":
                    if (!Guid.TryParse(id, out var gid))
                        return null;
                    return CreatePostings((subKind ?? string.Empty).Trim().ToLowerInvariant(), gid);
                default:
                    return null;
            }
        }

        // Create postings list providers with optional subkind (e.g. 'account', 'contact') and optional id filter
        private ListFactoryResult? CreatePostings(string subKind, Guid? id)
        {
            switch ((subKind ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "account":
                    if (id.HasValue)
                    {
                        var vm = ActivatorUtilities.CreateInstance<FinanceManager.Web.ViewModels.Postings.AccountPostingsListViewModel>(_sp, id.Value);
                        return new ListFactoryResult(vm);
                    }
                    break;
                case "contact":
                    if (id.HasValue)
                    {
                        var vm = ActivatorUtilities.CreateInstance<FinanceManager.Web.ViewModels.Postings.ContactPostingsListViewModel>(_sp, id.Value);
                        return new ListFactoryResult(vm);
                    }
                    break;
                case "savings-plan":
                    if (id.HasValue)
                    {
                        var vm = ActivatorUtilities.CreateInstance<FinanceManager.Web.ViewModels.Postings.SavingsPlanPostingsListViewModel>(_sp, id.Value);
                        return new ListFactoryResult(vm);
                    }
                    break;
                case "security":
                    if (id.HasValue)
                    {
                        var vm = ActivatorUtilities.CreateInstance<FinanceManager.Web.ViewModels.Postings.SecurityPostingsListViewModel>(_sp, id.Value);
                        return new ListFactoryResult(vm);
                    }
                    break;
            }
            return null;
        }
    }
}
