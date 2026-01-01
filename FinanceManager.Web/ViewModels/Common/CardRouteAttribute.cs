using System;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Declares routing metadata for a card view model type. The attribute indicates the "kind" (URL segment)
    /// used to navigate to the card (for example "accounts" => "/card/accounts/{id}").
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class CardRouteAttribute : Attribute
    {
        /// <summary>
        /// The primary route kind (used as the URL segment for the card route).
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Optional sub-kind that can be used to further distinguish route variants for the same view model type.
        /// </summary>
        public string? SubKind { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CardRouteAttribute"/>.
        /// </summary>
        /// <param name="kind">Primary route kind (required). Leading/trailing whitespace will be trimmed.</param>
        /// <param name="subKind">Optional sub-kind; when null or whitespace it is stored as <c>null</c>.</param>
        public CardRouteAttribute(string kind, string? subKind = null)
        {
            Kind = kind?.Trim() ?? string.Empty;
            SubKind = string.IsNullOrWhiteSpace(subKind) ? null : subKind?.Trim();
        }
    }
}
