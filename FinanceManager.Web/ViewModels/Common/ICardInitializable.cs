using System;
using System.Threading.Tasks;

namespace FinanceManager.Web.ViewModels.Common
{
    public interface ICardInitializable
    {
        Task InitializeAsync(Guid id);
        void SetInitValue(string? prefill);
        void SetBackNavigation(string? backUrl);
    }
}
