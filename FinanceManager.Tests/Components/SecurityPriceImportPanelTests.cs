using System.Reflection;
using System.Text;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Components.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

public sealed class SecurityPriceImportPanelTests
{
    /// <summary>
    /// Verifies that import execution calls the API and stores result counters on successful import.
    /// </summary>
    [Fact]
    public async Task Panel_ShouldCallApiAndStoreResult_WhenImportSucceeds()
    {
        var apiMock = new Mock<IApiClient>();
        var securityId = Guid.NewGuid();
        var file = new TestBrowserFile("prices.csv", "text/csv", "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\n");
        var expected = new SecurityPriceImportResultDto(1, 2, 3, 4, Array.Empty<SecurityPriceImportErrorDto>());

        apiMock.Setup(x => x.Securities_ImportPricesAsync(
                securityId,
                It.IsAny<Stream>(),
                file.Name,
                "ing",
                file.ContentType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var panel = CreatePanel(apiMock.Object, securityId);
        SetPrivateField(panel, "_selectedFile", file);
        SetPrivateField(panel, "_fileName", file.Name);

        await InvokePrivateAsync(panel, "ImportAsync");

        var result = GetPrivateField<SecurityPriceImportResultDto?>(panel, "_result");
        var error = GetPrivateField<string?>(panel, "_error");

        Assert.NotNull(result);
        Assert.Null(error);
        Assert.Equal(1, result!.Inserted);
        Assert.Equal(2, result.Updated);
        Assert.Equal(3, result.Unchanged);
        Assert.Equal(4, result.Skipped);
    }

    /// <summary>
    /// Verifies that import execution keeps line errors returned by API result.
    /// </summary>
    [Fact]
    public async Task Panel_ShouldStoreLineErrors_WhenResultContainsErrors()
    {
        var apiMock = new Mock<IApiClient>();
        var securityId = Guid.NewGuid();
        var file = new TestBrowserFile("prices.csv", "text/csv", "sep=;\nZeit;Test Security\nnot-a-date;invalid\n");
        var expected = new SecurityPriceImportResultDto(
            0,
            0,
            0,
            1,
            new[] { new SecurityPriceImportErrorDto(3, "Invalid date format.") });

        apiMock.Setup(x => x.Securities_ImportPricesAsync(
                securityId,
                It.IsAny<Stream>(),
                file.Name,
                "ing",
                file.ContentType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var panel = CreatePanel(apiMock.Object, securityId);
        SetPrivateField(panel, "_selectedFile", file);
        SetPrivateField(panel, "_fileName", file.Name);

        await InvokePrivateAsync(panel, "ImportAsync");

        var result = GetPrivateField<SecurityPriceImportResultDto?>(panel, "_result");
        Assert.NotNull(result);
        Assert.Single(result!.Errors);
        Assert.Equal(3, result.Errors[0].LineNumber);
    }

    /// <summary>
    /// Verifies that import exceptions are mapped to the panel error state and clear result state.
    /// </summary>
    [Fact]
    public async Task Panel_ShouldShowErrorAndClearResult_WhenApiThrows()
    {
        var apiMock = new Mock<IApiClient>();
        var securityId = Guid.NewGuid();
        var file = new TestBrowserFile("prices.csv", "text/csv", "sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\n");
        apiMock.SetupGet(x => x.LastError).Returns("Import failed");
        apiMock.Setup(x => x.Securities_ImportPricesAsync(
                securityId,
                It.IsAny<Stream>(),
                file.Name,
                "ing",
                file.ContentType,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Bad request"));

        var panel = CreatePanel(apiMock.Object, securityId);
        SetPrivateField(panel, "_selectedFile", file);
        SetPrivateField(panel, "_fileName", file.Name);
        SetPrivateField(panel, "_result", new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        await InvokePrivateAsync(panel, "ImportAsync");

        var result = GetPrivateField<SecurityPriceImportResultDto?>(panel, "_result");
        var error = GetPrivateField<string?>(panel, "_error");
        Assert.Null(result);
        Assert.Equal("Import failed", error);
    }

    private static SecurityPriceImportPanel CreatePanel(IApiClient api, Guid securityId)
    {
        var panel = new SecurityPriceImportPanel
        {
            SecurityId = securityId
        };

        var apiProperty = typeof(SecurityPriceImportPanel).GetProperty("Api", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var localizerProperty = typeof(SecurityPriceImportPanel).GetProperty("Localizer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(apiProperty);
        Assert.NotNull(localizerProperty);
        apiProperty!.SetValue(panel, api);
        localizerProperty!.SetValue(panel, new PassthroughLocalizer<FinanceManager.Web.Pages>());
        return panel;
    }

    private static async Task InvokePrivateAsync(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsType<Task>(method!.Invoke(instance, null));
        await task;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private sealed class TestBrowserFile : IBrowserFile
    {
        private readonly byte[] _content;

        public TestBrowserFile(string name, string contentType, string content)
        {
            Name = name;
            ContentType = contentType;
            _content = Encoding.UTF8.GetBytes(content);
            LastModified = DateTimeOffset.UtcNow;
        }

        public string Name { get; }
        public DateTimeOffset LastModified { get; }
        public long Size => _content.Length;
        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(_content);
    }

    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
