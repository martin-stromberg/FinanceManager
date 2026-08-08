using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Tests.Infrastructure;

public sealed class RequestLoggingMiddlewareTests
{
    [Theory]
    [InlineData("token")]
    [InlineData("Token")]
    [InlineData("TOKEN")]
    public async Task InvokeAsync_Success_RedactsTokenQueryParameter(string parameterName)
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            logger);
        var context = CreateContext($"/api/attachments/id/download?{parameterName}=super-secret&page=1");

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.DoesNotContain("super-secret", entry.FormattedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", entry.StateText, StringComparison.Ordinal);
        Assert.Contains($"{parameterName}=%5BREDACTED%5D", entry.FormattedMessage, StringComparison.Ordinal);
        Assert.Contains("page=1", entry.FormattedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_Exception_RedactsTokenQueryParameter()
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(
            _ => throw new InvalidOperationException("boom"),
            logger);
        var context = CreateContext("/api/attachments/id/download?token=exception-secret&foo=bar");

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("exception-secret", entry.FormattedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("exception-secret", entry.StateText, StringComparison.Ordinal);
        Assert.Contains("token=%5BREDACTED%5D", entry.FormattedMessage, StringComparison.Ordinal);
        Assert.Contains("foo=bar", entry.FormattedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_PreservesNonSensitiveQueryParameters()
    {
        var logger = new CapturingLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            },
            logger);
        var context = CreateContext("/api/search?foo=bar&page=1");

        await middleware.InvokeAsync(context);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("foo=bar", entry.FormattedMessage, StringComparison.Ordinal);
        Assert.Contains("page=1", entry.FormattedMessage, StringComparison.Ordinal);
    }

    private static DefaultHttpContext CreateContext(string pathAndQuery)
    {
        var split = pathAndQuery.Split('?', 2);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = split[0];
        context.Request.QueryString = split.Length == 2 ? new QueryString("?" + split[1]) : QueryString.Empty;
        context.TraceIdentifier = "trace-test";
        return context;
    }
}
