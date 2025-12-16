using System.Threading.Tasks;
using System;

namespace FinanceManager.Web.ViewModels.Common
{
    public interface IDeletableViewModel
    {
        Task<bool> DeleteAsync();
        string? LastError { get; }
    }
}
