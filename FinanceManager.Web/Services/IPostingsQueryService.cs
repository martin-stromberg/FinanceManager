using FinanceManager.Application.Postings;

namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Provides query methods to retrieve postings filtered by various entities (contact, account, savings plan, security).
    /// Implementations encapsulate paging, filtering and any authorization/context handling required by the application.
    /// </summary>
    public interface IPostingsQueryService : FinanceManager.Application.Postings.IPostingsQueryService
    {
        // Intentionally empty: this Web-facing interface now derives from the Application-layer abstraction.
    }
}
