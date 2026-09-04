using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace FinanceManager.Tests.Updates;

/// <summary>
/// Minimal <see cref="IWebHostEnvironment"/> stub used by update/deployment tests that need a hosting
/// environment with a controllable content root but no real ASP.NET Core host behind it.
/// </summary>
internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestWebHostEnvironment"/> class.
    /// </summary>
    /// <param name="root">Directory used as both content root and web root.</param>
    public TestWebHostEnvironment(string root)
    {
        ContentRootPath = root;
        WebRootPath = root;
    }

    public string ApplicationName { get; set; } = "Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
