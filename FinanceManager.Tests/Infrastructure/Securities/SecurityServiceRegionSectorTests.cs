using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Infrastructure.Securities;

/// <summary>
/// Tests that <see cref="SecurityService.CreateAsync"/> and <see cref="SecurityService.UpdateAsync"/>
/// correctly persist the <c>Region</c>/<c>Sector</c> fields and return them via <see cref="FinanceManager.Shared.Dtos.Securities.SecurityDto"/>.
/// </summary>
public sealed class SecurityServiceRegionSectorTests
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
    /// CreateAsync persists Region/Sector and returns them in the resulting DTO.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistAndReturnRegionAndSector()
    {
        var (db, sut, ownerUserId) = CreateSut();

        var dto = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None, region: "Nordamerika", sector: "Technologie");

        dto.Region.Should().Be("Nordamerika");
        dto.Sector.Should().Be("Technologie");

        var stored = await db.Securities.AsNoTracking().SingleAsync(s => s.Id == dto.Id, cancellationToken: TestContext.Current.CancellationToken);
        stored.Region.Should().Be("Nordamerika");
        stored.Sector.Should().Be("Technologie");
    }

    /// <summary>
    /// CreateAsync without Region/Sector persists them as null and returns null in the DTO.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldPersistNullRegionAndSector_WhenNotProvided()
    {
        var (db, sut, ownerUserId) = CreateSut();

        var dto = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None);

        dto.Region.Should().BeNull();
        dto.Sector.Should().BeNull();
    }

    /// <summary>
    /// UpdateAsync persists changed Region/Sector values and returns them in the resulting DTO.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldPersistAndReturnUpdatedRegionAndSector()
    {
        var (db, sut, ownerUserId) = CreateSut();
        var created = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None, region: "Nordamerika", sector: "Technologie");

        var updated = await sut.UpdateAsync(created.Id, ownerUserId, "Apple Inc.", "US0378331005", null, null, "USD", null, CancellationToken.None, region: "Europa", sector: "Pharma");

        updated.Should().NotBeNull();
        updated!.Region.Should().Be("Europa");
        updated.Sector.Should().Be("Pharma");

        var stored = await db.Securities.AsNoTracking().SingleAsync(s => s.Id == created.Id, cancellationToken: TestContext.Current.CancellationToken);
        stored.Region.Should().Be("Europa");
        stored.Sector.Should().Be("Pharma");
    }

    /// <summary>
    /// UpdateAsync can clear previously set Region/Sector values by passing null.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldClearRegionAndSector_WhenNullIsPassed()
    {
        var (db, sut, ownerUserId) = CreateSut();
        var created = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None, region: "Nordamerika", sector: "Technologie");

        var updated = await sut.UpdateAsync(created.Id, ownerUserId, "Apple Inc.", "US0378331005", null, null, "USD", null, CancellationToken.None, region: null, sector: null);

        updated.Should().NotBeNull();
        updated!.Region.Should().BeNull();
        updated.Sector.Should().BeNull();
    }
}
