using Microsoft.Extensions.Localization;
using System.Collections;
using System.Globalization;
using System.Resources;

namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Simple string localizer that reads localization values from the compiled resource set for the <c>Pages</c> resource class.
    /// This implementation implements <see cref="IStringLocalizer{TResource}"/> and performs lookups in the current <see cref="CultureInfo.CurrentUICulture"/>.
    /// </summary>
    public class PagesStringLocalizer : IStringLocalizer<Pages>
    {
        private ResourceSet? resourceSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="PagesStringLocalizer"/> class and loads the resource set
        /// for the current UI culture.
        /// </summary>
        /// <remarks>
        /// The constructor uses <see cref="ResourceManager.GetResourceSet"/> to obtain the resources for <see cref="Pages"/>.
        /// If the resource assembly is not available a <see cref="MissingManifestResourceException"/> may be thrown by <see cref="ResourceManager"/>.
        /// </remarks>
        public PagesStringLocalizer()
        {
            var rm = new ResourceManager(typeof(FinanceManager.Web.Pages));
            resourceSet = rm.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
        }

        /// <summary>
        /// Gets the localized string for the specified name. When no resource is found the returned <see cref="LocalizedString"/>
        /// will have <see cref="LocalizedString.ResourceNotFound"/> set to <c>true</c> and the <see cref="LocalizedString.Value"/> will equal the key.
        /// </summary>
        /// <param name="name">Resource key.</param>
        /// <returns>A <see cref="LocalizedString"/> containing the resolved value or the key when not found.</returns>
        public LocalizedString this[string name]
        {
            get => resourceSet.GetObject(name) is string value
                ? new LocalizedString(name, value, false)
                : new LocalizedString(name, name, true);
        }

        /// <summary>
        /// Gets the localized and formatted string for the specified name and format arguments.
        /// When no resource format is found the returned <see cref="LocalizedString"/> will have
        /// <see cref="LocalizedString.ResourceNotFound"/> set to <c>true</c> and the <see cref="LocalizedString.Value"/> will equal the key.
        /// </summary>
        /// <param name="name">Resource key.</param>
        /// <param name="arguments">Format arguments to apply to the resource value when present.</param>
        /// <returns>A <see cref="LocalizedString"/> containing the formatted value or the key when not found.</returns>
        public LocalizedString this[string name, params object[] arguments]
        {
            get => resourceSet.GetObject(name) is string format
                ? new LocalizedString(name, string.Format(format, arguments), false)
                : new LocalizedString(name, name, true);
        }

        /// <summary>
        /// Returns all localized strings from the resource set for the current UI culture.
        /// </summary>
        /// <param name="includeParentCultures">When true include values from parent cultures. This parameter is currently
        /// forwarded to <see cref="ResourceManager.GetResourceSet"/> when resources are resolved.</param>
        /// <returns>An enumerable of <see cref="LocalizedString"/> instances representing available localized values.</returns>
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return resourceSet?
                .Cast<DictionaryEntry>()
                .Select(de => new LocalizedString((string)de.Key, (string)de.Value, false))
                ?? Enumerable.Empty<LocalizedString>();
        }
    }
}
