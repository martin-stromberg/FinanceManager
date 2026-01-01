namespace FinanceManager.Application.Savings;

/// <summary>
/// Service to manage savings plans: list, get, create, update, archive, delete, analyze.
/// </summary>
public interface ISavingsPlanService
{
    /// <summary>
    /// Lists savings plans for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner whose savings plans should be returned.</param>
    /// <param name="onlyActive">When <c>true</c> returns only active plans; when <c>false</c> returns only archived/inactive plans.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of <see cref="SavingsPlanDto"/> matching the criteria.</returns>
    Task<IReadOnlyList<SavingsPlanDto>> ListAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct);

    /// <summary>
    /// Gets a savings plan by identifier for the specified owner.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to retrieve.</param>
    /// <param name="ownerUserId">Identifier of the owner requesting the savings plan.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The matching <see cref="SavingsPlanDto"/>, or <c>null</c> when not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<SavingsPlanDto?> GetAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new savings plan for the specified owner.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner creating the savings plan.</param>
    /// <param name="name">Display name of the savings plan. Required.</param>
    /// <param name="type">Type of savings plan.</param>
    /// <param name="targetAmount">Optional target amount to save towards.</param>
    /// <param name="targetDate">Optional target date for achieving the goal.</param>
    /// <param name="interval">Optional contribution interval for automated contributions.</param>
    /// <param name="categoryId">Optional category id grouping the plan.</param>
    /// <param name="contractNumber">Optional contract number or reference.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The created <see cref="SavingsPlanDto"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c> or empty.</exception>
    Task<SavingsPlanDto> CreateAsync(Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber, CancellationToken ct);

    /// <summary>
    /// Updates an existing savings plan and returns the updated DTO or <c>null</c> when not found.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to update.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the update.</param>
    /// <param name="name">New display name. Required.</param>
    /// <param name="type">Updated savings plan type.</param>
    /// <param name="targetAmount">Optional updated target amount.</param>
    /// <param name="targetDate">Optional updated target date.</param>
    /// <param name="interval">Optional updated contribution interval.</param>
    /// <param name="categoryId">Optional updated category id.</param>
    /// <param name="contractNumber">Optional updated contract number.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The updated <see cref="SavingsPlanDto"/>, or <c>null</c> if the plan does not exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c> or empty.</exception>
    Task<SavingsPlanDto?> UpdateAsync(Guid id, Guid ownerUserId, string name, SavingsPlanType type, decimal? targetAmount, DateTime? targetDate, SavingsPlanInterval? interval, Guid? categoryId, string? contractNumber, CancellationToken ct);

    /// <summary>
    /// Archives (deactivates) a savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to archive.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the archive action.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the plan was successfully archived; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> ArchiveAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Deletes a savings plan permanently.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to delete.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the deletion.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns><c>true</c> when the plan was deleted; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Performs analysis for the given savings plan, returning forecasting and scheduling information.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to analyze.</param>
    /// <param name="ownerUserId">Identifier of the owner requesting the analysis.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="SavingsPlanAnalysisDto"/> containing analysis results.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task<SavingsPlanAnalysisDto> AnalyzeAsync(Guid id, Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Returns the count of savings plans for the specified owner, optionally filtered by active state.
    /// </summary>
    /// <param name="ownerUserId">Identifier of the owner whose plans are counted.</param>
    /// <param name="onlyActive">When <c>true</c> counts only active plans; when <c>false</c> counts only archived plans.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>Non-negative integer representing the number of matching savings plans.</returns>
    Task<int> CountAsync(Guid ownerUserId, bool onlyActive, CancellationToken ct);

    /// <summary>
    /// Sets or clears the symbol attachment for a savings plan.
    /// </summary>
    /// <param name="id">Identifier of the savings plan to update.</param>
    /// <param name="ownerUserId">Identifier of the owner performing the update.</param>
    /// <param name="attachmentId">Attachment id to set as symbol, or <c>null</c> to clear the symbol.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when the operation has finished.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> or <paramref name="ownerUserId"/> is <see cref="Guid.Empty"/>.</exception>
    Task SetSymbolAttachmentAsync(Guid id, Guid ownerUserId, Guid? attachmentId, CancellationToken ct);
}