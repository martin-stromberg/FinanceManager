using System.Globalization;
using System.Text;
using FinanceManager.Application.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Imports security daily close prices from ING CSV files.
/// </summary>
public sealed class IngSecurityPriceImportService : ISecurityPriceImportService
{
    private static readonly string[] SupportedExtensions = [".csv"];
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
        if (!string.IsNullOrWhiteSpace(context.Provider) &&
            string.Equals(context.Provider.Trim(), "ing", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SupportedExtensions.Any(ext => context.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
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

        var parsedByDate = new Dictionary<DateTime, SecurityPriceImportItem>();
        var errors = new List<SecurityPriceImportErrorDto>();
        var skipped = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);

        var lineNumber = 0;
        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(ct);
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                skipped++;
                continue;
            }

            if (lineNumber == 1 && line.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (lineNumber == 2 && line.StartsWith("Zeit;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 2)
            {
                skipped++;
                errors.Add(new SecurityPriceImportErrorDto(lineNumber, "Missing required columns."));
                continue;
            }

            if (!DateTime.TryParseExact(parts[0].Trim(), "dd.MM.yyyy HH:mm:ss", GermanCulture, DateTimeStyles.None, out var parsedDate))
            {
                skipped++;
                errors.Add(new SecurityPriceImportErrorDto(lineNumber, "Invalid date format."));
                continue;
            }

            if (!decimal.TryParse(parts[1].Trim(), NumberStyles.Number, GermanCulture, out var close))
            {
                skipped++;
                errors.Add(new SecurityPriceImportErrorDto(lineNumber, "Invalid close value format."));
                continue;
            }

            if (close < 0m)
            {
                skipped++;
                errors.Add(new SecurityPriceImportErrorDto(lineNumber, "Close value must not be negative."));
                continue;
            }

            parsedByDate[parsedDate.Date] = new SecurityPriceImportItem(parsedDate.Date, close, lineNumber);
        }

        if (parsedByDate.Count == 0)
        {
            return new SecurityPriceImportResultDto(0, 0, 0, skipped, errors);
        }

        var upsertResult = await _priceService.UpsertDailyPricesAsync(
            ownerUserId,
            securityId,
            parsedByDate.Values.OrderBy(x => x.Date).ToList(),
            ct);

        return upsertResult with { Skipped = upsertResult.Skipped + skipped, Errors = errors };
    }
}
