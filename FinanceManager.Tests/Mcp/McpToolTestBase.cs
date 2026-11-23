using FinanceManager.Application.Mcp;
using FinanceManager.Domain.Mcp;
using FinanceManager.Web.Mcp;
using Moq;
using System.Text.Json;

namespace FinanceManager.Tests.Mcp;

/// <summary>
/// Shared Moq/JSON plumbing of the MCP tool handler test suites: test accesses, access and
/// call contexts, the record-scope filter and the argument/result JSON helpers. The default
/// access level is <see cref="McpAccessLevel.Analyse"/> — suites exercising higher-level
/// tools override <see cref="DefaultLevel"/>.
/// </summary>
public abstract class McpToolTestBase
{
    /// <summary>
    /// Access level applied by <see cref="NewAccess(Guid)"/> and by the default scope set of
    /// <see cref="NewFilter"/>.
    /// </summary>
    protected virtual McpAccessLevel DefaultLevel => McpAccessLevel.Analyse;

    /// <summary>Creates a test access for the owner at the suite's <see cref="DefaultLevel"/>.</summary>
    /// <param name="owner">Owner user id of the access.</param>
    /// <returns>The configured access instance.</returns>
    protected McpAccess NewAccess(Guid owner)
    {
        return NewAccess(owner, DefaultLevel);
    }

    /// <summary>Creates a test access for the owner at an explicit level.</summary>
    /// <param name="owner">Owner user id of the access.</param>
    /// <param name="level">Access level of the access.</param>
    /// <returns>The configured access instance.</returns>
    protected McpAccess NewAccess(Guid owner, McpAccessLevel level)
        => new(owner, "acc", "HINT01", "pbkdf2|1|AA|AA", level, McpApprovalMode.PerAction);

    /// <summary>Creates an access context mock for the owner (or wraps the given access).</summary>
    /// <param name="owner">Owner user id reported by the context.</param>
    /// <param name="access">Explicit access instance; a default-level access is created when omitted.</param>
    /// <returns>The configured context mock.</returns>
    protected Mock<IMcpAccessContext> NewAccessContext(Guid owner, McpAccess? access = null)
    {
        var ctx = new Mock<IMcpAccessContext>();
        ctx.SetupGet(c => c.OwnerUserId).Returns(owner);
        ctx.SetupGet(c => c.Access).Returns(access ?? NewAccess(owner));
        return ctx;
    }

    /// <summary>Creates a tool call context with a stub service provider.</summary>
    /// <param name="access">The access context of the call.</param>
    /// <param name="mode">Execution mode (interactive vs. approved queue execution).</param>
    /// <returns>The configured call context.</returns>
    protected static McpToolCallContext NewCallContext(IMcpAccessContext access,
        McpToolExecutionMode mode = McpToolExecutionMode.Interactive)
        => new(access, Mock.Of<IServiceProvider>(), Guid.NewGuid(), null, mode);

    /// <summary>
    /// Creates the record-scope filter whose evaluator returns the given scopes, or the full
    /// default scope set of <see cref="DefaultLevel"/> when no scopes are supplied.
    /// </summary>
    /// <param name="scopes">Effective scopes the evaluator mock reports.</param>
    /// <returns>The configured record-scope filter.</returns>
    protected McpRecordScopeFilter NewFilter(params McpAccessScope[] scopes)
    {
        var evaluator = new Mock<IMcpScopeEvaluator>();
        evaluator.Setup(e => e.GetEffectiveScopesAsync(It.IsAny<McpAccess>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scopes.Length == 0
                ? McpLevelProfiles.GetDefaultScopes(DefaultLevel, isAdmin: true)
                    .Select(t => new McpAccessScope(Guid.NewGuid(), Guid.NewGuid(), t.DataType, t.Right)).ToList()
                : scopes.ToList());
        return new McpRecordScopeFilter(evaluator.Object);
    }

    /// <summary>Parses a JSON argument string into the <c>arguments</c> element.</summary>
    /// <param name="json">JSON object text of the tool arguments.</param>
    /// <returns>The root element of the parsed document.</returns>
    protected static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>Serializes a tool result into an inspectable JSON document.</summary>
    /// <param name="result">The handler result object.</param>
    /// <returns>The serialized result as JSON document.</returns>
    protected static JsonDocument Serialize(object result) => JsonDocument.Parse(JsonSerializer.Serialize(result));
}
