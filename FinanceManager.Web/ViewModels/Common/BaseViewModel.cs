using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using FinanceManager.Web.ViewModels; // for IRibbonProvider

namespace FinanceManager.Web.ViewModels.Common
{
    public abstract class BaseViewModel : IAsyncDisposable, IRibbonProvider
    {
        public bool Loading { get; protected set; }
        public string? LastError { get; protected set; }

        public event EventHandler? StateChanged;
        public event EventHandler<string?>? UiActionRequested;

        protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
        protected void RaiseUiActionRequested(string? action) => UiActionRequested?.Invoke(this, action);

        // IRibbonProvider implementation
        public virtual IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer) => null;
        public void SetActiveTab<TTabEnum>(TTabEnum id)
        {
        }
        public TTabEnum? GetActiveTab<TTabEnum>()
        {
            return default;
        }


        public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

        
    }
}
