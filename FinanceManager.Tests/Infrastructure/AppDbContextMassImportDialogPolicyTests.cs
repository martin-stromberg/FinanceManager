using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceManager.Tests.Infrastructure;

public sealed class AppDbContextMassImportDialogPolicyTests
{
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
