using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace FinanceManager.Tests.Updates;

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
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
