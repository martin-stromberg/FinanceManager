using FinanceManager.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Notifications;

/// <summary>
/// Provides EF Core model configuration for the <see cref="Notification"/> entity.
/// Registers keys, indexes and column constraints used by the infrastructure data model.
/// </summary>
public static class NotificationModelConfig
{
    /// <summary>
    /// Applies entity configuration for the <see cref="Notification"/> type to the provided <see cref="ModelBuilder"/>.
    /// </summary>
    /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to configure. Must not be <c>null</c>.</param>
    /// <remarks>
    /// This method configures indexes and column constraints (lengths, conversions and required flags) for the
    /// <see cref="Notification"/> entity. It is intended to be called from <c>OnModelCreating</c> of the application's DbContext.
    /// </remarks>
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.OwnerUserId, x.Type, x.ScheduledDateUtc });
            b.Property(x => x.Title).HasMaxLength(140);
            b.Property(x => x.Message).HasMaxLength(1000);
            b.Property(x => x.TriggerEventKey).HasMaxLength(120);
            b.Property(x => x.Type).HasConversion<int>().IsRequired();
            b.Property(x => x.Target).HasConversion<int>().IsRequired();
            b.Property(x => x.ScheduledDateUtc).IsRequired();
            b.Property(x => x.IsEnabled).IsRequired();
            b.Property(x => x.IsDismissed).IsRequired();
            b.Property(x => x.CreatedUtc).IsRequired();
        });
    }
}
