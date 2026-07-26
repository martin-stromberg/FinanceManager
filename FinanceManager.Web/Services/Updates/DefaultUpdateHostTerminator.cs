#pragma warning disable CS1591
namespace FinanceManager.Web.Services.Updates;

public sealed class DefaultUpdateHostTerminator : IUpdateHostTerminator
{
    private readonly IHostApplicationLifetime _lifetime;

    public DefaultUpdateHostTerminator(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public void StopApplication() => _lifetime.StopApplication();
}
#pragma warning restore CS1591
