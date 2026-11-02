using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Infrastructure.MassImport;

/// <summary>
/// Applies the configured mass-import request body size limit before the JSON body is read.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MassImportRequestSizeLimitAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    /// <summary>
    /// Runs early in the resource-filter phase, before model binding deserializes the request body.
    /// </summary>
    public int Order => int.MinValue + 100;

    /// <inheritdoc />
    public bool IsReusable => false;

    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        => ActivatorUtilities.CreateInstance<MassImportRequestSizeLimitFilter>(serviceProvider);
}

internal sealed class MassImportRequestSizeLimitFilter : IResourceFilter, IOrderedFilter
{
    private readonly MassImportOptions _massImportOptions;

    public MassImportRequestSizeLimitFilter(IOptions<MassImportOptions> massImportOptions)
    {
        _massImportOptions = massImportOptions.Value;
    }

    public int Order => int.MinValue + 100;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var maxRequestBodySizeFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (maxRequestBodySizeFeature is { IsReadOnly: false })
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = _massImportOptions.MaxRequestBytes;
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
