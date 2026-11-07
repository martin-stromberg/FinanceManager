namespace FinanceManager.Shared.Dtos.Mcp;

/// <summary>
/// List item of an MCP access (verwaltungs API <c>/api/mcp-accesses</c>). Enum values are
/// transported as <see langword="short"/> following the DTO convention; names:
/// Level = McpAccessLevel, ApprovalMode = McpApprovalMode, Status = McpAccessStatus.
/// </summary>
/// <param name="Id">Access identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional note.</param>
/// <param name="Level">Access level (McpAccessLevel as short).</param>
/// <param name="ApprovalMode">Approval mode (McpApprovalMode as short).</param>
/// <param name="Status">Lifecycle status (McpAccessStatus as short).</param>
/// <param name="ExpiresUtc">Optional expiry timestamp.</param>
/// <param name="IsExpired">Whether the access is currently expired.</param>
/// <param name="LastUsedUtc">Last successful authentication.</param>
/// <param name="TokenIssuedUtc">Issue time of the current token.</param>
/// <param name="RevokedUtc">Revocation timestamp.</param>
/// <param name="CreatedUtc">Creation timestamp.</param>
public sealed record McpAccessListItemDto(
    Guid Id,
    string Name,
    string? Description,
    short Level,
    short ApprovalMode,
    short Status,
    DateTime? ExpiresUtc,
    bool IsExpired,
    DateTime? LastUsedUtc,
    DateTime TokenIssuedUtc,
    DateTime? RevokedUtc,
    DateTime CreatedUtc);

/// <summary>
/// One scope tuple (Datentyp × Rechteart × Filter) of an access profile. Id is set for
/// persisted rows of individually configured accesses.
/// </summary>
/// <param name="Id">Scope row id; <see cref="Guid.Empty"/> for derived (non-persisted) tuples.</param>
/// <param name="DataType">Data type (McpDataType as short).</param>
/// <param name="Right">Right kind (McpRightKind as short).</param>
/// <param name="AccountTypesCsv">Optional CSV of account type values.</param>
/// <param name="ContactCategoryIdsCsv">Optional CSV of contact category ids.</param>
/// <param name="SecurityCategoryIdsCsv">Optional CSV of security category ids.</param>
/// <param name="SavingsPlanCategoryIdsCsv">Optional CSV of savings plan category ids.</param>
/// <param name="AttachmentCategoryIdsCsv">Optional CSV of attachment category ids.</param>
public sealed record McpAccessScopeDto(
    Guid Id,
    short DataType,
    short Right,
    string? AccountTypesCsv,
    string? ContactCategoryIdsCsv,
    string? SecurityCategoryIdsCsv,
    string? SavingsPlanCategoryIdsCsv,
    string? AttachmentCategoryIdsCsv);

/// <summary>
/// Detail view of an MCP access including the effective scope profile and restrictions.
/// </summary>
/// <param name="Id">Access identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional note.</param>
/// <param name="Level">Access level (McpAccessLevel as short).</param>
/// <param name="ApprovalMode">Approval mode (McpApprovalMode as short).</param>
/// <param name="Status">Lifecycle status (McpAccessStatus as short).</param>
/// <param name="ExpiresUtc">Optional expiry timestamp.</param>
/// <param name="IsExpired">Whether the access is currently expired.</param>
/// <param name="AllowedIpRanges">Optional comma-separated CIDR/address list.</param>
/// <param name="LastUsedUtc">Last successful authentication.</param>
/// <param name="TokenIssuedUtc">Issue time of the current token.</param>
/// <param name="RevokedUtc">Revocation timestamp.</param>
/// <param name="RevocationReason">Business reason of the revocation.</param>
/// <param name="EffectiveScopes">Effective scope profile (derived for standard levels).</param>
/// <param name="CreatedUtc">Creation timestamp.</param>
/// <param name="ModifiedUtc">Last modification timestamp.</param>
public sealed record McpAccessDetailDto(
    Guid Id,
    string Name,
    string? Description,
    short Level,
    short ApprovalMode,
    short Status,
    DateTime? ExpiresUtc,
    bool IsExpired,
    string? AllowedIpRanges,
    DateTime? LastUsedUtc,
    DateTime TokenIssuedUtc,
    DateTime? RevokedUtc,
    string? RevocationReason,
    IReadOnlyList<McpAccessScopeDto> EffectiveScopes,
    DateTime CreatedUtc,
    DateTime? ModifiedUtc);

/// <summary>
/// Creation request of an MCP access. The plaintext token is returned once in the response.
/// </summary>
/// <param name="Name">Display name (unique per owner).</param>
/// <param name="Description">Optional note.</param>
/// <param name="Level">Access level (McpAccessLevel as short).</param>
/// <param name="ApprovalMode">Approval mode (McpApprovalMode as short).</param>
/// <param name="ExpiresUtc">Optional expiry timestamp.</param>
/// <param name="AllowedIpRanges">Optional comma-separated CIDR/address list.</param>
/// <param name="Scopes">Optional scope tuples (only for the individual level).</param>
public sealed record McpAccessCreateRequest(
    string Name,
    string? Description,
    short Level,
    short ApprovalMode,
    DateTime? ExpiresUtc,
    string? AllowedIpRanges,
    IReadOnlyList<McpAccessScopeDto>? Scopes);

/// <summary>
/// Update request of an MCP access (master data, level, approval mode, expiry, IP binding).
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="Description">Optional note.</param>
/// <param name="Level">Access level (McpAccessLevel as short).</param>
/// <param name="ApprovalMode">Approval mode (McpApprovalMode as short).</param>
/// <param name="ExpiresUtc">Optional expiry timestamp.</param>
/// <param name="AllowedIpRanges">Optional comma-separated CIDR/address list.</param>
public sealed record McpAccessUpdateRequest(
    string Name,
    string? Description,
    short Level,
    short ApprovalMode,
    DateTime? ExpiresUtc,
    string? AllowedIpRanges);

/// <summary>
/// Response of access creation; contains the plaintext token exactly once.
/// </summary>
/// <param name="Access">The created access detail.</param>
/// <param name="PlainTextToken">The plaintext token — never retrievable again.</param>
public sealed record McpAccessCreatedDto(McpAccessDetailDto Access, string PlainTextToken);

/// <summary>
/// Response of a token rotation; contains the new plaintext token exactly once.
/// </summary>
/// <param name="Id">Access identifier.</param>
/// <param name="PlainTextToken">The new plaintext token — never retrievable again.</param>
/// <param name="TokenIssuedUtc">Issue timestamp of the new token.</param>
public sealed record McpAccessRotatedDto(Guid Id, string PlainTextToken, DateTime TokenIssuedUtc);

/// <summary>
/// Request to replace the persisted scope profile of an individually configured access.
/// </summary>
/// <param name="Scopes">The scope tuples to persist.</param>
public sealed record McpScopeSetRequest(IReadOnlyList<McpAccessScopeDto> Scopes);

/// <summary>
/// Request to replace the filter dimensions of one persisted scope row.
/// </summary>
/// <param name="AccountTypesCsv">Optional CSV of account type values.</param>
/// <param name="ContactCategoryIdsCsv">Optional CSV of contact category ids.</param>
/// <param name="SecurityCategoryIdsCsv">Optional CSV of security category ids.</param>
/// <param name="SavingsPlanCategoryIdsCsv">Optional CSV of savings plan category ids.</param>
/// <param name="AttachmentCategoryIdsCsv">Optional CSV of attachment category ids.</param>
public sealed record McpFilterSetRequest(
    string? AccountTypesCsv,
    string? ContactCategoryIdsCsv,
    string? SecurityCategoryIdsCsv,
    string? SavingsPlanCategoryIdsCsv,
    string? AttachmentCategoryIdsCsv);

/// <summary>
/// Optional revocation request body.
/// </summary>
/// <param name="Reason">Optional business reason recorded with the revocation.</param>
public sealed record McpAccessRevokeRequest(string? Reason);

/// <summary>
/// Server-side definition of one access level including its default scope profile
/// (<c>GET /api/mcp-accesses/levels</c>) — the UI renders this without duplicating the rules.
/// </summary>
/// <param name="Level">Access level (McpAccessLevel as short).</param>
/// <param name="Name">Stable level name for localization.</param>
/// <param name="DefaultScopes">Default scope profile (filter-less tuples).</param>
public sealed record McpAccessLevelDto(short Level, string Name, IReadOnlyList<McpAccessScopeDto> DefaultScopes);

/// <summary>
/// One selectable category option of a filter dimension.
/// </summary>
/// <param name="Id">Category id.</param>
/// <param name="Name">Category name.</param>
public sealed record McpCategoryOptionDto(Guid Id, string Name);

/// <summary>
/// One data type option of the scope matrix with its valid right kinds.
/// </summary>
/// <param name="DataType">Data type (McpDataType as short).</param>
/// <param name="Name">Stable data type name for localization.</param>
/// <param name="AllowedRights">Valid right kinds (McpRightKind as short).</param>
public sealed record McpDataTypeOptionDto(short DataType, string Name, IReadOnlyList<short> AllowedRights);

/// <summary>
/// UI metadata of the access management (<c>GET /api/mcp-accesses/options</c>): scope matrix,
/// right kinds, filter dimensions, the owner's concrete category lists and the TLS flag for
/// the transport-security hint.
/// </summary>
/// <param name="DataTypes">The scope matrix (data types with valid rights).</param>
/// <param name="RightKinds">All right kinds (McpRightKind as short).</param>
/// <param name="FilterDimensions">Stable names of the five filter dimensions.</param>
/// <param name="ContactCategories">The owner's contact categories.</param>
/// <param name="SecurityCategories">The owner's security categories.</param>
/// <param name="SavingsPlanCategories">The owner's savings plan categories.</param>
/// <param name="AttachmentCategories">The owner's attachment categories.</param>
/// <param name="IsTlsRequest">Whether the calling request used TLS (hint flag for the token warning).</param>
public sealed record McpAccessOptionsDto(
    IReadOnlyList<McpDataTypeOptionDto> DataTypes,
    IReadOnlyList<short> RightKinds,
    IReadOnlyList<string> FilterDimensions,
    IReadOnlyList<McpCategoryOptionDto> ContactCategories,
    IReadOnlyList<McpCategoryOptionDto> SecurityCategories,
    IReadOnlyList<McpCategoryOptionDto> SavingsPlanCategories,
    IReadOnlyList<McpCategoryOptionDto> AttachmentCategories,
    bool IsTlsRequest);
