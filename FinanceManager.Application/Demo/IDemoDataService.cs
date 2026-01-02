using System;
using System.Threading;
using System.Threading.Tasks;

namespace FinanceManager.Application.Demo;

/// <summary>
/// Service responsible for creating demo data for a user account.
/// </summary>
public interface IDemoDataService
{
    /// <summary>
    /// Creates demo data for the specified user.
    /// </summary>
    /// <param name="userId">Identifier of the user to create demo data for.</param>
    /// <param name="createPostings">When true, create statement imports and entries (postings) as part of the demo data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when demo data creation has been scheduled or finished.</returns>
    Task CreateDemoDataAsync(Guid userId, bool createPostings, CancellationToken ct);
}
