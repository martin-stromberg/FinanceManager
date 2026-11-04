using FinanceManager.Shared.Dtos.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// Administration service of the MCP accesses (F-0104): creation with one-time plaintext
/// token, rotation with immediate invalidation, revocation, activate/deactivate, expiry,
/// IP binding and individual scopes. Every method is strictly owner-scoped — a foreign id
/// behaves like a non-existing record (404 semantics).
/// </summary>
public interface IMcpAccessService
{
    /// <summary>
    /// Lists the accesses of an owner (search, paging).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="search">Optional name search text.</param>
    /// <param name="skip">Items to skip (≥ 0).</param>
    /// <param name="take">Items to take (server-clamped).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching access list items.</returns>
    Task<IReadOnlyList<McpAccessListItemDto>> ListAsync(Guid ownerUserId, string? search, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Gets one access of the owner including its effective scope profile.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The detail DTO or <see langword="null"/> when not found/not owned.</returns>
    Task<McpAccessDetailDto?> GetAsync(Guid ownerUserId, Guid id, CancellationToken ct);

    /// <summary>
    /// Creates a new access and issues the first token. The plaintext is returned exactly
    /// once in <see cref="McpAccessCreatedDto.PlainTextToken"/>.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="request">Creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created access plus the one-time plaintext token.</returns>
    /// <exception cref="FinanceManager.Application.Exceptions.DomainValidationException">
    /// <c>Err_Mcp_InvalidScope</c> for invalid tuples, <c>Err_Mcp_NameInUse</c> for duplicate names.
    /// </exception>
    Task<McpAccessCreatedDto> CreateAsync(Guid ownerUserId, McpAccessCreateRequest request, CancellationToken ct);

    /// <summary>
    /// Updates master data, level, approval mode, expiry and IP binding.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="request">Update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when updated; <see langword="false"/> when not found.</returns>
    Task<bool> UpdateAsync(Guid ownerUserId, Guid id, McpAccessUpdateRequest request, CancellationToken ct);

    /// <summary>
    /// Replaces the persisted scope profile of an individually configured access; tuples are
    /// validated against the F3 matrix.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="request">Scope set request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when applied; <see langword="false"/> when not found.</returns>
    /// <exception cref="FinanceManager.Application.Exceptions.DomainValidationException">
    /// <c>Err_Mcp_InvalidScope</c> for invalid tuples or a non-individual level.
    /// </exception>
    Task<bool> SetScopesAsync(Guid ownerUserId, Guid id, McpScopeSetRequest request, CancellationToken ct);

    /// <summary>
    /// Replaces the filter dimensions of one persisted scope row of the access.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="scopeId">Scope row id.</param>
    /// <param name="request">Filter set request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when applied; <see langword="false"/> when not found.</returns>
    Task<bool> SetFiltersAsync(Guid ownerUserId, Guid id, Guid scopeId, McpFilterSetRequest request, CancellationToken ct);

    /// <summary>
    /// Activates an access (effective immediately).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when changed; <see langword="false"/> when not found.</returns>
    Task<bool> ActivateAsync(Guid ownerUserId, Guid id, CancellationToken ct);

    /// <summary>
    /// Deactivates an access (effective immediately).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when changed; <see langword="false"/> when not found.</returns>
    Task<bool> DeactivateAsync(Guid ownerUserId, Guid id, CancellationToken ct);

    /// <summary>
    /// Rotates the token: issues a new credential and invalidates the previous one
    /// immediately (no grace period). The plaintext is returned exactly once.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rotation result or <see langword="null"/> when not found.</returns>
    Task<McpAccessRotatedDto?> RotateTokenAsync(Guid ownerUserId, Guid id, CancellationToken ct);

    /// <summary>
    /// Revokes an access (terminal state): the token is rejected from the next request on.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="id">Access id.</param>
    /// <param name="reason">Optional business reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when revoked; <see langword="false"/> when not found.</returns>
    Task<bool> RevokeAsync(Guid ownerUserId, Guid id, string? reason, CancellationToken ct);

    /// <summary>
    /// Lists the server-side level definitions including their default scope profiles.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The level definitions.</returns>
    Task<IReadOnlyList<McpAccessLevelDto>> GetLevelsAsync(CancellationToken ct);

    /// <summary>
    /// Builds the UI metadata: scope matrix, right kinds, filter dimensions, the owner's
    /// category lists and the TLS flag.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="isTlsRequest">Whether the calling request used TLS.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The options DTO.</returns>
    Task<McpAccessOptionsDto> GetOptionsAsync(Guid ownerUserId, bool isTlsRequest, CancellationToken ct);
}
