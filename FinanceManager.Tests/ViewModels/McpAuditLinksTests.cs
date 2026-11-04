using FinanceManager.Web.ViewModels.Mcp;
using Xunit;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Tests of <see cref="McpAuditLinks"/> (F-0602 drilling links): references whose target
/// card route exists today must resolve to a link; everything else (unknown entity
/// types, fields without a route, <c>AiActionRequest</c>/import ids, malformed payloads)
/// must yield no link and never throw.
/// </summary>
public sealed class McpAuditLinksTests
{
    /// <summary>Known payload fields resolve to their card route.</summary>
    [Theory]
    [InlineData("draftId", "statement-drafts")]
    [InlineData("entryId", "statement-drafts/entries")]
    [InlineData("accountId", "accounts")]
    [InlineData("contactId", "contacts")]
    [InlineData("postingId", "postings")]
    [InlineData("savingsPlanId", "savings-plans")]
    [InlineData("securityId", "securities")]
    [InlineData("mcpAccessId", "mcp-accesses")]
    public void PayloadReference_KnownField_ResolvesToCardRoute(string field, string kind)
    {
        var id = Guid.NewGuid();
        var json = $"{{\"{field}\":\"{id}\"}}";

        var refs = McpAuditLinks.ExtractPayloadReferences(json);

        var r = Assert.Single(refs);
        Assert.Equal(field, r.Field);
        Assert.Equal(id, r.Id);
        Assert.Equal($"/card/{kind}/{id}", r.Href);
    }

    /// <summary>Nested payloads (objects inside arrays) are scanned as well.</summary>
    [Fact]
    public void PayloadReference_NestedObject_IsFound()
    {
        var draftId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var json = $"{{\"outer\":[{{\"draftId\":\"{draftId}\"}}],\"args\":{{\"accountId\":\"{accountId}\"}}}}";

        var refs = McpAuditLinks.ExtractPayloadReferences(json);

        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.Href == $"/card/statement-drafts/{draftId}");
        Assert.Contains(refs, r => r.Href == $"/card/accounts/{accountId}");
    }

    /// <summary>Fields without an existing target route yield no reference (text only).</summary>
    [Theory]
    [InlineData("importId")]       // no statement-import card route today
    [InlineData("requestId")]      // AiActionRequest → approval center (EP-09)
    [InlineData("aiActionRequestId")]
    [InlineData("correlationId")]  // grouping key, not a navigation target
    [InlineData("unknownField")]
    public void PayloadReference_FieldWithoutRoute_YieldsNoReference(string field)
    {
        var json = $"{{\"{field}\":\"{Guid.NewGuid()}\"}}";

        Assert.Empty(McpAuditLinks.ExtractPayloadReferences(json));
    }

    /// <summary>Malformed, empty and non-Guid payloads never throw and yield nothing.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("{\"draftId\":\"not-a-guid\"}")]
    [InlineData("{\"draftId\":42}")]
    [InlineData("{\"draftId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public void PayloadReference_InvalidInput_YieldsNoReference(string? json)
    {
        Assert.Empty(McpAuditLinks.ExtractPayloadReferences(json));
    }

    /// <summary>Field-delta entity types resolve to their card route.</summary>
    [Theory]
    [InlineData("Posting", "postings")]
    [InlineData("Contact", "contacts")]
    [InlineData("Account", "accounts")]
    [InlineData("StatementDraft", "statement-drafts")]
    [InlineData("StatementDraftEntry", "statement-drafts/entries")]
    [InlineData("Security", "securities")]
    [InlineData("SavingsPlan", "savings-plans")]
    [InlineData("McpAccess", "mcp-accesses")]
    public void EntityHref_KnownType_ResolvesToCardRoute(string entityType, string kind)
    {
        var id = Guid.NewGuid();

        Assert.Equal($"/card/{kind}/{id}", McpAuditLinks.TryGetEntityHref(entityType, id));
    }

    /// <summary>Entity types without a route (or AiActionRequest, EP-09) stay text.</summary>
    [Theory]
    [InlineData("McpAccessScope")]
    [InlineData("StatementImport")]
    [InlineData("AiActionRequest")]
    [InlineData("Attachment")]
    [InlineData("SomethingUnknown")]
    public void EntityHref_UnknownType_ReturnsNull(string entityType)
    {
        Assert.Null(McpAuditLinks.TryGetEntityHref(entityType, Guid.NewGuid()));
    }
}
