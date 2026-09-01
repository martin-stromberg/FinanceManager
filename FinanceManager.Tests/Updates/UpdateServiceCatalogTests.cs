using FinanceManager.Web.Services.Updates;
using FluentAssertions;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Covers <see cref="DefaultUpdateServiceCatalog"/>'s parsing of raw <c>sc query</c> (Windows) and
/// <c>systemctl</c> (Linux) command output into service name lists - used by the updater to discover which OS
/// service(s) host the running application so it can be stopped and restarted around an install without the admin
/// having to configure the service name manually on every platform.
/// </summary>
public sealed class UpdateServiceCatalogTests
{
    /// <summary>
    /// Verifies that Windows <c>sc query</c> output is parsed into the distinct set of "SERVICE_NAME:" values -
    /// including deduplicating a name that appears more than once in the raw output, since a real installation
    /// should not be reported as running under the same service twice.
    /// </summary>
    [Fact]
    public void ParseWindowsServiceNames_ExtractsServiceNameLines()
    {
        const string output = """
            SERVICE_NAME: FinanceManager
                    TYPE               : 10  WIN32_OWN_PROCESS
            SERVICE_NAME: W3SVC
                    TYPE               : 20  WIN32_SHARE_PROCESS
            SERVICE_NAME: FinanceManager
            """;

        var names = DefaultUpdateServiceCatalog.ParseWindowsServiceNames(output);

        names.Should().Equal("FinanceManager", "W3SVC");
    }

    /// <summary>
    /// Verifies that <c>systemctl list-units</c>-style output is parsed by taking the first column of each line and
    /// filtering to actual "*.service" units - a non-service unit like "system.slice" must be excluded, since only
    /// service units are meaningful candidates for the application's own managed service.
    /// </summary>
    [Fact]
    public void ParseLinuxServiceNames_ExtractsFirstServiceColumn()
    {
        const string output = """
            financemanager.service loaded active running FinanceManager
            ssh.service loaded active running OpenSSH server daemon
            system.slice loaded active active System Slice
            """;

        var names = DefaultUpdateServiceCatalog.ParseLinuxServiceNames(output);

        names.Should().Equal("financemanager.service", "ssh.service");
    }

    /// <summary>
    /// Verifies that both parsers degrade gracefully to an empty list on empty input (e.g. the OS command produced
    /// no output) instead of throwing - service discovery is best-effort and must not crash the update flow when
    /// nothing is found.
    /// </summary>
    [Fact]
    public void Parsers_WhenOutputIsEmpty_ReturnEmptyLists()
    {
        DefaultUpdateServiceCatalog.ParseWindowsServiceNames(string.Empty).Should().BeEmpty();
        DefaultUpdateServiceCatalog.ParseLinuxServiceNames(string.Empty).Should().BeEmpty();
    }
}
