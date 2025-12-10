using FinanceManager.Shared;
using FinanceManager.Web.ViewModels; // for IRibbonProvider
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinanceManager.Web.ViewModels.Common
{
    public abstract class BaseViewModel : IAsyncDisposable, IRibbonProvider
    {
        protected BaseViewModel(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            Localizer = serviceProvider.GetRequiredService<IStringLocalizer<Pages>>();
        }
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
                var entry = Localizer[LastErrorCode];
                if (!entry.ResourceNotFound)
                    LastError = entry.Value;
            }
        }
        protected IServiceProvider ServiceProvider { get; }
        protected IStringLocalizer? Localizer { get; }

        public event EventHandler? StateChanged;
        public sealed class UiActionEventArgs : EventArgs
        {
            public string? Action { get; }
            public string? Payload { get; }
            public UiActionEventArgs(string? action, string? payload)
            {
                Action = action; Payload = payload;
            }
        }
        public event EventHandler<UiActionEventArgs?>? UiActionRequested;
        public sealed record LookupItem(System.Guid Key, string Name);
        protected void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
        protected void RaiseUiActionRequested(string? action) => UiActionRequested?.Invoke(this, new UiActionEventArgs(action, null));
        protected void RaiseUiActionRequested(string? action, string? payload) => UiActionRequested?.Invoke(this, new UiActionEventArgs(action, payload));

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

        private IReadOnlyList<LookupItem> QueryEnumLookup(string? q, Type enumType)
        {
            var items = Enum.GetValues(enumType).Cast<object>()
                .Select(v =>
                {
                    var raw = v?.ToString() ?? string.Empty;
                    string display = raw;
                    try
                    {
                        var key = $"EnumType_{enumType.Name}_{raw}";
                        var val = Localizer[key];
                        if (!string.IsNullOrEmpty(val)) display = val.Value;
                    }
                    catch { }
                    return new LookupItem(Guid.Empty, display);
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
                    var val = Localizer[key];
                    if (!string.IsNullOrWhiteSpace(val))
                        field.Text = val;
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
