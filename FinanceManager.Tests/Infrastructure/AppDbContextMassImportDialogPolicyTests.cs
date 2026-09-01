using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceManager.Tests.Infrastructure;

/// <summary>
/// Verifies the EF Core model configuration for <see cref="User.MassImportDialogPolicy"/>: the enum is stored as a
/// non-nullable short with an explicit sentinel/default value, and existing rows without an explicit policy resolve
/// to that default without EF raising a "possible unintended value" warning (which would fail the build because
/// warnings are configured to throw during these tests).
/// </summary>
public sealed class AppDbContextMassImportDialogPolicyTests
{
    /// <summary>
    /// Verifies that the property is configured with <see cref="MassImportDialogPolicy.OnMissingInformation"/> as
    /// both the CLR sentinel and the database default, is non-nullable, and that the value converter maps the
    /// sentinel to the expected short value in the provider representation - the mapping EF relies on to decide
    /// whether an unset property should trigger a value-generation warning.
    /// </summary>
    [Fact]
    public void MassImportDialogPolicy_ModelConfiguresSentinelAndDefaultWithoutWarning()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Default(WarningBehavior.Throw))
            .Options;

        using var db = new AppDbContext(options);

        var property = db.Model
            .FindEntityType(typeof(User))!
            .FindProperty(nameof(User.MassImportDialogPolicy))!;

        Assert.Equal(MassImportDialogPolicy.OnMissingInformation, property.Sentinel);
        Assert.Equal(ValueGenerated.OnAdd, property.ValueGenerated);
        Assert.False(property.IsNullable);

        var converter = property.GetTypeMapping().Converter;
        Assert.NotNull(converter);
        Assert.Equal(typeof(short), converter!.ProviderClrType);
        Assert.Equal((short)MassImportDialogPolicy.OnMissingInformation, converter.ConvertToProvider(property.GetDefaultValue()));
    }

    /// <summary>
    /// Verifies that an explicitly set <see cref="MassImportDialogPolicy.AlwaysConfirm"/> survives a round trip
    /// through SQLite, while a user left without an explicit policy still resolves to the
    /// <see cref="MassImportDialogPolicy.OnMissingInformation"/> default - guarding against the sentinel/default
    /// configuration silently overwriting an explicitly chosen non-default value on insert.
    /// </summary>
    [Fact]
    public async Task MassImportDialogPolicy_PersistsExplicitAlwaysConfirmOnInsert()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var alwaysConfirmUserId = Guid.NewGuid();
        var defaultPolicyUserId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            var alwaysConfirmUser = new User("always-confirm", "hash");
            alwaysConfirmUser.Id = alwaysConfirmUserId;
            alwaysConfirmUser.SetMassImportDialogPolicy(MassImportDialogPolicy.AlwaysConfirm);

            var defaultPolicyUser = new User("default-policy", "hash");
            defaultPolicyUser.Id = defaultPolicyUserId;

            db.Users.AddRange(alwaysConfirmUser, defaultPolicyUser);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var db = new AppDbContext(options))
        {
            var policies = await db.Users
                .OrderBy(user => user.UserName)
                .Select(user => user.MassImportDialogPolicy)
                .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(
                new[]
                {
                    MassImportDialogPolicy.AlwaysConfirm,
                    MassImportDialogPolicy.OnMissingInformation
                },
                policies);
        }
    }
}
