using Microsoft.Extensions.DependencyInjection;

namespace msTools.Web.Blazor;

/// <summary>
/// Registers reusable Blazor UI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the reusable loading bar service and options.
    /// </summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="configure">Optional host-specific loading bar configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddLoadingBar(
        this IServiceCollection services,
        Action<LoadingBarOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<LoadingBarOptions>();
        }

        services.AddScoped<LoadingBarService>();
        return services;
    }
}
