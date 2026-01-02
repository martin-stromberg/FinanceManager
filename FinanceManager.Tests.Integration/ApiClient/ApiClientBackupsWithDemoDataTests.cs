using FinanceManager.Application;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Shared.Dtos.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

public class ApiClientBackupsWithDemoDataTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiClientBackupsWithDemoDataTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private async Task<string> RegisterAndAuthenticateAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        var resp = await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
        return username;
    }

    [Fact]
    public async Task Backup_With_DemoData_Restore_Removes_NewlyCreatedContact()
    {
        var api = CreateClient();
        var username = await RegisterAndAuthenticateAsync(api);

        // locate created user id and services in server scope
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = db.Users.Single(u => u.UserName == username);
            userId = user.Id;

            // create demo data for this user (including postings)
            var demo = scope.ServiceProvider.GetRequiredService<FinanceManager.Application.Demo.IDemoDataService>();
            await demo.CreateDemoDataAsync(userId, true, default);

            var contactService = scope.ServiceProvider.GetRequiredService<FinanceManager.Application.Contacts.IContactService>();
            await contactService.CreateAsync(userId, "FixContact", FinanceManager.Shared.Dtos.Contacts.ContactType.Person, null, "fix", false, default);
        }

        // create backup via API
        var created = await api.Backups_CreateAsync();
        created.Should().NotBeNull();

        var allBackups = await api.Backups_ListAsync();
        allBackups.Should().ContainSingle(b => b.Id == created.Id);

        // download backup stream
        var stream = await api.Backups_DownloadAsync(created.Id);
        stream.Should().NotBeNull();
        stream!.Length.Should().BeGreaterThan(0);

        // create a new contact after the backup (this should be removed by restore)
        using (var scope = _factory.Services.CreateScope())
        {
            var contactService = scope.ServiceProvider.GetRequiredService<FinanceManager.Application.Contacts.IContactService>();
            await contactService.CreateAsync(userId, "TempContactToRemove", FinanceManager.Shared.Dtos.Contacts.ContactType.Person, null, "temp", false, default);
        }

        // start apply backup
        var status = await api.Backups_StartApplyAsync(created.Id);
        status.Running.Should().BeTrue();

        // run background task runner to process the restore
        using (var cts = new CancellationTokenSource())
        {
            var scope = _factory.Services.CreateScope();
            var runner = new BackgroundTaskRunner(scope.ServiceProvider.GetService<IBackgroundTaskManager>(), scope.ServiceProvider.GetService<ILogger<BackgroundTaskRunner>>(), scope.ServiceProvider.GetServices<IBackgroundTaskExecutor>());
            await runner.StartAsync(cts.Token);

            // poll until finished
            var lastProcessed = 0;
            var processedChanged = false;
            for (int i = 0; i < 60; i = processedChanged ? 0 : i + 1)
            {
                var polled = await api.Backups_GetStatusAsync();
                if (!polled.Running) break;
                processedChanged = lastProcessed != polled.Processed;
                lastProcessed = polled.Processed;
                await Task.Delay(200, default);
            }
            cts.Cancel();
            scope.Dispose();
        }


        // verify the contact created after backup no longer exists and demo data exists
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var exists = db.Contacts.Any(c => c.OwnerUserId == userId && c.Name == "TempContactToRemove");
            exists.Should().BeFalse();

            // basic check that demo data restored: at least one savings plan exists
            var hasPlans = db.SavingsPlans.Any(p => p.OwnerUserId == userId);
            hasPlans.Should().BeTrue();

            var contacts = db.Contacts.ToList();
            contacts.Count(c => c.OwnerUserId == userId).Should().Be(4);
            contacts.Count(c => c.OwnerUserId == userId && c.Name == "FixContact").Should().Be(1);
            contacts.Count(c => c.OwnerUserId == userId && c.Name == "TempContactToRemove").Should().Be(0);
        }
    }
}
