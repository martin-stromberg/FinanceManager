using System.Text.Json;
using Microsoft.Playwright;

namespace FinanceManager.Tests.E2E;

public static class BrowserApiHelper
{
    public sealed record BrowserApiResponse<T>(int Status, T? Value, string? Raw);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<TResponse> PostJsonAsync<TRequest, TResponse>(IPage page, string path, TRequest payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var json = await page.EvaluateAsync<string>("""
            async ({ path, payloadJson }) => {
                const response = await fetch(path, {
                    method: 'POST',
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' },
                    body: payloadJson
                });

                if (!response.ok) {
                    throw new Error(await response.text());
                }

                return JSON.stringify(await response.json());
            }
            """, new { path, payloadJson });

        var value = JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
        return value ?? throw new InvalidOperationException($"Unable to deserialize response for {typeof(TResponse).Name}.");
    }

    public static async Task PostJsonAsync<TRequest>(IPage page, string path, TRequest payload)
    {
        var result = await SendJsonAsync<TRequest>(page, "POST", path, payload);
        if (result.Status < 200 || result.Status >= 300)
        {
            throw new InvalidOperationException(result.Raw ?? $"Request to {path} failed with status {result.Status}.");
        }
    }

    public static async Task<TResponse> GetJsonAsync<TResponse>(IPage page, string path)
    {
        var result = await SendWithoutBodyAsync<TResponse>(page, "GET", path);
        if (result.Status < 200 || result.Status >= 300 || result.Value is null)
        {
            throw new InvalidOperationException(result.Raw ?? $"GET {path} failed with status {result.Status}.");
        }

        return result.Value;
    }

    public static async Task<TResponse> PutJsonAsync<TRequest, TResponse>(IPage page, string path, TRequest payload)
    {
        var result = await SendJsonAsync<TRequest, TResponse>(page, "PUT", path, payload);
        if (result.Status < 200 || result.Status >= 300 || result.Value is null)
        {
            throw new InvalidOperationException(result.Raw ?? $"PUT {path} failed with status {result.Status}.");
        }

        return result.Value;
    }

    public static Task<BrowserApiResponse<TResponse>> PostJsonWithStatusAsync<TRequest, TResponse>(IPage page, string path, TRequest payload)
        => SendJsonAsync<TRequest, TResponse>(page, "POST", path, payload);

    public static Task<BrowserApiResponse<TResponse>> PostWithStatusAsync<TResponse>(IPage page, string path)
        => SendWithoutBodyAsync<TResponse>(page, "POST", path);

    public static Task<BrowserApiResponse<TResponse>> GetWithStatusAsync<TResponse>(IPage page, string path)
        => SendWithoutBodyAsync<TResponse>(page, "GET", path);

    public static async Task<int> DeleteAsync(IPage page, string path)
    {
        var raw = await SendRawAsync(page, "DELETE", path, null);
        return raw.Status;
    }

    public static async Task PostNoContentAsync(IPage page, string path)
    {
        var status = await page.EvaluateAsync<int>("""
            async (path) => {
                const response = await fetch(path, { method: 'POST', credentials: 'include' });
                return response.status;
            }
            """, path);

        if (status < 200 || status >= 300)
        {
            throw new InvalidOperationException($"Request to {path} failed with status {status}.");
        }
    }

    public static async Task<TResponse> PostMultipartAsync<TResponse>(
        IPage page,
        string path,
        string fileName,
        string contentType,
        byte[] content,
        IReadOnlyDictionary<string, string>? additionalFormFields = null)
    {
        var json = await page.EvaluateAsync<string>("""
            async ({ path, fileName, contentType, contentBase64, additionalFormFields }) => {
                const binary = atob(contentBase64);
                const bytes = new Uint8Array(binary.length);
                for (let i = 0; i < binary.length; i++) {
                    bytes[i] = binary.charCodeAt(i);
                }

                const form = new FormData();
                form.append('file', new File([bytes], fileName, { type: contentType }));
                if (additionalFormFields) {
                    for (const [key, value] of Object.entries(additionalFormFields)) {
                        form.append(key, value);
                    }
                }

                const response = await fetch(path, {
                    method: 'POST',
                    credentials: 'include',
                    body: form
                });

                if (!response.ok) {
                    throw new Error(await response.text());
                }

                return JSON.stringify(await response.json());
            }
            """, new
            {
                path,
                fileName,
                contentType,
                contentBase64 = Convert.ToBase64String(content),
                additionalFormFields
            });

        var value = JsonSerializer.Deserialize<TResponse>(json, JsonOptions);
        return value ?? throw new InvalidOperationException($"Unable to deserialize response for {typeof(TResponse).Name}.");
    }

    private static async Task<BrowserApiResponse<TResponse>> SendJsonAsync<TRequest, TResponse>(IPage page, string method, string path, TRequest payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var raw = await SendRawAsync(page, method, path, payloadJson);
        var value = DeserializeBody<TResponse>(raw.Text);
        return new BrowserApiResponse<TResponse>(raw.Status, value, raw.Text);
    }

    private static async Task<BrowserApiResponse<object?>> SendJsonAsync<TRequest>(IPage page, string method, string path, TRequest payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var raw = await SendRawAsync(page, method, path, payloadJson);
        return new BrowserApiResponse<object?>(raw.Status, null, raw.Text);
    }

    private static async Task<BrowserApiResponse<TResponse>> SendWithoutBodyAsync<TResponse>(IPage page, string method, string path)
    {
        var raw = await SendRawAsync(page, method, path, null);
        var value = DeserializeBody<TResponse>(raw.Text);
        return new BrowserApiResponse<TResponse>(raw.Status, value, raw.Text);
    }

    private static async Task<BrowserRawResponse> SendRawAsync(IPage page, string method, string path, string? payloadJson)
    {
        var json = await page.EvaluateAsync<string>("""
            async ({ method, path, payloadJson }) => {
                const options = {
                    method,
                    credentials: 'include',
                    headers: { 'Content-Type': 'application/json' }
                };

                if (payloadJson !== null) {
                    options.body = payloadJson;
                }

                const response = await fetch(path, options);
                const text = await response.text();
                return JSON.stringify({ status: response.status, text });
            }
            """, new { method, path, payloadJson });

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new BrowserRawResponse(
            root.GetProperty("status").GetInt32(),
            root.GetProperty("text").GetString() ?? string.Empty);
    }

    private static TResponse? DeserializeBody<TResponse>(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TResponse>(raw, JsonOptions);
    }

    private sealed record BrowserRawResponse(int Status, string Text);
}
