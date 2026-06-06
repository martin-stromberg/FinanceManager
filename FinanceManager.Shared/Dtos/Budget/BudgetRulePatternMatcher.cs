using System.Text.RegularExpressions;

namespace FinanceManager.Shared.Dtos.Budget;

/// <summary>
/// Matches a budget rule pattern against posting text.
/// </summary>
public static class BudgetRulePatternMatcher
{
    /// <summary>
    /// Determines whether the given pattern matches the combined posting subject and description.
    /// </summary>
    /// <param name="subject">The posting subject.</param>
    /// <param name="description">The posting description.</param>
    /// <param name="pattern">The rule pattern.</param>
    /// <param name="useRegex">Whether the pattern should be interpreted as a regular expression.</param>
    /// <param name="regexTimeout">Optional timeout used for regex matching.</param>
    /// <returns>True when the pattern matches the posting text; otherwise false.</returns>
    public static bool MatchesPosting(string? subject, string? description, string? pattern, bool useRegex, TimeSpan? regexTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var input = string.Join(" ", new[] { subject, description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var trimmedPattern = pattern.Trim();
        if (trimmedPattern.Length == 0)
        {
            return true;
        }

        if (useRegex)
        {
            try
            {
                return Regex.IsMatch(
                    input,
                    trimmedPattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    regexTimeout ?? TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        return input.IndexOf(trimmedPattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
