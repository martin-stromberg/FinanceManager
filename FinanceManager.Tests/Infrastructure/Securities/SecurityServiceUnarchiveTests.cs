using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Infrastructure.Securities;

/// <summary>
/// Tests that <see cref="SecurityService.UnarchiveAsync"/> reactivates an archived
/// security owner-scoped and returns <see langword="false"/> for the no-op cases
/// (already active, not found / foreign owner) — mirroring the
/// <see cref="SecurityService.ArchiveAsync"/> contract.
/// </summary>
public sealed class SecurityServiceUnarchiveTests
{
    private static (AppDbContext db, SecurityService sut, Guid ownerUserId) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new AppDbContext(options);
        var owner = new User("owner", "hash", true);
        db.Users.Add(owner);
        db.SaveChanges();

        return (db, new SecurityService(db), owner.Id);
    }

    /// <summary>
    /// An archived security is reactivated: <c>true</c> result, persisted
    /// <c>IsActive=true</c> and <c>ArchivedUtc=null</c>.
    /// </summary>
    [Fact]
    public async Task UnarchiveAsync_ShouldReactivate_WhenArchived()
    {
        var (db, sut, ownerUserId) = CreateSut();
        var created = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None);
        (await sut.ArchiveAsync(created.Id, ownerUserId, CancellationToken.None)).Should().BeTrue();

        var result = await sut.UnarchiveAsync(created.Id, ownerUserId, CancellationToken.None);

        result.Should().BeTrue();
        var stored = await db.Securities.AsNoTracking().SingleAsync(s => s.Id == created.Id, cancellationToken: TestContext.Current.CancellationToken);
        stored.IsActive.Should().BeTrue();
        stored.ArchivedUtc.Should().BeNull();
    }

    /// <summary>
    /// Unarchiving an already active security is a no-op returning <see langword="false"/>.
    /// </summary>
    [Fact]
    public async Task UnarchiveAsync_ShouldReturnFalse_WhenAlreadyActive()
    {
        var (_, sut, ownerUserId) = CreateSut();
        var created = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None);

        var result = await sut.UnarchiveAsync(created.Id, ownerUserId, CancellationToken.None);

        result.Should().BeFalse();
    }

    /// <summary>
    /// A foreign owner's archived security is invisible to the caller — same
    /// <see langword="false"/> not-found contract as <see cref="SecurityService.ArchiveAsync"/>.
    /// </summary>
    [Fact]
    public async Task UnarchiveAsync_ShouldReturnFalse_WhenForeignOwned()
    {
        var (db, sut, ownerUserId) = CreateSut();
        var foreignOwner = new User("other", "hash", true);
        db.Users.Add(foreignOwner);
        db.SaveChanges();
        var created = await sut.CreateAsync(foreignOwner.Id, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None);
        (await sut.ArchiveAsync(created.Id, foreignOwner.Id, CancellationToken.None)).Should().BeTrue();

        var result = await sut.UnarchiveAsync(created.Id, ownerUserId, CancellationToken.None);

        result.Should().BeFalse();
        var stored = await db.Securities.AsNoTracking().SingleAsync(s => s.Id == created.Id, cancellationToken: TestContext.Current.CancellationToken);
        stored.IsActive.Should().BeFalse();
        stored.ArchivedUtc.Should().NotBeNull();
    }
}
