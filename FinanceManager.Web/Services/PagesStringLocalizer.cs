using DocumentFormat.OpenXml;
using Microsoft.Extensions.Localization;
using System.Collections;
using System.Globalization;
using System.Resources;

namespace FinanceManager.Web.Services
{
    public class PagesStringLocalizer : IStringLocalizer<Pages>
    {
        private ResourceSet? resourceSet;

        public PagesStringLocalizer()
        {
            var rm = new ResourceManager(typeof(FinanceManager.Web.Pages));
            resourceSet = rm.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
        }

        public LocalizedString this[string name]
        {
            get => resourceSet.GetObject(name) is string value
                ? new LocalizedString(name, value, false)
                : new LocalizedString(name, name, true);
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get => resourceSet.GetObject(name) is string format
                ? new LocalizedString(name, string.Format(format, arguments), false)
                : new LocalizedString(name, name, true);
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return resourceSet?
                .Cast<DictionaryEntry>()
                .Select(de => new LocalizedString((string)de.Key, (string)de.Value, false))
                ?? Enumerable.Empty<LocalizedString>();
        }
    }
}
