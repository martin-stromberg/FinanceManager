using FinanceManager.Shared;
using Microsoft.Extensions.Localization;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.Components.Shared;
using Microsoft.Extensions.DependencyInjection;
using FinanceManager.Application;
using FinanceManager.Shared.Dtos.Admin;

namespace FinanceManager.Web.ViewModels.Common
{
    public enum BooleanSelection { True, False }
    // Position where an embedded panel should be rendered on the Card page
    public enum EmbeddedPanelPosition { AfterRibbon, AfterCard }
     public abstract class BaseViewModel : IAsyncDisposable, IRibbonProvider
     {
        protected BaseViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }
        private IApiClient _ApiClient = null;
        public virtual string Title { get; } = string.Empty;
        public bool Loading { get; protected set; }
        public string? LastError { get; protected set; }
        // Optional machine-readable error code coming from API (e.g. Err_Invalid_bankContactId)
        public string? LastErrorCode { get; protected set; }
        protected void SetError(string errorCode, string errorMessage)
        {
            LastErrorCode = errorCode;
            LastError = errorMessage;
            if (!string.IsNullOrEmpty(LastErrorCode))
            {
                var entry = Localizer?[LastErrorCode];
                if (entry != null && !entry.ResourceNotFound)
                    LastError = entry.Value;
            }
        }
        protected IServiceProvider ServiceProvider { get; }
        protected IApiClient ApiClient  => _ApiClient ??= ServiceProvider.GetRequiredService<IApiClient>();

        // Lazy-resolved localizer. Resolve on first access and swallow resolution errors (e.g. provider disposed).
        private IStringLocalizer<Pages>? _localizerCache;
        protected IStringLocalizer? Localizer
        {
            get
            {
                if (_localizerCache != null) return _localizerCache;
                try
                {
                    _localizerCache = ServiceProvider.GetService<IStringLocalizer<Pages>>();
                }
                catch
                {
                    _localizerCache = null;
                }
                return _localizerCache;
            }
        }

        public event EventHandler? StateChanged;
        // Event raised when a viewmodel requires the UI to request authentication from the user.
        // Payload may contain an optional reason or return URL.
        public event EventHandler<string?>? AuthenticationRequired;

        // New: allow carrying rich payloads (e.g. overlay component spec) in addition to the existing string payload
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

        // Generic overlay spec that pages can render using DynamicComponent
        public sealed record UiOverlaySpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, bool Modal = true);

        // Embedded panel spec: a viewmodel can request an inline panel to be shown on the card page
        public sealed record EmbeddedPanelSpec(Type ComponentType, IReadOnlyDictionary<string, object?>? Parameters = null, EmbeddedPanelPosition Position = EmbeddedPanelPosition.AfterCard, bool Visible = true);

        public event EventHandler<UiActionEventArgs?>? UiActionRequested;
        public sealed record LookupItem(System.Guid Key, string Name);
        protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
        protected void RaiseUiActionRequested(string? action) => UiActionRequested?.Invoke(this, new UiActionEventArgs(action, null));
        protected void RaiseUiActionRequested(string? action, string? payload) => UiActionRequested?.Invoke(this, new UiActionEventArgs(action, payload));
        // New overload to pass arbitrary object payloads (e.g. UiOverlaySpec)
        protected void RaiseUiActionRequested(string? action, object? payloadObject) => UiActionRequested?.Invoke(this, new UiActionEventArgs(action, payloadObject));
        /// <summary>
        /// Convenience helper to request an embedded inline panel on the Card page.
        /// View pages will render the supplied EmbeddedPanelSpec at the requested position.
        /// </summary>
        protected void RaiseUiEmbeddedPanelRequested(EmbeddedPanelSpec spec) => UiActionRequested?.Invoke(this, new UiActionEventArgs("EmbeddedPanel", spec));

        // Background task types that a page should show for this ViewModel. Default: none.
        public virtual BackgroundTaskType[]? VisibleBackgroundTaskTypes => Array.Empty<BackgroundTaskType>();

        /// <summary>
        /// Convenience helper for requesting the Attachments overlay from any ViewModel.
        /// View pages can render the supplied UiOverlaySpec generically (DynamicComponent).
        /// </summary>
        protected void RequestOpenAttachments(AttachmentEntityKind parentKind, Guid parentId)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["ParentKind"] = parentKind,
                ["ParentId"] = parentId
            };
            var spec = new UiOverlaySpec(typeof(AttachmentsPanel), parameters);
            RaiseUiActionRequested("OpenAttachments", spec);
        }

        #region Lookup Values
        public virtual async Task<IReadOnlyList<LookupItem>> QueryLookupAsync(CardField field, string? q, int skip, int take)
        {
            if (!string.IsNullOrWhiteSpace(field.LookupType) && field.LookupType.StartsWith("Enum:", StringComparison.OrdinalIgnoreCase))
            {
                var enumName = field.LookupType.Substring("Enum:".Length);
                var enumType = ResolveEnumType(enumName);
                if (enumType != null && enumType.IsEnum)
                    return QueryEnumLookup(q, enumType);
                return Array.Empty<LookupItem>();
            }

            if (string.Equals(field.LookupType, "Contact", StringComparison.OrdinalIgnoreCase))
                return await QueryContactLookupAsync(field, q, skip, take);

            if (string.Equals(field.LookupType, "SavingsPlan", StringComparison.OrdinalIgnoreCase))
                return await QuerySavingsPlanLookupAsync(field, q, skip, take);

            if (string.Equals(field.LookupType, "Security", StringComparison.OrdinalIgnoreCase))
                return await QuerySecurityLookupAsync(field, q, skip, take);

            return Array.Empty<LookupItem>();
        }

        private async Task<IReadOnlyList<LookupItem>> QueryContactLookupAsync(CardField field, string? q, int skip, int take)
        {
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            ContactType? typeFilter = null;
            if (!string.IsNullOrWhiteSpace(field.LookupFilter))
            {
                var parts = field.LookupFilter.Split('=', 2);
                if (parts.Length == 2 && string.Equals(parts[0].Trim(), "Type", StringComparison.OrdinalIgnoreCase))
                {
                    if (Enum.TryParse<ContactType>(parts[1].Trim(), ignoreCase: true, out var ct)) typeFilter = ct;
                }
            }
            var results = await api.Contacts_ListAsync(skip, take, typeFilter, false, q);
            return results.Select(c => new LookupItem(c.Id, c.Name)).ToList();
        }

        private bool ParseOnlyActiveFilter(string? filter, bool defaultValue = true)
        {
            if (string.IsNullOrWhiteSpace(filter)) return defaultValue;
            var parts = filter.Split('=', 2);
            if (parts.Length != 2) return defaultValue;
            if (!string.Equals(parts[0].Trim(), "OnlyActive", StringComparison.OrdinalIgnoreCase)) return defaultValue;
            if (bool.TryParse(parts[1].Trim(), out var parsed)) return parsed;
            return defaultValue;
        }

        private async Task<IReadOnlyList<LookupItem>> QuerySavingsPlanLookupAsync(CardField field, string? q, int skip, int take)
        {
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            // allow optional lookup filter like "OnlyActive=true"
            var onlyActive = ParseOnlyActiveFilter(field.LookupFilter, defaultValue: true);
            var all = await api.SavingsPlans_ListAsync(onlyActive, CancellationToken.None);
            var filtered = all
                .Where(sp => string.IsNullOrWhiteSpace(q) || (sp.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) == true))
                .Skip(skip).Take(take)
                .Select(sp => new LookupItem(sp.Id, sp.Name))
                .ToList();
            return filtered;
        }

        private async Task<IReadOnlyList<LookupItem>> QuerySecurityLookupAsync(CardField field, string? q, int skip, int take)
        {
            var api = ServiceProvider.GetRequiredService<IApiClient>();
            var onlyActive = ParseOnlyActiveFilter(field.LookupFilter, defaultValue: true);
            var all = await api.Securities_ListAsync(onlyActive, CancellationToken.None);
            var filtered = all
                .Where(sx => string.IsNullOrWhiteSpace(q) || (sx.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) == true) || (sx.Identifier?.Contains(q, StringComparison.OrdinalIgnoreCase) == true))
                .Skip(skip).Take(take)
                .Select(sx => new LookupItem(sx.Id, sx.Name))
                .ToList();
            return filtered;
        }

        private IReadOnlyList<LookupItem> QueryEnumLookup(string? q, Type enumType)
        {
            var items = Enum.GetValues(enumType).Cast<object>()
                .Select(v =>
                {
                    var raw = v?.ToString() ?? string.Empty;
                    // Try localized enum label first (key: EnumType_{EnumName}_{Member}) and fall back to raw member name
                    string name = raw;
                    try
                    {
                        var key = $"EnumType_{enumType.Name}_{raw}";
                        var val = Localizer?[key];
                        if (val != null && !val.ResourceNotFound && !string.IsNullOrWhiteSpace(val.Value)) name = val.Value;
                    }
                    catch { }
                    return new LookupItem(Guid.Empty, name);
                })
                .Where(li => string.IsNullOrWhiteSpace(q) || li.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return items;
        }
        #endregion Lookup Values

        protected virtual CardRecord ApplyEnumTranslations(CardRecord record)
        {
            foreach (var field in record.Fields)
                ApplyEnumTranslation(field);
            return record;
        }
        private void ApplyEnumTranslation(CardField field)
        {
            if (!string.IsNullOrWhiteSpace(field.LookupType) && field.LookupType.StartsWith("Enum:", StringComparison.OrdinalIgnoreCase))
            {
                var enumName = field.LookupType.Substring("Enum:".Length);
                var enumType = ResolveEnumType(enumName);
                if (enumType != null && enumType.IsEnum)
                {
                    var key = $"EnumType_{enumType.Name}_{field.Text}";
                    var val = Localizer?[key];
                    if (val != null && !string.IsNullOrWhiteSpace(val))
                        field.Text = val.Value;
                }
            }
        }

        private Type? ResolveEnumType(string enumName)
        {
            // Try known namespace first
            var candidates = new[] {
                $"FinanceManager.Shared.Dtos.Accounts.{enumName}",
                enumName
            };
            foreach (var n in candidates)
            {
                try
                {
                    var t = Type.GetType(n, throwOnError: false, ignoreCase: true);
                    if (t != null && t.IsEnum) return t;
                }
                catch { }
            }

            // Search loaded assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                try
                {
                    var t = asm.GetTypes().FirstOrDefault(x => x.IsEnum && (string.Equals(x.Name, enumName, StringComparison.OrdinalIgnoreCase) || string.Equals(x.FullName, enumName, StringComparison.OrdinalIgnoreCase) || x.FullName?.EndsWith("." + enumName, StringComparison.OrdinalIgnoreCase) == true));
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
        // IRibbonProvider implementation
        public virtual IReadOnlyList<UiRibbonRegister>? GetRibbonRegisters(IStringLocalizer localizer) => null;
        // Compatibility: legacy tests and callers expect a method named GetRibbon
        public IReadOnlyList<UiRibbonRegister>? GetRibbon(IStringLocalizer localizer) => GetRibbonRegisters(localizer);
        public void SetActiveTab<TTabEnum>(TTabEnum id)
        {
        }
        public TTabEnum? GetActiveTab<TTabEnum>()
        {
            return default;
        }


        public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Returns true when a current user service is available and the user is authenticated.
        /// This provides a simple way for viewmodels and components to check authentication state.
        /// </summary>
        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    var cur = ServiceProvider.GetService<ICurrentUserService>();
                    return cur?.IsAuthenticated ?? false;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Checks the current authentication state using ICurrentUserService and raises
        /// the <see cref="AuthenticationRequired"/> event when the user is not authenticated.
        /// Returns true when the user is authenticated (or no current-user service is available).
        /// </summary>
        public bool CheckAuthentication()
        {
            try
            {
                var cur = ServiceProvider.GetService<FinanceManager.Application.ICurrentUserService>();
                if (cur != null && !cur.IsAuthenticated)
                {
                    AuthenticationRequired?.Invoke(this, null);
                    return false;
                }
            }
            catch { }
            return true;
        }
    }
}
