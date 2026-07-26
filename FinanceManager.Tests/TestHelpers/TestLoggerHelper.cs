using Microsoft.Extensions.Logging;

namespace FinanceManager.Tests.TestHelpers;

internal static class TestLoggerHelper
{
    public static ILogger<T> CreateLogger<T>() where T : class
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        return loggerFactory.CreateLogger<T>();
    }
}
