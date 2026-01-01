namespace FinanceManager.Web
{

    /// <summary>
    /// Application entry point for the FinanceManager Web application.
    /// Configures the web host, registers services, applies migrations and starts the HTTP server.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Application entry method. Builds and runs the web host.
        /// </summary>
        /// <param name="args">Command line arguments forwarded to the web host builder.</param>
        /// <remarks>
        /// This method configures logging and application services using extension methods on the <see cref="WebApplicationBuilder"/>,
        /// applies EF migrations and seed data, configures localization and middleware and finally starts the HTTP server.
        /// Exceptions during startup will terminate the process — callers may observe process exit codes for failure diagnostics.
        /// </remarks>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // configure logging and services moved to extensions
            builder.ConfigureLogging();
            builder.RegisterAppServices();

            var app = builder.Build();

            // apply migrations and seeding
            app.ApplyMigrationsAndSeed();

            // configure localization and middleware
            app.ConfigureLocalization();
            app.ConfigureMiddleware();

            app.Run();
        }
    }

}