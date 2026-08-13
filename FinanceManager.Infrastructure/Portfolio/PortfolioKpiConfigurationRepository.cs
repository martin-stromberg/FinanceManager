using FinanceManager.Application.Portfolio;
using FinanceManager.Domain.Portfolio;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Portfolio;

/// <summary>
/// EF Core-backed implementation of <see cref="IPortfolioKpiConfigurationRepository"/>.
/// </summary>
public sealed class PortfolioKpiConfigurationRepository : IPortfolioKpiConfigurationRepository
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortfolioKpiConfigurationRepository"/> class.
    /// </summary>
    /// <param name="db">Application database context.</param>
    public PortfolioKpiConfigurationRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<PortfolioKpiConfiguration?> GetAsync(Guid ownerUserId, CancellationToken ct)
        => _db.PortfolioKpiConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.OwnerUserId == ownerUserId, ct);

    /// <inheritdoc />
    public async Task<PortfolioKpiConfiguration> UpsertAsync(Guid ownerUserId, string activeTileIds, string tileOrder, CancellationToken ct)
    {
        var entity = await _db.PortfolioKpiConfigurations.FirstOrDefaultAsync(c => c.OwnerUserId == ownerUserId, ct);
        if (entity == null)
        {
            entity = new PortfolioKpiConfiguration(ownerUserId, activeTileIds, tileOrder);
            _db.PortfolioKpiConfigurations.Add(entity);
        }
        else
        {
            entity.Update(activeTileIds, tileOrder);
        }

        await _db.SaveChangesAsync(ct);
        return entity;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid ownerUserId, CancellationToken ct)
    {
        var entity = await _db.PortfolioKpiConfigurations.FirstOrDefaultAsync(c => c.OwnerUserId == ownerUserId, ct);
        if (entity == null) { return; }

        _db.PortfolioKpiConfigurations.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
