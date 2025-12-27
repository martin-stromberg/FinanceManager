using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace FinanceManager.Web.ViewModels.Common
{
    public static class CardViewModelResolver
    {
        private static readonly ConcurrentDictionary<string, Type?> _cache = new();

        private static string Normalize(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        private static Type? FindType(string kind, string? subKind)
        {
            var normKind = Normalize(kind);
            var normSub = Normalize(subKind);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (!typeof(ICardInitializable).IsAssignableFrom(t) || t.IsAbstract) continue;
                    var attr = t.GetCustomAttribute<CardRouteAttribute>(false);
                    if (attr == null) continue;
                    var aKind = Normalize(attr.Kind);
                    var aSub = Normalize(attr.SubKind);
                    if (aKind == normKind && aSub == normSub) return t;
                    // match when attr.SubKind is null and request subKind empty
                    if (aKind == normKind && string.IsNullOrEmpty(aSub) && string.IsNullOrEmpty(normSub)) return t;
                }
            }
            return null;
        }

        public static Type? Resolve(string kind, string? subKind)
        {
            var key = Normalize(kind) + "|" + Normalize(subKind);
            return _cache.GetOrAdd(key, _ => FindType(kind, subKind));
        }
    }
}
