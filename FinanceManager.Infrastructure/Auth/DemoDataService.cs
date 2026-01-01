using FinanceManager.Domain.Contacts;
using FinanceManager.Infrastructure;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Helper service that seeds example/demo contact categories for a newly created user.
/// Intended for demo or development environments only.
/// </summary>
public sealed class DemoDataService
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoDataService"/> class.
    /// </summary>
    /// <param name="db">The application database context used to persist demo data.</param>
    public DemoDataService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates a set of sample contact categories for the specified user.
    /// </summary>
    /// <param name="userId">The owner user identifier for whom demo categories are created.</param>
    /// <param name="ct">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>A task that completes when the demo data has been written to the database.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the provided <paramref name="userId"/> is <see cref="Guid.Empty"/> (caller should provide a valid user id).</exception>
    /// <exception cref="Microsoft.EntityFrameworkCore.DbUpdateException">May be thrown when saving changes to the database fails.</exception>
    public async Task CreateDemoDataForUserAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentNullException(nameof(userId));

        await _db.ContactCategories.AddRangeAsync(new[]
        {
            new ContactCategory(userId, "Freunde & Bekannte"),
            new ContactCategory(userId, "Versicherungen"),
            new ContactCategory(userId, "Arbeit"),
            new ContactCategory(userId, "Banken"),
            new ContactCategory(userId, "Supermärkte & Einzelhandel"),
            new ContactCategory(userId, "Transport & Tanken"),
            new ContactCategory(userId, "Lieferdienste"),
            new ContactCategory(userId, "Onlineshops"),
            new ContactCategory(userId, "Glücksspiele"),
            new ContactCategory(userId, "Freizeiteinrichtungen"),
            new ContactCategory(userId, "Streaminganbieter"),
            new ContactCategory(userId, "Behörden"),
            new ContactCategory(userId, "Autohäuser"),
            new ContactCategory(userId, "Wohlfahrtsunternehmen"),
            new ContactCategory(userId, "Baumärkte & Gartencenter"),
            new ContactCategory(userId, "Gastronomie"),
            new ContactCategory(userId, "Bäckereien & Cafés"),
            new ContactCategory(userId, "Dienstleister"),
            new ContactCategory(userId, "Hotels & Ferienanlagen"),
            new ContactCategory(userId, "Sanitäranlagen"),
            new ContactCategory(userId, "Privatverkäufer"),
            new ContactCategory(userId, "Sonstiges"),
        });
        await _db.SaveChangesAsync(ct);
    }
}
