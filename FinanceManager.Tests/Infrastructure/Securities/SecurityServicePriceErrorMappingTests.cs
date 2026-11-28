using FinanceManager.Domain.Securities;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Securities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Infrastructure.Securities;

/// <summary>
/// Tests that <see cref="SecurityService"/> maps the persisted price-error fields
/// (<c>PriceErrorMessage</c>, <c>PriceErrorSinceUtc</c>) into the <see cref="FinanceManager.Shared.Dtos.Securities.SecurityDto"/>.
/// </summary>
public sealed class SecurityServicePriceErrorMappingTests
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
    /// A security with a recorded price error returns message and timestamp in the DTO.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldMapPriceErrorFields_WhenErrorRecorded()
    {
        var (db, sut, ownerUserId) = CreateSut();
        var created = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None);

        var entity = await db.Securities.SingleAsync(s => s.Id == created.Id, cancellationToken: TestContext.Current.CancellationToken);
        entity.SetPriceError("Symbol not found");
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var dto = await sut.GetAsync(created.Id, ownerUserId, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.HasPriceError.Should().BeTrue();
        dto.PriceErrorMessage.Should().Be("Symbol not found");
        dto.PriceErrorSinceUtc.Should().Be(entity.PriceErrorSinceUtc);
    }

    /// <summary>
    /// A security without a price error returns null error fields in the DTO.
    /// </summary>
    [Fact]
    public async Task GetAsync_ShouldReturnNullErrorFields_WhenNoErrorRecorded()
    {
        var (db, sut, ownerUserId) = CreateSut();
        var created = await sut.CreateAsync(ownerUserId, "Apple", "US0378331005", null, null, "USD", null, CancellationToken.None);

        var dto = await sut.GetAsync(created.Id, ownerUserId, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.HasPriceError.Should().BeFalse();
        dto.PriceErrorMessage.Should().BeNull();
        dto.PriceErrorSinceUtc.Should().BeNull();
    }
}
