using FinanceManager.Application; // BackgroundTaskRunner
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure;
using FinanceManager.Web;
using FinanceManager.Web.Services; // SecurityPriceWorker
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; // for IHostedService
using System.Data.Common;
using System.Diagnostics;

namespace FinanceManager.Tests.Integration;

// Custom factory that wires AppDbContext to a fresh SQLite in-memory database per factory instance
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string BootstrapAdminUsername = "bootstrap.admin";
    public const string BootstrapAdminPassword = "Bootstr4pAdmin!";

    private DbConnection? _connection;
    /// <summary>
    /// When set, the factory will register a <see cref="TimeProvider"/> that returns this fixed UTC time.
    /// Set this property in tests before calling CreateClient() to force server-side "now".
    /// </summary>
    public DateTime? FixedUtcNow { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

        builder.UseEnvironment("Development");
        // Disable background hosted services for integration tests via configuration flags
        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["BackgroundTasks:Enabled"] = "false",
                ["Workers:SecurityPriceWorker:Enabled"] = "false",
                ["Updates:HostedServicesEnabled"] = "false",
                ["FileLogging:Enabled"] = "false"
            };
            cfg.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            // Remove specific hosted services so they do not start in tests
            var hostedToRemove = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            (d.ImplementationType == typeof(BackgroundTaskRunner)
                             || d.ImplementationType == typeof(SecurityPriceWorker)
                             || (d.ImplementationType?.Name == "MonthlyReminderScheduler")))
                .ToList();
            foreach (var d in hostedToRemove)
            {
                services.Remove(d);
            }

            // Remove existing AppDbContext registration
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Use a uniquely-named shared-cache in-memory database (not a shared SqliteConnection object):
            // a single SqliteConnection instance is not safe for concurrent use from multiple threads and
            // causes "SQLite Error 5: database is locked" when the background task runner and an HTTP
            // request thread access the database at the same time. Each AppDbContext instead opens its own
            // connection against the same named in-memory database (Mode=Memory;Cache=Shared), which SQLite
            // handles safely for concurrent access. The anchor connection below is kept open for the
            // lifetime of the factory so the named in-memory database isn't dropped between uses.
            var dbName = $"testdb_{Guid.NewGuid():N}";
            var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(connectionString);
            });

            // If a fixed time was requested by the test, replace the application's TimeProvider
            // registration so server-side code observes the deterministic time.
            if (FixedUtcNow.HasValue)
            {
                var dtDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
                if (dtDescriptor != null)
                {
                    services.Remove(dtDescriptor);
                }

                services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedUtcNow.Value));
            }

            // Ensure schema is created for the fresh database using migrations
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.Migrate();

            // Seed a bootstrap admin user so that test registrations are never treated as the first user.
            // Without this, the first registered user in each test run would automatically receive Admin rights.
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            const string adminRole = "Admin";
            if (!roleManager.RoleExistsAsync(adminRole).GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new IdentityRole<Guid> { Name = adminRole, NormalizedName = adminRole.ToUpperInvariant() }).GetAwaiter().GetResult();
            }
            var bootstrapAdmin = new User(BootstrapAdminUsername, isAdmin: true)
            {
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                LockoutEnabled = false,
            };
            var createBootstrapResult = userManager.CreateAsync(bootstrapAdmin, BootstrapAdminPassword).GetAwaiter().GetResult();
            if (!createBootstrapResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed bootstrap admin user: {string.Join("; ", createBootstrapResult.Errors.Select(e => e.Description))}");
            }

            var addToRoleResult = userManager.AddToRoleAsync(bootstrapAdmin, adminRole).GetAwaiter().GetResult();
            if (!addToRoleResult.Succeeded)
            {
                throw new InvalidOperationException($"Failed to assign bootstrap admin role: {string.Join("; ", addToRoleResult.Errors.Select(e => e.Description))}");
            }
        });
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        private readonly long _timestamp;

        public FixedTimeProvider(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
            _timestamp = Stopwatch.GetTimestamp();
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
