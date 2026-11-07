using FinanceManager.Domain.Securities;
using FluentAssertions;

namespace FinanceManager.Tests.Securities;

/// <summary>
/// Pure domain-layer unit tests for <see cref="Security.Unarchive"/> — the inverse of
/// <see cref="Security.Archive"/> — including the no-op contract on an already active
/// security and the symmetric reactivation inside <see cref="Security.AssignBackupDto"/>.
/// </summary>
public sealed class SecurityUnarchiveTests
{
    private static readonly Guid OwnerUserId = Guid.NewGuid();

    private static Security CreateSecurity()
        => new(OwnerUserId, "Test Security", "ISIN123", null, null, "EUR", null);

    /// <summary>
    /// Unarchive reactivates an archived security: <c>IsActive</c> flips back to
    /// <see langword="true"/>, <c>ArchivedUtc</c> is cleared and <c>ModifiedUtc</c> is stamped.
    /// </summary>
    [Fact]
    public void Unarchive_ShouldReactivate_WhenArchived()
    {
        var security = CreateSecurity();
        security.Archive();
        security.IsActive.Should().BeFalse();
        security.ArchivedUtc.Should().NotBeNull();

        security.Unarchive();

        security.IsActive.Should().BeTrue();
        security.ArchivedUtc.Should().BeNull();
        security.ModifiedUtc.Should().NotBeNull();
    }

    /// <summary>
    /// Unarchive is a no-op on an already active security (mirrors the
    /// <see cref="Security.Archive"/> no-op on an already archived one): no timestamp is stamped.
    /// </summary>
    [Fact]
    public void Unarchive_ShouldBeNoOp_WhenAlreadyActive()
    {
        var security = CreateSecurity();

        security.Unarchive();

        security.IsActive.Should().BeTrue();
        security.ArchivedUtc.Should().BeNull();
        security.ModifiedUtc.Should().BeNull();
    }

    /// <summary>
    /// A backup restore of an active DTO onto an archived entity reactivates it —
    /// symmetric to the existing archive branch of <see cref="Security.AssignBackupDto"/>;
    /// otherwise <c>IsActive=false</c> would collide with the restored <c>ArchivedUtc=null</c>.
    /// </summary>
    [Fact]
    public void AssignBackupDto_ShouldReactivate_WhenDtoIsActive()
    {
        var security = CreateSecurity();
        security.Archive();
        var dto = security.ToBackupDto() with { IsActive = true, ArchivedUtc = null };

        security.AssignBackupDto(dto);

        security.IsActive.Should().BeTrue();
        security.ArchivedUtc.Should().BeNull();
    }
}
