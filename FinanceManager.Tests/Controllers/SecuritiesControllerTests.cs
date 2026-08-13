using FinanceManager.Application;
using FinanceManager.Application.Attachments;
using FinanceManager.Application.Common;
using FinanceManager.Application.Reports;
using FinanceManager.Application.Securities;
using FinanceManager.Application.Securities.ReturnAnalysis;
using FinanceManager.Shared.Dtos.Common;
using FinanceManager.Shared.Dtos.Securities;
using FinanceManager.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="SecuritiesController"/> covering the create/update endpoints, with a focus on the
/// <c>Region</c>/<c>Sector</c> fields introduced for the portfolio analysis report's regional/sector distribution.
/// </summary>
public sealed class SecuritiesControllerTests
{
    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid UserId { get; set; } = Guid.NewGuid();
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static (
        SecuritiesController controller,
        Mock<ISecurityService> service,
        Mock<IParentAssignmentService> parentAssign,
        TestCurrentUserService currentUser)
        Create()
    {
        var service = new Mock<ISecurityService>();
        var currentUser = new TestCurrentUserService();
        var attachments = new Mock<IAttachmentService>();
        var tasks = new Mock<IBackgroundTaskManager>();
        var series = new Mock<IPostingTimeSeriesService>();
        var priceService = new Mock<ISecurityPriceService>();
        var priceImportFactory = new Mock<ISecurityPriceImportServiceFactory>();
        var reports = new Mock<ISecurityReportService>();
        var parentAssign = new Mock<IParentAssignmentService>();
        var localizer = new Mock<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();
        var returnAnalysis = new Mock<IReturnAnalysisService>();

        localizer
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: false));

        parentAssign
            .Setup(p => p.TryAssignAsync(It.IsAny<Guid>(), It.IsAny<ParentLinkRequest?>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new SecuritiesController(
            service.Object,
            currentUser,
            attachments.Object,
            tasks.Object,
            series.Object,
            priceService.Object,
            priceImportFactory.Object,
            reports.Object,
            NullLogger<SecuritiesController>.Instance,
            parentAssign.Object,
            localizer.Object,
            returnAnalysis.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return (controller, service, parentAssign, currentUser);
    }

    private static SecurityDto CreateDto(Guid id, string? region, string? sector) => new()
    {
        Id = id,
        Name = "Apple",
        Identifier = "US0378331005",
        CurrencyCode = "USD",
        IsActive = true,
        CreatedUtc = DateTime.UtcNow,
        Region = region,
        Sector = sector
    };

    /// <summary>
    /// CreateAsync passes Region/Sector from the request through to the service and returns them in the response DTO.
    /// </summary>
    [Fact]
    public async Task CreateAsync_Controller_PassesThroughRegionAndSector()
    {
        var (controller, service, _, currentUser) = Create();
        var securityId = Guid.NewGuid();
        var request = new SecurityRequest
        {
            Name = "Apple",
            Identifier = "US0378331005",
            CurrencyCode = "USD",
            Region = "Nordamerika",
            Sector = "Technologie"
        };

        service
            .Setup(s => s.CreateAsync(currentUser.UserId, request.Name, request.Identifier, request.Description, request.AlphaVantageCode, request.CurrencyCode, request.CategoryId, It.IsAny<CancellationToken>(), request.Region, request.Sector))
            .ReturnsAsync(CreateDto(securityId, request.Region, request.Sector));

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtRouteResult>().Subject;
        var dto = created.Value.Should().BeOfType<SecurityDto>().Subject;
        dto.Region.Should().Be("Nordamerika");
        dto.Sector.Should().Be("Technologie");
        service.Verify(s => s.CreateAsync(currentUser.UserId, request.Name, request.Identifier, request.Description, request.AlphaVantageCode, request.CurrencyCode, request.CategoryId, It.IsAny<CancellationToken>(), "Nordamerika", "Technologie"), Times.Once);
    }

    /// <summary>
    /// CreateAsync returns 400 BadRequest (via ValidationProblem) when ModelState is invalid, without calling the service.
    /// </summary>
    [Fact]
    public async Task CreateAsync_Controller_ReturnsBadRequest_WhenModelStateInvalid()
    {
        var (controller, service, _, _) = Create();
        controller.ModelState.AddModelError("Region", "Region must not exceed 255 characters");
        var request = new SecurityRequest { Name = "Apple", Identifier = "US0378331005", CurrencyCode = "USD" };

        var result = await controller.CreateAsync(request, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.Value.Should().BeOfType<ValidationProblemDetails>();
        service.Verify(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// CreateAsync maps an <see cref="ArgumentException"/> thrown by the service to a 400 BadRequest response.
    /// </summary>
    [Fact]
    public async Task CreateAsync_Controller_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var (controller, service, _, currentUser) = Create();
        var request = new SecurityRequest { Name = "Apple", Identifier = "US0378331005", CurrencyCode = "USD" };

        service
            .Setup(s => s.CreateAsync(currentUser.UserId, request.Name, request.Identifier, request.Description, request.AlphaVantageCode, request.CurrencyCode, request.CategoryId, It.IsAny<CancellationToken>(), request.Region, request.Sector))
            .ThrowsAsync(new ArgumentException("Security name must be unique per user", "name"));

        var result = await controller.CreateAsync(request, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    /// <summary>
    /// UpdateAsync passes Region/Sector from the request through to the service and returns them in the response DTO.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Controller_PassesThroughRegionAndSector()
    {
        var (controller, service, _, currentUser) = Create();
        var securityId = Guid.NewGuid();
        var request = new SecurityRequest
        {
            Name = "Apple Inc.",
            Identifier = "US0378331005",
            CurrencyCode = "USD",
            Region = "Europa",
            Sector = "Pharma"
        };

        service
            .Setup(s => s.UpdateAsync(securityId, currentUser.UserId, request.Name, request.Identifier, request.Description, request.AlphaVantageCode, request.CurrencyCode, request.CategoryId, It.IsAny<CancellationToken>(), request.Region, request.Sector))
            .ReturnsAsync(CreateDto(securityId, request.Region, request.Sector));

        var result = await controller.UpdateAsync(securityId, request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<SecurityDto>().Subject;
        dto.Region.Should().Be("Europa");
        dto.Sector.Should().Be("Pharma");
    }

    /// <summary>
    /// UpdateAsync returns 404 NotFound when the service reports that the security does not exist.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Controller_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var (controller, service, _, currentUser) = Create();
        var securityId = Guid.NewGuid();
        var request = new SecurityRequest { Name = "Apple", Identifier = "US0378331005", CurrencyCode = "USD" };

        service
            .Setup(s => s.UpdateAsync(securityId, currentUser.UserId, request.Name, request.Identifier, request.Description, request.AlphaVantageCode, request.CurrencyCode, request.CategoryId, It.IsAny<CancellationToken>(), request.Region, request.Sector))
            .ReturnsAsync((SecurityDto?)null);

        var result = await controller.UpdateAsync(securityId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }
}
