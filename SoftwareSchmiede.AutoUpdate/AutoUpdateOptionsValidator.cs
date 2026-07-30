using Microsoft.Extensions.Options;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Validates <see cref="AutoUpdateOptions"/> at startup. Does not validate <see cref="AutoUpdateOptions.HealthTimeoutSeconds"/>:
/// <see cref="AutoUpdateHostBuilderExtensions.UseAutoUpdate"/> clamps it into range before validation runs, so an
/// out-of-range value is silently corrected rather than rejected.
/// </summary>
public sealed class AutoUpdateOptionsValidator : IValidateOptions<AutoUpdateOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AutoUpdateOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DownloadPath) || options.DownloadPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            failures.Add("DownloadPath must not be empty and must not contain invalid path characters.");
        }

        if (options.Source is null)
        {
            failures.Add("Source must be configured.");
        }

        if (options.MaxAssetBytes <= 0)
        {
            failures.Add("MaxAssetBytes must be greater than zero.");
        }

        if (options.SourceCheck.Interval < 1)
        {
            failures.Add("SourceCheck.Interval must be at least 1 minute.");
        }

        foreach (var range in options.SourceCheck.TimeRanges)
        {
            if (range.StartTime >= range.EndTime)
            {
                failures.Add($"SourceCheck.TimeRanges entry for {range.DayOfWeek} must have StartTime before EndTime.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
