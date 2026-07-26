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

    // xUnit constructs many TestWebApplicationFactory instances concurrently (one per test class, run in
    // parallel collections by default). CI runs (fewer/slower cores than local dev machines) intermittently
    // hit transient "no such table" / SQLite errors during this startup phase, affecting whichever test
    // class's factory happened to be initializing at that moment - a different test each time, consistent
    // with a race in concurrent SQLite native-library-level database creation rather than a bug specific to
    // any one factory's own connection handling. Serializing schema creation and seeding across all factory
    // instances removes that concurrent-construction window; it only affects one-time test startup cost, not
    // the actual concurrent request handling the tests exercise.
    private static readonly SemaphoreSlim InitializationGate = new(1, 1);

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
            // connection against the same named in-memory database, which SQLite handles safely for
            // concurrent access. The anchor connection below is kept open for the lifetime of the factory
            // so the named in-memory database isn't dropped between uses.
            //
            // The "file:...?mode=memory&cache=shared" URI form is used deliberately instead of the
            // "Data Source=name;Mode=Memory;Cache=Shared" keyword form: per SQLite's own documentation,
            // only the URI filename syntax is guaranteed to give multiple separate connections a genuinely
            // shared, name-addressable in-memory database. The keyword form was observed to work locally but
            // failed intermittently in CI with "no such table" errors, consistent with connections sometimes
            // not actually attaching to the same underlying in-memory database.
            // "Default Timeout" sets SQLite's busy-retry timeout (seconds): when two separate connections
            // briefly contend for the same shared-cache database, the later one waits instead of immediately
            // failing with "database is locked".
            var dbName = $"testdb_{Guid.NewGuid():N}";
            var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared;Default Timeout=30";

            // Serialize the whole create-database/migrate/seed critical section across all concurrently
            // constructing TestWebApplicationFactory instances (see InitializationGate for why).
            InitializationGate.Wait();
            try
            {
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

                // Create and migrate the schema directly against the anchor connection object (not through the
                // connection-string-based DI registration used above). This removes any timing dependency on
                // SQLite's shared-cache database-name registration between opening the anchor connection and a
                // second, separately-opened connection resolved via DI reaching the same named database -
                // schema creation happens on the exact connection instance kept open for the factory's lifetime.
                var migrationOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
                using (var migrationDb = new AppDbContext(migrationOptions))
                {
                    migrationDb.Database.EnsureDeleted();
                    migrationDb.Database.Migrate();
                }

                // Seed a bootstrap admin user so that test registrations are never treated as the first user.
                // Without this, the first registered user in each test run would automatically receive Admin rights.
                // Resolved through the normal DI-registered (connection-string-based) AppDbContext: by this point
                // the schema is guaranteed present on the shared in-memory database via the anchor connection above.
                using var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
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
            }
            finally
            {
                InitializationGate.Release();
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
