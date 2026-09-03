using FinanceManager.Application.Securities;
using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Shared.Dtos.Statements;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Statements;

/// <summary>
/// Covers <see cref="MassImportOrchestrator.ProcessAsync"/>, the entry point for batch-uploading security price
/// files: resolving which security each file belongs to, deciding whether the user must confirm ambiguous or
/// unrecognized files based on the configured <see cref="MassImportDialogPolicy"/>, and dispatching recognized
/// files to the matching <see cref="ISecurityPriceImportService"/>.
/// </summary>
public sealed class MassImportOrchestratorTests
{
    /// <summary>
    /// When the security can be resolved unambiguously from the file content and the policy only asks for
    /// confirmation on missing information, the orchestrator should import the file straight away without
    /// surfacing a confirmation dialog to the user.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldSkipDialogAndImport_WhenPolicyIsOnMissingAndFileIsComplete()
    {
        var security = new SecurityDto { Id = Guid.NewGuid(), Name = "Test Security", Identifier = "ABC123", IsActive = true };
        var fixture = CreateFixture(
            activeSecurities: [security],
            resolvedSecurity: security,
            importResult: new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var request = new MassImportBatchRequestDto
        {
            DialogPolicy = MassImportDialogPolicy.OnMissingInformation,
            ConfirmExecution = false,
            Files = new[]
            {
                new MassImportFileUploadDto
                {
                    FileId = Guid.NewGuid(),
                    FileName = "prices.csv",
                    Content = "sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray()
                }
            }
        };

        var result = await fixture.Orchestrator.ProcessAsync(Guid.NewGuid(), request, "trace-1", CancellationToken.None);

        Assert.False(result.RequiresConfirmation);
        Assert.True(result.DialogSkipped);
        Assert.Single(result.Files);
        Assert.Equal(MassImportFileExecutionStatus.Imported, result.Files[0].ExecutionStatus);
    }

    /// <summary>
    /// When a file's header names a security that cannot be matched against any active security, the orchestrator
    /// must require the confirmation dialog and mark the file as pending / not importable rather than guessing or
    /// silently dropping it.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldRequireDialog_WhenSecurityAssignmentIsMissing()
    {
        var fixture = CreateFixture(
            activeSecurities: [],
            resolvedSecurity: null,
            importResult: new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var request = new MassImportBatchRequestDto
        {
            DialogPolicy = MassImportDialogPolicy.OnMissingInformation,
            ConfirmExecution = false,
            Files = new[]
            {
                new MassImportFileUploadDto
                {
                    FileId = Guid.NewGuid(),
                    FileName = "prices.csv",
                    Content = "sep=;\nZeit;Unknown Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray()
                }
            }
        };

        var result = await fixture.Orchestrator.ProcessAsync(Guid.NewGuid(), request, "trace-2", CancellationToken.None);

        Assert.True(result.RequiresConfirmation);
        Assert.True(result.DialogRequired);
        Assert.Equal(MassImportFileExecutionStatus.Pending, result.Files[0].ExecutionStatus);
        Assert.False(result.Files[0].CanImport);
    }

    /// <summary>
    /// Guards against trusting a stale user decision: even after the user confirms a security selection in the
    /// dialog, the orchestrator must re-resolve that security right before import rather than assuming the earlier
    /// choice is still valid. Here re-resolution fails (the security service returns no match for the chosen id),
    /// so the file must fail with the "not available or inactive" validation message and the underlying import
    /// service must never be invoked.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldRevalidateSecurityBeforeImport_WhenUserConfirms()
    {
        var chosenSecurityId = Guid.NewGuid();
        var fixture = CreateFixture(
            activeSecurities: [],
            resolvedSecurity: null,
            importResult: new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var fileId = Guid.NewGuid();
        var request = new MassImportBatchRequestDto
        {
            DialogPolicy = MassImportDialogPolicy.OnMissingInformation,
            ConfirmExecution = true,
            Files = new[]
            {
                new MassImportFileUploadDto
                {
                    FileId = fileId,
                    FileName = "prices.csv",
                    Content = "sep=;\nZeit;Unknown Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray()
                }
            },
            Decisions = new[]
            {
                new MassImportFileDecisionDto
                {
                    FileId = fileId,
                    Excluded = false,
                    SelectedSecurityId = chosenSecurityId
                }
            }
        };

        var result = await fixture.Orchestrator.ProcessAsync(Guid.NewGuid(), request, "trace-3", CancellationToken.None);

        Assert.Equal(MassImportFileExecutionStatus.Failed, result.Files[0].ExecutionStatus);
        Assert.Contains("inactive", result.Files[0].ValidationMessage, StringComparison.OrdinalIgnoreCase);
        fixture.ImportServiceMock.Verify(x => x.ImportAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Stream>(),
            It.IsAny<SecurityPriceImportContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Even when a file is fully recognizable and its security can be resolved without any ambiguity, the
    /// <see cref="MassImportDialogPolicy.AlwaysConfirm"/> policy must still force the confirmation dialog and
    /// leave the file in a pending, not-yet-imported state.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldRequireDialog_WhenPolicyIsAlwaysConfirm()
    {
        var security = new SecurityDto { Id = Guid.NewGuid(), Name = "Test Security", Identifier = "ABC123", IsActive = true };
        var fixture = CreateFixture(
            activeSecurities: [security],
            resolvedSecurity: security,
            importResult: new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var request = new MassImportBatchRequestDto
        {
            DialogPolicy = MassImportDialogPolicy.AlwaysConfirm,
            ConfirmExecution = false,
            Files = new[]
            {
                new MassImportFileUploadDto
                {
                    FileId = Guid.NewGuid(),
                    FileName = "prices.csv",
                    Content = "sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray()
                }
            }
        };

        var result = await fixture.Orchestrator.ProcessAsync(Guid.NewGuid(), request, "trace-4", CancellationToken.None);

        Assert.True(result.RequiresConfirmation);
        Assert.True(result.DialogRequired);
        Assert.False(result.DialogSkipped);
        Assert.Equal(MassImportFileExecutionStatus.Pending, result.Files[0].ExecutionStatus);
    }

    /// <summary>
    /// When the user's confirmation decision explicitly excludes a file (rather than assigning it a security), the
    /// orchestrator must honor that choice by skipping the file and recording the decision as user-confirmed,
    /// without ever calling the import service for that file.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldSkipExcludedFile_WhenUserConfirmsExclusion()
    {
        var security = new SecurityDto { Id = Guid.NewGuid(), Name = "Test Security", Identifier = "ABC123", IsActive = true };
        var fixture = CreateFixture(
            activeSecurities: [security],
            resolvedSecurity: security,
            importResult: new SecurityPriceImportResultDto(1, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()));

        var fileId = Guid.NewGuid();
        var request = new MassImportBatchRequestDto
        {
            DialogPolicy = MassImportDialogPolicy.OnMissingInformation,
            ConfirmExecution = true,
            Files = new[]
            {
                new MassImportFileUploadDto
                {
                    FileId = fileId,
                    FileName = "prices.csv",
                    Content = "sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray()
                }
            },
            Decisions = new[]
            {
                new MassImportFileDecisionDto
                {
                    FileId = fileId,
                    Excluded = true
                }
            }
        };

        var result = await fixture.Orchestrator.ProcessAsync(Guid.NewGuid(), request, "trace-5", CancellationToken.None);

        Assert.False(result.RequiresConfirmation);
        Assert.Equal(MassImportDecisionSource.UserConfirmed, result.Files[0].DecisionSource);
        Assert.Equal(MassImportFileExecutionStatus.Skipped, result.Files[0].ExecutionStatus);
        fixture.ImportServiceMock.Verify(x => x.ImportAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<Stream>(),
            It.IsAny<SecurityPriceImportContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// When no registered <see cref="ISecurityPriceImportService"/> recognizes the uploaded file's content, the
    /// orchestrator must classify the file as <see cref="MassImportFileType.Unknown"/>, mark it as not importable,
    /// require confirmation, and surface a "file type could not be recognized" message instead of throwing or
    /// silently ignoring the file.
    /// </summary>
    [Fact]
    public async Task ProcessAsync_ShouldMarkFileAsUnknown_WhenNoSecurityImportServiceIsRegistered()
    {
        var fixture = CreateFixture(
            activeSecurities: [],
            resolvedSecurity: null,
            importResult: new SecurityPriceImportResultDto(0, 0, 0, 0, Array.Empty<SecurityPriceImportErrorDto>()),
            hasMatchingImportService: false);

        var request = new MassImportBatchRequestDto
        {
            DialogPolicy = MassImportDialogPolicy.OnMissingInformation,
            ConfirmExecution = false,
            Files = new[]
            {
                new MassImportFileUploadDto
                {
                    FileId = Guid.NewGuid(),
                    FileName = "prices.csv",
                    ContentType = "text/csv",
                    Content = "sep=;\nZeit;Test Security\n01.07.2026 00:00:00;10,00\n"u8.ToArray()
                }
            }
        };

        var result = await fixture.Orchestrator.ProcessAsync(Guid.NewGuid(), request, "trace-6", CancellationToken.None);

        Assert.True(result.RequiresConfirmation);
        Assert.Single(result.Files);
        Assert.Equal(MassImportFileType.Unknown, result.Files[0].FileType);
        Assert.False(result.Files[0].CanImport);
        Assert.Contains("File type could not be recognized", result.Files[0].ValidationMessage, StringComparison.Ordinal);
    }

    private static Fixture CreateFixture(
        IReadOnlyList<SecurityDto> activeSecurities,
        SecurityDto? resolvedSecurity,
        SecurityPriceImportResultDto importResult,
        bool hasMatchingImportService = true)
    {
        var statementDraftService = new Mock<IStatementDraftService>();
        statementDraftService
            .Setup(x => x.CreateDraftAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyDrafts());

        var statementFileFactory = new Mock<IStatementFileFactory>();
        statementFileFactory
            .Setup(x => x.Load(It.IsAny<string>(), It.IsAny<byte[]>()))
            .Returns((IStatementFile?)null);

        var securityService = new Mock<ISecurityService>();
        securityService
            .Setup(x => x.ListAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSecurities);
        securityService
            .Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedSecurity);

        var importService = new Mock<ISecurityPriceImportService>();
        importService
            .Setup(x => x.ImportAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Stream>(),
                It.IsAny<SecurityPriceImportContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(importResult);

        var importServiceFactory = new Mock<ISecurityPriceImportServiceFactory>();
        if (hasMatchingImportService)
        {
            ISecurityPriceImportService? detectedService = importService.Object;
            SecurityPriceImportInspectionResult? inspection = new("ing", "ing", "ING", "Test Security");
            importServiceFactory
                .Setup(x => x.TryResolveByContent(
                    It.IsAny<SecurityPriceImportContext>(),
                    It.IsAny<byte[]>(),
                    out detectedService,
                    out inspection))
                .Returns(true);
            importServiceFactory
                .Setup(x => x.Resolve(It.IsAny<SecurityPriceImportContext>()))
                .Returns(importService.Object);
        }
        else
        {
            ISecurityPriceImportService? detectedService = null;
            SecurityPriceImportInspectionResult? inspection = null;
            importServiceFactory
                .Setup(x => x.TryResolveByContent(
                    It.IsAny<SecurityPriceImportContext>(),
                    It.IsAny<byte[]>(),
                    out detectedService,
                    out inspection))
                .Returns(false);
            importServiceFactory
                .Setup(x => x.Resolve(It.IsAny<SecurityPriceImportContext>()))
                .Throws(new InvalidOperationException("No matching security price import service found for the uploaded file."));
        }

        var orchestrator = new MassImportOrchestrator(
            statementDraftService.Object,
            statementFileFactory.Object,
            [],
            securityService.Object,
            importServiceFactory.Object,
            NullLogger<MassImportOrchestrator>.Instance);

        return new Fixture(orchestrator, importService);
    }

    private static async IAsyncEnumerable<StatementDraftDto> EmptyDrafts()
    {
        await Task.CompletedTask;
        yield break;
    }

    private sealed record Fixture(MassImportOrchestrator Orchestrator, Mock<ISecurityPriceImportService> ImportServiceMock);
}
