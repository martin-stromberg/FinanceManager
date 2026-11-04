using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Contacts;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the suggestion tool <c>fm_contacts_suggest</c> (T11): contact care
/// suggestions — missing category assignments (<c>Contact.CategoryId</c>), new
/// <c>AliasName.Pattern</c> candidates for better import matching, duplicate candidates,
/// alias conflicts and contact resolution for an arbitrary counterparty text. Output is a
/// suggestion list with reason/confidence — never persists. Scopes: (Kontakte, Vorschlag),
/// (Kontaktgruppen, Vorschlag).
/// </summary>
public sealed class FmContactsSuggestDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_contacts_suggest";

    /// <summary>Creates the descriptor.</summary>
    public FmContactsSuggestDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"contactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single contact restriction.\"},\"referenceText\":{\"type\":\"string\",\"description\":\"Optional counterparty text to resolve (e.g. a recipient line from a statement entry) — switches to resolution mode.\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200},\"maxSuggestions\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Contact care suggestions without persistence: missing category assignment, alias-pattern candidates, duplicate candidates, alias conflicts — or resolution of a counterparty text to contacts (incl. new-contact suggestion). Scopes: (Kontakte, Vorschlag), (Kontaktgruppen, Vorschlag).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Vorschlagend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Contacts, McpRightKind.Suggest),
        new McpToolScope(McpDataType.ContactCategories, McpRightKind.Suggest)
    };
}

/// <summary>
/// Handler of <c>fm_contacts_suggest</c>: evaluates the contact base of the owner through
/// <see cref="IContactService"/>/<see cref="IContactCategoryService"/> and — in resolution
/// mode — <see cref="IKnownContactCatalog"/> for new-contact proposals. All rules are
/// deterministic and derived from the pipeline matching semantics; contact names, alias
/// patterns and descriptions are untrusted content (<c>untrusted_data</c>). Record-level
/// filtering applies the Suggest-right rows of the Contacts/ContactCategories data types.
/// No service write method is invoked.
/// </summary>
public sealed class FmContactsSuggestHandler : IMcpToolHandler
{
    /// <summary>Upper bound of the contact catalog loaded for matching (paged).</summary>
    private const int MaxContacts = 500;

    private readonly IContactService _contacts;
    private readonly IContactCategoryService _categories;
    private readonly IKnownContactCatalog _knownContacts;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmContactsSuggestHandler(
        IContactService contacts,
        IContactCategoryService categories,
        IKnownContactCatalog knownContacts,
        McpRecordScopeFilter recordFilter)
    {
        _contacts = contacts;
        _categories = categories;
        _knownContacts = knownContacts;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmContactsSuggestDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var contactId = McpToolArguments.OptionalGuid(arguments, "contactId");
        var referenceText = McpToolArguments.OptionalString(arguments, "referenceText", maxLength: 500);
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);
        var maxSuggestions = Math.Clamp(
            McpToolArguments.OptionalInt(arguments, "maxSuggestions") ?? 50, 1, McpToolArguments.MaxTake);

        IReadOnlyList<ContactDto> contacts;
        var catalogTruncated = false;
        if (contactId.HasValue)
        {
            var single = await _contacts.GetAsync(contactId.Value, owner, ct);
            contacts = single is null ? Array.Empty<ContactDto>() : new[] { single };
        }
        else
        {
            var loaded = new List<ContactDto>();
            var offset = 0;
            while (loaded.Count < MaxContacts)
            {
                var page = await _contacts.ListAsync(owner, offset, McpToolArguments.MaxTake, null, null, ct);
                loaded.AddRange(page);
                offset += page.Count;
                if (page.Count < McpToolArguments.MaxTake) { break; }
            }
            catalogTruncated = loaded.Count > MaxContacts;
            contacts = loaded.Take(MaxContacts).ToList();
        }

        // Record-level filter on the Suggest rows of the Contacts data type
        // (Kontaktgruppen dimension via Contact.CategoryId).
        var visible = await _recordFilter.ApplyAsync(context.Access, McpDataType.Contacts, contacts,
            c => new McpRecordContext { ContactCategoryId = c.CategoryId }, ct);

        var aliasLookup = new Dictionary<Guid, IReadOnlyList<AliasNameDto>>(visible.Count);
        foreach (var c in visible)
        {
            var aliases = await _contacts.ListAliases(c.Id, owner, ct);
            if (aliases.Count > 0)
            {
                aliasLookup[c.Id] = aliases;
            }
        }

        object? categories = null;
        if (referenceText is null)
        {
            var cats = await _recordFilter.ApplyAsync(context.Access, McpDataType.ContactCategories,
                await _categories.ListAsync(owner, ct),
                cat => new McpRecordContext { ContactCategoryId = cat.Id }, ct);
            categories = cats.Select(cat => new
            {
                id = cat.Id,
                untrusted_data = new { name = cat.Name }
            }).ToList();
        }

        IReadOnlyList<object> suggestions = referenceText is not null
            ? await BuildResolutionSuggestionsAsync(visible, aliasLookup, referenceText, maxSuggestions, ct)
            : BuildMaintenanceSuggestions(visible.Skip(skip).Take(take).ToList(), aliasLookup, maxSuggestions);

        return new
        {
            mode = referenceText is not null ? "resolution" : "maintenance",
            contactCount = visible.Count,
            catalogTruncated,
            categories,
            suggestions
        };
    }

    /// <summary>
    /// Resolution mode: predicts which contact a counterparty text resolves to (same
    /// name/alias priority as the pipeline classification) and proposes a new contact via
    /// <see cref="IKnownContactCatalog"/> when nothing matches.
    /// </summary>
    private async Task<IReadOnlyList<object>> BuildResolutionSuggestionsAsync(
        IReadOnlyList<ContactDto> contacts,
        IReadOnlyDictionary<Guid, IReadOnlyList<AliasNameDto>> aliasLookup,
        string referenceText, int maxSuggestions, CancellationToken ct)
    {
        var suggestions = new List<object>();
        var searchText = McpSuggestionMatching.NormalizeSearchText(referenceText);
        var tuples = contacts.Select(c => (c.Id, c.Name)).ToList();
        var patternLookup = aliasLookup.ToDictionary(
            kvp => kvp.Key, kvp => (IReadOnlyList<string>)kvp.Value.Select(a => a.Pattern).ToList());

        var matchedId = McpSuggestionMatching.MatchContactId(tuples, patternLookup, searchText);
        var matched = matchedId.HasValue ? contacts.FirstOrDefault(c => c.Id == matchedId.Value) : null;
        if (matched is not null)
        {
            var aliasCovered = aliasLookup.TryGetValue(matched.Id, out var ownAliases)
                               && ownAliases.Any(a => McpSuggestionMatching.AliasPatternMatches(a.Pattern, searchText));
            var basis = aliasCovered ? "aliasPattern" : "nameMatch";
            suggestions.Add(new
            {
                kind = "resolve_contact",
                field = "ContactId",
                targetContactId = matched.Id,
                proposedContactId = matched.Id,
                matchBasis = basis,
                confidence = "high",
                reason = aliasCovered
                    ? "An alias pattern of this contact matches the reference text."
                    : "The contact name occurs in the reference text; no alias pattern covers it.",
                untrusted_data = new { contactName = matched.Name }
            });
            if (!aliasCovered)
            {
                suggestions.Add(new
                {
                    kind = "add_alias",
                    field = "AliasName.Pattern",
                    targetContactId = matched.Id,
                    confidence = "medium",
                    reason = "The reference text resolved via name matching only — an explicit alias pattern would make future matching deterministic.",
                    untrusted_data = new { contactName = matched.Name, proposedPattern = referenceText }
                });
            }
        }
        else if (_knownContacts is not null)
        {
            var known = await _knownContacts.FindMatchAsync(new[] { referenceText }, ct);
            if (known is not null)
            {
                var existing = contacts.FirstOrDefault(c =>
                    string.Equals(c.Name, known.Name, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    suggestions.Add(new
                    {
                        kind = "resolve_contact",
                        field = "ContactId",
                        targetContactId = existing.Id,
                        proposedContactId = existing.Id,
                        matchBasis = "knownContactCatalog",
                        confidence = "medium",
                        reason = "KnownContactCatalog maps the text to this existing contact.",
                        untrusted_data = new { contactName = existing.Name }
                    });
                }
                else
                {
                    suggestions.Add(new
                    {
                        kind = "new_contact",
                        field = "Contact",
                        confidence = "low",
                        reason = "KnownContactCatalog matches; no contact with that name exists — creating it is a separate privileged action.",
                        untrusted_data = new { name = known.Name, type = known.Type.ToString(), aliases = known.Aliases }
                    });
                }
            }
        }
        return suggestions.Take(maxSuggestions).ToList();
    }

    /// <summary>
    /// Maintenance mode: missing category assignments, duplicate candidates, alias
    /// conflicts and redundant alias patterns — all with the rule basis that produced them.
    /// </summary>
    private static IReadOnlyList<object> BuildMaintenanceSuggestions(
        IReadOnlyList<ContactDto> page,
        IReadOnlyDictionary<Guid, IReadOnlyList<AliasNameDto>> aliasLookup,
        int maxSuggestions)
    {
        var suggestions = new List<object>();

        // Dominant category per contact type across the page — basis for the
        // missing-category proposal ("the usual bucket for this contact type").
        var dominantByType = page
            .Where(c => c.CategoryId.HasValue)
            .GroupBy(c => c.Type)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(c => c.CategoryId!.Value).OrderByDescending(x => x.Count()).First().Key);

        foreach (var c in page)
        {
            if (suggestions.Count >= maxSuggestions) { break; }

            if (c.CategoryId is null)
            {
                dominantByType.TryGetValue(c.Type, out var proposal);
                suggestions.Add(new
                {
                    kind = "missing_category",
                    field = "CategoryId",
                    targetContactId = c.Id,
                    proposedCategoryId = proposal == Guid.Empty ? (Guid?)null : proposal,
                    confidence = proposal == Guid.Empty ? "low" : "medium",
                    reason = proposal == Guid.Empty
                        ? "Contact has no category; no dominant category of the same contact type is derivable."
                        : "Contact has no category; the proposal is the most-used category of the same contact type.",
                    untrusted_data = new { contactName = c.Name }
                });
            }

            if (aliasLookup.TryGetValue(c.Id, out var aliases))
            {
                var normalizedName = Normalize(c.Name);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var alias in aliases)
                {
                    var normalized = Normalize(alias.Pattern);
                    if (normalized.Length == 0) { continue; }
                    if (string.Equals(normalized, normalizedName, StringComparison.Ordinal))
                    {
                        suggestions.Add(new
                        {
                            kind = "redundant_alias",
                            field = "AliasName.Pattern",
                            targetContactId = c.Id,
                            aliasId = alias.Id,
                            confidence = "medium",
                            reason = "The alias pattern equals the contact name — name matching already covers it.",
                            untrusted_data = new { contactName = c.Name, pattern = alias.Pattern }
                        });
                    }
                    else if (!seen.Add(normalized))
                    {
                        suggestions.Add(new
                        {
                            kind = "redundant_alias",
                            field = "AliasName.Pattern",
                            targetContactId = c.Id,
                            aliasId = alias.Id,
                            confidence = "medium",
                            reason = "Duplicate alias pattern on the same contact.",
                            untrusted_data = new { contactName = c.Name, pattern = alias.Pattern }
                        });
                    }
                }
            }
        }

        // Duplicate candidates: identical normalized names or an alias pattern of one
        // contact that literally equals another contact's name.
        var byName = page
            .GroupBy(c => Normalize(c.Name))
            .Where(g => g.Key.Length > 0 && g.Count() > 1);
        foreach (var g in byName)
        {
            var pair = g.Take(2).ToList();
            suggestions.Add(new
            {
                kind = "duplicate_candidate",
                field = "Contact",
                targetContactId = pair[0].Id,
                relatedContactId = pair[1].Id,
                confidence = "high",
                reason = "Two contacts share the identical normalized name — merge review advised (merge is a separate privileged action).",
                untrusted_data = new { contactName = pair[0].Name, relatedContactName = pair[1].Name }
            });
        }
        foreach (var c in page)
        {
            if (!aliasLookup.TryGetValue(c.Id, out var aliases)) { continue; }
            var normalizedName = Normalize(c.Name);
            foreach (var alias in aliases)
            {
                var normalized = Normalize(alias.Pattern);
                if (normalized.Length == 0 || normalized == normalizedName) { continue; }
                var other = page.FirstOrDefault(o =>
                    o.Id != c.Id && string.Equals(Normalize(o.Name), normalized, StringComparison.Ordinal));
                if (other is not null)
                {
                    suggestions.Add(new
                    {
                        kind = "duplicate_candidate",
                        field = "Contact",
                        targetContactId = c.Id,
                        relatedContactId = other.Id,
                        confidence = "medium",
                        reason = "An alias pattern of this contact equals another contact's name — possible duplicate or intentional alias (merge is a separate privileged action).",
                        untrusted_data = new { contactName = c.Name, relatedContactName = other.Name, pattern = alias.Pattern }
                    });
                }
            }
        }

        // Alias conflicts: the same normalized pattern assigned to multiple contacts —
        // the pipeline's first-match rule makes matching ambiguous.
        var patternOwners = new Dictionary<string, List<(ContactDto Contact, AliasNameDto Alias)>>(StringComparer.Ordinal);
        foreach (var c in page)
        {
            if (!aliasLookup.TryGetValue(c.Id, out var aliases)) { continue; }
            foreach (var alias in aliases)
            {
                var normalized = Normalize(alias.Pattern);
                if (normalized.Length == 0) { continue; }
                if (!patternOwners.TryGetValue(normalized, out var list))
                {
                    patternOwners[normalized] = list = new List<(ContactDto, AliasNameDto)>();
                }
                if (!list.Any(x => x.Contact.Id == c.Id))
                {
                    list.Add((c, alias));
                }
            }
        }
        foreach (var (normalized, owners) in patternOwners)
        {
            if (owners.Count < 2) { continue; }
            suggestions.Add(new
            {
                kind = "alias_conflict",
                field = "AliasName.Pattern",
                affectedContactIds = owners.Select(o => o.Contact.Id).ToList(),
                confidence = "high",
                reason = "The same alias pattern is assigned to multiple contacts — matching order decides the outcome.",
                untrusted_data = new
                {
                    pattern = owners[0].Alias.Pattern,
                    contactNames = owners.Select(o => o.Contact.Name).ToList()
                }
            });
        }

        return suggestions.Take(maxSuggestions).ToList();

        static string Normalize(string? s)
            => Regex.Replace(McpSuggestionMatching.NormalizeUmlauts(s).ToLowerInvariant(), "\\s+", " ").Trim();
    }
}
