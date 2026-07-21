using FinanceManager.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Xunit;

namespace FinanceManager.Tests.Integration;

/// <summary>
/// Reproduces a suspected concurrency issue in <see cref="TestWebApplicationFactory"/>, where multiple
/// <see cref="AppDbContext"/> instances resolved from different threads all reuse the same externally-owned
/// <see cref="Microsoft.Data.Sqlite.SqliteConnection"/> object, which is not safe for concurrent access.
/// </summary>
public class TestWebApplicationFactoryConcurrencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Creates a new instance of <see cref="TestWebApplicationFactoryConcurrencyTests"/>.
    /// </summary>
    /// <param name="factory">Shared test factory injected by xUnit's class fixture.</param>
    public TestWebApplicationFactoryConcurrencyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Runs several concurrent <see cref="AppDbContext"/> queries from different threads against the same
    /// factory and asserts that no exception occurs.
    /// </summary>
    [Fact]
    public async Task ConcurrentDbContextUsage_FromMultipleThreads_DoesNotThrow()
    {
        _ = _factory.Server; // force host startup

        var deadline = DateTime.UtcNow.AddSeconds(3);
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            while (DateTime.UtcNow < deadline)
            {
                using var scope = _factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                _ = await db.Users.AsNoTracking().CountAsync();
            }
        })).ToArray();

        var exceptions = new List<Exception>();
        await Task.WhenAll(tasks.Select(async t =>
        {
            try { await t; }
            catch (Exception ex) { lock (exceptions) { exceptions.Add(ex); } }
        }));

        exceptions.Should().BeEmpty("concurrent DbContext usage against the shared test-factory connection should not throw");
    }
}
