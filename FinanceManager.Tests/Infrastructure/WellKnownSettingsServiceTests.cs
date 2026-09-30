using System;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Application.WellKnown;
using FinanceManager.Domain.WellKnown;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.WellKnown;
using FinanceManager.Shared.Dtos.Admin;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Covers <see cref="WellKnownSettingsService"/>: the singleton settings row (fixed key, materialized
/// on first write only), the get/update persistence round trip, and the default fallback applied when
/// the stored redirect target is missing or invalid.
/// </summary>
public sealed class WellKnownSettingsServiceTests
{
    private static (WellKnownSettingsService service, AppDbContext db) Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        var service = new WellKnownSettingsService(db);
        return (service, db);
    }

    /// <summary>
    /// Verifies that the first read returns the default settings without persisting a row — read
    /// paths (including the anonymous well-known endpoint) must never write to the database.
    /// </summary>
    [Fact]
    public async Task GetAsync_ReturnsDefaults_WithoutPersistingRow()
    {
        var (service, db) = Create();

        var dto = await service.GetAsync(CancellationToken.None);

        dto.ChangePasswordUrl.Should().Be(WellKnownSettings.DefaultChangePasswordUrl);
        var rowCount = await db.WellKnownSettings.CountAsync(CancellationToken.None);
        rowCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that the first update materializes the singleton row under the fixed
    /// <see cref="WellKnownSettings.SingletonId"/> key, so concurrent first writes collide on the
    /// primary key instead of creating duplicates.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_CreatesSingletonRow_OnFirstWrite()
    {
        var (service, db) = Create();

        await service.UpdateAsync(new WellKnownSettingsUpdateRequest("/account/password"), CancellationToken.None);

        var rows = await db.WellKnownSettings.ToListAsync(CancellationToken.None);
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(WellKnownSettings.SingletonId);
        rows[0].ChangePasswordUrl.Should().Be("/account/password");
    }

    /// <summary>
    /// Verifies that an updated redirect target round-trips through the database unchanged.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Roundtrip()
    {
        var (service, _) = Create();

        await service.UpdateAsync(new WellKnownSettingsUpdateRequest("https://idp.example.com/account/password"), CancellationToken.None);
        var dto = await service.GetAsync(CancellationToken.None);

        dto.ChangePasswordUrl.Should().Be("https://idp.example.com/account/password");
    }

    /// <summary>
    /// Verifies that an update to a local path is reflected by the public redirect resolver.
    /// </summary>
    [Fact]
    public async Task GetChangePasswordUrlAsync_ReturnsConfiguredValue()
    {
        var (service, _) = Create();

        await service.UpdateAsync(new WellKnownSettingsUpdateRequest("/account/password"), CancellationToken.None);
        var url = await service.GetChangePasswordUrlAsync(CancellationToken.None);

        url.Should().Be("/account/password");
    }

    /// <summary>
    /// Verifies that a stored value that is empty or no longer passes the URL rule falls back to the
    /// default — the public endpoint must never redirect to a broken target even if the row was
    /// manipulated outside the validated update path.
    /// </summary>
    /// <param name="storedUrl">A stored value that fails the URL rule.</param>
    [Theory]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//host/path")]
    public async Task GetChangePasswordUrlAsync_ReturnsDefault_WhenInvalid(string storedUrl)
    {
        var (service, db) = Create();
        await service.UpdateAsync(new WellKnownSettingsUpdateRequest("/account/password"), CancellationToken.None);
        var entity = await db.WellKnownSettings.FirstAsync(CancellationToken.None);
        db.Entry(entity).Property(nameof(WellKnownSettings.ChangePasswordUrl)).CurrentValue = storedUrl;
        await db.SaveChangesAsync(CancellationToken.None);

        var url = await service.GetChangePasswordUrlAsync(CancellationToken.None);

        url.Should().Be(WellKnownSettings.DefaultChangePasswordUrl);
    }
}
