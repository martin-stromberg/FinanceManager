using FinanceManager.Application.Contacts;
using FinanceManager.Domain.Contacts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FinanceManager.Infrastructure.Contacts;

/// <summary>
/// JSON-backed implementation of <see cref="IKnownContactCatalog"/>.
/// </summary>
public sealed class KnownContactCatalog : IKnownContactCatalog
{
    private const string DefaultRelativePath = "Data/KnownContacts.json";

    private readonly string _filePath;
    private readonly ILogger<KnownContactCatalog> _logger;
    private IReadOnlyList<KnownContactDefinition>? _cache;

    /// <summary>
    /// Creates a known contact catalog that reads from the application data directory.
    /// </summary>
    /// <param name="environment">Host environment used to resolve the content root.</param>
    /// <param name="logger">Logger for catalog loading diagnostics.</param>
    public KnownContactCatalog(IHostEnvironment environment, ILogger<KnownContactCatalog> logger)
        : this(Path.Combine(environment.ContentRootPath, DefaultRelativePath), logger)
    {
    }

    /// <summary>
    /// Creates a known contact catalog for a specific JSON file path.
    /// </summary>
    /// <param name="filePath">Path to the JSON catalog file.</param>
    /// <param name="logger">Logger for catalog loading diagnostics.</param>
    public KnownContactCatalog(string filePath, ILogger<KnownContactCatalog> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<KnownContactMatch?> FindMatchAsync(IEnumerable<string?> searchTexts, CancellationToken ct)
    {
        var definitions = await LoadAsync(ct);
        if (definitions.Count == 0)
        {
            return null;
        }

        var normalizedTexts = searchTexts
            .Select(Normalize)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedTexts.Count == 0)
        {
            return null;
        }

        var matches = definitions
            .Where(definition => Matches(definition, normalizedTexts))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(2)
            .ToList();

        if (matches.Count != 1)
        {
            return null;
        }

        var match = matches[0];
        var aliases = match.Aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new KnownContactMatch(match.Name, match.Type, aliases);
    }

    private async Task<IReadOnlyList<KnownContactDefinition>> LoadAsync(CancellationToken ct)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        if (!File.Exists(_filePath))
        {
            _cache = Array.Empty<KnownContactDefinition>();
            return _cache;
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var catalog = await JsonSerializer.DeserializeAsync<KnownContactCatalogFile>(stream, JsonOptions, ct);
            _cache = (catalog?.Contacts ?? Array.Empty<KnownContactDefinition>())
                .Where(definition => !string.IsNullOrWhiteSpace(definition.Name))
                .Select(definition => definition.Normalize())
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Known contact catalog could not be loaded from {Path}", _filePath);
            _cache = Array.Empty<KnownContactDefinition>();
        }

        return _cache;
    }

    private static bool Matches(KnownContactDefinition definition, IReadOnlyList<string> normalizedTexts)
    {
        var normalizedName = Normalize(definition.Name);
        if (!string.IsNullOrWhiteSpace(normalizedName)
            && normalizedTexts.Any(text => text.Contains(normalizedName, StringComparison.Ordinal)))
        {
            return true;
        }

        foreach (var alias in definition.Aliases)
        {
            var normalizedAlias = Normalize(alias);
            if (string.IsNullOrWhiteSpace(normalizedAlias))
            {
                continue;
            }

            var regexPattern = "^" + Regex.Escape(normalizedAlias)
                .Replace("\\*", ".*", StringComparison.Ordinal)
                .Replace("\\?", ".", StringComparison.Ordinal) + "$";

            if (normalizedTexts.Any(text => Regex.IsMatch(text, regexPattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim().ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.OrdinalIgnoreCase)
            .Replace("ö", "oe", StringComparison.OrdinalIgnoreCase)
            .Replace("ü", "ue", StringComparison.OrdinalIgnoreCase)
            .Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);

        return Regex.Replace(normalized, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class KnownContactCatalogFile
    {
        public IReadOnlyList<KnownContactDefinition> Contacts { get; init; } = Array.Empty<KnownContactDefinition>();
    }

    private sealed class KnownContactDefinition
    {
        public string Name { get; init; } = string.Empty;
        public ContactType Type { get; init; } = ContactType.Organization;
        public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

        public KnownContactDefinition Normalize()
        {
            return new KnownContactDefinition
            {
                Name = Name.Trim(),
                Type = Type,
                Aliases = Aliases
                    .Where(alias => !string.IsNullOrWhiteSpace(alias))
                    .Select(alias => alias.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
    }
}
