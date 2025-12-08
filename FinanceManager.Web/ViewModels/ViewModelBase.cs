using FinanceManager.Application;
using FinanceManager.Web.ViewModels.Common;
using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels;

public interface IRibbonProvider
{
    IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer);
    TTabEnum? GetActiveTab<TTabEnum>();
    void SetActiveTab<TTabEnum>(TTabEnum id);
}

public abstract class ViewModelBase : IAsyncDisposable, IRibbonProvider
{
    private readonly IServiceProvider _services;
    private readonly List<IAsyncDisposable> _children = new();
    private readonly List<ViewModelBase> _childViewModels = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ICurrentUserService _currentUser;

    protected ViewModelBase(IServiceProvider services)
    {
        _services = services;
        _currentUser = services.GetRequiredService<ICurrentUserService>();
    }

    public event EventHandler? StateChanged;
    // Raised when the VM requires authentication; argument may contain a suggested returnUrl
    public event EventHandler<string?>? AuthenticationRequired;
    // Raised when the VM wants the UI to perform an action (e.g., open file picker)
    public event EventHandler<string?>? UiActionRequested;

    protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    protected void RequireAuthentication(string? returnUrl = null) => AuthenticationRequired?.Invoke(this, returnUrl);

    protected void RaiseUiActionRequested(string? action) => UiActionRequested?.Invoke(this, action);

    public bool IsAuthenticated => _currentUser.IsAuthenticated;

    protected T CreateSubViewModel<T>(Action<T>? configure = null) where T : ViewModelBase
    {
        var vm = ActivatorUtilities.CreateInstance<T>(_services);
        vm.StateChanged += (_, __) => RaiseStateChanged();
        vm.AuthenticationRequired += (_, ret) => AuthenticationRequired?.Invoke(this, ret);
        vm.UiActionRequested += (_, act) => UiActionRequested?.Invoke(this, act);
        _children.Add(vm);
        _childViewModels.Add(vm);
        configure?.Invoke(vm);
        return vm;
    }

    protected CancellationToken CancellationToken => _cts.Token;

    // Ausführungshilfe inkl. Render-Trigger
    protected async Task RunAsync(Func<CancellationToken, Task> action, bool raiseAfter = true)
    {
        await action(_cts.Token);
        if (raiseAfter) { RaiseStateChanged(); }
    }

    public virtual ValueTask InitializeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;

    // New ribbon API: aggregate registers from children
    public virtual IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer)
    {
        var registers = new List<UiRibbonRegister>();


        // Aggregate registers from child viewmodels
        foreach (var vm in _childViewModels)
        {
            if (vm is IRibbonProvider rp)
            {
                var child = rp.GetRibbonRegisters(localizer);
                if (child != null) registers.AddRange(child);
            }
        }

        return registers.Count == 0 ? null : registers;
    }

    // Default ActiveTab handling: VMs can override to persist/return a tab id
    public virtual TTabEnum? GetActiveTab<TTabEnum>() => default;
    public virtual void SetActiveTab<TTabEnum>(TTabEnum id) { }

    public virtual async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        foreach (var c in _children)
        {
            try { await c.DisposeAsync(); } catch { }
        }
        _children.Clear();
        _childViewModels.Clear();
    }
}