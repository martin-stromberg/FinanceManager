namespace FinanceManager.Application.Reports;

/// <summary>
/// Service to manage home page KPI widgets (create, list, update, delete) for a user.
/// </summary>
public interface IHomeKpiService
{
    /// <summary>
    /// Lists home KPIs for the specified owner.
    /// </summary>
    Task<IReadOnlyList<HomeKpiDto>> ListAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Creates a new home KPI for the owner.
    /// </summary>
    Task<HomeKpiDto> CreateAsync(Guid ownerUserId, HomeKpiCreateRequest request, CancellationToken ct);

    /// <summary>
    /// Updates an existing home KPI and returns the updated DTO or null when not found.
    /// </summary>
    Task<HomeKpiDto?> UpdateAsync(Guid id, Guid ownerUserId, HomeKpiUpdateRequest request, CancellationToken ct);

    /// <summary>
    /// Deletes a home KPI.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid ownerUserId, CancellationToken ct);
}

