namespace FinanceManager.Application.Security;

/// <summary>
/// Service to manage IP blocks and rate-limit related operations.
/// </summary>
public interface IIpBlockService
{
    /// <summary>
    /// Lists IP block entries.
    /// </summary>
    /// <param name="onlyBlocked">If <c>true</c>, only currently blocked entries will be returned; if <c>false</c>, only unblocked entries; if <c>null</c>, all entries.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="IpBlockDto"/> representing matching IP block entries.</returns>
    Task<IReadOnlyList<IpBlockDto>> ListAsync(bool? onlyBlocked, CancellationToken ct);

    /// <summary>
    /// Creates a new IP block entry for the specified IP address.
    /// </summary>
    /// <param name="ipAddress">IP address to block (IPv4 or IPv6 string).</param>
    /// <param name="reason">Optional reason for the block; may be <c>null</c>.</param>
    /// <param name="isBlocked">Initial blocked state for the entry.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created <see cref="IpBlockDto"/> describing the new entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ipAddress"/> is <c>null</c> or empty.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="ipAddress"/> is not a valid IP address format.</exception>
    Task<IpBlockDto> CreateAsync(string ipAddress, string? reason, bool isBlocked, CancellationToken ct);

    /// <summary>
    /// Updates an existing IP block entry.
    /// </summary>
    /// <param name="id">Identifier of the IP block entry to update.</param>
    /// <param name="reason">New reason text or <c>null</c> to clear.</param>
    /// <param name="isBlocked">New blocked state or <c>null</c> to leave unchanged.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The updated <see cref="IpBlockDto"/>, or <c>null</c> if no entry with the specified id exists.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<IpBlockDto?> UpdateAsync(Guid id, string? reason, bool? isBlocked, CancellationToken ct);

    /// <summary>
    /// Marks the specified IP block entry as blocked.
    /// </summary>
    /// <param name="id">Identifier of the IP block entry to block.</param>
    /// <param name="reason">Optional reason for blocking or <c>null</c> to leave existing reason unchanged.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the entry was successfully marked as blocked; otherwise <c>false</c> (for example, when not found).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> BlockAsync(Guid id, string? reason, CancellationToken ct);

    /// <summary>
    /// Unblocks the specified IP block entry.
    /// </summary>
    /// <param name="id">Identifier of the IP block entry to unblock.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the entry was successfully unblocked; otherwise <c>false</c> (for example, when not found).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> UnblockAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Resets failure counters (e.g. login attempt counters) associated with the specified IP block entry.
    /// </summary>
    /// <param name="id">Identifier of the IP block entry whose counters should be reset.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when counters were successfully reset; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> ResetCountersAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Deletes an IP block entry permanently.
    /// </summary>
    /// <param name="id">Identifier of the IP block entry to delete.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> if the entry was deleted; otherwise <c>false</c> (for example, when not found).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Registers a login failure observed for an unknown or unauthenticated user that originated from the given IP address.
    /// Implementations typically increment failure counters and may trigger blocking rules.
    /// </summary>
    /// <param name="ipAddress">IP address where the failure originated.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the failure has been recorded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ipAddress"/> is <c>null</c> or empty.</exception>
    Task RegisterUnknownUserFailureAsync(string ipAddress, CancellationToken ct);

    /// <summary>
    /// Immediately blocks the specified IP address by address (creating an entry if required).
    /// </summary>
    /// <param name="ipAddress">IP address to block (IPv4 or IPv6 string).</param>
    /// <param name="reason">Optional reason for the block.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the block operation has finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ipAddress"/> is <c>null</c> or empty.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="ipAddress"/> is not a valid IP address format.</exception>
    Task BlockByAddressAsync(string ipAddress, string? reason, CancellationToken ct);
}

