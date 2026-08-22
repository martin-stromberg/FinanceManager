using msTools.Updater;

namespace FinanceManager.Web.Services.Updates;

internal static class UpdateErrorMessageMapper
{
    public const string GithubRateLimitMessage =
        "GitHub hat die Update-Pruefung wegen einer Rate-Limit-Begrenzung voruebergehend abgelehnt. Bitte spaeter erneut versuchen.";

    public static string? Map(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return IsGithubRateLimit(message) ? GithubRateLimitMessage : message;
    }

    public static string Map(Exception exception)
        => IsGithubRateLimit(exception.ToString()) ? GithubRateLimitMessage : exception.Message;

    public static string Map(AutoUpdateError error)
        => IsGithubRateLimit(error.ToString()) ? GithubRateLimitMessage : error.Message;

    public static bool IsGithubRateLimit(string value)
        => value.Contains("403", StringComparison.OrdinalIgnoreCase)
            && value.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
}
