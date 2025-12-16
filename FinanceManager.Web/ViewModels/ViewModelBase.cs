using FinanceManager.Application;
using Microsoft.Extensions.Localization;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.Components.Shared;

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
    // Backwards-compatible: original simple action event
    public event EventHandler<string?>? UiActionRequested;

    // New richer event carrying an object payload
    public sealed class UiActionEventArgs : EventArgs
    {
        public string? Action { get; }
        public string? Payload { get; }
        public object? PayloadObject { get; }

        public UiActionEventArgs(string? action, string? payload)
        {
            Action = action; Payload = payload; PayloadObject = null;
        }

        public UiActionEventArgs(string? action, object? payloadObject)
        {
            Action = action; Payload = null; PayloadObject = payloadObject;
        }
    }

    public sealed record UiOverlaySpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, bool Modal = true);

    public event EventHandler<UiActionEventArgs?>? UiActionRequestedEx;

    protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    protected void RequireAuthentication(string? returnUrl = null) => AuthenticationRequired?.Invoke(this, returnUrl);

    // Backwards-compatible wrappers: raise both legacy and rich events as appropriate
    protected void RaiseUiActionRequested(string? action)
    {
        UiActionRequested?.Invoke(this, action);
        UiActionRequestedEx?.Invoke(this, new UiActionEventArgs(action, (string?)null));
    }
    protected void RaiseUiActionRequested(string? action, string? payload)
    {
        UiActionRequested?.Invoke(this, action);
        UiActionRequestedEx?.Invoke(this, new UiActionEventArgs(action, payload));
    }
    // New overload: pass arbitrary object payload (e.g. UiOverlaySpec)
    protected void RaiseUiActionRequested(string? action, object? payloadObject)
    {
        UiActionRequested?.Invoke(this, action);
        UiActionRequestedEx?.Invoke(this, new UiActionEventArgs(action, payloadObject));
    }

    // Convenience helper for requesting the Attachments overlay from any ViewModel
    protected void RequestOpenAttachments(AttachmentEntityKind parentKind, Guid parentId)
    {
        var parameters = new Dictionary<string, object?>
        {
            ["ParentKind"] = parentKind,
            ["ParentId"] = parentId
        };
        var spec = new UiOverlaySpec(typeof(AttachmentsPanel), parameters);
        UiActionRequestedEx?.Invoke(this, new UiActionEventArgs("OpenAttachments", spec));
    }

    public bool IsAuthenticated => _currentUser.IsAuthenticated;

    protected T CreateSubViewModel<T>(Action<T>? configure = null) where T : ViewModelBase
    {
        var vm = ActivatorUtilities.CreateInstance<T>(_services);
        vm.StateChanged += (_, __) => RaiseStateChanged();
        vm.AuthenticationRequired += (_, ret) => AuthenticationRequired?.Invoke(this, ret);
        vm.UiActionRequested += (_, act) => UiActionRequested?.Invoke(this, act);
        vm.UiActionRequestedEx += (_, evt) => UiActionRequestedEx?.Invoke(this, evt);
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

    // Backwards-compatible helper used by tests and older callers
    public IReadOnlyList<UiRibbonRegister>? GetRibbon(IStringLocalizer localizer) => GetRibbonRegisters(localizer);

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