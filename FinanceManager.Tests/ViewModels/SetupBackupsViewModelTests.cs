using FinanceManager.Application;
using FinanceManager.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <see cref="SetupBackupsViewModel"/>'s backup list/create/delete lifecycle and starting a
/// restore-from-backup ("apply") operation. Unlike most view model tests in this project, these exercise
/// the real <see cref="FinanceManager.Shared.ApiClient"/> against a fake <see cref="HttpMessageHandler"/>
/// that asserts on the actual HTTP method/route/body, verifying the wire contract rather than just an
/// interface mock.
/// </summary>
public sealed class SetupBackupsViewModelTests
{
    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new HttpClient(new DelegateHandler(responder)) { BaseAddress = new Uri("http://localhost") };

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    private static IServiceProvider CreateSp(IApiClient apiClient)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton<IApiClient>(apiClient);
        return services.BuildServiceProvider();
    }

    private static string ListJson(params object[] items) => JsonSerializer.Serialize(items);

    private static FinanceManager.Shared.IApiClient CreateApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var http = CreateHttpClient(responder);
        return new FinanceManager.Shared.ApiClient(http);
    }

    /// <summary>
    /// Verifies that loading backups issues a GET to <c>/api/setup/backups</c> and deserializes the
    /// returned list into the view model's <c>Backups</c> collection.
    /// </summary>
    [Fact]
    public async Task Initialize_Loads_List()
    {
        var item = new { Id = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow, FileName = "b1.zip", SizeBytes = 123L, Source = "Manual" };
        var api = CreateApiClient(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == "/api/setup/backups")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ListJson(item), Encoding.UTF8, "application/json") };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var vm = new SetupBackupsViewModel(CreateSp(api));
        await vm.LoadBackupsAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(vm.Backups);
        Assert.Single(vm.Backups);
        Assert.Equal("b1.zip", vm.Backups![0].FileName);
    }

    /// <summary>
    /// Verifies the create/delete round-trip against the real HTTP routes: creating a backup POSTs to
    /// <c>/api/setup/backups</c> and inserts the returned item into the list, and deleting it DELETEs the
    /// specific <c>/api/setup/backups/{id}</c> route and removes it from the list.
    /// </summary>
    [Fact]
    public async Task Create_Inserts_Item_And_Delete_Removes()
    {
        var created = new { Id = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow, FileName = "b2.zip", SizeBytes = 456L, Source = "Manual" };
        var api = CreateApiClient(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == "/api/setup/backups")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ListJson(), Encoding.UTF8, "application/json") };
            }
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/api/setup/backups")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(created), Encoding.UTF8, "application/json") };
            }
            if (req.Method == HttpMethod.Delete && req.RequestUri!.AbsolutePath == $"/api/setup/backups/{created.Id}")
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var vm = new SetupBackupsViewModel(CreateSp(api));
        await vm.LoadBackupsAsync(TestContext.Current.CancellationToken);

        await vm.CreateAsync(TestContext.Current.CancellationToken);
        Assert.Single(vm.Backups!);
        Assert.Equal("b2.zip", vm.Backups![0].FileName);

        await vm.DeleteAsync(created.Id, TestContext.Current.CancellationToken);
        Assert.Empty(vm.Backups!);
    }

    /// <summary>
    /// Verifies that starting a restore ("apply") for a backup POSTs the file name in the request body to
    /// <c>/api/setup/backups/{id}/apply/start</c> and, on success, marks the view model as having an active
    /// restore in progress so the UI can switch to a progress display.
    /// </summary>
    [Fact]
    public async Task StartApply_Sets_Flag_On_Success()
    {
        var id = Guid.NewGuid();
        const string fileName = "backup.zip";
        string? requestBody = null;
        var api = CreateApiClient(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == "/api/setup/backups")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ListJson(), Encoding.UTF8, "application/json") };
            }
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == $"/api/setup/backups/{id}/apply/start")
            {
                requestBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var status = new FinanceManager.Shared.Dtos.Admin.BackupRestoreStatusDto(true, 0, 1, null, null, 0, 0, null);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(status), Encoding.UTF8, "application/json") };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var vm = new SetupBackupsViewModel(CreateSp(api));
        await vm.LoadBackupsAsync(TestContext.Current.CancellationToken);

        await vm.StartApplyAsync(id, fileName, fileName, TestContext.Current.CancellationToken);
        Assert.True(vm.HasActiveRestore);
        Assert.Contains(fileName, requestBody);
    }
}
