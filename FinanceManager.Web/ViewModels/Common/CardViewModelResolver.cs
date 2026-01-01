using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Resolves a card view model <see cref="Type"/> for a given route kind and optional sub-kind.
    /// The resolver scans loaded assemblies for types implementing <see cref="ICardInitializable"/> and
    /// decorated with <see cref="CardRouteAttribute"/>. Results are cached to avoid repeated reflections.
    /// </summary>
    public static class CardViewModelResolver
    {
        private static readonly ConcurrentDictionary<string, Type?> _cache = new();

        /// <summary>
        /// Normalizes a route segment by trimming and converting to lower-case invariant.
        /// </summary>
        /// <param name="s">Input string that may be <c>null</c>.</param>
        /// <returns>Normalized string suitable for comparisons (never <c>null</c>).</returns>
        private static string Normalize(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>
        /// Performs a reflection-based search for a type matching the provided kind and subKind.
        /// </summary>
        /// <param name="kind">Primary route kind to match.</param>
        /// <param name="subKind">Optional route sub-kind to match.</param>
        /// <returns>The <see cref="Type"/> when a matching view model type is found; otherwise <c>null</c>.</returns>
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

        /// <summary>
        /// Resolves and returns the view model <see cref="Type"/> for the supplied <paramref name="kind"/> and optional <paramref name="subKind"/>.
        /// The result is cached for subsequent lookups.
        /// </summary>
        /// <param name="kind">Primary route kind (e.g. "accounts").</param>
        /// <param name="subKind">Optional sub-kind to further distinguish route variants (may be <c>null</c>).</param>
        /// <returns>
        /// A <see cref="Type"/> implementing <see cref="ICardInitializable"/> that matches the specified route metadata,
        /// or <c>null</c> when no matching type could be found.
        /// </returns>
        public static Type? Resolve(string kind, string? subKind)
        {
            var key = Normalize(kind) + "|" + Normalize(subKind);
            return _cache.GetOrAdd(key, _ => FindType(kind, subKind));
        }
    }
}
