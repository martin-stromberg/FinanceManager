using DocumentFormat.OpenXml.ExtendedProperties;
using FinanceManager.Application;
using FinanceManager.Application.Accounts;
using FinanceManager.Application.Aggregates;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Reports;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.Users;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
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

        // capture full snapshot before backup (ID-agnostic projections)
        Snapshot beforeSnapshot;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            beforeSnapshot = await CaptureSnapshotAsync(db, scope.ServiceProvider, userId);
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
                if (!string.IsNullOrWhiteSpace(polled.Error))
                    throw new InvalidOperationException($"Backup restore failed: {polled.Error}");
                if (!polled.Running) break;
                processedChanged = lastProcessed != polled.Processed;
                lastProcessed = polled.Processed;
                await Task.Delay(200, default);
            }
            cts.Cancel();
            scope.Dispose();
        }

        // capture full snapshot after restore
        Snapshot afterSnapshot;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            afterSnapshot = await CaptureSnapshotAsync(db, scope.ServiceProvider, userId);
        }

        // compare snapshots (ID-mapped)
        CompareSnapshots(beforeSnapshot, afterSnapshot);
    }

    private sealed record AggregateDto(string EntityType, string EntityName, string Amount);

    private static string FormatDecimalForSnapshot(decimal value)
    {
        // Ensure invariant culture formatting and at least one decimal place for whole numbers (so 0 -> "0.0" not "0").
        if (value % 1 == 0)
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        return value.ToString("0.###############################", CultureInfo.InvariantCulture);
    }

    private static async Task<List<AggregateDto>> CalculateAggregatesAsync(IServiceProvider sp, Guid userId, CancellationToken ct)
    {
        var timeSeriesService = sp.GetRequiredService<IPostingTimeSeriesService>();
        var contactService = sp.GetRequiredService<IContactService>();
        var contacts = await contactService.ListAsync(userId, 0, int.MaxValue, null, null, ct);
        var savingsPlansService = sp.GetRequiredService<ISavingsPlanService>();
        var savingsPlans = await savingsPlansService.ListAsync(userId, false, ct);
        var securitysService = sp.GetRequiredService<ISecurityService>();
        var securities = await securitysService.ListAsync(userId, false, ct);
        var bankAccountService = sp.GetRequiredService<IAccountService>();
        var bankAccounts = await bankAccountService.ListAsync(userId, 0, int.MaxValue, ct);

        var result = new List<AggregateDto>();
        foreach (var c in contacts.OrderBy(c => c.Name))
        {
            var series = await timeSeriesService.GetAsync(userId, PostingKind.Contact, c.Id, Domain.Postings.AggregatePeriod.Year, int.MaxValue, int.MaxValue, ct);
            var sum = series?.Sum(x => x.Amount) ?? 0m;
            result.Add(new AggregateDto(nameof(ContactDto), c.Name, FormatDecimalForSnapshot(sum)));
        }
        foreach (var plan in savingsPlans.OrderBy(c => c.Name))
        {
            var series = await timeSeriesService.GetAsync(userId, PostingKind.SavingsPlan, plan.Id, Domain.Postings.AggregatePeriod.Year, int.MaxValue, int.MaxValue, ct);
            var sum = series?.Sum(x => x.Amount) ?? 0m;
            result.Add(new AggregateDto(nameof(SavingsPlanDto), plan.Name, FormatDecimalForSnapshot(sum)));
        }
        foreach (var s in securities.OrderBy(c => c.Name))
        {
            var series = await timeSeriesService.GetAsync(userId, PostingKind.Security, s.Id, Domain.Postings.AggregatePeriod.Year, int.MaxValue, int.MaxValue, ct);
            var sum = series?.Sum(x => x.Amount) ?? 0m;
            result.Add(new AggregateDto(nameof(SecurityDto), s.Name, FormatDecimalForSnapshot(sum)));
        }
        foreach (var b in bankAccounts.OrderBy(c => c.Name))
        {
            var series = await timeSeriesService.GetAsync(userId, PostingKind.Bank, b.Id, Domain.Postings.AggregatePeriod.Year, int.MaxValue, int.MaxValue, ct);
            var sum = series?.Sum(x => x.Amount) ?? 0m;
            result.Add(new AggregateDto(nameof(AccountDto), b.Name, FormatDecimalForSnapshot(sum)));
        }
        return result;
    }

    private sealed record Snapshot(
        List<FinanceManager.Domain.Contacts.Contact.ContactBackupDto> Contacts,
        List<FinanceManager.Domain.Contacts.ContactCategory.ContactCategoryBackupDto> ContactCategories,
        List<FinanceManager.Domain.Contacts.AliasName.AliasNameBackupDto> AliasNames,
        List<FinanceManager.Domain.Accounts.Account.AccountBackupDto> Accounts,
        List<FinanceManager.Domain.Savings.SavingsPlanCategory.SavingsPlanCategoryBackupDto> SavingsPlanCategories,
        List<FinanceManager.Domain.Savings.SavingsPlan.SavingsPlanBackupDto> SavingsPlans,
        List<FinanceManager.Domain.Securities.SecurityCategory.SecurityCategoryBackupDto> SecurityCategories,
        List<FinanceManager.Domain.Securities.Security.SecurityBackupDto> Securities,
        List<FinanceManager.Domain.Securities.SecurityPrice.SecurityPriceBackupDto> SecurityPrices,
        List<FinanceManager.Domain.Postings.Posting.PostingBackupDto> Postings,
        List<FinanceManager.Domain.Statements.StatementImport.StatementImportBackupDto> StatementImports,
        List<FinanceManager.Domain.Statements.StatementEntry.StatementEntryBackupDto> StatementEntries,
        List<FinanceManager.Domain.Statements.StatementDraft.StatementDraftBackupDto> StatementDrafts,
        List<FinanceManager.Domain.Statements.StatementDraftEntry.StatementDraftEntryBackupDto> StatementDraftEntries,
        List<FinanceManager.Domain.Reports.ReportFavorite.ReportFavoriteBackupDto> ReportFavorites,
        List<FinanceManager.Domain.Reports.HomeKpi.HomeKpiBackupDto> HomeKpis,
        List<FinanceManager.Domain.Attachments.AttachmentCategory.AttachmentCategoryBackupDto> AttachmentCategories,
        List<FinanceManager.Domain.Attachments.Attachment.AttachmentBackupDto> Attachments,
        List<FinanceManager.Domain.Notifications.Notification.NotificationBackupDto> Notifications,
        List<FinanceManager.Domain.Accounts.AccountShare.AccountShareBackupDto> AccountShares,
        List<AggregateDto> Aggregates
    );

    private static async Task<Snapshot> CaptureSnapshotAsync(AppDbContext db, IServiceProvider sp, Guid userId)
    {
        // Retrieve backup DTOs / entities and project into simple lists
        var contactCategories = db.ContactCategories.AsNoTracking().Where(c => c.OwnerUserId == userId).Select(c => c.ToBackupDto()).ToList();
        var contacts = db.Contacts.AsNoTracking().Where(c => c.OwnerUserId == userId).Select(c => c.ToBackupDto()).ToList();
        var contactIds = contacts.Select(c => c.Id).ToList();
        var aliasNames = db.AliasNames.AsNoTracking().Where(a => a.ContactId != null && contactIds.Contains(a.ContactId)).Select(a => a.ToBackupDto()).ToList();

        var securityCategories = db.SecurityCategories.AsNoTracking().Where(s => s.OwnerUserId == userId).Select(s => s.ToBackupDto()).ToList();
        var securities = db.Securities.AsNoTracking().Where(s => s.OwnerUserId == userId).Select(s => s.ToBackupDto()).ToList();
        var securityIds = securities.Select(s => s.Id).ToList();
        var securityPrices = db.SecurityPrices.AsNoTracking().Where(p => securityIds.Contains(p.SecurityId)).Select(p => p.ToBackupDto()).ToList();

        var savingsPlanCategories = db.SavingsPlanCategories.AsNoTracking().Where(s => s.OwnerUserId == userId).Select(s => s.ToBackupDto()).ToList();
        var savingsPlans = db.SavingsPlans.AsNoTracking().Where(s => s.OwnerUserId == userId).Select(s => s.ToBackupDto()).ToList();
        var savingsPlanIds = savingsPlans.Select(s => s.Id).ToList();

        var accounts = db.Accounts.AsNoTracking().Where(a => a.OwnerUserId == userId).Select(a => a.ToBackupDto()).ToList();
        var accountIds = accounts.Select(a => a.Id).ToList();

        var statementImports = db.StatementImports.AsNoTracking().Where(i => accountIds.Contains(i.AccountId)).Select(i => i.ToBackupDto()).ToList();
        var importIds = statementImports.Select(i => i.Id).ToList();
        var statementEntries = db.StatementEntries.AsNoTracking().Where(e => importIds.Contains(e.StatementImportId)).Select(e => e.ToBackupDto()).ToList();

        var postings = db.Postings.AsNoTracking().Where(p => (p.AccountId != null && accountIds.Contains(p.AccountId.Value))
                     || (p.ContactId != null && contactIds.Contains(p.ContactId.Value))
                     || (p.SavingsPlanId != null && savingsPlanIds.Contains(p.SavingsPlanId.Value))
                     || (p.SecurityId != null && securityIds.Contains(p.SecurityId.Value)))
            .Select(p => p.ToBackupDto()).ToList();

        var drafts = db.StatementDrafts.AsNoTracking().Where(d => d.OwnerUserId == userId).Select(d => d.ToBackupDto()).ToList();
        var draftIds = drafts.Select(d => d.Id).ToList();
        var draftEntries = db.StatementDraftEntries.AsNoTracking().Where(e => draftIds.Contains(e.DraftId)).Select(e => e.ToBackupDto()).ToList();

        var reportFavorites = db.ReportFavorites.AsNoTracking().Where(r => r.OwnerUserId == userId).Select(r => r.ToBackupDto()).ToList();
        var homeKpis = db.HomeKpis.AsNoTracking().Where(h => h.OwnerUserId == userId).Select(h => h.ToBackupDto()).ToList();

        var attachmentCategories = db.AttachmentCategories.AsNoTracking().Where(ac => ac.OwnerUserId == userId).Select(ac => ac.ToBackupDto()).ToList();
        var attachments = db.Attachments.AsNoTracking().Where(a => a.OwnerUserId == userId).Select(a => a.ToBackupDto()).ToList();

        var notifications = db.Notifications.AsNoTracking().Where(n => n.OwnerUserId == userId).Select(n => n.ToBackupDto()).ToList();

        var accountShares = db.AccountShares.AsNoTracking().Where(s => accountIds.Contains(s.AccountId) || s.UserId == userId).Select(s => s.ToBackupDto()).ToList();

        var aggregates = await CalculateAggregatesAsync(sp, userId, default);

        return new Snapshot(contacts, contactCategories, aliasNames, accounts, savingsPlanCategories, savingsPlans, securityCategories, securities, securityPrices, postings, statementImports, statementEntries, drafts, draftEntries, reportFavorites, homeKpis, attachmentCategories, attachments, notifications, accountShares, aggregates);
    }

    private static void CompareSnapshots(Snapshot before, Snapshot after)
    {
        // Build name->id maps from 'after' snapshot for remapping
        var contactNameToId = after.Contacts.ToDictionary(c => c.Name, c => c.Id);
        var accountNameToId = after.Accounts.ToDictionary(a => a.Name, a => a.Id);
        var contactCategoryNameToId = after.ContactCategories.ToDictionary(c => c.Name, c => c.Id);
        var savingsPlanCategoryNameToId = after.SavingsPlanCategories.ToDictionary(s => s.Name, s => s.Id);
        var securityCategoryNameToId = after.SecurityCategories.ToDictionary(s => s.Name, s => s.Id);

        // Map contact ids from before->after by name
        var contactIdMap = new Dictionary<Guid, Guid>();
        foreach (var bc in before.Contacts)
        {
            if (contactNameToId.TryGetValue(bc.Name, out var aid)) contactIdMap[bc.Id] = aid;
        }

        // Map account ids from before->after by name
        var accountIdMap = new Dictionary<Guid, Guid>();
        foreach (var ba in before.Accounts)
        {
            if (accountNameToId.TryGetValue(ba.Name, out var aid)) accountIdMap[ba.Id] = aid;
        }

        // Map contact category ids from before->after by name and update before contacts' CategoryId
        var contactCategoryIdMap = new Dictionary<Guid, Guid>();
        foreach (var cc in before.ContactCategories)
        {
            if (contactCategoryNameToId.TryGetValue(cc.Name, out var aid)) contactCategoryIdMap[cc.Id] = aid;
        }

        // Update before snapshot contacts so their CategoryId points to the mapped after-id (if known)
        before = before with
        {
            Contacts = before.Contacts.Select(c => c with
            {
                CategoryId = c.CategoryId.HasValue && contactCategoryIdMap.TryGetValue(c.CategoryId.Value, out var mappedCat) ? mappedCat : c.CategoryId
            }).ToList()
        };

        // Build attachment key -> id map for 'after' using entity names
        static string AttachmentKey(FinanceManager.Domain.Attachments.Attachment.AttachmentBackupDto a, Snapshot snap, Dictionary<Guid, Guid> contactMap, Dictionary<Guid, Guid> accountMap)
        {
            string? entityName = ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, snap, contactMap, accountMap);
            return $"{a.EntityKind}|{entityName}|{a.FileName}|{a.ContentType}|{a.SizeBytes}|{a.Sha256}";
        }

        var afterAttachmentMap = new Dictionary<string, Guid>();
        foreach (var a in after.Attachments)
        {
            var key = AttachmentKey(a, after, contactIdMap, accountIdMap);
            if (!afterAttachmentMap.ContainsKey(key)) afterAttachmentMap[key] = a.Id;
        }

        // Build before->after attachment id mapping by comparing keys
        var beforeAttachmentKeyMap = new Dictionary<Guid, string>();
        foreach (var a in before.Attachments)
        {
            var key = AttachmentKey(a, before, contactIdMap, accountIdMap);
            beforeAttachmentKeyMap[a.Id] = key;
        }

        var attachmentIdMap = new Dictionary<Guid, Guid>();
        foreach (var kv in beforeAttachmentKeyMap)
        {
            if (afterAttachmentMap.TryGetValue(kv.Value, out var mapped)) attachmentIdMap[kv.Key] = mapped;
        }

        // Remap SymbolAttachmentId references in the before snapshot using attachmentIdMap
        before = before with
        {
            Contacts = before.Contacts.Select(c => c with
            {
                SymbolAttachmentId = c.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(c.SymbolAttachmentId.Value, out var mappedC) ? mappedC : c.SymbolAttachmentId
            }).ToList(),

            ContactCategories = before.ContactCategories.Select(cc => cc with
            {
                SymbolAttachmentId = cc.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(cc.SymbolAttachmentId.Value, out var mappedCC) ? mappedCC : cc.SymbolAttachmentId
            }).ToList(),

            Accounts = before.Accounts.Select(a => a with
            {
                SymbolAttachmentId = a.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(a.SymbolAttachmentId.Value, out var mappedA) ? mappedA : a.SymbolAttachmentId
            }).ToList(),

            SavingsPlanCategories = before.SavingsPlanCategories.Select(spc => spc with
            {
                SymbolAttachmentId = spc.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(spc.SymbolAttachmentId.Value, out var mappedSPC) ? mappedSPC : spc.SymbolAttachmentId
            }).ToList(),

            SavingsPlans = before.SavingsPlans.Select(sp => sp with
            {
                SymbolAttachmentId = sp.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(sp.SymbolAttachmentId.Value, out var mappedSP) ? mappedSP : sp.SymbolAttachmentId
            }).ToList(),

            SecurityCategories = before.SecurityCategories.Select(sc => sc with
            {
                SymbolAttachmentId = sc.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(sc.SymbolAttachmentId.Value, out var mappedSC) ? mappedSC : sc.SymbolAttachmentId
            }).ToList(),

            Securities = before.Securities.Select(s => s with
            {
                SymbolAttachmentId = s.SymbolAttachmentId.HasValue && attachmentIdMap.TryGetValue(s.SymbolAttachmentId.Value, out var mappedS) ? mappedS : s.SymbolAttachmentId
            }).ToList(),

            AttachmentCategories = before.AttachmentCategories.ToList(),
            Attachments = before.Attachments.ToList(),
            Notifications = before.Notifications.ToList(),
            AccountShares = before.AccountShares.ToList()
        };

        // Align IDs in the before snapshot to match the after snapshot (name-based remapping)
        var alignedBefore = AlignBeforeSnapshot(before, after, contactIdMap, accountIdMap, attachmentIdMap);

        // Sort both snapshots to deterministic order and compare their JSON representation
        var sortedBefore = SortSnapshot(alignedBefore);
        var sortedAfter = SortSnapshot(after);

        var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = false, PropertyNameCaseInsensitive = true };
        var beforeJson = System.Text.Json.JsonSerializer.Serialize(sortedBefore, jsonOptions);
        var afterJson = System.Text.Json.JsonSerializer.Serialize(sortedAfter, jsonOptions);
        beforeJson.Should().BeEquivalentTo(afterJson);
    }

    private static Snapshot AlignBeforeSnapshot(Snapshot before, Snapshot after, Dictionary<Guid, Guid> contactIdMap, Dictionary<Guid, Guid> accountIdMap, Dictionary<Guid, Guid> attachmentIdMap)
    {
        // Create additional name->id maps from 'after' for other entity types
        var contactNameToId = after.Contacts.ToDictionary(c => c.Name, c => c.Id);
        var accountNameToId = after.Accounts.ToDictionary(a => a.Name, a => a.Id);
        var contactCategoryNameToId = after.ContactCategories.ToDictionary(c => c.Name, c => c.Id);
        var savingsPlanCategoryNameToId = after.SavingsPlanCategories.ToDictionary(s => s.Name, s => s.Id);
        var savingsPlanNameToId = after.SavingsPlans.ToDictionary(s => s.Name, s => s.Id);
        var securityCategoryNameToId = after.SecurityCategories.ToDictionary(s => s.Name, s => s.Id);
        var securityNameToId = after.Securities.ToDictionary(s => s.Name, s => s.Id);
        var attachmentCategoryNameToId = after.AttachmentCategories.ToDictionary(ac => ac.Name, ac => ac.Id);

        // Remap contacts by name
        var contacts = before.Contacts.Select(c =>
        {
            if (contactNameToId.TryGetValue(c.Name, out var nid))
                return c with { Id = nid };
            return c;
        }).ToList();

        // Remap contact categories
        var contactCategories = before.ContactCategories.Select(cc =>
        {
            if (contactCategoryNameToId.TryGetValue(cc.Name, out var nid))
                return cc with { Id = nid };
            return cc;
        }).ToList();

        // Remap alias names (contacts referenced) and align alias Ids with 'after' snapshot where possible
        var aliases = before.AliasNames.Select(a =>
        {
            var mappedContactId = contactIdMap.TryGetValue(a.ContactId, out var mc) ? mc : a.ContactId;
            // try to find matching alias in 'after' by pattern + contactId
            var mappedAliasId = after.AliasNames.FirstOrDefault(x => x.Pattern == a.Pattern && x.ContactId == mappedContactId)?.Id ?? a.Id;
            return a with { Id = mappedAliasId, ContactId = mappedContactId };
        }).ToList();

        // Remap accounts
        var accounts = before.Accounts.Select(a =>
        {
            if (accountNameToId.TryGetValue(a.Name, out var nid))
                return a with { Id = nid, BankContactId = contactNameToId.ContainsKey(after.Contacts.FirstOrDefault(c => c.Id == a.BankContactId)?.Name ?? string.Empty) ? contactNameToId.GetValueOrDefault(after.Contacts.FirstOrDefault(c => c.Id == a.BankContactId)?.Name ?? string.Empty) : a.BankContactId };
            return a;
        }).ToList();

        // Remap savings plans and categories
        var savingsPlanCategories = before.SavingsPlanCategories.Select(spc =>
        {
            if (savingsPlanCategoryNameToId.TryGetValue(spc.Name, out var nid)) return spc with { Id = nid };
            return spc;
        }).ToList();
        var savingsPlans = before.SavingsPlans.Select(sp =>
        {
            // remap by name
            var adjusted = sp;
            if (savingsPlanNameToId.TryGetValue(sp.Name, out var nid)) adjusted = sp with { Id = nid };
            // remap CategoryId by resolving category name from 'before' and mapping to 'after' id
            if (sp.CategoryId.HasValue)
            {
                var beforeCatName = before.SavingsPlanCategories.FirstOrDefault(c => c.Id == sp.CategoryId.Value)?.Name;
                if (!string.IsNullOrEmpty(beforeCatName) && savingsPlanCategoryNameToId.TryGetValue(beforeCatName, out var mappedCat))
                {
                    adjusted = adjusted with { CategoryId = mappedCat };
                }
            }
            return adjusted;
        }).ToList();

        // Remap securities and categories
        var securityCategories = before.SecurityCategories.Select(sc =>
        {
            if (securityCategoryNameToId.TryGetValue(sc.Name, out var nid)) return sc with { Id = nid };
            return sc;
        }).ToList();
        var securities = before.Securities.Select(s =>
        {
            var adjusted = s;
            if (securityNameToId.TryGetValue(s.Name, out var nid)) adjusted = s with { Id = nid };
            // remap security category by name from 'before' -> 'after'
            if (s.CategoryId.HasValue)
            {
                var beforeCatName = before.SecurityCategories.FirstOrDefault(c => c.Id == s.CategoryId.Value)?.Name;
                if (!string.IsNullOrEmpty(beforeCatName) && securityCategoryNameToId.TryGetValue(beforeCatName, out var mappedCat))
                {
                    adjusted = adjusted with { CategoryId = mappedCat };
                }
            }
            return adjusted;
        }).ToList();

        // Remap attachment categories by name so attachments can reference the correct category ids
        var remappedAttachmentCategories = before.AttachmentCategories.Select(ac =>
        {
            if (attachmentCategoryNameToId.TryGetValue(ac.Name, out var nid)) return ac with { Id = nid };
            return ac;
        }).ToList();

        // Build attachment category id map from before.Id -> after.Id for use when remapping attachments
        var attachmentCategoryIdMap = new Dictionary<Guid, Guid>();
        foreach (var bac in before.AttachmentCategories)
        {
            if (attachmentCategoryNameToId.TryGetValue(bac.Name, out var aid)) attachmentCategoryIdMap[bac.Id] = aid;
        }

        // Build security id map (before.Id -> after.Id) by name so prices can be remapped
        var securityIdMap = new Dictionary<Guid, Guid>();
        foreach (var bs in before.Securities)
        {
            if (securityNameToId.TryGetValue(bs.Name, out var aid)) securityIdMap[bs.Id] = aid;
        }

        // Remap security prices: match by (securityId, date, close) using after snapshot ids
        string PriceKey(FinanceManager.Domain.Securities.SecurityPrice.SecurityPriceBackupDto p) => $"{p.SecurityId}|{p.Date.Ticks}|{p.Close}";
        var afterPriceMap = new Dictionary<string, Guid>();
        foreach (var ap in after.SecurityPrices)
        {
            var key = PriceKey(ap);
            if (!afterPriceMap.ContainsKey(key)) afterPriceMap[key] = ap.Id;
        }
        var securityPrices = before.SecurityPrices.Select(p =>
        {
            var newSecurityId = securityIdMap.TryGetValue(p.SecurityId, out var ns) ? ns : p.SecurityId;
            var key = $"{newSecurityId}|{p.Date.Ticks}|{p.Close}";
            if (afterPriceMap.TryGetValue(key, out var mappedId))
            {
                return p with { Id = mappedId, SecurityId = newSecurityId };
            }
            return p with { SecurityId = newSecurityId };
        }).ToList();

        // Remap attachments: use key matching against 'after' (pass contact/account maps so entity names resolve correctly)
        string KeyForAttachment(FinanceManager.Domain.Attachments.Attachment.AttachmentBackupDto a, Snapshot snap)
            => $"{a.EntityKind}|{ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, snap, contactIdMap, accountIdMap)}|{a.FileName}|{a.ContentType}|{a.SizeBytes}|{a.Sha256}";

        // Build draft name->id map early so attachments referencing StatementDraft can be remapped
        var afterDraftNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in after.StatementDrafts)
        {
            var key = d.OriginalFileName ?? string.Empty;
            if (!afterDraftNameToId.ContainsKey(key)) afterDraftNameToId[key] = d.Id;
        }
        var draftIdMap = new Dictionary<Guid, Guid>();
        foreach (var bd in before.StatementDrafts)
        {
            var mapped = afterDraftNameToId.TryGetValue(bd.OriginalFileName ?? string.Empty, out var aid) ? aid : bd.Id;
            draftIdMap[bd.Id] = mapped;
        }

        var afterAttachmentKeys = after.Attachments.ToDictionary(a => KeyForAttachment(a, after), a => a.Id);
        var attachments = before.Attachments.Select(a =>
        {
            var key = KeyForAttachment(a, before); // use after for name resolution

            var newEntityId = a.EntityKind switch
            {
                FinanceManager.Domain.Attachments.AttachmentEntityKind.Contact => contactIdMap.TryGetValue(a.EntityId, out var nc) ? nc : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.Account => accountIdMap.TryGetValue(a.EntityId, out var na) ? na : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.ContactCategory => contactCategoryNameToId.TryGetValue(ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, before) ?? string.Empty, out var ncc) ? ncc : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.SavingsPlanCategory => savingsPlanCategoryNameToId.TryGetValue(ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, before) ?? string.Empty, out var nspc) ? nspc : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.SavingsPlan => savingsPlanNameToId.TryGetValue(ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, before) ?? string.Empty, out var nsp) ? nsp : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.SecurityCategory => securityCategoryNameToId.TryGetValue(ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, before) ?? string.Empty, out var nsc) ? nsc : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.Security => securityNameToId.TryGetValue(ResolveEntityNameForAttachment(a.EntityKind, a.EntityId, before) ?? string.Empty, out var ns) ? ns : a.EntityId,
                FinanceManager.Domain.Attachments.AttachmentEntityKind.StatementDraft => draftIdMap.TryGetValue(a.EntityId, out var md) ? md : a.EntityId,
                _ => a.EntityId
            };

            // determine mapped category id if any
            Guid? mappedCategory = a.CategoryId.HasValue && attachmentCategoryIdMap.TryGetValue(a.CategoryId.Value, out var ncat) ? ncat : a.CategoryId;

            // prefer mapping provided by the outer comparison (attachmentIdMap) if available
            if (attachmentIdMap != null && attachmentIdMap.TryGetValue(a.Id, out var outerMappedId))
            {
                return a with { Id = outerMappedId, EntityId = newEntityId, CategoryId = mappedCategory };
            }

            // fallback to key-based mapping
            if (afterAttachmentKeys.TryGetValue(key, out var mappedId))
            {
                return a with { Id = mappedId, EntityId = newEntityId, CategoryId = mappedCategory };
            }

            // no id mapping found for the attachment itself — still remap EntityId & CategoryId where possible
            return a with { EntityId = newEntityId, CategoryId = mappedCategory };
        }).ToList();

        // Remap postings references
        // Build lookup for after postings by composite key to remap posting Ids deterministically
        string PostingKey(FinanceManager.Domain.Postings.Posting.PostingBackupDto p, Snapshot snap)
            => $"{p.BookingDate.Ticks}|{(p is { } && ((dynamic)p).ValutaDate is DateTime vd ? vd.Ticks : 0)}|{p.Kind}|{p.SourceId}|{p.AccountId}|{p.RecipientName}|{p.Subject}|{p.Amount}";

        var afterPostingMap = new Dictionary<string, Guid>();
        foreach (var ap in after.Postings)
        {
            var key = PostingKey(ap, after);
            if (!afterPostingMap.ContainsKey(key)) afterPostingMap[key] = ap.Id;
        }

        var postings = before.Postings.Select(p =>
        {
            // remap referenced ids
            var accountId = p.AccountId.HasValue && accountIdMap.TryGetValue(p.AccountId.Value, out var na) ? na : p.AccountId;
            var contactId = p.ContactId.HasValue && contactIdMap.TryGetValue(p.ContactId.Value, out var nc) ? nc : p.ContactId;
            Guid? savingsPlanId = p.SavingsPlanId.HasValue && savingsPlanNameToId.ContainsKey(before.SavingsPlans.FirstOrDefault(sp => sp.Id == p.SavingsPlanId)?.Name ?? string.Empty) ? savingsPlanNameToId.GetValueOrDefault(before.SavingsPlans.FirstOrDefault(sp => sp.Id == p.SavingsPlanId)?.Name ?? string.Empty) : p.SavingsPlanId;
            Guid? securityId = null;
            if (p.SecurityId.HasValue)
            {
                var beforeSecName = before.Securities.FirstOrDefault(s => s.Id == p.SecurityId.Value)?.Name;
                if (!string.IsNullOrEmpty(beforeSecName) && securityNameToId.TryGetValue(beforeSecName, out var mappedSec)) securityId = mappedSec;
            }

            var adjusted = p with { AccountId = accountId, ContactId = contactId, SavingsPlanId = savingsPlanId, SecurityId = securityId };

            // build key using adjusted values (use account id as string)
            string key = $"{adjusted.BookingDate.Ticks}|{(adjusted is { } && ((dynamic)adjusted).ValutaDate is DateTime vdt ? vdt.Ticks : 0)}|{adjusted.Kind}|{adjusted.SourceId}|{adjusted.AccountId?.ToString() ?? string.Empty}|{adjusted.RecipientName ?? string.Empty}|{adjusted.Subject ?? string.Empty}|{adjusted.Amount}";
            if (afterPostingMap.TryGetValue(key, out var mappedPid))
            {
                adjusted = adjusted with { Id = mappedPid };
            }
            return adjusted;
        }).ToList();

        // Remap statement imports
        var statementImports = before.StatementImports.Select(si => si with { AccountId = accountIdMap.TryGetValue(si.AccountId, out var na) ? na : si.AccountId }).ToList();

        // Other lists: keep as-is, but update IDs where possible via maps
        var statementEntries = before.StatementEntries.ToList();


        // Remap StatementDrafts: update DetectedAccountId and align draft Ids to 'after' snapshot by OriginalFileName when possible
        var drafts = before.StatementDrafts.Select(d =>
        {
            var detected = d.DetectedAccountId.HasValue && accountIdMap.TryGetValue(d.DetectedAccountId.Value, out var na) ? na : d.DetectedAccountId;
            var newId = afterDraftNameToId.TryGetValue(d.OriginalFileName ?? string.Empty, out var mappedDraft) ? mappedDraft : d.Id;
            return d with { Id = newId, DetectedAccountId = detected };
        }).ToList();

        // Build before->after draft id map for remapping draft entries
        draftIdMap = new Dictionary<Guid, Guid>();
        foreach (var bd in before.StatementDrafts)
        {
            var mapped = afterDraftNameToId.TryGetValue(bd.OriginalFileName ?? string.Empty, out var aid) ? aid : bd.Id;
            draftIdMap[bd.Id] = mapped;
        }

        //Build lookup for after draft entries by composite key (DraftId + BookingDate + ValutaDate + Amount + Subject + RecipientName)
        string DraftEntryKey(FinanceManager.Domain.Statements.StatementDraftEntry.StatementDraftEntryBackupDto e)
            => $"{e.DraftId}|{e.BookingDate.Ticks}|{(e.ValutaDate.HasValue ? e.ValutaDate.Value.Ticks : 0)}|{e.Amount}|{e.Subject ?? string.Empty}|{e.RecipientName ?? string.Empty}";

        var afterDraftEntryMap = new Dictionary<string, Guid>();
        foreach (var ade in after.StatementDraftEntries)
        {
            var key = DraftEntryKey(ade);
            if (!afterDraftEntryMap.ContainsKey(key)) afterDraftEntryMap[key] = ade.Id;
        }

        // Remap before draft entries: update DraftId and Id where matching after entry exists
        var draftEntries = before.StatementDraftEntries.Select(e =>
        {
            var newDraftId = draftIdMap.TryGetValue(e.DraftId, out var nd) ? nd : e.DraftId;
            var key = $"{newDraftId}|{e.BookingDate.Ticks}|{(e.ValutaDate.HasValue ? e.ValutaDate.Value.Ticks : 0)}|{e.Amount}|{e.Subject ?? string.Empty}|{e.RecipientName ?? string.Empty}";
            var newId = afterDraftEntryMap.TryGetValue(key, out var aid) ? aid : e.Id;
            var newSavingPlanId = e.SavingsPlanId.HasValue && savingsPlanNameToId.ContainsKey(before.SavingsPlans.FirstOrDefault(sp => sp.Id == e.SavingsPlanId)?.Name ?? string.Empty) ? savingsPlanNameToId.GetValueOrDefault(before.SavingsPlans.FirstOrDefault(sp => sp.Id == e.SavingsPlanId)?.Name ?? string.Empty) : e.SavingsPlanId;
            var newContactId = e.ContactId.HasValue && contactIdMap.TryGetValue(e.ContactId.Value, out var nc) ? nc : e.ContactId;
            return e with { Id = newId, DraftId = newDraftId, SavingsPlanId = newSavingPlanId, ContactId = newContactId };
        }).ToList();

        var reportFavorites = before.ReportFavorites.ToList();
        var homeKpis = before.HomeKpis.ToList();
        var attachmentCategories = remappedAttachmentCategories;
        var notifications = before.Notifications.ToList();
        var accountShares = before.AccountShares.ToList();

        return new Snapshot(contacts, contactCategories, aliases, accounts, savingsPlanCategories, savingsPlans, securityCategories, securities, securityPrices, postings, statementImports, statementEntries, drafts, draftEntries, reportFavorites, homeKpis, attachmentCategories, attachments, notifications, accountShares, before.Aggregates);
    }

    // Helper used to resolve an entity's human-readable name inside a Snapshot (supports optional id maps)
    private static string? ResolveEntityNameForAttachment(FinanceManager.Domain.Attachments.AttachmentEntityKind kind, Guid entityId, Snapshot snap, Dictionary<Guid, Guid>? contactIdMap = null, Dictionary<Guid, Guid>? accountIdMap = null)
    {
        var mappedId = entityId;
        if (kind == FinanceManager.Domain.Attachments.AttachmentEntityKind.Contact && contactIdMap != null && contactIdMap.TryGetValue(entityId, out var mc)) mappedId = mc;
        if (kind == FinanceManager.Domain.Attachments.AttachmentEntityKind.Account && accountIdMap != null && accountIdMap.TryGetValue(entityId, out var ma)) mappedId = ma;

        return kind switch
        {
            FinanceManager.Domain.Attachments.AttachmentEntityKind.Contact => snap.Contacts.FirstOrDefault(c => c.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.Account => snap.Accounts.FirstOrDefault(a => a.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.ContactCategory => snap.ContactCategories.FirstOrDefault(cc => cc.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.SavingsPlan => snap.SavingsPlans.FirstOrDefault(sp => sp.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.SavingsPlanCategory => snap.SavingsPlanCategories.FirstOrDefault(spc => spc.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.Security => snap.Securities.FirstOrDefault(s => s.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.SecurityCategory => snap.SecurityCategories.FirstOrDefault(sc => sc.Id == mappedId)?.Name,
            FinanceManager.Domain.Attachments.AttachmentEntityKind.StatementDraft => snap.StatementDrafts.FirstOrDefault(d => d.Id == mappedId)?.OriginalFileName,
            _ => null,
        };
    }

    private static Snapshot SortSnapshot(Snapshot s)
    {
        return new Snapshot(
            s.Contacts.OrderBy(c => c.Name).ToList(),
            s.ContactCategories.OrderBy(c => c.Name).ToList(),
            s.AliasNames.OrderBy(a => a.Pattern).ToList(),
            s.Accounts.OrderBy(a => a.Name).ToList(),
            s.SavingsPlanCategories.OrderBy(c => c.Name).ToList(),
            s.SavingsPlans.OrderBy(sp => sp.Name).ToList(),
            s.SecurityCategories.OrderBy(c => c.Name).ToList(),
            s.Securities.OrderBy(sec => sec.Name).ToList(),
            s.SecurityPrices.OrderBy(p => p.Id).ToList(),
            s.Postings.OrderBy(p => p.Id).ToList(),
            s.StatementImports.OrderBy(i => i.OriginalFileName).ToList(),
            s.StatementEntries.OrderBy(e => e.BookingDate).ToList(),
            s.StatementDrafts.OrderBy(d => d.Id).ToList(),
            s.StatementDraftEntries.OrderBy(e => e.Id).ToList(),
            s.ReportFavorites.OrderBy(r => r.Name).ToList(),
            s.HomeKpis.OrderBy(h => h.SortOrder).ToList(),
            s.AttachmentCategories.OrderBy(ac => ac.Name).ToList(),
            s.Attachments.OrderBy(a => a.Id).ToList(),
            s.Notifications.OrderBy(n => n.Title).ToList(),
            s.AccountShares.OrderBy(sh => sh.AccountId).ToList(),
            s.Aggregates.OrderBy(a => a.EntityType).ThenBy(a => a.EntityName).ToList()
        );
    }
}
