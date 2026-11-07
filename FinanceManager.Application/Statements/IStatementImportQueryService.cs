using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Application.Statements;

/// <summary>
/// Read-only query contract over the committed, revision-safe statement documents
/// (<c>StatementImport</c> headers and <c>StatementEntry</c> records). Introduced for the
/// MCP tool <c>fm_statement_imports_review</c> (T08, F-0204): the documents have no write or
/// delete path at all, so this contract exposes list/get/count operations only. Ownership
/// is derived exclusively via the anchor account
/// (<c>StatementImport.AccountId</c> → <c>Account.OwnerUserId</c>), identical to the
/// derivation table of the permission model (berechtigungsmodell.md §6) — records anchored
/// to foreign or non-existing accounts are invisible (empty result / null).
/// </summary>
public interface IStatementImportQueryService
{
    /// <summary>
    /// Lists statement imports of the owner, optionally restricted to one account and a
    /// commit-time range (<see cref="StatementImportListItemDto.ImportedAtUtc"/>).
    /// </summary>
    /// <param name="ownerUserId">Owner user id; only imports on accounts owned by this user are returned.</param>
    /// <param name="accountId">Optional account anchor filter.</param>
    /// <param name="from">Optional inclusive start of <c>ImportedAtUtc</c>.</param>
    /// <param name="to">Optional inclusive end (date) of <c>ImportedAtUtc</c>.</param>
    /// <param name="skip">Number of items to skip (clamped to ≥ 0).</param>
    /// <param name="take">Page size (clamped to 1–200, API convention).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Imports ordered by commit time descending.</returns>
    Task<IReadOnlyList<StatementImportListItemDto>> ListImportsAsync(
        Guid ownerUserId, Guid? accountId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Counts the statement imports of the owner (same restrictions as
    /// <see cref="ListImportsAsync"/> without paging).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="accountId">Optional account anchor filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total count of visible imports.</returns>
    Task<int> CountImportsAsync(Guid ownerUserId, Guid? accountId, CancellationToken ct);

    /// <summary>
    /// Gets a single statement import by id, or <see langword="null"/> when the import does
    /// not exist or is not anchored to an account of the owner (404 semantics).
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="importId">Import identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The import or <see langword="null"/>.</returns>
    Task<StatementImportListItemDto?> GetImportAsync(Guid ownerUserId, Guid importId, CancellationToken ct);

    /// <summary>
    /// Lists the entries of one statement import, or an empty list when the import does not
    /// exist or is not anchored to an account of the owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="importId">Parent import identifier.</param>
    /// <param name="skip">Number of items to skip (clamped to ≥ 0).</param>
    /// <param name="take">Page size (clamped to 1–200, API convention).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Entries ordered by booking date then id.</returns>
    Task<IReadOnlyList<StatementImportEntryDto>> ListEntriesAsync(
        Guid ownerUserId, Guid importId, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Counts the entries of one statement import; zero when the import is not visible to
    /// the owner.
    /// </summary>
    /// <param name="ownerUserId">Owner user id.</param>
    /// <param name="importId">Parent import identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total count of entries of the import (0 when invisible).</returns>
    Task<int> CountEntriesAsync(Guid ownerUserId, Guid importId, CancellationToken ct);
}
