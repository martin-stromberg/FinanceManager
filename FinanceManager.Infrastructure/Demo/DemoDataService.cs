using DocumentFormat.OpenXml.Bibliography;
using FinanceManager.Application.Accounts;
using FinanceManager.Application.Attachments;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Demo;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Attachments;
using FinanceManager.Domain.Statements;
using FinanceManager.Infrastructure;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.Extensions.Logging;
using System;
using System.Formats.Asn1;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FinanceManager.Infrastructure.Demo;

/// <summary>
/// Demo data service implementation (skeleton).
/// Creates a small set of domain objects for demo purposes using application services.
/// The implementation is intentionally minimal and can be extended later to create richer demo datasets.
/// </summary>
public sealed class DemoDataService : IDemoDataService
{
    private readonly IAccountService _accountService;
    private readonly IContactService _contactService;
    private readonly IContactCategoryService _contactCategoryService;
    private readonly IAttachmentService _attachmentService;
    private readonly ISavingsPlanCategoryService _savingsPlanCategoryService;
    private readonly ISavingsPlanService _savingsPlanService;
    private readonly ISecurityService _securityService;
    private readonly ISecurityPriceService _securityPriceService;
    private readonly ISecurityCategoryService _securityCategoryService;
    private readonly ILogger<DemoDataService> _logger;
    private readonly IStatementDraftService _statementDraftService;
    private readonly IAttachmentCategoryService _attachmentCategoryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoDataService"/> class.
    /// </summary>
    /// <param name="accountService">Account service used to create accounts.</param>
    /// <param name="contactCategoryService">Contact category service used to create categories.</param>
    /// <param name="contactService">Contact service used to create contacts.</param>
    /// <param name="attachmentService">Attachment service used to upload symbol images.</param>
    /// <param name="savingsPlanCategoryService">Savings plan category service used to create savings plans.</param>
    /// <param name="savingsPlanService">Savings plan service used to create and manage savings plans.</param>
    /// <param name="securityCategoryService">Security category service used to create security categories.</param>
    /// <param name="securityService">Security service used to create and manage securities.</param>
    /// <param name="logger">Logger instance.</param>
    public DemoDataService(IAccountService accountService, IContactCategoryService contactCategoryService, IContactService contactService, IAttachmentService attachmentService, ISavingsPlanCategoryService savingsPlanCategoryService, ISavingsPlanService savingsPlanService, ISecurityCategoryService securityCategoryService, ISecurityService securityService, ISecurityPriceService securityPriceService, IStatementDraftService statementDraftService, IAttachmentCategoryService attachmentCategoryService, ILogger<DemoDataService> logger)
    {
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _contactCategoryService = contactCategoryService ?? throw new ArgumentNullException(nameof(contactCategoryService));
        _contactService = contactService ?? throw new ArgumentNullException(nameof(contactService));
        _attachmentService = attachmentService ?? throw new ArgumentNullException(nameof(attachmentService));
        _savingsPlanCategoryService = savingsPlanCategoryService ?? throw new ArgumentNullException(nameof(savingsPlanCategoryService));
        _savingsPlanService = savingsPlanService ?? throw new ArgumentNullException(nameof(savingsPlanService));
        _securityCategoryService = securityCategoryService ?? throw new ArgumentNullException(nameof(securityCategoryService));
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _securityPriceService = securityPriceService ?? throw new ArgumentNullException(nameof(securityPriceService));
        _statementDraftService = statementDraftService ?? throw new ArgumentNullException(nameof(statementDraftService));
        _attachmentCategoryService = attachmentCategoryService ?? throw new ArgumentNullException(nameof(attachmentCategoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates demo data for the specified user. Currently creates one Giro account and two Savings accounts.
    /// </summary>
    /// <param name="userId">Identifier of the user to create demo data for.</param>
    /// <param name="createPostings">Whether to create postings and statement imports for demo savings plans.</param>
    /// <param name="ct">Cancellation token.</param>    
    public async Task CreateDemoDataAsync(Guid userId, bool createPostings, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("userId required", nameof(userId));

        _logger.LogInformation("Creating demo data for user {UserId}", userId);

        try
        {
            // create attachment category for symbols and ensure attachments representing symbols are categorized
            var symbolsAttachmentCategory = await _attachmentCategoryService.CreateAsync(userId, "Symbole", ct);

            // create contact categories
            var banksCat = await _contactCategoryService.CreateAsync(userId, "Banken", ct);
            var insurancesCat = await _contactCategoryService.CreateAsync(userId, "Versicherungen", ct);
            var providersCat = await _contactCategoryService.CreateAsync(userId, "Dienstleister", ct);
            var retailCat = await _contactCategoryService.CreateAsync(userId, "Supermärkte & Einzelhandel", ct);

            var attachmentId = await CreateSvgSymbolAsync(userId, AttachmentEntityKind.ContactCategory, banksCat.Id, "bank-symbol.svg", banksCat.Name, symbolsAttachmentCategory.Id, ct);
            await _contactCategoryService.SetSymbolAttachmentAsync(banksCat.Id, userId, attachmentId, ct);

            // create two bank contacts: one for Giro, one for Savings
            var giroBank = await CreateContactAsync(userId, "Demo Giro Bank", ContactType.Bank, banksCat.Id, "Demo bank for giro account", false, "", ct);
            var savingsBank = await CreateContactAsync(userId, "Demo Savings Bank", ContactType.Bank, banksCat.Id, "Demo bank for savings accounts", false, "", ct);
            var contAldi = await CreateContactAsync(userId, "Aldi", ContactType.Organization, retailCat.Id, "Demo contact for grocery store", false, "*Aldi Payments*", ct);

            // create savings plan categories
            var spInsurances = await _savingsPlanCategoryService.CreateAsync(userId, "Versicherungen", ct);
            var spSparen = await _savingsPlanCategoryService.CreateAsync(userId, "Sparen", ct);
            var spRuckstellungen = await _savingsPlanCategoryService.CreateAsync(userId, "Rückstellungen", ct);

            // create security categories
            var secAktien = await _securityCategoryService.CreateAsync(userId, "Aktien", ct);
            var secFonds = await _securityCategoryService.CreateAsync(userId, "Fonds", ct);

            // create a sample security: MSCI World assigned to Fonds
            var msci = await _securityService.CreateAsync(userId, "MSCI World", "MSCIW", "Global equity index fund", null, "USD", secFonds.Id, ct);
            // assign a symbol to the security
            var msciSymbolId = await CreateSvgSymbolAsync(userId, AttachmentEntityKind.Security, msci.Id, "msci-symbol.svg", msci.Name, symbolsAttachmentCategory.Id, ct);
            await _securityService.SetSymbolAttachmentAsync(msci.Id, userId, msciSymbolId, ct);

            // create monthly security prices for the past 24 months (demo data)
            await CreateSecurityPrices(userId, msci, ct);

            // create three savings plans
            // 1) Urlaubskasse, type Open
            var sp1 = await _savingsPlanService.CreateAsync(userId, "Urlaubskasse", SavingsPlanType.Open, null, null, null, spSparen.Id, null, ct);

            // 2) KFZ Versicherung, recurring annually, target in three months, amount 432
            var threeMonths = DateTime.UtcNow.Date.AddMonths(3);
            var kfz = await _savingsPlanService.CreateAsync(userId, "KFZ Versicherung", SavingsPlanType.Recurring, 432m, threeMonths, SavingsPlanInterval.Annually, spInsurances.Id, null, ct);

            // 3) Auto, target in 10 years, amount 12500
            var tenYears = DateTime.UtcNow.Date.AddYears(10);
            var sp3 = await _savingsPlanService.CreateAsync(userId, "Auto", SavingsPlanType.OneTime, 12500m, tenYears, null, spRuckstellungen.Id, null, ct);

            // Create one Giro (checking) account
            var accGiro = await _accountService.CreateAsync(
                ownerUserId: userId,
                name: "Demo Giro Account",
                type: AccountType.Giro,
                iban: "DE00DEMO0000000001",
                bankContactId: giroBank.Id,
                expectation: SavingsPlanExpectation.Optional,
                ct: ct);

            // Create two savings accounts
            var accSave1 = await _accountService.CreateAsync(
                ownerUserId: userId,
                name: "Demo Savings 1",
                type: AccountType.Savings,
                iban: "DE00DEMO0000000002",
                bankContactId: savingsBank.Id,
                expectation: SavingsPlanExpectation.None,
                ct: ct);

            var accSave2 = await _accountService.CreateAsync(
                ownerUserId: userId,
                name: "Demo Savings 2",
                type: AccountType.Savings,
                iban: "DE00DEMO0000000003",
                bankContactId: savingsBank.Id,
                expectation: SavingsPlanExpectation.None,
                ct: ct);

            // if requested, create statement draft and entries for KFZ Versicherung
            if (createPostings)
            {
                // determine accounts to attach imports to
                var giroAccountId = accGiro.Id;
                var savingsAccountId = accSave1.Id;
                var savingsAccountId2 = accSave2.Id;

                // debit side on Giro: negative amounts, assign to savings plan
                await CreateDemoPostingsForInsurance(userId, kfz, giroAccountId, positive: false, assignSavingsPlan: true, ct);

                // credit side on Savings account: positive amounts, do NOT assign to savings plan
                await CreateDemoPostingsForInsurance(userId, kfz, savingsAccountId, positive: true, assignSavingsPlan: false, ct);

                // also create vacation savings postings: 15 months of 100€ each
                await CreateDemoPostingsForSavingsPlan(userId, sp1, giroAccountId, savingsAccountId2, 100m, 15, ct);
                // create car savings postings: 12 months of 150€ each
                await CreateDemoPostingsForSavingsPlan(userId, sp3, giroAccountId, savingsAccountId2, 150m, 12, ct);

                // create unbooked monthly account statements for the current month
                await CreateMonthlyUnbookedStatementsAsync(userId, accGiro.Id, savingsAccountId, savingsAccountId2, sp1, kfz, sp3, contAldi, ct);
            }

            _logger.LogInformation("Demo data creation finished for user {UserId}", userId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Demo data creation cancelled for user {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating demo data for user {UserId}", userId);
            throw;
        }
    }

    private async Task CreateSecurityPrices(Guid userId, SecurityDto msci, CancellationToken ct)
    {
        // deterministic random seed based on security id to keep demo reproducible
        var rnd = new Random(msci.Id.GetHashCode());
        decimal basePrice = 100m; // starting base price for demo
        for (int monthsAgo = 24; monthsAgo >= 1; monthsAgo--)
        {
            var priceDate = DateTime.UtcNow.Date.AddMonths(-monthsAgo);
            // simulate small monthly change within ±5%
            var change = (decimal)(rnd.NextDouble() * 0.10 - 0.05);
            basePrice = Math.Max(0.01m, Math.Round(basePrice * (1 + change), 2));
            await _securityPriceService.CreateAsync(userId, msci.Id, priceDate, basePrice, ct);
        }
    }

    private async Task<ContactDto> CreateContactAsync(Guid userId, string name, ContactType contactType, Guid categoryId, string description, bool isPaymentIntermediary, string secondaryAlias, CancellationToken ct)
    {
        var contact = await _contactService.CreateAsync(userId, name, contactType, categoryId, description, isPaymentIntermediary, ct);
        await _contactService.AddAliasAsync(contact.Id, userId, $"*{name.ToLowerInvariant().Replace(" ", ".")}*", ct);
        if (!string.IsNullOrWhiteSpace(secondaryAlias))
            await _contactService.AddAliasAsync(contact.Id, userId, secondaryAlias, ct);
        return contact;
    }
    private async Task CreateDemoPostingsForInsurance(Guid userId, SavingsPlanDto? kfz, Guid accountId, bool positive, bool assignSavingsPlan, CancellationToken ct)
    {
        // ensure Self contact exists
        var selfList = await _contactService.ListAsync(userId, 0, 10, ContactType.Self, null, ct);
        var self = selfList.Count > 0 ? selfList[0] : await _contactService.CreateAsync(userId, "Self", ContactType.Self, null, null, false, ct);

        // create empty draft (acts as account statement container)
        var draft = await _statementDraftService.CreateEmptyDraftAsync(userId, positive ? "demo_kfz_credit.csv" : "demo_kfz_debit.csv", ct);
        if (draft == null) throw new InvalidOperationException("Failed to create statement draft");
        await _statementDraftService.SetAccountAsync(draft.DraftId, userId, accountId, ct);

        // create 9 monthly entries for past 9 months
        var monthly = (kfz.TargetAmount ?? 0m) / 12m;
        var amount = positive ? monthly : -monthly;
        for (int i = 1; i <= 9; i++)
        {
            var bookingDate = DateTime.UtcNow.Date.AddMonths(-i);
            await _statementDraftService.AddEntryAsync(draft.DraftId, userId, bookingDate, amount, $"Rueckstellung {kfz.Name}", ct);
        }

        // update entries with recipient name and booking description
        var entries = (await _statementDraftService.GetDraftEntriesAsync(draft.DraftId, ct)).ToList();
        foreach (var e in entries)
        {
            await _statementDraftService.UpdateEntryCoreAsync(draft.DraftId, e.Id, userId, e.BookingDate, e.BookingDate, e.Amount, e.Subject, self.Name, "EUR", $"Rueckstellung {kfz.Name}", ct);
        }

        // run classification to attempt auto-matching
        await _statementDraftService.ClassifyAsync(draft.DraftId, null, userId, ct);

        // validate draft and ensure no errors
        var validation = await _statementDraftService.ValidateAsync(draft.DraftId, null, userId, ct);
        if (!validation.IsValid)
        {
            // if any error, log and throw to surface problem in demo setup
            _logger.LogError("Draft validation failed for demo draft {DraftId}: {Messages}", draft.DraftId, string.Join(";", validation.Messages.Select(m => m.Message)));
            throw new InvalidOperationException("Draft validation reported errors during demo data creation");
        }

        // when assignment requested ensure assignment exists, otherwise ensure none are assigned
        entries = (await _statementDraftService.GetDraftEntriesAsync(draft.DraftId, ct)).ToList();
        if (assignSavingsPlan)
        {
            if (entries.Any(e => e.SavingsPlanId is null || e.SavingsPlanId != kfz.Id))
            {
                _logger.LogError("Draft entries not assigned to savings plan as expected");
                throw new InvalidOperationException("Draft entries not assigned to savings plan as expected");
            }
        }
        else
        {
            if (entries.Any(e => e.SavingsPlanId is not null))
            {
                _logger.LogError("Draft entries unexpectedly assigned to savings plan");
                throw new InvalidOperationException("Draft entries must not be assigned to savings plans");
            }
        }

        var bookingResult = await _statementDraftService.BookAsync(draft.DraftId, null, userId, true, ct);
        if (!bookingResult.Success)
        {
            _logger.LogError("Booking failed for demo draft {DraftId}", draft.DraftId);
            throw new InvalidOperationException("Booking failed during demo data creation");
        }

        if (assignSavingsPlan)
        {
            kfz = await _savingsPlanService.GetAsync(kfz.Id, userId, ct);
            var expectedRemaining = kfz.TargetAmount - (9 * monthly);
            if (kfz.RemainingAmount != expectedRemaining)
            {
                _logger.LogError("No postings created for demo savings plan {SavingsPlanId}", kfz.Id);
                throw new InvalidOperationException("No postings created for demo savings plan");
            }
        }
    }

    private async Task CreateDemoPostingsForSavingsPlan(Guid userId, SavingsPlanDto? plan, Guid accountIdDebit, Guid accountIdCredit, decimal monthlyAmount, int months, CancellationToken ct)
    {
        // ensure Self contact exists
        var selfList = await _contactService.ListAsync(userId, 0, 10, ContactType.Self, null, ct);
        var self = selfList.Count > 0 ? selfList[0] : await _contactService.CreateAsync(userId, "Self", ContactType.Self, null, null, false, ct);

        // DEBIT draft on Giro (negative amounts) assigned to savings plan
        var debitDraft = await _statementDraftService.CreateEmptyDraftAsync(userId, $"demo_{plan?.Name}_{months}months_debit.csv", ct);
        if (debitDraft == null) throw new InvalidOperationException("Failed to create debit statement draft for vacation savings");
        debitDraft = await _statementDraftService.SetAccountAsync(debitDraft.DraftId, userId, accountIdDebit, ct);

        for (int i = 1; i <= months; i++)
        {
            var bookingDate = DateTime.UtcNow.Date.AddMonths(-i);
            await _statementDraftService.AddEntryAsync(debitDraft.DraftId, userId, bookingDate, -monthlyAmount, $"{plan.Name} Einzahlung", ct);
        }

        var debitEntries = (await _statementDraftService.GetDraftEntriesAsync(debitDraft.DraftId, ct)).ToList();
        foreach (var e in debitEntries)
        {
            await _statementDraftService.UpdateEntryCoreAsync(debitDraft.DraftId, e.Id, userId, e.BookingDate, e.BookingDate, e.Amount, e.Subject, self.Name, "EUR", $"{plan.Name}", ct);
        }

        await _statementDraftService.ClassifyAsync(debitDraft.DraftId, null, userId, ct);
        var debitValidation = await _statementDraftService.ValidateAsync(debitDraft.DraftId, null, userId, ct);
        if (!debitValidation.IsValid)
        {
            _logger.LogError("Debit draft (vacation) validation failed for demo draft {DraftId}: {Messages}", debitDraft.DraftId, string.Join(";", debitValidation.Messages.Select(m => m.Message)));
            throw new InvalidOperationException("Debit draft (vacation) validation reported errors during demo data creation");
        }

        // ensure assignment to savings plan
        debitEntries = (await _statementDraftService.GetDraftEntriesAsync(debitDraft.DraftId, ct)).ToList();
        if (debitEntries.Any(e => e.SavingsPlanId is null || e.SavingsPlanId != plan.Id))
        {
            _logger.LogError("Debit draft entries (vacation) not assigned to savings plan as expected");
            throw new InvalidOperationException("Debit draft entries (vacation) not assigned to savings plan as expected");
        }

        var debitBooking = await _statementDraftService.BookAsync(debitDraft.DraftId, null, userId, true, ct);
        if (!debitBooking.Success)
        {
            _logger.LogError("Booking failed for debit vacation draft {DraftId}", debitDraft.DraftId);
            throw new InvalidOperationException("Booking failed during demo data creation (vacation debit)");
        }

        // CREDIT draft on savings account (positive amounts), must NOT be assigned to savings plan
        var creditDraft = await _statementDraftService.CreateEmptyDraftAsync(userId, $"demo_{plan?.Name}_{months}months_credit.csv", ct);
        if (creditDraft == null) throw new InvalidOperationException("Failed to create credit statement draft for vacation savings");
        creditDraft = await _statementDraftService.SetAccountAsync(creditDraft.DraftId, userId, accountIdCredit, ct);

        for (int i = 1; i <= months; i++)
        {
            var bookingDate = DateTime.UtcNow.Date.AddMonths(-i);
            await _statementDraftService.AddEntryAsync(creditDraft.DraftId, userId, bookingDate, monthlyAmount, $"Gegenbuchung {plan.Name}", ct);
        }

        var creditEntries = (await _statementDraftService.GetDraftEntriesAsync(creditDraft.DraftId, ct)).ToList();
        foreach (var e in creditEntries)
        {
            await _statementDraftService.UpdateEntryCoreAsync(creditDraft.DraftId, e.Id, userId, e.BookingDate, e.BookingDate, e.Amount, e.Subject, self.Name, "EUR", $"Gegenbuchung {plan.Name}", ct);
        }

        await _statementDraftService.ClassifyAsync(creditDraft.DraftId, null, userId, ct);
        var creditValidation = await _statementDraftService.ValidateAsync(creditDraft.DraftId, null, userId, ct);
        if (!creditValidation.IsValid)
        {
            _logger.LogError("Credit draft (vacation) validation failed for demo draft {DraftId}: {Messages}", creditDraft.DraftId, string.Join(";", creditValidation.Messages.Select(m => m.Message)));
            throw new InvalidOperationException("Credit draft (vacation) validation reported errors during demo data creation");
        }

        creditEntries = (await _statementDraftService.GetDraftEntriesAsync(creditDraft.DraftId, ct)).ToList();
        if (creditEntries.Any(e => e.SavingsPlanId is not null))
        {
            _logger.LogError("Credit draft entries (vacation) unexpectedly assigned to savings plan");
            throw new InvalidOperationException("Credit draft entries (vacation) must not be assigned to savings plans");
        }

        var creditBooking = await _statementDraftService.BookAsync(creditDraft.DraftId, null, userId, true, ct);
        if (!creditBooking.Success)
        {
            _logger.LogError("Booking failed for credit vacation draft {DraftId}", creditDraft.DraftId);
            throw new InvalidOperationException("Booking failed during demo data creation (vacation credit)");
        }

        plan = await _savingsPlanService.GetAsync(plan.Id, userId, ct);
        if (plan.CurrentAmount != months * monthlyAmount)
        {
            _logger.LogError("No postings created for demo savings plan {SavingsPlanId}", plan.Id);
            throw new InvalidOperationException($"No postings created for demo savings plan ({plan.Name})");
        }
    }

    private async Task<Guid> CreateSvgSymbolAsync(Guid ownerUserId, AttachmentEntityKind kind, Guid entityId, string fileName, string displayName, Guid? categoryId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        displayName = (displayName ?? string.Empty).Trim();
        var letter = 'B';
        if (!string.IsNullOrEmpty(displayName))
        {
            letter = char.ToUpperInvariant(displayName[0]);
        }

        var svg = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                  "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64\" height=\"64\" viewBox=\"0 0 64 64\">" +
                  "<circle cx=\"32\" cy=\"32\" r=\"30\" fill=\"#2b6cb0\"/>" +
                  $"<text x=\"32\" y=\"38\" font-size=\"28\" text-anchor=\"middle\" fill=\"#ffffff\" font-family=\"Arial, Helvetica, sans-serif\">{letter}</text>" +
                  "</svg>";

        var svgBytes = Encoding.UTF8.GetBytes(svg);
        await using var ms = new MemoryStream(svgBytes);
        var attachment = await _attachmentService.UploadAsync(ownerUserId, kind, entityId, ms, fileName, "image/svg+xml", categoryId, ct);
        return attachment.Id;
    }

    private async Task CreateMonthlyUnbookedStatementsAsync(Guid userId, Guid giroAccountId, Guid savingsAccountId, Guid savingsAccountId2, SavingsPlanDto sp1, SavingsPlanDto kfz, SavingsPlanDto sp3, ContactDto contAldi, CancellationToken ct)
    {
        // ensure Self contact exists
        var selfList = await _contactService.ListAsync(userId, 0, 10, ContactType.Self, null, ct);
        var self = selfList.Count > 0 ? selfList[0] : await _contactService.CreateAsync(userId, "Self", ContactType.Self, null, null, false, ct);

        var firstOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        // Giro account draft
        var giroDraft = await _statementDraftService.CreateEmptyDraftAsync(userId, "demo_monthly_giro.csv", ct);
        if (giroDraft == null) throw new InvalidOperationException("Failed to create giro statement draft");
        await _statementDraftService.SetAccountAsync(giroDraft.DraftId, userId, giroAccountId, ct);

        // Add entries on giro (negative amounts)
        await _statementDraftService.AddEntryAsync(giroDraft.DraftId, userId, firstOfMonth, -100m, $"Rueckstellung {sp1.Name}", ct);
        await _statementDraftService.AddEntryAsync(giroDraft.DraftId, userId, firstOfMonth, -150m, $"Rueckstellung {sp3.Name}", ct);
        var kfzMonthly = (kfz.TargetAmount ?? 0m) / 12m;
        await _statementDraftService.AddEntryAsync(giroDraft.DraftId, userId, firstOfMonth, -kfzMonthly, $"Rueckstellung {kfz.Name}", ct);
        await _statementDraftService.AddEntryAsync(giroDraft.DraftId, userId, firstOfMonth, -32.95m, "VISA Aldi", ct);
        await _statementDraftService.AddEntryAsync(giroDraft.DraftId, userId, firstOfMonth, -16.20m, "Lastschrift Lidl", ct);

        // Update entries: set recipient and description
        var giroEntries = (await _statementDraftService.GetDraftEntriesAsync(giroDraft.DraftId, ct)).ToList();
        foreach (var e in giroEntries)
        {
            var desc = e.Subject;
            var recipient = self.Name;
            if (e.Subject?.Contains("Aldi", StringComparison.OrdinalIgnoreCase) == true) recipient = contAldi.Name;
            await _statementDraftService.UpdateEntryCoreAsync(giroDraft.DraftId, e.Id, userId, e.BookingDate, e.BookingDate, e.Amount, e.Subject, recipient, "EUR", desc, ct);
        }

        // Savings account 2: credits for sp1 and sp3 (counter bookings)
        var save2Draft = await _statementDraftService.CreateEmptyDraftAsync(userId, "demo_monthly_save2.csv", ct);
        if (save2Draft == null) throw new InvalidOperationException("Failed to create savings account 2 draft");
        await _statementDraftService.SetAccountAsync(save2Draft.DraftId, userId, savingsAccountId2, ct);
        await _statementDraftService.AddEntryAsync(save2Draft.DraftId, userId, firstOfMonth, 100m, $"Gegenbuchung {sp1.Name}", ct);
        await _statementDraftService.AddEntryAsync(save2Draft.DraftId, userId, firstOfMonth, 150m, $"Gegenbuchung {sp3.Name}", ct);
        var save2Entries = (await _statementDraftService.GetDraftEntriesAsync(save2Draft.DraftId, ct)).ToList();
        foreach (var e in save2Entries)
        {
            await _statementDraftService.UpdateEntryCoreAsync(save2Draft.DraftId, e.Id, userId, e.BookingDate, e.BookingDate, e.Amount, e.Subject, self.Name, "EUR", e.Subject, ct);
        }

        // Savings account 1: credit for kfz
        var save1Draft = await _statementDraftService.CreateEmptyDraftAsync(userId, "demo_monthly_save1.csv", ct);
        if (save1Draft == null) throw new InvalidOperationException("Failed to create savings account 1 draft");
        await _statementDraftService.SetAccountAsync(save1Draft.DraftId, userId, savingsAccountId, ct);
        await _statementDraftService.AddEntryAsync(save1Draft.DraftId, userId, firstOfMonth, kfzMonthly, $"Gegenbuchung {kfz.Name}", ct);
        var save1Entries = (await _statementDraftService.GetDraftEntriesAsync(save1Draft.DraftId, ct)).ToList();
        foreach (var e in save1Entries)
        {
            await _statementDraftService.UpdateEntryCoreAsync(save1Draft.DraftId, e.Id, userId, e.BookingDate, e.BookingDate, e.Amount, e.Subject, self.Name, "EUR", e.Subject, ct);
        }

        await _statementDraftService.ClassifyAsync(giroDraft.DraftId, null, userId, ct);
        await _statementDraftService.ClassifyAsync(save1Draft.DraftId, null, userId, ct);
        await _statementDraftService.ClassifyAsync(save2Draft.DraftId, null, userId, ct);
    }
}
