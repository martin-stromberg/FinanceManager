using Microsoft.Extensions.Hosting;

namespace SoftwareSchmiede.AutoUpdate;

/// <summary>
/// Default <see cref="IAutoUpdateEnvironment"/> implementation backed by <see cref="IHostEnvironment.ContentRootPath"/>.
/// </summary>
public sealed class HostAutoUpdateEnvironment : IAutoUpdateEnvironment
{
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostAutoUpdateEnvironment"/> class.
    /// </summary>
    /// <param name="hostEnvironment">The host environment to read the content root path from.</param>
    public HostAutoUpdateEnvironment(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public string ApplicationDirectory => _hostEnvironment.ContentRootPath;
}
