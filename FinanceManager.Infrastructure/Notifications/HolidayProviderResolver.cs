using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Infrastructure.Notifications;

/// <summary>
/// Resolves an implementation of <see cref="IHolidayProvider"/> for a requested <see cref="HolidayProviderKind"/>.
/// The resolver uses an <see cref="IServiceProvider"/> to obtain concrete provider instances.
/// </summary>
public sealed class HolidayProviderResolver : IHolidayProviderResolver
{
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Initializes a new instance of the <see cref="HolidayProviderResolver"/> class.
    /// </summary>
    /// <param name="sp">The service provider used to resolve holiday provider implementations. Cannot be <c>null</c>.</param>
    public HolidayProviderResolver(IServiceProvider sp)
    {
        _sp = sp;
    }

    /// <summary>
    /// Resolves a holiday provider implementation for the given <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The kind of holiday provider to resolve.</param>
    /// <returns>An implementation of <see cref="IHolidayProvider"/> corresponding to the requested kind.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the required provider type is not registered in the service provider.</exception>
    public IHolidayProvider Resolve(HolidayProviderKind kind)
    {
        return kind switch
        {
            HolidayProviderKind.NagerDate => _sp.GetRequiredService<NagerDateHolidayProvider>(),
            _ => _sp.GetRequiredService<InMemoryHolidayProvider>()
        };
    }
}
