using System.Reflection;
using FinanceManager.Application;
using FinanceManager.Shared;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using FinanceManager.Web.ViewModels.Home;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace FinanceManager.Tests.ViewModels;

/// <summary>
/// Covers <c>HomeViewModel</c>'s mass-import file drop workflow: how uploaded statement files are turned
/// into a pending confirmation dialog, how per-file security selection and exclusion decisions are collected,
/// and how confirming the dialog submits those decisions back to the API and applies the execution result.
/// </summary>
public sealed class HomeViewModelTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public bool IsAdmin { get; set; } = false;
    }

    private static (HomeViewModel vm, Mock<IApiClient> apiMock) CreateVm()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        var apiMock = new Mock<IApiClient>();
        services.AddSingleton(apiMock.Object);
        var vm = new HomeViewModel(services.BuildServiceProvider());
        return (vm, apiMock);
    }

    /// <summary>
    /// Verifies that dropping a file while the user's import-split policy is "always confirm" surfaces a
    /// pending mass-import dialog populated with the file and the active securities list to choose from,
    /// instead of importing silently in the background.
    /// </summary>
    [Fact]
    public async Task ProcessMassImportSelectionAsync_ShouldOpenPendingDialog_WhenConfirmationIsRequired()
    {
        var (vm, apiMock) = CreateVm();
        var security = new SecurityDto { Id = Guid.NewGuid(), Name = "Test Security", Identifier = "ABC123", IsActive = true };

        apiMock
            .Setup(x => x.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportSplitSettingsDto { MassImportDialogPolicy = MassImportDialogPolicy.AlwaysConfirm });
        apiMock
            .Setup(x => x.StatementDrafts_ProcessMassImportAsync(It.IsAny<MassImportBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MassImportBatchRequestDto request, CancellationToken _) => new MassImportBatchResultDto
            {
                DialogRequired = true,
                RequiresConfirmation = true,
                Files = request.Files
                    .Select(file => new MassImportBatchFileResultDto
                    {
                        FileId = file.FileId,
                        FileName = file.FileName,
                        FileType = MassImportFileType.SecurityPrices,
                        ServiceKey = "ing",
                        ServiceDisplayName = "ING",
                        CanImport = false,
                        ExecutionStatus = MassImportFileExecutionStatus.Pending
                    })
                    .ToList()
            });
        apiMock
            .Setup(x => x.Securities_ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([security]);

        await InvokeProcessMassImportSelectionAsync(vm, [new FakeBrowserFile("prices.csv", "text/csv", "sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray())]);

        Assert.NotNull(vm.PendingMassImport);
        Assert.Single(vm.PendingMassImport!.Files);
        Assert.Equal(MassImportDialogPolicy.AlwaysConfirm, vm.MassImportDialogPolicy);
        Assert.Single(vm.ActiveSecurities);
        Assert.Equal(security.Id, vm.ActiveSecurities[0].Id);
        Assert.False(vm.UploadInProgress);
    }

    /// <summary>
    /// Verifies the full confirm round-trip: after the user assigns a security to a pending file and
    /// confirms, the view model submits a request with <c>ConfirmExecution</c> set and the user's per-file
    /// decision, then clears the pending dialog and applies the server's execution result (success flag and
    /// resulting draft id) once the import actually runs.
    /// </summary>
    [Fact]
    public async Task ConfirmMassImportAsync_ShouldSubmitDecisionsAndApplyExecutionResult()
    {
        var (vm, apiMock) = CreateVm();
        var securityId = Guid.NewGuid();
        MassImportBatchRequestDto? confirmRequest = null;

        apiMock
            .Setup(x => x.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportSplitSettingsDto { MassImportDialogPolicy = MassImportDialogPolicy.OnMissingInformation });
        apiMock
            .Setup(x => x.StatementDrafts_ProcessMassImportAsync(It.IsAny<MassImportBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MassImportBatchRequestDto request, CancellationToken _) =>
            {
                if (!request.ConfirmExecution)
                {
                    return new MassImportBatchResultDto
                    {
                        DialogRequired = true,
                        RequiresConfirmation = true,
                        Files = request.Files
                            .Select(file => new MassImportBatchFileResultDto
                            {
                                FileId = file.FileId,
                                FileName = file.FileName,
                                FileType = MassImportFileType.SecurityPrices,
                                ServiceKey = "ing",
                                ServiceDisplayName = "ING",
                                CanImport = false,
                                ExecutionStatus = MassImportFileExecutionStatus.Pending,
                                ValidationMessage = "Missing security assignment."
                            })
                            .ToList()
                    };
                }

                confirmRequest = request;
                return new MassImportBatchResultDto
                {
                    DialogRequired = false,
                    RequiresConfirmation = false,
                    Files = request.Files
                        .Select(file => new MassImportBatchFileResultDto
                        {
                            FileId = file.FileId,
                            FileName = file.FileName,
                            FileType = MassImportFileType.SecurityPrices,
                            ServiceKey = "ing",
                            ServiceDisplayName = "ING",
                            CanImport = true,
                            ExecutionStatus = MassImportFileExecutionStatus.Imported,
                            StatementDraftId = Guid.NewGuid()
                        })
                        .ToList()
                };
            });
        apiMock
            .Setup(x => x.Securities_ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SecurityDto { Id = securityId, Name = "My Security", Identifier = "XYZ", IsActive = true }]);

        await InvokeProcessMassImportSelectionAsync(vm, [new FakeBrowserFile("prices.csv", "text/csv", "sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray())]);
        var fileId = vm.PendingMassImport!.Files[0].FileId;
        vm.SetPendingFileSecurity(fileId, securityId);
        vm.SetPendingFileExcluded(fileId, false);

        await vm.ConfirmMassImportAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(confirmRequest);
        Assert.True(confirmRequest!.ConfirmExecution);
        Assert.Equal(MassImportDialogPolicy.OnMissingInformation, confirmRequest.DialogPolicy);
        Assert.Single(confirmRequest.Decisions);
        Assert.Equal(securityId, confirmRequest.Decisions[0].SelectedSecurityId);
        Assert.Null(vm.PendingMassImport);
        Assert.True(vm.ImportSuccess);
        Assert.NotNull(vm.FirstDraftId);
    }

    /// <summary>
    /// Verifies that a file the server could not classify (<see cref="MassImportFileType.Unknown"/>) is
    /// forced to <c>Excluded = true</c> in the pending dialog by default, protecting the user from
    /// accidentally importing a file the system does not recognize, while still allowing them to manually
    /// re-include it afterward via <c>SetPendingFileExcluded</c>.
    /// </summary>
    [Fact]
    public async Task ProcessMassImportSelectionAsync_ShouldForceExcludeUnknownType_AndIgnoreManualSelection()
    {
        var (vm, apiMock) = CreateVm();

        apiMock
            .Setup(x => x.UserSettings_GetImportSplitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportSplitSettingsDto { MassImportDialogPolicy = MassImportDialogPolicy.OnMissingInformation });
        apiMock
            .Setup(x => x.StatementDrafts_ProcessMassImportAsync(It.IsAny<MassImportBatchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MassImportBatchRequestDto request, CancellationToken _) => new MassImportBatchResultDto
            {
                DialogRequired = true,
                RequiresConfirmation = true,
                Files = request.Files
                    .Select(file => new MassImportBatchFileResultDto
                    {
                        FileId = file.FileId,
                        FileName = file.FileName,
                        FileType = MassImportFileType.Unknown,
                        ServiceKey = string.Empty,
                        ServiceDisplayName = string.Empty,
                        CanImport = false,
                        Excluded = false,
                        ExecutionStatus = MassImportFileExecutionStatus.Pending
                    })
                    .ToList()
            });
        apiMock
            .Setup(x => x.Securities_ListAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SecurityDto>());

        await InvokeProcessMassImportSelectionAsync(vm, [new FakeBrowserFile("unknown.bin", "application/octet-stream", "data"u8.ToArray())]);

        Assert.NotNull(vm.PendingMassImport);
        var file = Assert.Single(vm.PendingMassImport!.Files);
        Assert.True(file.Excluded);

        vm.SetPendingFileExcluded(file.FileId, false);

        Assert.True(vm.PendingMassImport.Files[0].Excluded);
    }

    private static async Task InvokeProcessMassImportSelectionAsync(HomeViewModel vm, IReadOnlyList<IBrowserFile> files)
    {
        var method = typeof(HomeViewModel).GetMethod("ProcessMassImportSelectionAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(vm, [files]));
        await task;
    }

    private sealed class FakeBrowserFile : IBrowserFile
    {
        private readonly byte[] _content;

        public FakeBrowserFile(string name, string contentType, byte[] content)
        {
            Name = name;
            ContentType = contentType;
            _content = content;
        }

        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => _content.Length;
        public string ContentType { get; }

        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => new MemoryStream(_content, writable: false);
    }
}
