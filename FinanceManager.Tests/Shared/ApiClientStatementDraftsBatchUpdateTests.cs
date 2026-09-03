using System.Net;
using System.Text;
using System.Text.Json;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.UnitTests.Http;

/// <summary>
/// Covers <see cref="ApiClient.StatementDrafts_BatchUpdateDetailedAsync"/>'s JSON payload shape - in particular
/// that booking/valuta dates are serialized as plain date-only strings ("yyyy-MM-dd") without a time component,
/// since the server-side model treats them as dates rather than timestamps.
/// </summary>
public sealed class ApiClientStatementDraftsBatchUpdateTests
{
    /// <summary>Verifies that create-entry dates are serialized without a time-of-day component, so the request body carries "2026-07-21" rather than a full ISO datetime that would misrepresent booking/valuta dates as timestamps.</summary>
    [Fact]
    public async Task StatementDrafts_BatchUpdateDetailedAsync_ShouldSerializeCreateDatesAsDateOnlyStrings()
    {
        string? capturedBody = null;
        var api = CreateApiClient(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });
        var createClientId = Guid.NewGuid();
        var deleteId = Guid.NewGuid();
        var request = new BatchUpdateRequestDto();
        request.Deletes.Add(deleteId);
        request.Creates.Add(new EntryCreateDto
        {
            ClientId = createClientId,
            BookingDate = new DateTime(2026, 7, 21, 14, 15, 16),
            ValutaDate = new DateTime(2026, 7, 22, 9, 8, 7),
            Amount = 19.95m,
            Subject = "Created",
            BookingDescription = "Description",
            RecipientName = "Recipient"
        });

        await api.StatementDrafts_BatchUpdateDetailedAsync(Guid.NewGuid(), request, TestContext.Current.CancellationToken);

        Assert.NotNull(capturedBody);
        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        var create = root.GetProperty("creates")[0];
        Assert.Equal(createClientId, create.GetProperty("clientId").GetGuid());
        Assert.Equal("2026-07-21", create.GetProperty("bookingDate").GetString());
        Assert.Equal("2026-07-22", create.GetProperty("valutaDate").GetString());
        Assert.DoesNotContain("T14:15:16", capturedBody);
        Assert.Equal(deleteId, root.GetProperty("deletes")[0].GetGuid());
    }

    private static ApiClient CreateApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http = new HttpClient(new DelegateHandler(responder)) { BaseAddress = new Uri("http://localhost") };
        return new ApiClient(http);
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
