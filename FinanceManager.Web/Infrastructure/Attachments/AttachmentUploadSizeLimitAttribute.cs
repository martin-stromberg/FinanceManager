using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceManager.Web.Infrastructure.Attachments;

/// <summary>
/// Applies attachment upload request and multipart limits from runtime configuration before form parsing.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AttachmentUploadSizeLimitAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    /// <summary>
    /// Runs early in the resource-filter phase, before model binding reads multipart form data.
    /// </summary>
    public int Order => int.MinValue + 100;

    /// <inheritdoc />
    public bool IsReusable => false;

    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        => ActivatorUtilities.CreateInstance<AttachmentUploadSizeLimitFilter>(serviceProvider);
}

internal sealed class AttachmentUploadSizeLimitFilter : IResourceFilter, IOrderedFilter
{
    private const long MultipartRequestOverheadBytes = 1L * 1024L * 1024L;

    private readonly AttachmentUploadOptions _attachmentOptions;
    private readonly FormOptions _formOptions;

    public AttachmentUploadSizeLimitFilter(
        IOptions<AttachmentUploadOptions> attachmentOptions,
        IOptions<FormOptions> formOptions)
    {
        _attachmentOptions = attachmentOptions.Value;
        _formOptions = formOptions.Value;
    }

    public int Order => int.MinValue + 100;

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var multipartBodyLimitBytes = _attachmentOptions.NormalizedMaxSizeBytes;
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
