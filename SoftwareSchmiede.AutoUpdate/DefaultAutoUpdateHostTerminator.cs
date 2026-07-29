using Microsoft.Extensions.Hosting;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateHostTerminator"/> implementation, delegating to
/// <see cref="IHostApplicationLifetime.StopApplication"/>.
/// </summary>
public sealed class DefaultAutoUpdateHostTerminator : IAutoUpdateHostTerminator
{
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAutoUpdateHostTerminator"/> class.
    /// </summary>
    /// <param name="lifetime">The host application lifetime used to stop the application.</param>
    public DefaultAutoUpdateHostTerminator(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    /// <inheritdoc />
    public void StopApplication() => _lifetime.StopApplication();
}
