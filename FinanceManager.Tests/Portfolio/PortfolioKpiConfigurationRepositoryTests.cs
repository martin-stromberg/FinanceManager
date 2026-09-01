using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Portfolio;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Portfolio;

/// <summary>
/// Tests for <see cref="PortfolioKpiConfigurationRepository"/> covering create, update and delete
/// of a user's portfolio KPI configuration.
/// </summary>
public sealed class PortfolioKpiConfigurationRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PortfolioKpiConfigurationRepository _sut;

    public PortfolioKpiConfigurationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new PortfolioKpiConfigurationRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_PortfolioKpiConfiguration_Persists()
    {
        var userId = Guid.NewGuid();

        var created = await _sut.UpsertAsync(userId, "[0,1]", "[0,1]", CancellationToken.None);

        created.OwnerUserId.Should().Be(userId);
        var loaded = await _sut.GetAsync(userId, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.ActiveTileIds.Should().Be("[0,1]");
    }

    [Fact]
    public async Task Update_PortfolioKpiConfiguration_ReflectsChanges()
    {
        var userId = Guid.NewGuid();
        await _sut.UpsertAsync(userId, "[0]", "[0]", CancellationToken.None);

        var updated = await _sut.UpsertAsync(userId, "[0,1,2]", "[0,1,2]", CancellationToken.None);

        updated.ActiveTileIds.Should().Be("[0,1,2]");
        (await _db.PortfolioKpiConfigurations.CountAsync(c => c.OwnerUserId == userId, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingConfiguration()
    {
        var userId = Guid.NewGuid();
        await _sut.UpsertAsync(userId, "[0]", "[0]", CancellationToken.None);

        await _sut.DeleteAsync(userId, CancellationToken.None);

        (await _sut.GetAsync(userId, CancellationToken.None)).Should().BeNull();
    }
}
