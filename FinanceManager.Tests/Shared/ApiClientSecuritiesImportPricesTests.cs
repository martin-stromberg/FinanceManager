using System.Net;
using System.Text;
using System.Text.Json;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Securities;

namespace FinanceManager.UnitTests.Http;

public sealed class ApiClientSecuritiesImportPricesTests
{
    /// <summary>
    /// Verifies that the API client sends a multipart import request with file and provider form fields.
    /// </summary>
    [Fact]
    public async Task Securities_ImportPricesAsync_ShouldSendMultipartWithFileAndProvider_WhenCalled()
    {
        HttpRequestMessage? capturedRequest = null;
        var api = CreateApiClient(request =>
        {
            capturedRequest = request;
            var ok = new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(ok), Encoding.UTF8, "application/json")
            };
        });

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sep=;\nZeit;Test Security\n01.07.2026 02:00:00;42,61\n"));
        await api.Securities_ImportPricesAsync(Guid.NewGuid(), stream, "ing-prices.csv", provider: "ing", contentType: "text/csv");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Contains("/api/securities/", capturedRequest.RequestUri!.AbsolutePath);
        Assert.EndsWith("/prices/import", capturedRequest.RequestUri!.AbsolutePath);

        var multipart = Assert.IsType<MultipartFormDataContent>(capturedRequest.Content);
        var parts = multipart.ToList();
        Assert.Equal(2, parts.Count);

        var filePart = parts.Single(x => string.Equals(x.Headers.ContentDisposition?.Name?.Trim('"'), "file", StringComparison.Ordinal));
        Assert.Equal("ing-prices.csv", filePart.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal("text/csv", filePart.Headers.ContentType?.MediaType);

        var providerPart = parts.Single(x => string.Equals(x.Headers.ContentDisposition?.Name?.Trim('"'), "provider", StringComparison.Ordinal));
        Assert.Equal("ing", await providerPart.ReadAsStringAsync());
    }

    /// <summary>
    /// Verifies that a successful import response is deserialized into the expected result DTO.
    /// </summary>
    [Fact]
    public async Task Securities_ImportPricesAsync_ShouldDeserializeResultDto_WhenApiReturns200()
    {
        var expected = new SecurityPriceImportResultDto(
            Inserted: 3,
            Updated: 2,
            Unchanged: 1,
            Skipped: 4,
            Errors: new[] { new SecurityPriceImportErrorDto(7, "Invalid close value format.") });

        var api = CreateApiClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(expected), Encoding.UTF8, "application/json")
        });

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("dummy"));
        var result = await api.Securities_ImportPricesAsync(Guid.NewGuid(), stream, "prices.csv");

        Assert.Equal(expected.Inserted, result.Inserted);
        Assert.Equal(expected.Updated, result.Updated);
        Assert.Equal(expected.Unchanged, result.Unchanged);
        Assert.Equal(expected.Skipped, result.Skipped);
        Assert.Single(result.Errors);
        Assert.Equal(7, result.Errors[0].LineNumber);
        Assert.Equal("Invalid close value format.", result.Errors[0].Message);
    }

    /// <summary>
    /// Verifies that API errors are surfaced via exception and LastError metadata.
    /// </summary>
    [Fact]
    public async Task Securities_ImportPricesAsync_ShouldSetLastError_WhenApiReturns400()
    {
        var error = ApiErrorDto.Create("API_Securities", "Err_Invalid_Import", "No valid price rows were found.");
        var api = CreateApiClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(JsonSerializer.Serialize(error), Encoding.UTF8, "application/json")
        });

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("dummy"));
        var act = async () => await api.Securities_ImportPricesAsync(Guid.NewGuid(), stream, "prices.csv");

        await Assert.ThrowsAsync<HttpRequestException>(act);
        Assert.Equal("Err_Invalid_Import", api.LastErrorCode);
        Assert.Equal("No valid price rows were found.", api.LastError);
    }

    private static FinanceManager.Shared.ApiClient CreateApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http = new HttpClient(new DelegateHandler(responder)) { BaseAddress = new Uri("http://localhost") };
        return new FinanceManager.Shared.ApiClient(http);
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
