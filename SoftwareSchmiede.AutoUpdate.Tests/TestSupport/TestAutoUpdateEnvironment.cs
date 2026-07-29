using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests.TestSupport;

/// <summary>
/// <see cref="IAutoUpdateEnvironment"/> test double pointing at a fixed, typically temporary, directory.
/// </summary>
public sealed class TestAutoUpdateEnvironment : IAutoUpdateEnvironment
{
    public TestAutoUpdateEnvironment(string applicationDirectory)
    {
        ApplicationDirectory = applicationDirectory;
    }

    public string ApplicationDirectory { get; }
}
