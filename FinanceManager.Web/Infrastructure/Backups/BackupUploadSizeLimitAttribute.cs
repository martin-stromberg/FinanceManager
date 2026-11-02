using FinanceManager.Infrastructure.Backups;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Infrastructure.Backups;

/// <summary>
/// Applies the configured backup upload request and multipart limits before form parsing.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BackupUploadSizeLimitAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    /// <summary>
    /// Runs early in the resource-filter phase, before model binding reads multipart form data.
    /// </summary>
    public int Order => int.MinValue + 100;

    /// <inheritdoc />
    public bool IsReusable => false;

    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        => ActivatorUtilities.CreateInstance<BackupUploadSizeLimitFilter>(serviceProvider);
}

internal sealed class BackupUploadSizeLimitFilter : IResourceFilter, IOrderedFilter
{
    private const long MultipartRequestOverheadBytes = 1L * 1024L * 1024L;

    private readonly BackupSecurityOptions _backupOptions;
    private readonly FormOptions _formOptions;

    public BackupUploadSizeLimitFilter(
        IOptions<BackupSecurityOptions> backupOptions,
        IOptions<FormOptions> formOptions)
    {
        _backupOptions = backupOptions.Value;
        _formOptions = formOptions.Value;
    }

    public int Order => int.MinValue + 100;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var multipartBodyLimitBytes = _backupOptions.MaxUploadBytes;
        var requestBodyLimitBytes = AddMultipartOverhead(multipartBodyLimitBytes);
        var maxRequestBodySizeFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (maxRequestBodySizeFeature is { IsReadOnly: false })
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = requestBodyLimitBytes;
        }

        context.HttpContext.Features.Set<IFormFeature>(
            new FormFeature(context.HttpContext.Request, CreateFormOptions(multipartBodyLimitBytes)));
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }

    private FormOptions CreateFormOptions(long limitBytes)
        => new()
        {
            BufferBody = _formOptions.BufferBody,
            MemoryBufferThreshold = _formOptions.MemoryBufferThreshold,
            BufferBodyLengthLimit = _formOptions.BufferBodyLengthLimit,
            ValueCountLimit = _formOptions.ValueCountLimit,
            KeyLengthLimit = _formOptions.KeyLengthLimit,
            ValueLengthLimit = _formOptions.ValueLengthLimit,
            MultipartBoundaryLengthLimit = _formOptions.MultipartBoundaryLengthLimit,
            MultipartHeadersCountLimit = _formOptions.MultipartHeadersCountLimit,
            MultipartHeadersLengthLimit = _formOptions.MultipartHeadersLengthLimit,
            MultipartBodyLengthLimit = limitBytes
        };

    private static long AddMultipartOverhead(long multipartBodyLimitBytes)
    {
        if (multipartBodyLimitBytes > long.MaxValue - MultipartRequestOverheadBytes)
        {
            return long.MaxValue;
        }

        return multipartBodyLimitBytes + MultipartRequestOverheadBytes;
    }
}
