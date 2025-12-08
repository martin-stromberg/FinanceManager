using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinanceManager.Web.ViewModels.Common
{
    public abstract class BaseCardViewModel<TKeyValue> : BaseViewModel
    {
        public virtual Task InitializeAsync(System.Guid id) => LoadAsync(id);

        public abstract Task LoadAsync(System.Guid id);

        public virtual Task<bool> SaveAsync() => Task.FromResult(true);

        public virtual Task<bool> DeleteAsync() => Task.FromResult(false);

        public virtual CardRecord? CardRecord { get; protected set; }
    }
}
