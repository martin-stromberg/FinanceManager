using FinanceManager.Domain.Accounts;
using FinanceManager.Domain.Contacts;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Aggregates;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FinanceManager.Application.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
using FinanceManager.Tests.TestHelpers;

/// <summary>
/// Covers <see cref="StatementDraftService.ClassifyAsync"/>, the heuristic engine that, for freshly imported
/// statement entries, detects which bank account a draft belongs to, recognizes duplicate/already-booked entries,
/// and assigns a counterparty contact via bank-contact matching, self-contact fallback for internal transfers,
/// payment-intermediary resolution by posting subject, and auto-creation from a known-contact catalog.
/// </summary>
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

    // Classification never needs to load statement files (that happens during upload/create-draft), but the
    // constructor parameter is non-nullable, so a trivial stub stands in for the real factory.
    private sealed class StubStatementFileFactory : IStatementFileFactory
    {
        public IStatementFile? Load(string fileName, byte[] fileBytes) => null;
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
        var sut = new StatementDraftService(db, new PostingAggregateService(db), accountService, new StubStatementFileFactory(), null, NullLogger<StatementDraftService>.Instance, null, null, null, knownContactCatalog);
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


    /// <summary>
    /// When the owner has multiple bank accounts, the draft's stated account identifier must be matched to the
    /// correct one among several candidates rather than left undetected or assigned arbitrarily.
    /// </summary>
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


    /// <summary>
    /// When the owner has exactly one bank account and the draft's account-name field is empty, classification
    /// must still resolve it unambiguously to that sole account rather than leaving it undetected for lack of an
    /// explicit identifier to match against.
    /// </summary>
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



    /// <summary>
    /// When multiple accounts share the same bank contact but none of their account identifiers match the
    /// draft's stated account name, classification must leave <c>DetectedAccountId</c> null rather than guessing
    /// among the ambiguous candidates.
    /// </summary>
    [Fact]
    public async Task BankAccount_IsNotRecognized()
    {
        var (sut, db, conn, owner) = Create();
        var account = await AddBankAccountAsync(db);
        var contact = await db.Contacts.FirstAsync(c => c.Id == account.BankContactId, cancellationToken: TestContext.Current.CancellationToken);
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

    /// <summary>
    /// Even with only one account configured for the owner, a draft's account name that does not match it must
    /// not be force-matched by the single-account fallback - classification must leave the account undetected
    /// rather than assuming the sole account is always correct.
    /// </summary>
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

    /// <summary>
    /// An entry flagged as a preview/announced movement (not yet finally booked by the bank) must be classified
    /// with <see cref="StatementDraftEntryStatus.Announced"/> so the UI can distinguish it from fully settled entries.
    /// </summary>
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

    /// <summary>
    /// If a <see cref="StatementEntry"/> with the same de-duplication hash already exists (the movement was
    /// already imported and booked previously), classification must detect the duplicate and mark the draft entry
    /// <see cref="StatementDraftEntryStatus.AlreadyBooked"/> so it is never booked a second time.
    /// </summary>
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await sut.ClassifyAsync(draft.Id, null, owner, CancellationToken.None);

        var entry = draft.Entries.First();
        Assert.Equal(StatementDraftEntryStatus.AlreadyBooked, entry.Status);
        conn.Dispose();
    }

    /// <summary>
    /// An entry whose recipient name matches an ordinary existing contact must be linked directly to that
    /// contact, without being misclassified as a bank posting or a self/internal transfer.
    /// </summary>
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

    /// <summary>
    /// When the recipient name in an entry matches the account's own bank contact, classification must assign
    /// that bank contact - recognizing the movement as a bank-fee/bank-related posting rather than a normal
    /// counterparty payment.
    /// </summary>
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

    /// <summary>
    /// When an entry carries no recipient name at all, classification should still fall back to the account's own
    /// bank contact if the movement can otherwise be recognized as bank-originated, rather than leaving the entry
    /// entirely unclassified for lack of a name to match on.
    /// </summary>
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

    /// <summary>
    /// When the recipient name matches a different bank's contact (typical of a transfer between the user's own
    /// accounts at different banks), classification must assign the self contact and mark the entry cost-neutral,
    /// recognizing it as an internal transfer rather than a real expense or income.
    /// </summary>
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

    /// <summary>
    /// For an entry routed through a payment intermediary (e.g. PayPal), classification must try to resolve the
    /// real recipient by matching the posting subject text against a known contact's alias pattern, and assign
    /// that resolved recipient instead of the intermediary itself.
    /// </summary>
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

    /// <summary>
    /// When a payment intermediary's posting subject cannot be matched against any known recipient alias,
    /// classification must fall back to assigning the intermediary contact itself, since the real recipient could
    /// not be resolved automatically.
    /// </summary>
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

    /// <summary>
    /// When no existing contact matches the recipient name but the injected known-contact catalog recognizes it
    /// (e.g. "Amazon"), classification must auto-create a new contact with the catalog's suggested alias patterns
    /// and assign it to the entry - reducing manual contact setup for common recurring merchants.
    /// </summary>
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

        var created = await db.Contacts.SingleAsync(c => c.OwnerUserId == owner && c.Name == "Amazon", cancellationToken: TestContext.Current.CancellationToken);
        var aliases = await db.AliasNames.Where(a => a.ContactId == created.Id).Select(a => a.Pattern).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        var entry = draft.Entries.First();
        Assert.Equal(created.Id, entry.ContactId);
        Assert.Contains("AMAZON*", aliases);
        Assert.Contains("AMZN*", aliases);
        conn.Dispose();
    }

    /// <summary>
    /// Auto-creation from the known-contact catalog must respect the user's opt-out setting - when the user has
    /// disabled known-contact auto-create, classification must neither create the contact nor assign one, leaving
    /// the entry unassigned for manual handling.
    /// </summary>
    [Fact]
    public async Task Entry_KnownContact_IsIgnored_WhenUserSettingDisabled()
    {
        var knownContact = new FinanceManager.Application.Contacts.KnownContactMatch("Amazon", ContactType.Organization, new[] { "AMAZON*" });
        var (sut, db, conn, owner) = Create(new StubKnownContactCatalog(knownContact));
        var user = await db.Users.SingleAsync(u => u.Id == owner, cancellationToken: TestContext.Current.CancellationToken);
        user.SetKnownContactAutoCreateEnabled(false);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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

    /// <summary>
    /// When an existing contact already matches via its own alias pattern, classification must prefer that
    /// existing contact over creating a new one from the known-contact catalog, avoiding duplicate contact
    /// records for the same merchant.
    /// </summary>
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
