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

        await vm.ConfirmMassImportAsync();

        Assert.NotNull(confirmRequest);
        Assert.True(confirmRequest!.ConfirmExecution);
        Assert.Equal(MassImportDialogPolicy.OnMissingInformation, confirmRequest.DialogPolicy);
        Assert.Single(confirmRequest.Decisions);
        Assert.Equal(securityId, confirmRequest.Decisions[0].SelectedSecurityId);
        Assert.Null(vm.PendingMassImport);
        Assert.True(vm.ImportSuccess);
        Assert.NotNull(vm.FirstDraftId);
    }

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
        var task = Assert.IsType<Task>(method!.Invoke(vm, [files]));
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
