using System.Reflection;
using System.Text;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Components.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Moq;

namespace FinanceManager.Tests.Components;

/// <summary>
/// Covers <see cref="SecurityPriceImportPanel"/>'s import workflow, which reads a broker-specific
/// CSV file and asks the API to parse and persist the price rows. The panel is exercised directly
/// via reflection (bypassing Blazor rendering and DI injection points) so these tests focus purely
/// on how the panel's internal state (result counters, error state) reacts to a successful import,
/// an import that returns per-line errors, and an import that throws.
/// </summary>
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

    /// <summary>
    /// Builds a <see cref="SecurityPriceImportPanel"/> for the given security with its normally
    /// injected <c>Api</c> and <c>Localizer</c> properties set directly via reflection, since the
    /// panel is constructed here without going through Blazor's dependency injection or component
    /// activation pipeline. Callers can then use <see cref="InvokePrivateAsync"/> and the private
    /// field helpers to drive the panel's import logic and inspect its resulting state.
    /// </summary>
    /// <param name="api">The (mocked) API client the panel should call during import.</param>
    /// <param name="securityId">The security the import is scoped to.</param>
    /// <returns>A panel instance ready for <see cref="InvokePrivateAsync"/> to invoke <c>ImportAsync</c> on.</returns>
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

    /// <summary>
    /// Locates a non-public instance method by name via reflection, invokes it, and awaits the
    /// resulting <see cref="Task"/>. Used to call <see cref="SecurityPriceImportPanel"/>'s private
    /// <c>ImportAsync</c> method directly since these tests exercise the panel's logic without
    /// going through Blazor's component lifecycle or UI event dispatch.
    /// </summary>
    /// <param name="instance">The object to invoke the method on.</param>
    /// <param name="methodName">The name of the non-public instance method to invoke.</param>
    private static async Task InvokePrivateAsync(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(instance, null));
        await task;
    }

    /// <summary>
    /// Reads a non-public instance field's value via reflection, used to inspect
    /// <see cref="SecurityPriceImportPanel"/>'s private result/error state after driving its import
    /// logic, without exposing that state through public members just for testing.
    /// </summary>
    /// <typeparam name="T">The expected type of the field's value.</typeparam>
    /// <param name="instance">The object to read the field from.</param>
    /// <param name="fieldName">The name of the non-public instance field.</param>
    /// <returns>The field's current value, cast to <typeparamref name="T"/>.</returns>
    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (T)field!.GetValue(instance)!;
    }

    /// <summary>
    /// Writes a non-public instance field's value via reflection, used to seed
    /// <see cref="SecurityPriceImportPanel"/>'s private selected-file and result state before
    /// invoking its import logic, simulating what the file picker and a prior import would set.
    /// </summary>
    /// <param name="instance">The object to write the field on.</param>
    /// <param name="fieldName">The name of the non-public instance field.</param>
    /// <param name="value">The value to assign to the field.</param>
    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    /// <summary>
    /// In-memory <see cref="IBrowserFile"/> stand-in that wraps a string's UTF-8 bytes as the file
    /// content, letting tests simulate a user-selected CSV upload without a real browser file input
    /// or file system access.
    /// </summary>
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

    /// <summary>
    /// Fake <see cref="IStringLocalizer{T}"/> that echoes each resource key back as its own value,
    /// so the panel's <c>Localizer</c> dependency can be satisfied without loading real localization
    /// resources.
    /// </summary>
    private sealed class PassthroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments), resourceNotFound: false);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
        public IStringLocalizer WithCulture(System.Globalization.CultureInfo culture) => this;
    }
}
