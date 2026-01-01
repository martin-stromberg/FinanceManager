using System.Net.Http.Json;
using System.Text.Json;


namespace FinanceManager.Shared;

/// <summary>
/// HTTP API client used by the UI to call server endpoints. Wraps an <see cref="HttpClient"/> and
/// provides convenience methods for the application's REST API surface.
/// </summary>
public partial class ApiClient : IApiClient
{
    private readonly HttpClient _http;

    /// <summary>
    /// Last error message extracted from the most recent failed HTTP response (if any).
    /// This is intended for UI display and diagnostics.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Optional machine-readable error code extracted from the most recent failed HTTP response.
    /// </summary>
    public string? LastErrorCode { get; private set; }

    /// <summary>
    /// Creates a new instance of <see cref="ApiClient"/> using the provided <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="http">The configured HTTP client used to perform requests.</param>
    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Ensures the HTTP response indicates success. If not, attempts to extract structured error
    /// information (including RFC ProblemDetails style validation errors) and populate
    /// <see cref="LastError"/> and <see cref="LastErrorCode"/> before rethrowing via
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>.
    /// </summary>
    /// <param name="resp">The HTTP response message to inspect.</param>
    private async Task EnsureSuccessOrSetErrorAsync(HttpResponseMessage resp)
    {
        LastError = null; LastErrorCode = null;
        if (resp.IsSuccessStatusCode) return;

        try
        {
            var content = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                            LastError = m.GetString();
                        if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                            LastErrorCode = e.GetString();

                        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                        {
                            SetRFCStyleError(errors);
                        }
                    }
                }
                catch
                {
                    // not JSON, use raw content
                    LastError = content;
                }
            }
        }
        catch
        {
            // ignore
        }

        if (string.IsNullOrWhiteSpace(LastError)) LastError = resp.ReasonPhrase ?? $"HTTP {(int)resp.StatusCode}";
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Extracts RFC ProblemDetails-style validation error messages from the provided "errors"
    /// JSON object and aggregates them into <see cref="LastError"/>.
    /// This method is best-effort and must not throw.
    /// </summary>
    /// <param name="errors">JSON element representing the errors object.</param>
    private void SetRFCStyleError(JsonElement errors)
    {
        var messages = new List<string>();
        foreach (var prop in errors.EnumerateObject())
        {
            try
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            messages.Add($"{prop.Name}: {item.GetString()}");
                        }
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    messages.Add($"{prop.Name}: {prop.Value.GetString()}");
                }
            }
            catch { /* best-effort */ }
        }

        if (messages.Count > 0)
        {
            LastError = string.Join("; ", messages);
        }
    }

    /// <summary>
    /// Checks whether any user accounts exist in the system.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the HTTP request.</param>
    /// <returns><c>true</c> when at least one user exists; otherwise <c>false</c>.</returns>
    public async Task<bool> Users_HasAnyAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/users/exists", ct);
        await EnsureSuccessOrSetErrorAsync(resp);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        if (json.ValueKind == JsonValueKind.True || json.ValueKind == JsonValueKind.False)
        {
            return json.GetBoolean();
        }

        if (json.ValueKind == JsonValueKind.Object)
        {
            if (json.TryGetProperty("any", out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
                {
                    return prop.GetBoolean();
                }
            }
        }

        return false;
    }
}
