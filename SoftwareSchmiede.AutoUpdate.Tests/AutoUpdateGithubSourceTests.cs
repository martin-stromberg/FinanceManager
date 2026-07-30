using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateGithubSourceTests
{
    [Fact]
    public void Create_WithEmptyOwner_Throws()
    {
        var act = () => AutoUpdateGithubSource.Create("", "Repo");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Check_ParsesManifestResponse()
    {
        var json = $$"""
        {
          "version": "5.0.0",
          "releaseNotes": "notes",
          "publishedAt": "2026-07-01T00:00:00+00:00",
          "assets": [
            { "platform": "windows", "runtimeIdentifier": "win-x64", "assetName": "app.zip", "assetUrl": "https://github.com/o/r/releases/download/v5.0.0/app.zip", "sha256": "{{new string('a', 64)}}", "sizeBytes": 10 }
          ]
        }
        """;
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var source = new AutoUpdateGithubSource(new HttpClient(handler), "owner", "repo", new AutoUpdatePlatformResolver(p => p == OSPlatform.Windows, "win-x64"));

        var result = await source.CheckAsync();

        result.AvailableVersion.Should().Be("5.0.0");
        result.Package.Should().NotBeNull();
        result.Package!.FileName.Should().Be("app.zip");
    }

    [Fact]
    public async Task Download_WhenResponseExceedsLimit_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[1000])
        });
        var source = new AutoUpdateGithubSource(new HttpClient(handler), "owner", "repo");
        var descriptor = new AutoUpdatePackageDescriptor("1.0.0", "windows", "win-x64", "app.zip", new Uri("https://example.test/app.zip"), new string('a', 64), 1000);
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var act = () => source.DownloadAsync(descriptor, Path.Combine(dir.FullName, "app.zip"), maxBytes: 10);

            await act.Should().ThrowAsync<InvalidOperationException>();
            Directory.GetFiles(dir.FullName).Should().BeEmpty("the temporary .tmp file must be cleaned up when the download fails");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Download_WhenHttpFails_Throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var source = new AutoUpdateGithubSource(new HttpClient(handler), "owner", "repo");
        var descriptor = new AutoUpdatePackageDescriptor("1.0.0", "windows", "win-x64", "app.zip", new Uri("https://example.test/app.zip"), new string('a', 64), 10);
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var act = () => source.DownloadAsync(descriptor, Path.Combine(dir.FullName, "app.zip"), maxBytes: 1000);

            await act.Should().ThrowAsync<HttpRequestException>();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request, cancellationToken));
    }
}
