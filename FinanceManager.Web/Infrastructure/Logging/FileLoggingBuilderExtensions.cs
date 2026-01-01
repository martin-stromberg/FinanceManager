namespace FinanceManager.Web.Infrastructure.Logging;

/// <summary>
/// Extension methods for registering the file logger provider into an <see cref="ILoggingBuilder"/>.
/// </summary>
public static class FileLoggingBuilderExtensions
{
    /// <summary>
    /// Adds the file logger provider to the logging builder using the configured <see cref="FileLoggerOptions"/>.
    /// </summary>
    /// <param name="builder">The logging builder to extend.</param>
    /// <returns>The original <see cref="ILoggingBuilder"/> instance to allow fluent configuration.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder)
    {
        builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        return builder;
    }
}