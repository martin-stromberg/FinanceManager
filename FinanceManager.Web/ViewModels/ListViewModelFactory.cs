using FinanceManager.Web.ViewModels.Contacts.Groups;
using FinanceManager.Web.ViewModels.SavingsPlans.Categories;
using FinanceManager.Web.ViewModels.Securities.Categories;
using FinanceManager.Web.ViewModels.Securities.Prices;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels.StatementDrafts;

namespace FinanceManager.Web.ViewModels
{
    /// <summary>
    /// Represents a list item that can provide a navigation URL for list-to-card navigation.
    /// </summary>
    public interface IListItemNavigation
    {
        /// <summary>
        /// Returns the relative URL to navigate to the detail/card view for this item.
        /// </summary>
        /// <returns>Relative navigation URL as string.</returns>
        string GetNavigateUrl();
    }

    /// <summary>
    /// Result object returned by <see cref="ListViewModelFactory.Create(string,string,string)"/>, 
    /// containing the created <see cref="IListProvider"/> instance.
    /// </summary>
    public sealed class ListFactoryResult
    {
        /// <summary>
        /// Gets the provider instance created by the factory.
        /// </summary>
        public IListProvider Provider { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ListFactoryResult"/>.
        /// </summary>
        /// <param name="provider">The list provider created by the factory.</param>
        public ListFactoryResult(IListProvider provider)
        {
            Provider = provider;
        }
    }

    /// <summary>
    /// Factory that creates list view model instances based on a route kind, optional sub-kind and an identifier.
    /// Used by generic list pages to obtain the appropriate provider for rendering.
    /// </summary>
    public sealed class ListViewModelFactory
    {
        private readonly IServiceProvider _sp;
        private readonly IStringLocalizer<Pages> _L;

        /// <summary>
        /// Initializes a new instance of <see cref="ListViewModelFactory"/>.
        /// </summary>
        /// <param name="sp">Service provider used to create view model instances via <see cref="ActivatorUtilities"/>.</param>
        /// <param name="localizer">Localizer used for resolving UI labels if required by created view models.</param>
        public ListViewModelFactory(IServiceProvider sp, IStringLocalizer<Pages> localizer)
        {
            _sp = sp; _L = localizer;
        }

        /// <summary>
        /// Creates a list provider for the specified route kind, optional sub-kind and identifier.
        /// </summary>
        /// <param name="kind">Primary route kind (for example "accounts", "contacts", "postings").</param>
        /// <param name="subKind">Optional sub-kind that further qualifies the list (for example "categories" or "prices").</param>
        /// <param name="id">Optional identifier used by certain list providers (for example postings filtered by id). The value is interpreted as a <see cref="Guid"/> where applicable.</param>
        /// <returns>
        /// A <see cref="ListFactoryResult"/> containing the created <see cref="IListProvider"/>, or <c>null</c> when no matching provider could be created.
        /// </returns>
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

        /// <summary>
        /// Creates postings list providers depending on the specified postings sub-kind (e.g. account, contact, savings-plan, security) and optional id.
        /// </summary>
        /// <param name="subKind">Sub-kind that determines the specific postings provider.</param>
        /// <param name="id">Identifier used to filter postings when applicable.</param>
        /// <returns>A <see cref="ListFactoryResult"/> with the created provider or <c>null</c> when no match was found.</returns>
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
