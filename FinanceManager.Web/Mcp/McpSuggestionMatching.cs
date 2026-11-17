using System.Text.RegularExpressions;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Read-only replica of the statement-draft classification matching rules
/// (<c>StatementDraftService.Classification.cs</c>) for the EP-04 suggestion tools.
/// The pipeline methods mutate and persist draft entries; these helpers evaluate the same
/// heuristics against already materialized DTO data so that a suggestion can predict which
/// target <c>ClassifyAsync</c> would assign — without any write effect (core invariant of
/// EP-04: suggestions never persist user data).
/// </summary>
internal static class McpSuggestionMatching
{
    /// <summary>
    /// Normalizes German umlauts to ASCII-friendly replacements ("ä"→"ae", "ß"→"ss"),
    /// identical to the pipeline normalization.
    /// </summary>
    /// <param name="text">Input text (may be null/empty).</param>
    /// <returns>The normalized string (never null).</returns>
    public static string NormalizeUmlauts(string? text)
    {
        if (string.IsNullOrEmpty(text)) { return string.Empty; }
        return text
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the normalized search text the pipeline derives from a recipient/subject line
    /// (umlauts normalized, lower-cased, trailing whitespace trimmed).
    /// </summary>
    /// <param name="text">Raw booking text.</param>
    /// <returns>The normalized search text.</returns>
    public static string NormalizeSearchText(string? text)
        => NormalizeUmlauts((text ?? string.Empty).ToLowerInvariant().TrimEnd());

    /// <summary>
    /// Matches a normalized search text against one <c>AliasName.Pattern</c> wildcard
    /// pattern (<c>*</c> = any run, <c>?</c> = single char), identical to the pipeline's
    /// wildcard→regex expansion.
    /// </summary>
    /// <param name="pattern">The stored alias pattern (untrusted content).</param>
    /// <param name="searchText">The normalized search text.</param>
    /// <returns><see langword="true"/> when the pattern matches the whole search text.</returns>
    public static bool AliasPatternMatches(string? pattern, string searchText)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }
        var regexPattern = "^" + Regex.Escape(pattern.ToLowerInvariant())
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(searchText, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Predicts the contact the pipeline contact-assignment would choose for a normalized
    /// search text — same net priority as <c>AssignContact</c>: a contact whose normalized
    /// name is contained in the text wins; only without such a hit are the alias wildcard
    /// patterns consulted (first verbatim, then with all whitespace removed).
    /// </summary>
    /// <param name="contacts">The owner's contacts (id + name).</param>
    /// <param name="aliasLookup">Alias patterns grouped by contact id.</param>
    /// <param name="searchText">Normalized search text (see <see cref="NormalizeSearchText"/>).</param>
    /// <returns>The predicted contact id, or <see langword="null"/> without a match.</returns>
    public static Guid? MatchContactId(
        IReadOnlyList<(Guid Id, string Name)> contacts,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> aliasLookup,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        // Pipeline quirk (faithful replica): a contains-name match short-circuits before the
        // alias patterns are consulted.
        var nameHit = contacts
            .Where(c => !string.IsNullOrWhiteSpace(c.Name)
                        && searchText.Contains(NormalizeUmlauts(c.Name), StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Id)
            .FirstOrDefault();
        if (nameHit != Guid.Empty)
        {
            return nameHit;
        }

        for (int idxMode = 0; idxMode < 2; idxMode++)
        {
            var probe = idxMode == 1
                ? Regex.Replace(searchText, "\\s+", string.Empty)
                : searchText;
            foreach (var (contactId, patterns) in aliasLookup)
            {
                foreach (var pattern in patterns)
                {
                    if (AliasPatternMatches(pattern, probe))
                    {
                        return contactId;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Normalizes text for security matching (umlauts, upper-case, A–Z/0–9 only) —
    /// identical to the pipeline's <c>NormalizeForSecurityMatch</c>.
    /// </summary>
    /// <param name="text">Raw text.</param>
    /// <returns>The normalized probe string.</returns>
    public static string NormalizeForSecurityMatch(string? text)
    {
        var baseText = NormalizeUmlauts(text ?? string.Empty).ToUpperInvariant();
        return Regex.Replace(baseText, "[^A-Z0-9]", string.Empty, RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Collapses all whitespace — the savings-plan subject normalization of the pipeline.
    /// </summary>
    /// <param name="text">Raw text.</param>
    /// <returns>The whitespace-free string.</returns>
    public static string RemoveWhitespace(string? text)
        => Regex.Replace(text ?? string.Empty, "\\s+", string.Empty);
}
