using FinanceManager.Web.Services.Updates;
using FluentAssertions;

namespace FinanceManager.Tests.Updates;

public sealed class UpdateServiceCatalogTests
{
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

    [Fact]
    public void Parsers_WhenOutputIsEmpty_ReturnEmptyLists()
    {
        DefaultUpdateServiceCatalog.ParseWindowsServiceNames(string.Empty).Should().BeEmpty();
        DefaultUpdateServiceCatalog.ParseLinuxServiceNames(string.Empty).Should().BeEmpty();
    }
}
