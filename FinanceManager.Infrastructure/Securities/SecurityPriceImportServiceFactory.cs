using FinanceManager.Application.Securities;
using FinanceManager.Shared.Dtos.Securities;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Securities;

/// <summary>
/// Resolves a provider-specific <see cref="ISecurityPriceImportService"/> implementation.
/// </summary>
public sealed class SecurityPriceImportServiceFactory : ISecurityPriceImportServiceFactory
{
    private readonly IEnumerable<ISecurityPriceImportService> _services;
    private readonly ILogger<SecurityPriceImportServiceFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityPriceImportServiceFactory"/> class.
    /// </summary>
    /// <param name="services">Registered import services.</param>
    /// <param name="logger">Logger used for diagnostic messages.</param>
    public SecurityPriceImportServiceFactory(
        IEnumerable<ISecurityPriceImportService> services,
        ILogger<SecurityPriceImportServiceFactory> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc />
    public ISecurityPriceImportService Resolve(SecurityPriceImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "Resolving security price import service for provider '{Provider}' and file '{FileName}'",
            context.Provider,
            context.FileName);

        var service = _services.FirstOrDefault(x => x.CanHandle(context));
        if (service == null)
        {
            throw new InvalidOperationException("No matching security price import service found for the uploaded file.");
        }

        return service;
    }

    /// <inheritdoc />
    public bool TryResolveByContent(
        SecurityPriceImportContext context,
        byte[] content,
        out ISecurityPriceImportService? service,
        out SecurityPriceImportInspectionResult? inspection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(content);

        foreach (var candidate in _services)
        {
            if (candidate is not ISecurityPriceImportInspector inspector)
            {
                continue;
            }

            if (!inspector.TryInspect(context, content, out var detected))
            {
                continue;
            }

            service = candidate;
            inspection = detected;
            return true;
        }

        service = null;
        inspection = null;
        return false;
    }
}
