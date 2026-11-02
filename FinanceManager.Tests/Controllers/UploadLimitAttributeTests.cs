using FinanceManager.Web.Controllers;
using FinanceManager.Web.Infrastructure.Attachments;
using FinanceManager.Web.Infrastructure.Backups;
using FinanceManager.Web.Infrastructure.MassImport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Guards the request-size-limit contract of upload and mass-import endpoints: every action that
/// previously carried an unbounded <see cref="RequestSizeLimitAttribute"/> must now use a
/// configured limit filter, and the mass-import filter must apply its configured byte limit.
/// </summary>
public sealed class UploadLimitAttributeTests
{
    /// <summary>Multipart upload actions use the configured attachment limit, not an unbounded request size limit.</summary>
    /// <param name="controller">The controller type declaring the upload action.</param>
    /// <param name="methodName">The name of the upload action method.</param>
    [Theory]
    [InlineData(typeof(SavingsPlansController), nameof(SavingsPlansController.UploadSymbolAsync))]
    [InlineData(typeof(SecuritiesController), nameof(SecuritiesController.UploadSymbolAsync))]
    [InlineData(typeof(SecuritiesController), nameof(SecuritiesController.ImportPricesAsync))]
    public void UploadActions_UseAttachmentUploadSizeLimit_WithoutRequestSizeLimit(Type controller, string methodName)
    {
        var method = controller.GetMethod(methodName)!;
        Assert.Single(method.GetCustomAttributes(typeof(AttachmentUploadSizeLimitAttribute), inherit: false));
        Assert.Empty(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false));
        Assert.Empty(method.GetCustomAttributes(typeof(RequestFormLimitsAttribute), inherit: false));
    }

    /// <summary>The backup upload action uses the configured backup limit filter instead of a fixed 1 GB limit.</summary>
    [Fact]
    public void BackupUploadAsync_UsesBackupUploadSizeLimit_WithoutRequestSizeLimit()
    {
        var method = typeof(BackupsController).GetMethod(nameof(BackupsController.UploadAsync))!;
        Assert.Single(method.GetCustomAttributes(typeof(BackupUploadSizeLimitAttribute), inherit: false));
        Assert.Empty(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false));
        Assert.Empty(method.GetCustomAttributes(typeof(RequestFormLimitsAttribute), inherit: false));
    }

    /// <summary>The mass-import action uses the configured mass-import limit filter instead of an unbounded limit.</summary>
    [Fact]
    public void ProcessMassImportAsync_UsesMassImportRequestSizeLimit_WithoutRequestSizeLimit()
    {
        var method = typeof(StatementDraftsController).GetMethod(nameof(StatementDraftsController.ProcessMassImportAsync))!;
        Assert.Single(method.GetCustomAttributes(typeof(MassImportRequestSizeLimitAttribute), inherit: false));
        Assert.Empty(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false));
    }

    /// <summary>The mass-import filter sets the request body size feature from the configured options.</summary>
    [Fact]
    public void MassImportRequestSizeLimit_SetsBodyLimitFromOptions()
    {
        const long configuredLimit = 12_345L;
        var services = new ServiceCollection()
            .Configure<MassImportOptions>(options => options.MaxRequestBytes = configuredLimit)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var feature = new TestMaxRequestBodySizeFeature();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        var attribute = new MassImportRequestSizeLimitAttribute();
        var filter = Assert.IsAssignableFrom<IResourceFilter>(attribute.CreateInstance(services));
        var context = new ResourceExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new List<Microsoft.AspNetCore.Mvc.ModelBinding.IValueProviderFactory>());

        filter.OnResourceExecuting(context);

        Assert.Equal(configuredLimit, feature.MaxRequestBodySize);
    }

    private sealed class TestMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}
