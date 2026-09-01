using FinanceManager.Application;
using FinanceManager.Application.Attachments;
using FinanceManager.Domain.Attachments;
using FinanceManager.Web.Controllers;
using FinanceManager.Web.Infrastructure.Attachments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Net.Http;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="AttachmentsController"/> covering upload validation (size limits, MIME allow-list,
/// content-type sniffing, SVG sanitization against script injection), download content-type handling
/// (including the "nosniff" downgrade for risky types), category CRUD, and the paginated listing endpoint.
/// </summary>
public sealed class AttachmentsControllerTests
{
    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static LocalizedString L(string key, string value) => new(key, value, resourceNotFound: false);

    private sealed class TestMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; set; }
        public long? MaxRequestBodySize { get; set; }
    }

    private static (
        AttachmentsController controller,
        Mock<IAttachmentService> service,
        Mock<IAttachmentCategoryService> cats,
        TestCurrentUser current
    ) Create(AttachmentUploadOptions? options = null)
    {
        var svc = new Mock<IAttachmentService>(MockBehavior.Strict);
        var cats = new Mock<IAttachmentCategoryService>(MockBehavior.Strict);
        var current = new TestCurrentUser();
        var opts = Options.Create(options ?? new AttachmentUploadOptions
        {
            MaxSizeBytes = 10 * 1024, // 10 KB for tests
            AllowedMimeTypes = new[] { "application/pdf", "image/png", "text/plain" }
        });

        // Localizer mock with English fallbacks used in assertions
        var loc = new Mock<IStringLocalizer<AttachmentsController>>();
        loc.Setup(l => l[It.IsAny<string>()])
           .Returns((string key) => key switch
           {
               "Error_InvalidEntityKind" => L(key, "Invalid entityKind value."),
               "Error_FileOrUrlRequired" => L(key, "File or URL required."),
               "Error_EmptyFile" => L(key, "Empty file."),
               "Error_FileTooLarge" => L(key, "File too large. Max {0}."),
               "Error_UnsupportedContentType" => L(key, "Unsupported content type '{0}'."),
               "Error_UnexpectedError" => L(key, "Unexpected error"),
               _ => L(key, key)
           });

        var dp = DataProtectionProvider.Create("tests");
        var policy = new AttachmentContentPolicy(opts);
        var controller = new AttachmentsController(svc.Object, cats.Object, current, NullLogger<AttachmentsController>.Instance, opts, policy, loc.Object, dp)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return (controller, svc, cats, current);
    }

    /// <summary>
    /// Verifies that a zero-byte upload is rejected with a 400 and an "empty file" error before it reaches storage.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_EmptyFile()
    {
        var (controller, _, _, _) = Create();
        var stream = new MemoryStream(Array.Empty<byte>());
        var formFile = new FormFile(stream, 0, 0, "file", "a.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("empty file", err.message!.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that uploads exceeding the configured <c>MaxSizeBytes</c> are rejected with a descriptive
    /// "file too large" error before the file content is persisted.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_TooLarge()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 5, AllowedMimeTypes = new[] { "application/pdf" } };
        var (controller, _, _, _) = Create(opts);
        var data = new byte[6];
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "a.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("file too large", err.message!.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that a file whose content type is not in the configured MIME allow-list is rejected with 400,
    /// preventing arbitrary file types from being stored as attachments.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_UnsupportedContentType()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "application/pdf" } };
        var (controller, _, _, _) = Create(opts);
        var data = new byte[10];
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "a.bin") { Headers = new HeaderDictionary(), ContentType = "application/octet-stream" };

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var bad = Assert.IsAssignableFrom<ObjectResult>(resp);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("unsupported", err.message!.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that a PDF whose declared content type matches its byte-level signature is accepted and
    /// forwarded to <see cref="IAttachmentService"/>'s upload method.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldAccept_ValidPdf()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "application/pdf" } };
        var (controller, service, _, current) = Create(opts);
        var data = "%PDF-1.7 test"u8.ToArray();
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "doc.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        var dto = new AttachmentDto(
            Id: Guid.NewGuid(),
            EntityKind: (short)AttachmentEntityKind.Contact,
            EntityId: Guid.NewGuid(),
            FileName: "doc.pdf",
            ContentType: "application/pdf",
            SizeBytes: 10,
            CategoryId: null,
            UploadedUtc: DateTime.UtcNow,
            IsUrl: false);

        service.Setup(s => s.UploadAsync(current.UserId, AttachmentEntityKind.Contact, It.IsAny<Guid>(), It.IsAny<Stream>(), "doc.pdf", "application/pdf", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(dto);

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resp);
        Assert.IsType<AttachmentDto>(ok.Value);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that an SVG containing no scripts or event handlers passes the content-safety sanitization
    /// check and is accepted for upload.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldAccept_SafeSvg()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "image/svg+xml" } };
        var (controller, service, _, current) = Create(opts);
        var entityId = Guid.NewGuid();
        var data = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1 1"><path d="M0 0h1v1H0z"/></svg>"""u8.ToArray();
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "symbol.svg") { Headers = new HeaderDictionary(), ContentType = "image/svg+xml" };
        var dto = new AttachmentDto(
            Id: Guid.NewGuid(),
            EntityKind: (short)AttachmentEntityKind.Contact,
            EntityId: entityId,
            FileName: "symbol.svg",
            ContentType: "image/svg+xml",
            SizeBytes: data.Length,
            CategoryId: null,
            UploadedUtc: DateTime.UtcNow,
            IsUrl: false);

        service.Setup(s => s.UploadAsync(current.UserId, AttachmentEntityKind.Contact, entityId, It.IsAny<Stream>(), "symbol.svg", "image/svg+xml", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(dto);

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, entityId, formFile, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resp);
        Assert.IsType<AttachmentDto>(ok.Value);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that an SVG containing a <c>&lt;script&gt;</c> element and an inline <c>onload</c> handler is
    /// rejected as invalid content — SVG can carry executable script and is a known stored-XSS vector for
    /// uploaded attachments.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_UnsafeSvg()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "image/svg+xml" } };
        var (controller, _, _, _) = Create(opts);
        var data = """<svg xmlns="http://www.w3.org/2000/svg" onload="alert(1)"><script>alert(1)</script></svg>"""u8.ToArray();
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "symbol.svg") { Headers = new HeaderDictionary(), ContentType = "image/svg+xml" };

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Equal("Err_Invalid_ContentType", err.code);
    }

    /// <summary>
    /// Verifies that a file whose byte-level signature does not match its declared content type (client claims
    /// PDF, bytes are actually PNG) is rejected — guards against MIME-type spoofing via a manipulated
    /// Content-Type header.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_HeaderBytesMismatch()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "application/pdf", "image/png" } };
        var (controller, _, _, _) = Create(opts);
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2 };
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "doc.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var bad = Assert.IsAssignableFrom<ObjectResult>(resp);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Equal("Err_Invalid_ContentType", err.code);
    }

    /// <summary>
    /// Verifies that when the client sends an empty Content-Type header, the controller sniffs the real content
    /// type from the file's byte signature (here PNG) and normalizes it before forwarding to the service.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldNormalize_EmptyClientContentType_FromBytes()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "image/png" } };
        var (controller, service, _, current) = Create(opts);
        var entityId = Guid.NewGuid();
        var data = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2 };
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "image.bin") { Headers = new HeaderDictionary(), ContentType = "" };

        service.Setup(s => s.UploadAsync(current.UserId, AttachmentEntityKind.Contact, entityId, It.IsAny<Stream>(), "image.bin", "image/png", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AttachmentDto(Guid.NewGuid(), (short)AttachmentEntityKind.Contact, entityId, "image.bin", "image/png", data.Length, null, DateTime.UtcNow, false));

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, entityId, formFile, null, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that a file declared as <c>text/plain</c> but containing a NUL byte is rejected, since binary
    /// content masquerading as text should not be accepted as a text attachment.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_TextWithNulByte()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "text/plain" } };
        var (controller, _, _, _) = Create(opts);
        var data = new byte[] { (byte)'a', 0, (byte)'b' };
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "notes.txt") { Headers = new HeaderDictionary(), ContentType = "text/plain" };

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), formFile, null, null, CancellationToken.None);

        var bad = Assert.IsAssignableFrom<ObjectResult>(resp);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
    }

    /// <summary>
    /// Verifies that <see cref="AttachmentUploadSizeLimitAttribute"/> reads the configured
    /// <c>AttachmentUploadOptions.MaxSizeBytes</c> at request time (rather than a fixed compile-time constant) to
    /// set the request body size limit, and that no conflicting <c>RequestSizeLimit</c>/<c>RequestFormLimits</c>
    /// attributes are also present on the action, which would silently override it.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldUse_RuntimeConfiguredAttachmentSizeLimits()
    {
        var method = typeof(AttachmentsController).GetMethod(nameof(AttachmentsController.UploadAsync))!;
        var configuredLimit = AttachmentUploadOptions.DefaultMaxSizeBytes + 1024;
        var services = new ServiceCollection()
            .Configure<AttachmentUploadOptions>(options => options.MaxSizeBytes = configuredLimit)
            .Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 8)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var maxRequestBodySizeFeature = new TestMaxRequestBodySizeFeature();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(maxRequestBodySizeFeature);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[16]), "file", "a.pdf");
        var body = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = content.Headers.ContentType!.ToString();
        httpContext.Request.ContentLength = body.Length;
        httpContext.Request.Body = new MemoryStream(body);

        var filterAttribute = Assert.Single(method.GetCustomAttributes(typeof(AttachmentUploadSizeLimitAttribute), inherit: false).Cast<AttachmentUploadSizeLimitAttribute>());
        Assert.Empty(method.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false));
        Assert.Empty(method.GetCustomAttributes(typeof(RequestFormLimitsAttribute), inherit: false));

        var filter = Assert.IsAssignableFrom<IResourceFilter>(filterAttribute.CreateInstance(services));
        var context = new ResourceExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new List<IValueProviderFactory>());

        filter.OnResourceExecuting(context);
        var form = await httpContext.Request.ReadFormAsync(TestContext.Current.CancellationToken);

        Assert.True(maxRequestBodySizeFeature.MaxRequestBodySize > configuredLimit);
        Assert.Single(form.Files);
    }

    /// <summary>
    /// Verifies that a non-positive configured size limit (0 or negative, e.g. from a misconfigured setting) is
    /// normalized to the built-in default rather than disabling the upload size check entirely.
    /// </summary>
    /// <param name="configuredLimit">A non-positive value that should not be honored as-is.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UploadAsync_ShouldNormalize_InvalidConfiguredAttachmentSizeLimits(long configuredLimit)
    {
        var method = typeof(AttachmentsController).GetMethod(nameof(AttachmentsController.UploadAsync))!;
        var services = new ServiceCollection()
            .Configure<AttachmentUploadOptions>(options => options.MaxSizeBytes = configuredLimit)
            .Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 8)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var maxRequestBodySizeFeature = new TestMaxRequestBodySizeFeature();
        httpContext.Features.Set<IHttpMaxRequestBodySizeFeature>(maxRequestBodySizeFeature);

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent("%PDF-1.7 test"u8.ToArray()), "file", "a.pdf");
        var body = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = content.Headers.ContentType!.ToString();
        httpContext.Request.ContentLength = body.Length;
        httpContext.Request.Body = new MemoryStream(body);

        var filterAttribute = Assert.Single(method.GetCustomAttributes(typeof(AttachmentUploadSizeLimitAttribute), inherit: false).Cast<AttachmentUploadSizeLimitAttribute>());
        var filter = Assert.IsAssignableFrom<IResourceFilter>(filterAttribute.CreateInstance(services));
        var context = new ResourceExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new List<IValueProviderFactory>());

        filter.OnResourceExecuting(context);
        var form = await httpContext.Request.ReadFormAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentUploadOptions.DefaultMaxSizeBytes + 1024L * 1024L, maxRequestBodySizeFeature.MaxRequestBodySize);
        Assert.Single(form.Files);

        var opts = new AttachmentUploadOptions { MaxSizeBytes = configuredLimit, AllowedMimeTypes = new[] { "application/pdf" } };
        var (controller, service, _, current) = Create(opts);
        var entityId = Guid.NewGuid();
        var data = "%PDF-1.7 test"u8.ToArray();
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "doc.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        service.Setup(s => s.UploadAsync(current.UserId, AttachmentEntityKind.Contact, entityId, It.IsAny<Stream>(), "doc.pdf", "application/pdf", null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new AttachmentDto(Guid.NewGuid(), (short)AttachmentEntityKind.Contact, entityId, "doc.pdf", "application/pdf", data.Length, null, DateTime.UtcNow, false));

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, entityId, formFile, null, null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that supplying a URL instead of a file routes the request to
    /// <see cref="IAttachmentService.CreateUrlAsync"/>, producing an attachment with <c>IsUrl</c> set.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldCreateUrl_WhenUrlProvided()
    {
        var (controller, service, _, current) = Create();
        var entityId = Guid.NewGuid();
        var dto = new AttachmentDto(
            Id: Guid.NewGuid(),
            EntityKind: (short)AttachmentEntityKind.Contact,
            EntityId: entityId,
            FileName: "http://example",
            ContentType: "text/plain",
            SizeBytes: 0,
            CategoryId: null,
            UploadedUtc: DateTime.UtcNow,
            IsUrl: true);

        service.Setup(s => s.CreateUrlAsync(current.UserId, AttachmentEntityKind.Contact, entityId, "http://example", null, null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(dto);

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, entityId, null, null, "http://example", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resp);
        Assert.IsType<AttachmentDto>(ok.Value);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that omitting both file and URL is rejected with a 400 "file or url required" error, since the
    /// endpoint requires exactly one content source.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_WhenNeitherFileNorUrlProvided()
    {
        var (controller, _, _, _) = Create();

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, Guid.NewGuid(), null, null, null, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("file or url", err.message!.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that an out-of-range <c>entityKind</c> value is rejected with a 400 before any file/URL
    /// processing is attempted.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldReject_InvalidEntityKind()
    {
        var (controller, _, _, _) = Create();
        var resp = await controller.UploadAsync(short.MaxValue, Guid.NewGuid(), null, null, "http://example", CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("invalid entitykind", err.message!.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that an optional <c>categoryId</c> is forwarded unchanged to the attachment service when
    /// uploading a file.
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldPass_CategoryId_ToService_OnUpload()
    {
        var opts = new AttachmentUploadOptions { MaxSizeBytes = 1024, AllowedMimeTypes = new[] { "application/pdf" } };
        var (controller, service, _, current) = Create(opts);
        var entityId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var data = "%PDF-1.7 test"u8.ToArray();
        var formFile = new FormFile(new MemoryStream(data), 0, data.Length, "file", "doc.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

        service.Setup(s => s.UploadAsync(current.UserId, AttachmentEntityKind.Contact, entityId, It.IsAny<Stream>(), "doc.pdf", "application/pdf", categoryId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new AttachmentDto(Guid.NewGuid(), (short)AttachmentEntityKind.Contact, entityId, "doc.pdf", "application/pdf", 10, categoryId, DateTime.UtcNow, false));

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, entityId, formFile, categoryId, null, CancellationToken.None);
        Assert.IsType<OkObjectResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that an optional <c>categoryId</c> is forwarded unchanged to the attachment service when
    /// creating a URL attachment (i.e. the URL path shares the same category handling as the file-upload path).
    /// </summary>
    [Fact]
    public async Task UploadAsync_ShouldPass_CategoryId_ToService_OnCreateUrl()
    {
        var (controller, service, _, current) = Create();
        var entityId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        service.Setup(s => s.CreateUrlAsync(current.UserId, AttachmentEntityKind.Contact, entityId, "http://example", null, categoryId, It.IsAny<CancellationToken>())).ReturnsAsync(new AttachmentDto(Guid.NewGuid(), (short)AttachmentEntityKind.Contact, entityId, "http://example", "text/plain", 0, categoryId, DateTime.UtcNow, true));

        var resp = await controller.UploadAsync((short)AttachmentEntityKind.Contact, entityId, null, categoryId, "http://example", CancellationToken.None);
        Assert.IsType<OkObjectResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that downloading a non-existent attachment returns 404 rather than throwing.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ShouldReturn_NotFound_WhenMissing()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        service.Setup(s => s.DownloadAsync(current.UserId, id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(((Stream, string, string)?)null);

        var resp = await controller.DownloadAsync(id, null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that a successful download streams the file back with the correct file name and content type,
    /// and sets the <c>X-Content-Type-Options: nosniff</c> response header to stop browsers from
    /// MIME-sniffing the body into something more dangerous than the declared type.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ShouldReturn_FileContentResult()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });
        service.Setup(s => s.DownloadAsync(current.UserId, id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((content, "file.bin", "application/octet-stream"));

        var resp = await controller.DownloadAsync(id, null, CancellationToken.None);
        var file = Assert.IsType<FileStreamResult>(resp);
        Assert.Equal("file.bin", file.FileDownloadName);
        Assert.Equal("application/octet-stream", file.ContentType);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that a stored attachment with a potentially dangerous content type (e.g. <c>text/html</c>) is
    /// served back as <c>application/octet-stream</c> instead, preventing the browser from rendering or
    /// executing it inline when the attachment is opened directly from a download link.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ShouldFallback_RiskyContentType_ToOctetStream()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        var content = new MemoryStream(new byte[] { 1, 2, 3 });
        service.Setup(s => s.DownloadAsync(current.UserId, id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((content, "file.html", "text/html"));

        var resp = await controller.DownloadAsync(id, null, CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(resp);
        Assert.Equal("file.html", file.FileDownloadName);
        Assert.Equal("application/octet-stream", file.ContentType);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that SVG attachments — already sanitized against scripts at upload time — keep their
    /// <c>image/svg+xml</c> content type on download rather than being forced to octet-stream like other
    /// risky types.
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ShouldReturnSvgContentType_ForStoredSvg()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        var content = new MemoryStream("""<svg xmlns="http://www.w3.org/2000/svg"></svg>"""u8.ToArray());
        service.Setup(s => s.DownloadAsync(current.UserId, id, It.IsAny<CancellationToken>()))
               .ReturnsAsync((content, "symbol.svg", "image/svg+xml"));

        var resp = await controller.DownloadAsync(id, null, CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(resp);
        Assert.Equal("symbol.svg", file.FileDownloadName);
        Assert.Equal("image/svg+xml", file.ContentType);
        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that a successful delete returns 204 No Content.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldReturn_NoContent_WhenDeleted()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        service.Setup(s => s.DeleteAsync(current.UserId, id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var resp = await controller.DeleteAsync(id, CancellationToken.None);
        Assert.IsType<NoContentResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that deleting a non-existent attachment returns 404 rather than a generic error.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ShouldReturn_NotFound_WhenMissing()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        service.Setup(s => s.DeleteAsync(current.UserId, id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var resp = await controller.DeleteAsync(id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that a successful core-metadata update (e.g. renaming a file) returns 204 No Content.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldReturn_NoContent_WhenUpdated()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        service.Setup(s => s.UpdateCoreAsync(current.UserId, id, "name.pdf", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var resp = await controller.UpdateAsync(id, new AttachmentUpdateCoreRequest("name.pdf", null), CancellationToken.None);
        Assert.IsType<NoContentResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that updating a non-existent attachment's core metadata returns 404.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldReturn_NotFound_WhenMissing()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        service.Setup(s => s.UpdateCoreAsync(current.UserId, id, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var resp = await controller.UpdateAsync(id, new AttachmentUpdateCoreRequest(null, null), CancellationToken.None);
        Assert.IsType<NotFoundResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that successfully re-assigning an attachment's category returns 204 No Content.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_ShouldReturn_NoContent_WhenUpdated()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        var cat = Guid.NewGuid();
        service.Setup(s => s.UpdateCategoryAsync(current.UserId, id, cat, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var resp = await controller.UpdateCategoryAsync(id, new AttachmentUpdateCategoryRequest(cat), CancellationToken.None);
        Assert.IsType<NoContentResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that re-assigning a category on a non-existent attachment returns 404.
    /// </summary>
    [Fact]
    public async Task UpdateCategoryAsync_ShouldReturn_NotFound_WhenMissing()
    {
        var (controller, service, _, current) = Create();
        var id = Guid.NewGuid();
        service.Setup(s => s.UpdateCategoryAsync(current.UserId, id, null, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var resp = await controller.UpdateCategoryAsync(id, new AttachmentUpdateCategoryRequest(null), CancellationToken.None);
        Assert.IsType<NotFoundResult>(resp);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that listing attachments with an out-of-range <c>entityKind</c> is rejected with 400 before the
    /// service is queried.
    /// </summary>
    [Fact]
    public async Task ListAsync_ShouldReject_InvalidEntityKind()
    {
        var (controller, _, _, _) = Create();
        var resp = await controller.ListAsync(short.MaxValue, Guid.NewGuid(), 0, 50, null, null, null, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("invalid entitykind", err.message!.ToLowerInvariant());
    }

    /// <summary>
    /// Verifies that the paginated list endpoint wraps the service's results in a <see cref="PageResult{T}"/>
    /// envelope with the correct <c>Items</c>, <c>Total</c>, and <c>HasMore</c> values.
    /// </summary>
    [Fact]
    public async Task ListAsync_ShouldReturn_EnvelopeWithItems()
    {
        var (controller, service, _, current) = Create();
        var entityId = Guid.NewGuid();
        var list = new[] { new AttachmentDto(Guid.NewGuid(), (short)AttachmentEntityKind.Contact, entityId, "a.pdf", "application/pdf", 1, null, DateTime.UtcNow, false) } as IReadOnlyList<AttachmentDto>;
        service.Setup(s => s.ListAsync(current.UserId, AttachmentEntityKind.Contact, entityId, 0, 50, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(list);
        service.Setup(s => s.CountAsync(current.UserId, AttachmentEntityKind.Contact, entityId, null, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var resp = await controller.ListAsync((short)AttachmentEntityKind.Contact, entityId, 0, 50, null, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(resp);
        var page = Assert.IsType<PageResult<AttachmentDto>>(ok.Value);
        Assert.Equal(list, page.Items);
        Assert.False(page.HasMore);
        Assert.Equal(1, page.Total);
        service.VerifyAll();
    }

    /// <summary>
    /// Verifies that the category list endpoint passes through the categories returned by
    /// <see cref="IAttachmentCategoryService"/> unchanged.
    /// </summary>
    [Fact]
    public async Task ListCategoriesAsync_ShouldReturn_ListFromService()
    {
        var (controller, _, cats, current) = Create();
        var list = new[] { new AttachmentCategoryDto(Guid.NewGuid(), "Docs", false, false) } as IReadOnlyList<AttachmentCategoryDto>;
        cats.Setup(s => s.ListAsync(current.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var resp = await controller.ListCategoriesAsync(CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(resp);
        Assert.Equal(list, ok.Value);
        cats.VerifyAll();
    }

    /// <summary>
    /// Verifies that creating a category returns a 201 Created result carrying the persisted category DTO.
    /// </summary>
    [Fact]
    public async Task CreateCategoryAsync_ShouldReturn_CreatedDto()
    {
        var (controller, _, cats, current) = Create();
        var catId = Guid.NewGuid();
        var dto = new AttachmentCategoryDto(catId, "Docs", false, true);
        cats.Setup(s => s.CreateAsync(current.UserId, "Docs", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var resp = await controller.CreateCategoryAsync(new AttachmentCreateCategoryRequest("Docs"), CancellationToken.None);
        var created = Assert.IsType<CreatedResult>(resp);
        Assert.Equal("Docs", ((AttachmentCategoryDto)created.Value!).Name);
        cats.VerifyAll();
    }

    /// <summary>
    /// Verifies that an invalid category name — rejected by the service via <see cref="ArgumentException"/> —
    /// is translated into a 400 response with a descriptive error message rather than an unhandled exception.
    /// </summary>
    [Fact]
    public async Task CreateCategoryAsync_ShouldReturn_BadRequest_WhenInvalid()
    {
        var (controller, _, cats, current) = Create();
        var req = new AttachmentCreateCategoryRequest("");

        cats.Setup(s => s.CreateAsync(current.UserId, "", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("name"));

        var resp = await controller.CreateCategoryAsync(req, CancellationToken.None);
        var bad = Assert.IsType<BadRequestObjectResult>(resp);
        var err = Assert.IsType<ApiErrorDto>(bad.Value);
        Assert.Contains("name", err.message!.ToLowerInvariant());
    }
}
