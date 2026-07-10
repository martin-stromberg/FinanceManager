using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Statements;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Application.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
using FinanceManager.Tests.TestHelpers;

public sealed class StatementDraftClassificationTests
{
    private sealed class StubKnownContactCatalog : FinanceManager.Application.Contacts.IKnownContactCatalog
    {
        private readonly FinanceManager.Application.Contacts.KnownContactMatch? _match;

        public StubKnownContactCatalog(FinanceManager.Application.Contacts.KnownContactMatch? match)
        {
            _match = match;
        }

        public Task<FinanceManager.Application.Contacts.KnownContactMatch?> FindMatchAsync(IEnumerable<string?> searchTexts, CancellationToken ct)
        {
            return Task.FromResult(_match);
        }
    }

    private static (StatementDraftService sut, AppDbContext db, SqliteConnection conn, Guid ownerId) Create(FinanceManager.Application.Contacts.IKnownContactCatalog? knownContactCatalog = null)
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        var owner = new FinanceManager.Domain.Users.User("owner", "hash", true);
        db.Users.Add(owner);
        db.SaveChanges();

        var ownerContact = new Contact(owner.Id, "Ich", ContactType.Self, null, null);
        db.Contacts.Add(ownerContact);
        db.SaveChanges();

        var accountService = new StubAccountService();
        var sut = new StatementDraftService(db, new PostingAggregateService(db), accountService, null, null, NullLogger<StatementDraftService>.Instance, null, null, null, knownContactCatalog);
        return (sut, db, conn, owner.Id);
    }


    private static async Task<Account> AddBankAccountAsync(AppDbContext db, Contact? bankContact = null)
    {
        var owner = db.Users.First().Id;
        var accountName = $"Konto {db.Accounts.Count() + 1}";
        var iban = $"DE123{db.Accounts.Count() + 1}";
        if (bankContact is null)
        {
            bankContact = new Contact(owner, $"Bank {db.Contacts.Count(c => c.Type == ContactType.Bank) + 1}", ContactType.Bank, null, null);
            db.Contacts.Add(bankContact);
            await db.SaveChangesAsync();
        }
        var account = new Account(owner, AccountType.Giro, accountName, iban, bankContact.Id);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }
    private static async Task<StatementDraft> CreateStatementDraftAsync(AppDbContext db, Account account, Action<StatementDraft> callback)
    {
        var owner = db.Users.First().Id;
        var draft = new StatementDraft(owner, "file.csv", account.Iban, null);
        callback(draft);
        db.StatementDrafts.Add(draft);
        await db.SaveChangesAsync();
        return draft;
    }

    private static async Task<Contact> AddContact(AppDbContext db, Guid owner, string name, ContactType contactType = ContactType.Person, Guid? categoryId = null, string? description = null, bool? isPaymentIntermediary = null, params string[] aliasNames)
    {
        var contact = new Contact(owner, name, contactType, categoryId, description, isPaymentIntermediary);
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        foreach (var alias in aliasNames)
        {
            db.AliasNames.Add(new AliasName(contact.Id, alias));
        }
        await db.SaveChangesAsync();
        return contact;
    }


    [Fact]
    public async Task BankAccount_IsRecognized()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", "Empf�nger", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        Assert.Equal(account.Id, draft.DetectedAccountId);
        conn.Dispose();
    }


    [Fact]
    public async Task BankAccount_IsRecognized_ForSingleAccount()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AccountName = "";
            draft.AddEntry(DateTime.Today, 100, "Test", "Empf�nger", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        Assert.Equal(account.Id, draft.DetectedAccountId);
        conn.Dispose();
    }



    [Fact]
    public async Task BankAccount_IsNotRecognized()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = await db.Contacts.FirstAsync(c => c.Id == account.BankContactId);
        account = await AddBankAccountAsync(db, contact);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AccountName = "DE456";
            draft.AddEntry(DateTime.Today, 100, "Test", "Empf�nger", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        Assert.Null(draft.DetectedAccountId);
        conn.Dispose();
    }

    [Fact]
    public async Task BankAccount_IsNotRecognized_ForSingleAccount()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AccountName = "DE456";
            draft.AddEntry(DateTime.Today, 100, "Test", "Empf�nger", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        Assert.Null(draft.DetectedAccountId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_IsAnnounced_StatusSet()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", "Empf�nger", DateTime.Today, "EUR", "Buchung", true);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(StatementDraftEntryStatus.Announced, entry.Status);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_AlreadyBooked_IsIgnored()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", "Empf�nger", DateTime.Today, "EUR", "Buchung", false);
        });
        db.StatementEntries.Add(new StatementEntry(Guid.NewGuid(), DateTime.Today, 100, "Test", "hash", "Empf�nger", DateTime.Today, "EUR", "Buchung", false, false));
        await db.SaveChangesAsync();

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(StatementDraftEntryStatus.AlreadyBooked, entry.Status);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_ContactAssigned_NotBankOrSelf()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = await AddContact(db, owner, "Max Mustermann");
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", contact.Name, DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(contact.Id, entry.ContactId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_BankContact_MatchesAccountBankContact()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = db.Contacts.First(c => c.Id == account.BankContactId);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", contact.Name, DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(contact.Id, entry.ContactId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_NoRecipientName_BankContactAssigned_IfRecognized()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = db.Contacts.First(c => c.Id == account.BankContactId);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", "", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(contact.Id, entry.ContactId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_BankContact_NotMatchingAccountBankContact_SelfContactAssigned()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var ownerContact = db.Contacts.First(c => c.Type == ContactType.Self);
        var contact = db.Contacts.First(c => c.Id == account.BankContactId);
        var otherContact = await AddContact(db, owner, "Andere Bank", ContactType.Bank);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Test", otherContact.Name, DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(ownerContact.Id, entry.ContactId);
        Assert.True(entry.IsCostNeutral);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_PaymentIntermediary_ContactFoundBySubject()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = db.Contacts.First(c => c.Id == account.BankContactId);
        var intermediary = await AddContact(db, owner, "PayPal", ContactType.Organization, null, null, true);
        var recipient = await AddContact(db, owner, "Max Mustermann", ContactType.Person, null, null, false, "Rechnung*");
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Rechnung 123", "PayPal", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(recipient.Id, entry.ContactId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_PaymentIntermediary_ContactNotFoundBySubject()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = db.Contacts.First(c => c.Id == account.BankContactId);
        var intermediary = await AddContact(db, owner, "PayPal", ContactType.Organization, null, null, true);
        var recipient = await AddContact(db, owner, "Max Mustermann", ContactType.Person, null, null, false, "Rechnung*");
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, 100, "Unbekannt", "PayPal", DateTime.Today, "EUR", "Buchung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(intermediary.Id, entry.ContactId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_KnownContact_IsCreatedAndAssigned_WhenNoExistingContactMatches()
    {
        var knownContact = new FinanceManager.Application.Contacts.KnownContactMatch("Amazon", ContactType.Organization, new[] { "AMAZON*", "AMZN*" });
        var (sut, db, conn, owner) = Create(new StubKnownContactCatalog(knownContact));
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, -25, "Bestellung 123", "AMAZON EU", DateTime.Today, "EUR", "Kartenzahlung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var created = await db.Contacts.SingleAsync(c => c.OwnerUserId == owner && c.Name == "Amazon");
        var aliases = await db.AliasNames.Where(a => a.ContactId == created.Id).Select(a => a.Pattern).ToListAsync();
        var entry = draft.Entries.First();
        Assert.Equal(created.Id, entry.ContactId);
        Assert.Contains("AMAZON*", aliases);
        Assert.Contains("AMZN*", aliases);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_KnownContact_IsIgnored_WhenUserSettingDisabled()
    {
        var knownContact = new FinanceManager.Application.Contacts.KnownContactMatch("Amazon", ContactType.Organization, new[] { "AMAZON*" });
        var (sut, db, conn, owner) = Create(new StubKnownContactCatalog(knownContact));
        var user = await db.Users.SingleAsync(u => u.Id == owner);
        user.SetKnownContactAutoCreateEnabled(false);
        await db.SaveChangesAsync();
        var account = await AddBankAccountAsync(db);
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, -25, "Bestellung 123", "AMAZON EU", DateTime.Today, "EUR", "Kartenzahlung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        Assert.DoesNotContain(db.Contacts, c => c.OwnerUserId == owner && c.Name == "Amazon");
        Assert.Null(draft.Entries.First().ContactId);
        conn.Dispose();
    }

    [Fact]
    public async Task Entry_ExistingContact_HasPriorityOverKnownContactCatalog()
    {
        var knownContact = new FinanceManager.Application.Contacts.KnownContactMatch("Amazon", ContactType.Organization, new[] { "AMAZON*" });
        var (sut, db, conn, owner) = Create(new StubKnownContactCatalog(knownContact));
        var account = await AddBankAccountAsync(db);
        var existing = await AddContact(db, owner, "Amazon Marketplace", ContactType.Organization, null, null, false, "AMAZON*");
        var draft = await CreateStatementDraftAsync(db, account, (draft) =>
        {
            draft.AddEntry(DateTime.Today, -25, "Bestellung 123", "AMAZON EU", DateTime.Today, "EUR", "Kartenzahlung", false);
        });

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        Assert.Equal(existing.Id, draft.Entries.First().ContactId);
        Assert.DoesNotContain(db.Contacts, c => c.OwnerUserId == owner && c.Name == "Amazon");
        conn.Dispose();
    }
}
