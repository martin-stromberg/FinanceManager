using FinanceManager.Application.Mcp;
using System.Globalization;
using System.Text.Json;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Typed, whitelisted argument parsing helpers for MCP tool handlers (EP-02). Every read
/// adapter parses its <c>arguments</c> object exclusively through these helpers: unknown
/// properties are ignored, malformed values produce the stable error code
/// <c>Err_Mcp_InvalidArguments</c>, and paging is clamped server-side to the API convention
/// (<c>take</c> 1–200, scope-und-toolkatalog.md §3.1 / §4.6).
/// </summary>
internal static class McpToolArguments
{
    /// <summary>Server-side upper bound for the <c>take</c> paging parameter.</summary>
    internal const int MaxTake = 200;

    /// <summary>
    /// Reads the <c>skip</c> paging parameter (default 0, clamped to non-negative).
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <returns>The effective skip value.</returns>
    public static int Skip(JsonElement args)
        => Math.Max(0, OptionalInt(args, "skip") ?? 0);

    /// <summary>
    /// Reads the <c>take</c> paging parameter clamped to 1–<see cref="MaxTake"/>.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="fallback">Default when absent.</param>
    /// <returns>The effective take value.</returns>
    public static int Take(JsonElement args, int fallback = 50)
        => Math.Clamp(OptionalInt(args, "take") ?? fallback, 1, MaxTake);

    /// <summary>
    /// Whether the named property exists on the arguments object — <b>including</b> an
    /// explicit JSON <c>null</c>. Merge-style update paths interpret present-null as
    /// "clear the field"; use <see cref="HasNonNull"/> when only a usable value counts.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns><see langword="true"/> when the property is present (any JSON value).</returns>
    public static bool Has(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
           && args.TryGetProperty(name, out _);

    /// <summary>Whether the named property exists and carries a non-null JSON value.</summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns><see langword="true"/> when a non-null value is present.</returns>
    public static bool HasNonNull(JsonElement args, string name)
        => args.ValueKind == JsonValueKind.Object
           && args.TryGetProperty(name, out var el)
           && el.ValueKind != JsonValueKind.Null;

    /// <summary>
    /// Reads an optional UUID argument; invalid values raise a controlled tool error.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed id or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is present but not a UUID.</exception>
    public static Guid? OptionalGuid(JsonElement args, string name)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var id))
        {
            return id;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be a UUID string.");
    }

    /// <summary>
    /// Reads a required UUID argument.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed id.</returns>
    /// <exception cref="McpToolException">Missing or malformed value.</exception>
    public static Guid RequiredGuid(JsonElement args, string name)
        => OptionalGuid(args, name)
           ?? throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' is required and must be a UUID string.");

    /// <summary>
    /// Reads an optional integer argument.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed value or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is present but not an integer.</exception>
    public static int? OptionalInt(JsonElement args, string name)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var value))
        {
            return value;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be an integer.");
    }

    /// <summary>
    /// Reads an optional boolean argument.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed value or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is present but not a boolean.</exception>
    public static bool? OptionalBool(JsonElement args, string name)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be a boolean.")
        };
    }

    /// <summary>
    /// Reads an optional string argument with a hard length cap (defense against oversized
    /// untrusted input).
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <param name="maxLength">Maximum accepted length.</param>
    /// <returns>The string value or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is not a string or exceeds the cap.</exception>
    public static string? OptionalString(JsonElement args, string name, int maxLength = 500)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.String)
        {
            var value = el.GetString();
            if (value is { Length: > 0 } && value.Length <= maxLength)
            {
                return value;
            }
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
        }
        throw new McpToolException("Err_Mcp_InvalidArguments",
            $"Argument '{name}' must be a string of at most {maxLength} characters.");
    }

    /// <summary>
    /// Reads an optional date/date-time argument (ISO-8601; interpreted as UTC).
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed instant or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value is present but unparseable.</exception>
    public static DateTime? OptionalDateTime(JsonElement args, string name)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.String
            && DateTime.TryParse(el.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            return value;
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must be an ISO-8601 date string.");
    }

    /// <summary>
    /// Reads an optional enum argument given as its name (case-insensitive) or numeric value.
    /// </summary>
    /// <typeparam name="TEnum">Enum type.</typeparam>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <returns>The parsed value or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">The value does not map to a defined enum member.</exception>
    public static TEnum? OptionalEnum<TEnum>(JsonElement args, string name) where TEnum : struct, Enum
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind == JsonValueKind.String
            && Enum.TryParse<TEnum>(el.GetString(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var numeric)
            && Enum.IsDefined(typeof(TEnum), numeric))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }
        throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' is not a valid {typeof(TEnum).Name} value.");
    }

    /// <summary>
    /// Reads an optional array of UUID strings.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <param name="maxItems">Maximum accepted item count.</param>
    /// <returns>The parsed list or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">Malformed element or too many items.</exception>
    public static IReadOnlyList<Guid>? OptionalGuidList(JsonElement args, string name, int maxItems = 200)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() > maxItems)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument '{name}' must be an array of at most {maxItems} UUID strings.");
        }
        var list = new List<Guid>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !Guid.TryParse(item.GetString(), out var id))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must contain UUID strings only.");
            }
            list.Add(id);
        }
        return list;
    }

    /// <summary>
    /// Reads an optional array of integers.
    /// </summary>
    /// <param name="args">Tool arguments.</param>
    /// <param name="name">Property name.</param>
    /// <param name="maxItems">Maximum accepted item count.</param>
    /// <returns>The parsed list or <see langword="null"/>.</returns>
    /// <exception cref="McpToolException">Malformed element or too many items.</exception>
    public static IReadOnlyList<int>? OptionalIntList(JsonElement args, string name, int maxItems = 200)
    {
        if (!HasNonNull(args, name))
        {
            return null;
        }
        var el = args.GetProperty(name);
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() > maxItems)
        {
            throw new McpToolException("Err_Mcp_InvalidArguments",
                $"Argument '{name}' must be an array of at most {maxItems} integers.");
        }
        var list = new List<int>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value))
            {
                throw new McpToolException("Err_Mcp_InvalidArguments", $"Argument '{name}' must contain integers only.");
            }
            list.Add(value);
        }
        return list;
    }
}
