using FinanceManager.Application.Contacts;
using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Shared.Dtos.Contacts;
using System.Text.Json;

namespace FinanceManager.Web.Mcp.Tools;

/// <summary>
/// Descriptor of the read tool <c>fm_contacts_lookup</c> (T04): contact search/details
/// including alias patterns and contact categories (Kontaktgruppen).
/// Scopes: (Kontakte, Lesen), (Kontaktgruppen, Lesen).
/// </summary>
public sealed class FmContactsLookupDescriptor : McpToolDescriptorBase
{
    /// <summary>Tool name.</summary>
    public const string ToolName = "fm_contacts_lookup";

    /// <summary>Creates the descriptor.</summary>
    public FmContactsLookupDescriptor() : base(
        "{\"type\":\"object\",\"properties\":{\"contactId\":{\"type\":\"string\",\"format\":\"uuid\",\"description\":\"Optional single contact id; omitted = paged list.\"},\"type\":{\"type\":\"string\",\"enum\":[\"Self\",\"Bank\",\"Person\",\"Organization\",\"Other\"],\"description\":\"Optional contact type filter.\"},\"q\":{\"type\":\"string\",\"description\":\"Optional name search text.\"},\"includeAliases\":{\"type\":\"boolean\",\"description\":\"Include alias patterns per contact (default: only for single lookups).\"},\"includeCategories\":{\"type\":\"boolean\",\"description\":\"Include the contact category list.\"},\"skip\":{\"type\":\"integer\",\"minimum\":0},\"take\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":200}},\"additionalProperties\":false}")
    { }

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description
        => "Looks up contacts (list or detail) incl. alias patterns and contact categories. Scopes: (Kontakte, Lesen), (Kontaktgruppen, Lesen).";

    /// <inheritdoc />
    public override McpToolSecurityClass SecurityClass => McpToolSecurityClass.Lesend;

    /// <inheritdoc />
    public override IReadOnlyList<McpToolScope> RequiredScopes => new[]
    {
        new McpToolScope(McpDataType.Contacts, McpRightKind.Read),
        new McpToolScope(McpDataType.ContactCategories, McpRightKind.Read)
    };
}

/// <summary>
/// Handler of <c>fm_contacts_lookup</c>: adapts <see cref="IContactService"/> and
/// <see cref="IContactCategoryService"/>. Contact names, descriptions, alias patterns and
/// category names are untrusted content and are emitted inside <c>untrusted_data</c>.
/// The Kontaktgruppen filter dimension applies per contact via its category id.
/// </summary>
public sealed class FmContactsLookupHandler : IMcpToolHandler
{
    private readonly IContactService _contacts;
    private readonly IContactCategoryService _categories;
    private readonly McpRecordScopeFilter _recordFilter;

    /// <summary>Creates the handler.</summary>
    public FmContactsLookupHandler(IContactService contacts, IContactCategoryService categories, McpRecordScopeFilter recordFilter)
    {
        _contacts = contacts;
        _categories = categories;
        _recordFilter = recordFilter;
    }

    /// <inheritdoc />
    public string ToolName => FmContactsLookupDescriptor.ToolName;

    /// <inheritdoc />
    public async Task<object> InvokeAsync(JsonElement arguments, McpToolCallContext context, CancellationToken ct)
    {
        var owner = context.Access.OwnerUserId;
        var contactId = McpToolArguments.OptionalGuid(arguments, "contactId");
        var type = McpToolArguments.OptionalEnum<ContactType>(arguments, "type");
        var q = McpToolArguments.OptionalString(arguments, "q");
        var includeAliases = McpToolArguments.OptionalBool(arguments, "includeAliases");
        var includeCategories = McpToolArguments.OptionalBool(arguments, "includeCategories") ?? false;
        var skip = McpToolArguments.Skip(arguments);
        var take = McpToolArguments.Take(arguments);

        var contacts = new List<ContactDto>();
        if (contactId.HasValue)
        {
            var contact = await _contacts.GetAsync(contactId.Value, owner, ct);
            if (contact is not null)
            {
                contacts.Add(contact);
            }
            includeAliases ??= true; // single lookups include aliases by default
        }
        else
        {
            contacts.AddRange(await _contacts.ListAsync(owner, skip, take, type, q, ct));
            includeAliases ??= false;
        }

        // Record-level filter (Kontaktgruppen dimension via Contact.CategoryId).
        var visible = await _recordFilter.ApplyAsync(
            context.Access, McpDataType.Contacts, contacts,
            c => new McpRecordContext { ContactCategoryId = c.CategoryId }, ct);

        var result = new List<object>(visible.Count);
        foreach (var c in visible)
        {
            IReadOnlyList<AliasNameDto> aliases = Array.Empty<AliasNameDto>();
            if (includeAliases.Value)
            {
                aliases = await _contacts.ListAliases(c.Id, owner, ct);
            }
            result.Add(new
            {
                id = c.Id,
                type = c.Type.ToString(),
                categoryId = c.CategoryId,
                isPaymentIntermediary = c.IsPaymentIntermediary,
                symbolAttachmentId = c.SymbolAttachmentId,
                aliases = aliases.Select(al => new
                {
                    id = al.Id,
                    untrusted_data = new { pattern = al.Pattern }
                }).ToList(),
                untrusted_data = new
                {
                    name = c.Name,
                    description = c.Description
                }
            });
        }

        object? categories = null;
        if (includeCategories)
        {
            categories = (await _categories.ListAsync(owner, ct))
                .Select(cat => new
                {
                    id = cat.Id,
                    symbolAttachmentId = cat.SymbolAttachmentId,
                    untrusted_data = new { name = cat.Name }
                }).ToList();
        }

        return new { contacts = result, categories };
    }
}
