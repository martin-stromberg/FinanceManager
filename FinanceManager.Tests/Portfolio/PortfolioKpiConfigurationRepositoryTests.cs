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

    /// <summary>Sets up a fresh in-memory <see cref="AppDbContext"/> and the repository under test.</summary>
    public PortfolioKpiConfigurationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _sut = new PortfolioKpiConfigurationRepository(_db);
    }

    /// <summary>Releases the in-memory <see cref="AppDbContext"/> used by each test.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>
    /// Verifies that creating a KPI configuration for a user via <c>UpsertAsync</c> persists it and
    /// that it can subsequently be read back with the same tile selection.
    /// </summary>
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

    /// <summary>
    /// Verifies that calling <c>UpsertAsync</c> again for a user who already has a configuration
    /// updates the existing row in place - new tile selection is reflected and no second row is
    /// created - confirming the "upsert" semantics rather than accidental duplication.
    /// </summary>
    [Fact]
    public async Task Update_PortfolioKpiConfiguration_ReflectsChanges()
    {
        var userId = Guid.NewGuid();
        await _sut.UpsertAsync(userId, "[0]", "[0]", CancellationToken.None);

        var updated = await _sut.UpsertAsync(userId, "[0,1,2]", "[0,1,2]", CancellationToken.None);

        updated.ActiveTileIds.Should().Be("[0,1,2]");
        (await _db.PortfolioKpiConfigurations.CountAsync(c => c.OwnerUserId == userId, cancellationToken: TestContext.Current.CancellationToken)).Should().Be(1);
    }

    /// <summary>
    /// Verifies that deleting an existing KPI configuration removes it, so a subsequent read
    /// returns <see langword="null"/> rather than the deleted (or a stale) configuration.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesExistingConfiguration()
    {
        var userId = Guid.NewGuid();
        await _sut.UpsertAsync(userId, "[0]", "[0]", CancellationToken.None);

        await _sut.DeleteAsync(userId, CancellationToken.None);

        (await _sut.GetAsync(userId, CancellationToken.None)).Should().BeNull();
    }
}
