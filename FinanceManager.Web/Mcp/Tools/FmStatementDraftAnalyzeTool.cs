using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Application.Savings;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Accounts;
using FinanceManager.Shared.Dtos.Contacts;
using FinanceManager.Shared.Dtos.Postings;
using FinanceManager.Shared.Dtos.SavingsPlans;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using System.Text.Json;
using IPostingsQueryService = FinanceManager.Application.Postings.IPostingsQueryService;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the suggestion tool <c>fm_statement_draft_analyze</c> (T15): read-only
/// analysis of a statement draft — classification prediction (contact/savings-plan/
/// security matching via <c>AliasName.Pattern</c> and <c>IKnownContactCatalog</c>),
/// duplicate findings (<c>AlreadyBooked</c> pipeline verdict plus date/amount/subject
/// heuristics) and the list of missing assignments per entry. Emits a suggestion report —
/// never persists (EP-04 core invariant; the persisting <c>ClassifyAsync</c> is
/// deliberately not called). Scopes: (Kontoauszugsentwürfe, Vorschlag); reads additionally
/// (Kontakte, Lesen), (Sparpläne, Lesen), (Wertpapiere, Lesen), (Konten, Lesen) for
/// suggestion targets.
/// </summary>
public sealed class FmStatementDraftAnalyzeDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_statement_draft_analyze";

    /// <summary>Creates the descriptor.</summary>
    public FmStatementDraftAnalyzeDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"draftId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Draft to analyze (required).\"},\"entryId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single entry restriction.\"},\"entrySkip\":{\"type\":\"integer\",\"minimum\":0},\"entryTake\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"required\":[\"draftId\"],\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Analyzes a statement draft without persisting: predicted contact/savings-plan/security assignments (alias + known-contact matching), duplicate findings and missing assignments per entry — suggestion report only. Scopes: (Kontoauszugsentwürfe, Vorschlag) + read on Kontakte/Sparpläne/Wertpapiere/Konten.";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Vorschlagend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.StatementDrafts, McpRightKind.Suggest),
        new McpToolScope(McpDataType.Contacts, McpRightKind.Read),
        new McpToolScope(McpDataType.SavingsPlans, McpRightKind.Read),
        new McpToolScope(McpDataType.Securities, McpRightKind.Read),
        new McpToolScope(McpDataType.Accounts, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_statement_draft_analyze</c>: replicates the classification heuristics
/// of <c>StatementDraftService.Classification.cs</c> as a pure, non-mutating prediction
/// over service-contract data (Kontakt-Matching via Name/Alias, KnownContactCatalog,
/// Security-/Sparplan-Auto-Matching) and reports duplicates per the two-stage model of
/// toolkatalog §4.3. External duplicate sources (bank postings, committed statement
/// entries) are consulted only when the access additionally grants their read scopes —
/// the report then marks the heuristic coverage in <c>heuristicScope</c>. Suggested target
/// entities are record-filtered like read output, so a suggestion never leaks a record
/// the client may not see. No write path is invoked.
/// </summary>
public sealed class FmStatementDraftAnalyzeHandler : IMcpToolHandler
{
    /// <summary>Upper bound of the contact catalog loaded for matching (paged).</summary>
    private const int MaxContacts = 500;

    /// <summary>Upper bound of committed imports scanned for the duplicate heuristic.</summary>
    private const int MaxImportsScanned = 20;

    private readonly IStatementDraftService _drafts;
    private readonly IContactService _contacts;
    private readonly ISavingsPlanService _savingsPlans;
    private readonly ISecurityService _securities;
    private readonly IAccountService _accounts;
    private readonly IPostingsQueryService _postings;
    private readonly IStatementImportQueryService _imports;
    private readonly IKnownContactCatalog _knownContacts;
    private readonly IMcpScopeEvaluator _scopeEvaluator;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmStatementDraftAnalyzeHandler(
        IStatementDraftService drafts,
        IContactService contacts,
        ISavingsPlanService savingsPlans,
        ISecurityService securities,
        IAccountService accounts,
        IPostingsQueryService postings,
        IStatementImportQueryService imports,
        IKnownContactCatalog knownContacts,
        IMcpScopeEvaluator scopeEvaluator,
        McpRecordScopeFilter recordFilter)
    {
        _drafts = drafts;
        _contacts = contacts;
        _savingsPlans = savingsPlans;
        _securities = securities;
        _accounts = accounts;
        _postings = postings;
        _imports = imports;
        _knownContacts = knownContacts;
        _scopeEvaluator = scopeEvaluator;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmStatementDraftAnalyzeDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var draftId = McpToolArguments.RequiredGuid(arguments, "draftId");
        var entryId = McpToolArguments.OptionalGuid(arguments, "entryId");
        var entrySkip = Math.Max(0, McpToolArguments.OptionalInt(arguments, "entrySkip") ?? 0);
        var entryTake = Math.Clamp(McpToolArguments.OptionalInt(arguments, "entryTake") ?? 200, 1, McpToolArguments.MaxTake);

        var header = await _drafts.GetDraftHeaderAsync(draftId, owner, ct);
        if (header is null)
        {
            return new { draftId, analysis = (object?)null };
        }

        // Record-level check against the Suggest-right scope rows of the draft datatype.
        var draftScopes = await _recordFilter.GetScopesAsync(
            context.Access, McpDataType.StatementDrafts, McpRightKind.Suggest, ct);
        if (!await DraftVisibleAsync(header, draftScopes, owner, ct))
        {
            return new { draftId, analysis = (object?)null };
        }

        // Suggestion-target catalogs — record-filtered like read output so suggestions
        // never reference records the client may not see.
        var accounts = await _recordFilter.ApplyAsync(context.Access, McpDataType.Accounts,
            await _accounts.ListAsync(owner, 0, McpToolArguments.MaxTake, null, null, ct),
            a => new McpRecordContext { AccountTypeValue = (short)a.Type }, ct);
        var contacts = await LoadContactsAsync(context, owner, ct);
        var plans = await _recordFilter.ApplyAsync(context.Access, McpDataType.SavingsPlans,
            await _savingsPlans.ListAsync(owner, onlyActive: true, ct),
            p => new McpRecordContext { SavingsPlanCategoryId = p.CategoryId }, ct);
        var securities = (await _recordFilter.ApplyAsync(context.Access, McpDataType.Securities,
                await _securities.ListAsync(owner, onlyActive: true, ct),
                s => new McpRecordContext { SecurityCategoryId = s.CategoryId }, ct))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();

        var account = accounts.FirstOrDefault(a => a.Id == header.DetectedAccountId);
        var selfContact = contacts.FirstOrDefault(c => c.Type == ContactType.Self);
        var bankContactId = account?.BankContactId;
        var contactTuples = contacts.Select(c => (c.Id, c.Name)).ToList();

        var allEntries = (await _drafts.GetDraftEntriesAsync(draftId, ct)).ToList();
        var scopedEntries = allEntries
            .Where(e => !entryId.HasValue || e.Id == entryId.Value)
            .Skip(entrySkip).Take(entryTake).ToList();

        // Conditional heuristic scope (toolkatalog §4.3 step 2): external duplicate sources
        // only when their read scopes are granted — declared scopes stay as catalogued.
        var access = context.Access.Access;
        var postingsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.Postings, McpRightKind.Read, ct);
        var importsAllowed = access is not null
            && await _scopeEvaluator.IsAllowedAsync(access, McpDataType.StatementImports, McpRightKind.Read, ct);

        IReadOnlyList<PostingServiceDto> bankPosts = Array.Empty<PostingServiceDto>();
        var importEntries = new List<(Guid ImportId, StatementImportEntryDto Entry)>();
        var oldest = allEntries.Count == 0 ? (DateTime?)null : allEntries.Min(e => e.BookingDate).Date;
        if (oldest.HasValue && account is not null && postingsAllowed)
        {
            var posts = await _postings.GetAccountPostingsAsync(account.Id, 0, McpToolArguments.MaxTake, null, oldest, null, owner, ct);
            bankPosts = await _recordFilter.ApplyAsync(context.Access, McpDataType.Postings, posts,
                _ => new McpRecordContext { AccountTypeValue = (short)account.Type }, ct);
        }
        if (oldest.HasValue && importsAllowed)
        {
            var imports = await _imports.ListImportsAsync(owner, account?.Id, null, null, 0, MaxImportsScanned, ct);
            foreach (var import in imports)
            {
                var entries = await _imports.ListEntriesAsync(owner, import.Id, 0, McpToolArguments.MaxTake, ct);
                importEntries.AddRange(entries.Where(e => e.BookingDate >= oldest.Value)
                    .Select(e => (import.Id, e)));
            }
        }

        var needsAliases = scopedEntries.Any(e => e.Status is not StatementDraftEntryStatus.AlreadyBooked
                                                   and not StatementDraftEntryStatus.Announced
                                                   && e.ContactId is null);
        var aliasLookup = needsAliases
            ? await LoadAliasesAsync(contacts, owner, ct)
            : new Dictionary<Guid, IReadOnlyList<string>>();

        var analysisEntries = new List<object>(scopedEntries.Count);
        foreach (var e in scopedEntries)
        {
            if (!await EntryVisibleAsync(e, header.DetectedAccountId, draftScopes, contacts, plans, securities, owner, ct))
            {
                continue;
            }

            var suspects = new List<object>();
            var pipelineFlagged = e.Status == StatementDraftEntryStatus.AlreadyBooked;
            if (!pipelineFlagged)
            {
                // Stage-2 heuristic (§4.3): same booking date + identical amount + equal
                // subject — Verdachtshinweis, no automatic action.
                foreach (var other in allEntries)
                {
                    if (other.Id != e.Id
                        && other.BookingDate.Date == e.BookingDate.Date
                        && other.Amount == e.Amount
                        && string.Equals(other.Subject, e.Subject, StringComparison.OrdinalIgnoreCase))
                    {
                        suspects.Add(Suspect("draftEntry", other.Id, other.BookingDate, other.Amount, other.Subject, other.RecipientName));
                    }
                }
                foreach (var p in bankPosts)
                {
                    if (p.BookingDate.Date == e.BookingDate.Date
                        && p.Amount == e.Amount
                        && string.Equals(p.Subject, e.Subject, StringComparison.OrdinalIgnoreCase))
                    {
                        suspects.Add(Suspect("posting", p.Id, p.BookingDate, p.Amount, p.Subject, p.RecipientName));
                    }
                }
                foreach (var (_, entry) in importEntries)
                {
                    if (entry.BookingDate.Date == e.BookingDate.Date
                        && entry.Amount == e.Amount
                        && string.Equals(entry.Subject, e.Subject, StringComparison.OrdinalIgnoreCase))
                    {
                        suspects.Add(Suspect("importEntry", entry.Id, entry.BookingDate, entry.Amount, entry.Subject, entry.RecipientName));
                    }
                }
            }

            var suggestions = new List<object>();
            var missing = new List<string>();
            Guid? suggestedSecurityId = null;
            var skipped = e.Status switch
            {
                StatementDraftEntryStatus.AlreadyBooked => "alreadyBooked",
                StatementDraftEntryStatus.Announced => "announced",
                _ => (string?)null
            };

            if (skipped is null)
            {
                // --- Contact prediction (replica of TryAutoAssignContact, non-mutating) ---
                Guid? effectiveContactId = e.ContactId;
                var predictedCostNeutral = false;
                if (effectiveContactId is null)
                {
                    var recipientText = McpSuggestionMatching.NormalizeSearchText(e.RecipientName);
                    var match = McpSuggestionMatching.MatchContactId(contactTuples, aliasLookup, recipientText);
                    var matchBasis = match.HasValue ? "nameOrAlias" : (string?)null;
                    var matchedContact = match.HasValue ? contacts.FirstOrDefault(c => c.Id == match.Value) : null;

                    if (matchedContact is { IsPaymentIntermediary: true })
                    {
                        var subjectText = McpSuggestionMatching.NormalizeSearchText(e.Subject);
                        var subjectMatch = McpSuggestionMatching.MatchContactId(contactTuples, aliasLookup, subjectText);
                        effectiveContactId = subjectMatch ?? matchedContact.Id;
                        matchBasis = "paymentIntermediarySubject";
                    }
                    else if (matchedContact is { Type: ContactType.Bank }
                             && bankContactId.HasValue && bankContactId.Value != Guid.Empty
                             && matchedContact.Id != bankContactId.Value && selfContact is not null)
                    {
                        effectiveContactId = selfContact.Id;
                        matchBasis = "foreignBankToSelf";
                        predictedCostNeutral = true;
                    }
                    else if (matchedContact is not null)
                    {
                        effectiveContactId = matchedContact.Id;
                        if (selfContact is not null && matchedContact.Id == selfContact.Id)
                        {
                            predictedCostNeutral = true;
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(e.RecipientName)
                             && bankContactId.HasValue && bankContactId.Value != Guid.Empty)
                    {
                        effectiveContactId = bankContactId.Value;
                        matchBasis = "emptyRecipientBankFallback";
                    }

                    if (effectiveContactId.HasValue && matchBasis is not null)
                    {
                        var target = contacts.FirstOrDefault(c => c.Id == effectiveContactId.Value);
                        suggestions.Add(new
                        {
                            kind = "contact",
                            entryId = e.Id,
                            field = "ContactId",
                            proposedContactId = effectiveContactId.Value,
                            matchBasis,
                            proposedCostNeutral = predictedCostNeutral ? true : (bool?)null,
                            confidence = "high",
                            reason = "Pipeline classification heuristic (name/alias match) would assign this contact.",
                            untrusted_data = new { contactName = target?.Name }
                        });
                    }
                    else if (_knownContacts is not null)
                    {
                        var known = await _knownContacts.FindMatchAsync(
                            new[] { e.RecipientName, e.Subject, e.BookingDescription }, ct);
                        if (known is not null)
                        {
                            var existing = contacts.FirstOrDefault(c =>
                                string.Equals(c.Name, known.Name, StringComparison.OrdinalIgnoreCase));
                            if (existing is not null)
                            {
                                effectiveContactId = existing.Id;
                                suggestions.Add(new
                                {
                                    kind = "contact",
                                    entryId = e.Id,
                                    field = "ContactId",
                                    proposedContactId = existing.Id,
                                    matchBasis = "knownContactCatalog",
                                    proposedCostNeutral = (bool?)null,
                                    confidence = "medium",
                                    reason = "KnownContactCatalog match maps to an existing contact.",
                                    untrusted_data = new { contactName = existing.Name }
                                });
                            }
                            else
                            {
                                suggestions.Add(new
                                {
                                    kind = "new_contact",
                                    entryId = e.Id,
                                    field = "Contact",
                                    proposedContactId = (Guid?)null,
                                    matchBasis = "knownContactCatalog",
                                    proposedCostNeutral = (bool?)null,
                                    confidence = "low",
                                    reason = "KnownContactCatalog matches; no contact with that name exists — creating it is a separate privileged action.",
                                    untrusted_data = new { name = known.Name, type = known.Type.ToString(), aliases = known.Aliases }
                                });
                            }
                        }
                    }
                }

                // --- Savings plan prediction (replica of TryAutoAssignSavingsPlan) ---
                if (e.SavingsPlanId is null && selfContact is not null
                    && effectiveContactId == selfContact.Id
                    && account is { SavingsPlanExpectation: not SavingsPlanExpectation.None })
                {
                    var normalizedSubjectNoSpaces = McpSuggestionMatching.RemoveWhitespace(
                        McpSuggestionMatching.NormalizeUmlauts(e.Subject).ToLowerInvariant());
                    var subjectForContract = System.Text.RegularExpressions.Regex.Replace(
                        e.Subject ?? string.Empty, "[\\s-]", string.Empty,
                        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                    var matches = plans.Where(plan =>
                    {
                        if (string.IsNullOrWhiteSpace(plan.Name)) { return false; }
                        var planName = McpSuggestionMatching.RemoveWhitespace(
                            McpSuggestionMatching.NormalizeUmlauts(plan.Name).ToLowerInvariant());
                        if (normalizedSubjectNoSpaces.Contains(planName)) { return true; }
                        if (string.IsNullOrWhiteSpace(plan.ContractNumber)) { return false; }
                        var cn = System.Text.RegularExpressions.Regex.Replace(plan.ContractNumber.Trim(),
                            "[\\s-]", string.Empty, System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                        return cn.Length > 0 && subjectForContract.Contains(cn, StringComparison.OrdinalIgnoreCase);
                    }).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();

                    foreach (var plan in matches)
                    {
                        suggestions.Add(new
                        {
                            kind = "savings_plan",
                            entryId = e.Id,
                            field = "SavingsPlanId",
                            proposedSavingsPlanId = plan.Id,
                            confidence = matches.Count == 1 ? "high" : "low",
                            ambiguous = matches.Count > 1 ? true : (bool?)null,
                            reason = matches.Count == 1
                                ? "Self-contact entry matches savings plan name/contract number in the subject."
                                : "Multiple savings plans match — pipeline would flag the entry for manual check.",
                            untrusted_data = new { planName = plan.Name, contractNumber = plan.ContractNumber }
                        });
                    }
                }

                // --- Security prediction (replica of TryAutoAssignSecurity) ---
                if (e.SecurityId is null && account is { SecurityProcessingEnabled: true }
                    && (effectiveContactId is null || effectiveContactId == bankContactId)
                    && securities.Count > 0)
                {
                    var haystack = McpSuggestionMatching.NormalizeForSecurityMatch(
                        $"{e.Subject} {e.BookingDescription} {e.RecipientName}");
                    bool Matches(string? probe)
                    {
                        var p = McpSuggestionMatching.NormalizeForSecurityMatch(probe);
                        return p.Length > 0 && haystack.Contains(p, StringComparison.Ordinal);
                    }
                    var matches = securities.Where(s => Matches(s.Identifier) || Matches(s.Name)).ToList();
                    suggestedSecurityId = matches.FirstOrDefault()?.Id;
                    foreach (var sec in matches)
                    {
                        suggestions.Add(new
                        {
                            kind = "security",
                            entryId = e.Id,
                            field = "SecurityId",
                            proposedSecurityId = sec.Id,
                            confidence = matches.Count == 1 ? "high" : "low",
                            ambiguous = matches.Count > 1 ? true : (bool?)null,
                            reason = matches.Count == 1
                                ? "Security identifier/name occurs in the booking texts."
                                : "Multiple securities match — pipeline would pick the first and flag for review.",
                            untrusted_data = new { securityName = sec.Name, identifier = sec.Identifier }
                        });
                    }
                }

                // Missing assignment per Zuordnungsvollständigkeit (toolkatalog §4.4):
                // booking needs a contact OR savings plan OR security on the entry.
                if (effectiveContactId is null && e.SavingsPlanId is null && e.SecurityId is null
                    && !suggestions.Any())
                {
                    missing.Add("contactOrPlanOrSecurity");
                }
                else if (effectiveContactId is null && e.ContactId is null)
                {
                    missing.Add("contact");
                }
                if ((e.SecurityId ?? suggestedSecurityId) is not null && e.SecurityTransactionType is null)
                {
                    missing.Add("securityTransactionType");
                }
            }

            analysisEntries.Add(new
            {
                entryId = e.Id,
                entryNumber = e.EntryNumber,
                status = e.Status.ToString(),
                bookingDate = e.BookingDate,
                amount = e.Amount,
                contactId = e.ContactId,
                savingsPlanId = e.SavingsPlanId,
                securityId = e.SecurityId,
                classificationSkipped = skipped,
                duplicate = new
                {
                    pipelineFlagged,
                    suspectCount = suspects.Count,
                    suspects
                },
                suggestions,
                missingAssignments = missing,
                untrusted_data = new
                {
                    subject = e.Subject,
                    recipientName = e.RecipientName,
                    bookingDescription = e.BookingDescription
                }
            });
        }

        // Draft-level account suggestion (replica of ClassifyHeader's single-account rule;
        // the IBAN source field is not exposed by the read contract — documented limit).
        object? accountSuggestion = null;
        var missingDraft = new List<string>();
        if (header.DetectedAccountId is null)
        {
            missingDraft.Add("account");
            if (accounts.Count == 1)
            {
                accountSuggestion = new
                {
                    kind = "account",
                    field = "DetectedAccountId",
                    proposedAccountId = accounts[0].Id,
                    confidence = "medium",
                    reason = "No account detected; the owner has exactly one account.",
                    untrusted_data = new { accountName = accounts[0].Name, iban = accounts[0].Iban }
                };
            }
        }

        return new
        {
            draftId,
            analysis = (object)new
            {
                status = header.Status.ToString(),
                detectedAccountId = header.DetectedAccountId,
                isSplitDraft = header.IsSplitDraft,
                isPreliminary = header.IsPreliminary,
                totalEntries = allEntries.Count,
                analyzedEntries = analysisEntries.Count,
                missingDraftAssignments = missingDraft,
                accountSuggestion,
                heuristicScope = new
                {
                    intraDraft = true,
                    bankPostings = postingsAllowed && account is not null,
                    committedEntries = importsAllowed
                },
                entries = analysisEntries
            }
        };
    }

    private static object Suspect(string kind, Guid id, DateTime bookingDate, decimal amount, string? subject, string? recipientName)
        => new
        {
            kind,
            id,
            bookingDate,
            amount,
            untrusted_data = new { subject, recipientName }
        };

    private async Task<IReadOnlyList<ContactDto>> LoadContactsAsync(McpToolCallContext context, Guid owner, CancellationToken ct)
    {
        var contacts = new List<ContactDto>();
        var loaded = 0;
        while (loaded < MaxContacts)
        {
            var page = await _contacts.ListAsync(owner, loaded, McpToolArguments.MaxTake, null, null, ct);
            contacts.AddRange(page);
            loaded += page.Count;
            if (page.Count < McpToolArguments.MaxTake) { break; }
        }
        var capped = contacts.Take(MaxContacts).ToList();
        return await _recordFilter.ApplyAsync(context.Access, McpDataType.Contacts, capped,
            c => new McpRecordContext { ContactCategoryId = c.CategoryId }, ct);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadAliasesAsync(
        IReadOnlyList<ContactDto> contacts, Guid owner, CancellationToken ct)
    {
        var lookup = new Dictionary<Guid, IReadOnlyList<string>>(contacts.Count);
        foreach (var c in contacts)
        {
            var aliases = await _contacts.ListAliases(c.Id, owner, ct);
            if (aliases.Count > 0)
            {
                lookup[c.Id] = aliases.Select(a => a.Pattern).ToList();
            }
        }
        return lookup;
    }

    private async Task<bool> DraftVisibleAsync(
        StatementDraftDto draft, IReadOnlyList<McpAccessScope> scopes, Guid owner, CancellationToken ct)
    {
        if (!McpRecordScopeFilter.RequiresRecordFilter(scopes))
        {
            return true;
        }
        var record = new McpRecordContext
        {
            AccountTypeValue = draft.DetectedAccountId.HasValue
                ? (short?)(await _accounts.GetAsync(draft.DetectedAccountId.Value, owner, ct))?.Type
                : null
        };
        return McpRecordScopeFilter.IsAllowed(scopes, record);
    }

    private async Task<bool> EntryVisibleAsync(
        StatementDraftEntryDto entry, Guid? detectedAccountId, IReadOnlyList<McpAccessScope> scopes,
        IReadOnlyList<ContactDto> contacts, IReadOnlyList<SavingsPlanDto> plans,
        IReadOnlyList<SecurityDto> securities, Guid owner, CancellationToken ct)
    {
        if (!McpRecordScopeFilter.RequiresRecordFilter(scopes))
        {
            return true;
        }
        // Same derivation as fm_statement_drafts_read: Kontotyp via detected account and the
        // category dimensions of the linked records — resolved over the already filtered
        // catalogs (an invisible target can never widen a suggestion).
        var record = new McpRecordContext
        {
            AccountTypeValue = detectedAccountId.HasValue
                ? (short?)(await _accounts.GetAsync(detectedAccountId.Value, owner, ct))?.Type
                : null,
            ContactCategoryId = entry.ContactId.HasValue
                ? contacts.FirstOrDefault(c => c.Id == entry.ContactId.Value)?.CategoryId
                : null,
            SavingsPlanCategoryId = entry.SavingsPlanId.HasValue
                ? plans.FirstOrDefault(p => p.Id == entry.SavingsPlanId.Value)?.CategoryId
                : null,
            SecurityCategoryId = entry.SecurityId.HasValue
                ? securities.FirstOrDefault(s => s.Id == entry.SecurityId.Value)?.CategoryId
                : null
        };
        return McpRecordScopeFilter.IsAllowed(scopes, record);
    }
}
