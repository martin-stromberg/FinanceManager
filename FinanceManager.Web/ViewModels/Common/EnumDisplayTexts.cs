using Microsoft.Extensions.Localization;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Resolves display texts for enum members via the shared
    /// <c>EnumType_{EnumName}_{Member}</c> resource key convention used by lists, cards and panels.
    /// </summary>
    public static class EnumDisplayTexts
    {
        /// <summary>
        /// Returns the localized display text for <paramref name="value"/> by looking up the
        /// resource key <c>EnumType_{EnumName}_{Member}</c> on <paramref name="localizer"/>.
        /// Falls back to the raw member name when the resource is missing, empty or echoes the key.
        /// </summary>
        /// <typeparam name="TEnum">The enum type of <paramref name="value"/>.</typeparam>
        /// <param name="value">Enum member to display.</param>
        /// <param name="localizer">Localizer holding the <c>EnumType_*</c> resources; may be <c>null</c>.</param>
        /// <param name="emptyWhenUnspecified">
        /// When <c>true</c>, a member named <c>Unspecified</c> yields an empty string instead of a label.
        /// </param>
        /// <returns>The localized text, the enum member name as fallback, or an empty string.</returns>
        public static string GetText<TEnum>(TEnum value, IStringLocalizer? localizer, bool emptyWhenUnspecified = false)
            where TEnum : struct, Enum
        {
            var memberName = value.ToString();
            if (emptyWhenUnspecified && string.Equals(memberName, "Unspecified", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var key = $"EnumType_{typeof(TEnum).Name}_{memberName}";
            var localized = localizer?[key];
            return localized != null
                && !localized.ResourceNotFound
                && !string.IsNullOrWhiteSpace(localized.Value)
                && !string.Equals(localized.Value, key, StringComparison.Ordinal)
                ? localized.Value
                : memberName;
        }
    }
}
