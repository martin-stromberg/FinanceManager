using System.Globalization;
using System.Text;
using FinanceManager.Application.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Imports security daily close prices from ING CSV files.
/// </summary>
public sealed class IngSecurityPriceImportService : ISecurityPriceImportService, ISecurityPriceImportInspector
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    private readonly ISecurityPriceService _priceService;
    private readonly ILogger<IngSecurityPriceImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IngSecurityPriceImportService"/> class.
    /// </summary>
    /// <param name="priceService">Service used to upsert parsed daily prices.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    public IngSecurityPriceImportService(
        ISecurityPriceService priceService,
        ILogger<IngSecurityPriceImportService> logger)
    {
        _priceService = priceService;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanHandle(SecurityPriceImportContext context)
    {
        return !string.IsNullOrWhiteSpace(context.Provider) &&
               string.Equals(context.Provider.Trim(), "ing", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<SecurityPriceImportResultDto> ImportAsync(
        Guid ownerUserId,
        Guid securityId,
        Stream stream,
        SecurityPriceImportContext context,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Starting ING security price import for {SecurityId} with provider '{Provider}' and file '{FileName}'",
            securityId,
            context.Provider,
            context.FileName);

        ArgumentNullException.ThrowIfNull(stream);

        var parse = ParseIngCsv(stream);
        if (!parse.IsValid || parse.Error is not null)
        {
            var errors = parse.Error is null ? Array.Empty<SecurityPriceImportErrorDto>() : [parse.Error];
            return new SecurityPriceImportResultDto(0, 0, 0, 0, errors);
        }

        var upsertResult = await _priceService.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            parse.Items.Values.OrderBy(x => x.Date).ToList(),
            ct);

        return upsertResult;
    }

    internal static bool TryInspectContent(byte[] content, out string securityName, out string? validationMessage)
    {
        var parse = ParseIngCsv(new MemoryStream(content, writable: false));
        securityName = parse.SecurityName;
        validationMessage = parse.Error?.Message;
        return parse.IsValid;
    }

    /// <inheritdoc />
    public bool TryInspect(SecurityPriceImportContext context, byte[] content, out SecurityPriceImportInspectionResult result)
    {
        result = default!;

        if (!string.IsNullOrWhiteSpace(context.Provider) &&
            !string.Equals(context.Provider.Trim(), "ing", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryInspectContent(content, out var securityName, out _))
        {
            return false;
        }

        result = new SecurityPriceImportInspectionResult("ing", "ing", "ING", securityName);
        return true;
    }

    private static ParseResult ParseIngCsv(Stream stream)
    {
        if (!stream.CanRead)
        {
            return ParseResult.Invalid(0, "Invalid input stream.");
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var rows = new Dictionary<DateTime, SecurityPriceImportItem>();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        var lineNo = 0;
        var securityName = string.Empty;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            lineNo++;

            if (lineNo == 1)
            {
                continue;
            }

            if (lineNo == 2)
            {
                if (!TrySplitTwoColumns(line, out var headerDate, out var headerName) ||
                    !string.Equals(headerDate, "Zeit", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(headerName))
                {
                    return ParseResult.Invalid(lineNo, "Invalid header row. Expected 'Zeit;<SecurityName>'.");
                }

                securityName = headerName.Trim();
                continue;
            }

            if (line is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (reader.EndOfStream)
                {
                    continue;
                }

                return ParseResult.Invalid(lineNo, "Invalid empty data row.");
            }

            if (!TrySplitTwoColumns(line, out var datePart, out var closePart))
            {
                return ParseResult.Invalid(lineNo, "Invalid data row. Expected exactly two columns.");
            }

            if (!DateTime.TryParseExact(datePart.Trim(), "dd.MM.yyyy HH:mm:ss", GermanCulture, DateTimeStyles.None, out var parsedDate))
            {
                return ParseResult.Invalid(lineNo, "Invalid date format.");
            }

            if (!decimal.TryParse(closePart.Trim(), NumberStyles.Number, GermanCulture, out var close))
            {
                return ParseResult.Invalid(lineNo, "Invalid close value format.");
            }

            if (close < 0m)
            {
                return ParseResult.Invalid(lineNo, "Close value must not be negative.");
            }

            rows[parsedDate.Date] = new SecurityPriceImportItem(parsedDate.Date, close, lineNo);
        }

        if (lineNo < 2)
        {
            return ParseResult.Invalid(lineNo, "Missing header rows.");
        }

        if (rows.Count == 0)
        {
            return ParseResult.Invalid(lineNo, "No price rows found.");
        }

        return ParseResult.Valid(securityName, rows);
    }

    private static bool TrySplitTwoColumns(string? line, out string left, out string right)
    {
        left = string.Empty;
        right = string.Empty;
        if (line is null)
        {
            return false;
        }

        var separatorIndex = line.IndexOf(';');
        if (separatorIndex <= 0 || separatorIndex >= line.Length - 1)
        {
            return false;
        }

        if (line.IndexOf(';', separatorIndex + 1) >= 0)
        {
            return false;
        }

        left = line[..separatorIndex];
        right = line[(separatorIndex + 1)..];
        return true;
    }

    private sealed record ParseResult(bool IsValid, string SecurityName, IReadOnlyDictionary<DateTime, SecurityPriceImportItem> Items, SecurityPriceImportErrorDto? Error)
    {
        public static ParseResult Valid(string securityName, IReadOnlyDictionary<DateTime, SecurityPriceImportItem> items)
            => new(true, securityName, items, null);

        public static ParseResult Invalid(int lineNo, string message)
            => new(false, string.Empty, new Dictionary<DateTime, SecurityPriceImportItem>(), new SecurityPriceImportErrorDto(lineNo, message));
    }
}
