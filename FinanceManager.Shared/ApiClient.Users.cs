using System.Net.Http.Json;

namespace FinanceManager.Shared;

public partial class ApiClient
{
    /// <summary>
    /// Requests creation of demo data for the specified user. Intended for development/demo only.
    /// </summary>
    /// <param name="userId">User identifier to create demo data for.</param>
    /// <param name="createPostings">When true, create statement imports and entries (postings) as part of the demo data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the request; completes when server accepted the request.</returns>
    public async Task Users_CreateDemoDataAsync(Guid userId, bool createPostings, CancellationToken ct = default)
    {
        var request = new DemoRequest(createPostings);
        var resp = await _http.PostAsync($"/api/users/demo/{userId}", JsonContent.Create(request), ct);
        await EnsureSuccessOrSetErrorAsync(resp);
    }
}
