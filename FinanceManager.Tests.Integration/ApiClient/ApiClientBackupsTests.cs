using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;

namespace FinanceManager.Tests.Integration.ApiClient;

/// <summary>
/// End-to-end coverage for the backup API: creating, listing, uploading, downloading and deleting backup
/// files, plus the multi-step apply/status/cancel flow used to restore a backup onto the current data.
/// </summary>
public class ApiClientBackupsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    /// <summary>
    /// Initializes the test with the shared <see cref="TestWebApplicationFactory"/>, which hosts the
    /// application in-memory for the duration of the test class.
    /// </summary>
    /// <param name="factory">The shared in-memory application host injected by xUnit's class fixture.</param>
    public ApiClientBackupsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private FinanceManager.Shared.ApiClient CreateClient()
    {
        var http = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        return new FinanceManager.Shared.ApiClient(http);
    }

    private async Task EnsureAuthenticatedAsync(FinanceManager.Shared.ApiClient api)
    {
        var username = $"user_{Guid.NewGuid():N}";
        await api.Auth_RegisterAsync(new RegisterRequest(username, "Secret123", PreferredLanguage: null, TimeZoneId: null));
    }

    /// <summary>
    /// Verifies that a fresh user has no backups listed, and that creating one immediately makes it show
    /// up in the list - the basic create/list round trip the rest of the backup flow builds on.
    /// </summary>
    [Fact]
    public async Task List_InitiallyEmpty_Create_AddsEntry()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        var list1 = await api.Backups_ListAsync(TestContext.Current.CancellationToken);
        list1.Should().NotBeNull();
        list1.Should().BeEmpty();

        var created = await api.Backups_CreateAsync(TestContext.Current.CancellationToken);
        created.Should().NotBeNull();
        created.FileName.Should().NotBeNullOrEmpty();

        var list2 = await api.Backups_ListAsync(TestContext.Current.CancellationToken);
        list2.Should().NotBeNull();
        list2.Should().NotBeEmpty();
        list2.Any(b => b.Id == created.Id).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the upload endpoint enforces the zip container format - a raw .ndjson payload is
    /// rejected even though it is the same content that lives inside a valid backup zip - and that a
    /// correctly zipped backup is accepted and appears in the backup list under its uploaded file name.
    /// </summary>
    [Fact]
    public async Task Upload_AllowsValidZip_AndRejectsNdjson()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        await using var ndjson = new MemoryStream(Encoding.UTF8.GetBytes(CreateValidNdjson()));
        var ndjsonUpload = () => api.Backups_UploadAsync(ndjson, "backup.ndjson");
        await ndjsonUpload.Should().ThrowAsync<HttpRequestException>();

        // upload zip content
        await using var zipStream = CreateZip("backup.ndjson", CreateValidNdjson());
        var up2 = await api.Backups_UploadAsync(zipStream, "custom.zip", TestContext.Current.CancellationToken);
        up2.Should().NotBeNull();
        up2.FileName.Should().Be("custom.zip");

        var list = await api.Backups_ListAsync(TestContext.Current.CancellationToken);
        list.Should().ContainSingle(b => b.Id == up2.Id);
    }

    /// <summary>
    /// Verifies that uploading bytes with a valid zip file signature but corrupt/truncated content is
    /// rejected with the specific "Err_Backup_InvalidZip" error code, so the caller can distinguish a
    /// malformed archive from other upload failures rather than getting a generic error.
    /// </summary>
    [Fact]
    public async Task Upload_InvalidZip_ReturnsBadRequest()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);

        await using var zipStream = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        var act = () => api.Backups_UploadAsync(zipStream, "custom.zip");

        await act.Should().ThrowAsync<HttpRequestException>();
        api.LastErrorCode.Should().Be("Err_Backup_InvalidZip");
    }

    /// <summary>
    /// Verifies that a created backup can be downloaded as a non-empty stream, and that after deleting it
    /// the same download call returns null instead of a stale or empty stream - confirming delete actually
    /// removes the backend file rather than only the list entry.
    /// </summary>
    [Fact]
    public async Task Download_ReturnsStream_AndDelete_Removes()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var created = await api.Backups_CreateAsync(TestContext.Current.CancellationToken);
        var stream = await api.Backups_DownloadAsync(created.Id, TestContext.Current.CancellationToken);
        stream.Should().NotBeNull();
        stream!.Length.Should().BeGreaterThan(0);

        var deleted = await api.Backups_DeleteAsync(created.Id, TestContext.Current.CancellationToken);
        deleted.Should().BeTrue();

        var streamMissing = await api.Backups_DownloadAsync(created.Id, TestContext.Current.CancellationToken);
        streamMissing.Should().BeNull();
    }

    /// <summary>
    /// Verifies the guarded restore workflow: applying a backup without repeating its file name as an
    /// explicit confirmation is rejected with "Err_Backup_ConfirmationRequired" (protecting against an
    /// accidental, destructive data overwrite), while a correctly confirmed request starts an apply
    /// operation that can be polled via the status endpoint and aborted via cancel.
    /// </summary>
    [Fact]
    public async Task StartApply_Status_Cancel_Flow()
    {
        var api = CreateClient();
        await EnsureAuthenticatedAsync(api);
        var created = await api.Backups_CreateAsync(TestContext.Current.CancellationToken);

        var missingConfirmation = () => api.Backups_StartApplyAsync(created.Id, new BackupRestoreRequestDto(null, created.FileName));
        await missingConfirmation.Should().ThrowAsync<HttpRequestException>();
        api.LastErrorCode.Should().Be("Err_Backup_ConfirmationRequired");

        var status = await api.Backups_StartApplyAsync(created.Id, new BackupRestoreRequestDto(created.FileName, created.FileName), TestContext.Current.CancellationToken);
        status.Running.Should().BeTrue();

        var polled = await api.Backups_GetStatusAsync(TestContext.Current.CancellationToken);
        polled.Should().NotBeNull();

        var canceled = await api.Backups_CancelAsync(TestContext.Current.CancellationToken);
        canceled.Should().BeTrue();
    }

    private static MemoryStream CreateZip(string entryName, string ndjson)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.NoCompression);
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(ndjson);
            entryStream.Write(bytes, 0, bytes.Length);
        }

        stream.Position = 0;
        return stream;
    }

    private static string CreateValidNdjson()
    {
        var data = new Dictionary<string, object[]>
        {
            ["Accounts"] = [],
            ["Contacts"] = [],
            ["ContactCategories"] = [],
            ["AliasNames"] = [],
            ["SavingsPlanCategories"] = [],
            ["SavingsPlans"] = [],
            ["SecurityCategories"] = [],
            ["Securities"] = [],
            ["SecurityPrices"] = [],
            ["StatementImports"] = [],
            ["StatementEntries"] = [],
            ["Postings"] = [],
            ["StatementDrafts"] = [],
            ["StatementDraftEntries"] = [],
            ["ReportFavorites"] = [],
            ["HomeKpis"] = [],
            ["AttachmentCategories"] = [],
            ["Attachments"] = [],
            ["Notifications"] = [],
            ["AccountShares"] = [],
            ["BudgetCategories"] = [],
            ["BudgetPurposes"] = [],
            ["BudgetRules"] = [],
            ["BudgetOverrides"] = []
        };

        return JsonSerializer.Serialize(new { Type = "Backup", Version = 3 }) + "\n" + JsonSerializer.Serialize(data);
    }
}
