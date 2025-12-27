using System;

namespace FinanceManager.Web.ViewModels.Common
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class CardRouteAttribute : Attribute
    {
        public string Kind { get; }
        public string? SubKind { get; }

        public CardRouteAttribute(string kind, string? subKind = null)
        {
            Kind = kind?.Trim() ?? string.Empty;
            SubKind = string.IsNullOrWhiteSpace(subKind) ? null : subKind?.Trim();
        }
    }
}
