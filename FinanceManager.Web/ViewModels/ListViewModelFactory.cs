using FinanceManager.Web.ViewModels.Contacts.Groups;
using FinanceManager.Web.ViewModels.SavingsPlans.Categories;
using FinanceManager.Web.ViewModels.Securities.Categories;
using FinanceManager.Web.ViewModels.Securities.Prices;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels.StatementDrafts;

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
                case "contacts":
                    // support listing contact categories via "/list/contacts/categories"
                    if ((subKind ?? string.Empty).Trim().ToLowerInvariant() == "categories")
                    {
                        var gvm = ActivatorUtilities.CreateInstance<ContactGroupListViewModel>(_sp);
                        return new ListFactoryResult(gvm);
                    }

                    var cvm = ActivatorUtilities.CreateInstance<Contacts.ContactListViewModel>(_sp);
                    return new ListFactoryResult(cvm);
                case "postings":
                    if (!Guid.TryParse(id, out var gid))
                        return null;
                    return CreatePostings((subKind ?? string.Empty).Trim().ToLowerInvariant(), gid);
                case "savings-plans":
                    // support listing savings plan categories via "/list/savings-plans/categories"
                    if ((subKind ?? string.Empty).Trim().ToLowerInvariant() == "categories")
                    {
                        var spCvm = ActivatorUtilities.CreateInstance<SavingsPlanCategoryListViewModel>(_sp);
                        return new ListFactoryResult(spCvm);
                    }
                    var spvm = ActivatorUtilities.CreateInstance<FinanceManager.Web.ViewModels.SavingsPlans.SavingsPlansListViewModel>(_sp);
                    return new ListFactoryResult(spvm);
                case "securities":
                    // support sub-kind 'prices' -> /list/securities/prices/{id}
                    if ((subKind ?? string.Empty).Trim().ToLowerInvariant() == "prices")
                    {
                        if (Guid.TryParse(id, out var sid))
                        {
                            var pricesVm = ActivatorUtilities.CreateInstance<SecurityPricesListViewModel>(_sp, sid);
                            return new ListFactoryResult(pricesVm);
                        }
                        return null;
                    }

                    // support listing security categories via "/list/security-categories"
                    if ((subKind ?? string.Empty).Trim().ToLowerInvariant() == "categories")
                    {
                        var scVm = ActivatorUtilities.CreateInstance<SecurityCategoriesListViewModel>(_sp);
                        return new ListFactoryResult(scVm);
                    }

                    var secVm = ActivatorUtilities.CreateInstance<FinanceManager.Web.ViewModels.Securities.SecuritiesListViewModel>(_sp);
                    return new ListFactoryResult(secVm);
                case "statement-drafts":
                    // list of statement drafts
                    var sdVm = ActivatorUtilities.CreateInstance<StatementDraftsListViewModel>(_sp);
                    return new ListFactoryResult(sdVm);
                case "users":
                    var uvm = ActivatorUtilities.CreateInstance<Setup.UserListViewModel>(_sp);
                    return new ListFactoryResult(uvm);
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
