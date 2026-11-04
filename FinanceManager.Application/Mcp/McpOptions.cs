namespace FinanceManager.Application.Mcp;

/// <summary>
/// Configuration options of the MCP integration bound from the <c>Mcp</c> configuration
/// section (sicherheitskonzept.md §6, architekturkonzept.md §9.1). All switches follow the
/// feature-flag convention of the application and are bound reloadable via
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/>.
/// </summary>
public sealed class McpOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Mcp";

    /// <summary>
    /// Global kill switch. When <see langword="false"/> the <c>/mcp</c> endpoint rejects all
    /// requests with HTTP 503 before any authentication work. Default is <see langword="false"/>
    /// (opt-in: MCP ships disabled).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Write-stop switch options.</summary>
    public McpWriteAccessOptions WriteAccess { get; set; } = new();

    /// <summary>Token hash options.</summary>
    public McpTokenHashOptions TokenHash { get; set; } = new();

    /// <summary>Rate-limit tiers of the MCP endpoint and tool classes.</summary>
    public McpRateLimitingOptions RateLimiting { get; set; } = new();

    /// <summary>Approval defaults (effective from the approval feature onward).</summary>
    public McpApprovalOptions Approval { get; set; } = new();

    /// <summary>Anomaly-detection thresholds.</summary>
    public McpAnomalyOptions Anomaly { get; set; } = new();

    /// <summary>SSE subscription options of the <c>GET /mcp</c> stream (F-1101).</summary>
    public McpSseOptions Sse { get; set; } = new();

    /// <summary>Audit/request retention options (F-1104, audit-konzept.md §5).</summary>
    public McpRetentionOptions Retention { get; set; } = new();
}

/// <summary>
/// Write-stop switch: when disabled all tools of the classes
/// <see cref="FinanceManager.Domain.Mcp.McpToolSecurityClass.Schreibvorbereitend"/> and
/// <see cref="FinanceManager.Domain.Mcp.McpToolSecurityClass.Genehmigungspflichtig"/> are rejected.
/// </summary>
public sealed class McpWriteAccessOptions
{
    /// <summary>Whether write access via MCP is allowed. Default <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// PBKDF2 parameters of the MCP personal-access-token hashing.
/// </summary>
public sealed class McpTokenHashOptions
{
    /// <summary>
    /// PBKDF2 iteration count. Default follows the <c>Pbkdf2IdentityPasswordHasher</c>
    /// reference value (100.000); the ≥160-bit secret makes the count non-critical.
    /// </summary>
    public int Iterations { get; set; } = 100_000;
}

/// <summary>
/// Rate-limit configuration of the MCP surface: one endpoint-level limit plus a tier per
/// tool security class (sicherheitskonzept.md §7.2).
/// </summary>
public sealed class McpRateLimitingOptions
{
    /// <summary>
    /// Endpoint-level fixed window partitioned by the access key hint (authenticated path).
    /// </summary>
    public McpRateLimitTier Endpoint { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };

    /// <summary>
    /// Stricter fixed window partitioned by client IP for requests without a parseable token.
    /// </summary>
    public McpRateLimitTier Unauthenticated { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>Per-access limit for tools of class Lesend.</summary>
    public McpRateLimitTier Lesend { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>Per-access limit for tools of class Vorschlagend.</summary>
    public McpRateLimitTier Vorschlagend { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>Per-access limit for tools of class Schreibvorbereitend.</summary>
    public McpRateLimitTier Schreibvorbereitend { get; set; } = new() { PermitLimit = 30, WindowSeconds = 60 };

    /// <summary>Per-access limit for tools of class Genehmigungspflichtig.</summary>
    public McpRateLimitTier Genehmigungspflichtig { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Per-access limit for meta tools (polling-friendly).</summary>
    public McpRateLimitTier Meta { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };

    /// <summary>
    /// Resolves the tier of a tool security class.
    /// </summary>
    /// <param name="securityClass">The tool security class.</param>
    /// <param name="isMetaTool">Whether the tool is a meta tool (meta-scope instead of a data scope).</param>
    /// <returns>The configured rate-limit tier.</returns>
    public McpRateLimitTier ForClass(FinanceManager.Domain.Mcp.McpToolSecurityClass securityClass, bool isMetaTool)
    {
        if (isMetaTool)
        {
            return Meta;
        }
        return securityClass switch
        {
            FinanceManager.Domain.Mcp.McpToolSecurityClass.Lesend => Lesend,
            FinanceManager.Domain.Mcp.McpToolSecurityClass.Vorschlagend => Vorschlagend,
            FinanceManager.Domain.Mcp.McpToolSecurityClass.Schreibvorbereitend => Schreibvorbereitend,
            FinanceManager.Domain.Mcp.McpToolSecurityClass.Genehmigungspflichtig => Genehmigungspflichtig,
            _ => Genehmigungspflichtig
        };
    }
}

/// <summary>
/// Fixed-window rate-limit tier.
/// </summary>
public sealed class McpRateLimitTier
{
    /// <summary>Maximum number of permits per window.</summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>
/// Approval-related defaults of the MCP integration.
/// </summary>
public sealed class McpApprovalOptions
{
    /// <summary>Default decision deadline of an approval request in hours.</summary>
    public int DefaultExpiryHours { get; set; } = 72;

    /// <summary>Maximum number of pending approval requests per access.</summary>
    public int MaxPendingPerAccess { get; set; } = 25;

    /// <summary>Seconds between two poll passes of the queue executor.</summary>
    public int ExecutorPollSeconds { get; set; } = 5;

    /// <summary>Maximum execution time of one approved request before <c>Err_ExecutionTimeout</c>.</summary>
    public int ExecutionTimeoutSeconds { get; set; } = 300;

    /// <summary>Minutes between two expiry sweep passes (<c>Pending/Approved → Expired</c>).</summary>
    public int ExpiryCheckIntervalMinutes { get; set; } = 15;

    /// <summary>Maximum requests claimed and executed per executor pass.</summary>
    public int MaxExecutionBatchSize { get; set; } = 20;

    /// <summary>
    /// Minutes after which an unreleased claim is considered stale and reclaimable
    /// (crash recovery of the single-instance executor).
    /// </summary>
    public int ClaimStaleMinutes { get; set; } = 10;
}

/// <summary>
/// Anomaly-detection thresholds (sicherheitskonzept.md §6.3).
/// </summary>
public sealed class McpAnomalyOptions
{
    /// <summary>
    /// Number of scope denials of one access within <see cref="ScopeDenialWindowMinutes"/>
    /// that triggers an owner notification plus an admin system alert.
    /// </summary>
    public int ScopeDenialThreshold { get; set; } = 25;

    /// <summary>Sliding window (minutes) for the scope-denial counter.</summary>
    public int ScopeDenialWindowMinutes { get; set; } = 10;

    /// <summary>Cooldown (minutes) between two anomaly notifications of the same access.</summary>
    public int NotificationCooldownMinutes { get; set; } = 30;

    /// <summary>
    /// Number of failed request executions of one access within
    /// <see cref="ExecutionFailureWindowMinutes"/> that triggers an owner notification
    /// (agent workload alert, F-1103).
    /// </summary>
    public int ExecutionFailureThreshold { get; set; } = 5;

    /// <summary>Sliding window (minutes) for the execution-failure counter.</summary>
    public int ExecutionFailureWindowMinutes { get; set; } = 30;
}

/// <summary>
/// SSE subscription options of the <c>GET /mcp</c> streamable-HTTP upgrade (F-1101,
/// architekturkonzept.md §3).
/// </summary>
public sealed class McpSseOptions
{
    /// <summary>
    /// Seconds between two keep-alive comments of an open stream (proxy idle timeouts).
    /// Default 15.
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 15;

    /// <summary>
    /// Maximum number of concurrent SSE subscriptions per access. Additional GET requests
    /// are rejected with HTTP 429 so one agent cannot exhaust the process memory with
    /// open streams. Default 4.
    /// </summary>
    public int MaxSubscribersPerAccess { get; set; } = 4;
}

/// <summary>
/// Retention options of the MCP audit trail and the approval-request queue (F-1104,
/// audit-konzept.md §5). The retention executor is the only deletion path on the audit
/// table; chain truncation is documented by <c>RetentionPurged</c> anchor entries so the
/// hash chain stays verifiable "ab diesem Eintrag".
/// </summary>
public sealed class McpRetentionOptions
{
    /// <summary>Minutes between two retention passes. Default 720 (twice a day).</summary>
    public int IntervalMinutes { get; set; } = 720;

    /// <summary>
    /// Retention (months) of high-frequency event classes: <c>ToolCall</c>,
    /// <c>AccessEvent</c> and <c>SecurityEvent</c> audit entries. Default 12.
    /// </summary>
    public int ToolCallMonths { get; set; } = 12;

    /// <summary>
    /// Retention (months) of the long-tier event classes: <c>ApprovalEvent</c> and
    /// <c>DataChange</c> audit entries. Default 36.
    /// </summary>
    public int ApprovalMonths { get; set; } = 36;

    /// <summary>
    /// Retention (months) of terminal <c>AiActionRequest</c> rows (the queue table is
    /// audit substrate, not part of the hash chain). Default 12.
    /// </summary>
    public int RequestMonths { get; set; } = 12;

    /// <summary>Maximum audit entries deleted per chain per pass. Default 5000.</summary>
    public int BatchSize { get; set; } = 5000;
}
