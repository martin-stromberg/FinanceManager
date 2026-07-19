using FinanceManager.Application;
using FinanceManager.Domain.Users;
using FinanceManager.Infrastructure; // AppDbContext
using FinanceManager.Infrastructure.Auth;
using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web.Controllers;
using FinanceManager.Web.Infrastructure.Auth;
using FinanceManager.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace FinanceManager.Tests.Controllers;

public sealed class UserImportSplitSettingsControllerTests
{
    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid UserId { get; set; }
        public string? PreferredLanguage => null;
        public bool IsAuthenticated => true;
        public bool IsAdmin => false;
    }

    private static (UserSettingsController controller, AppDbContext db, TestCurrentUser currentUser) Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Users.Add(new User("u", "h", isAdmin: false));
        db.SaveChanges();

        var current = new TestCurrentUser { UserId = db.Users.Single().Id };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var sp = services.BuildServiceProvider();
        var localizer = sp.GetRequiredService<IStringLocalizer<FinanceManager.Web.Controllers.Controller>>();

        var logger = LoggerFactory.Create(b => { }).CreateLogger<UserSettingsController>();

        var jwtMock = new Mock<IJwtTokenService>();
        jwtMock.Setup(j => j.CreateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), out It.Ref<DateTime>.IsAny, It.IsAny<string?>(), It.IsAny<string?>()))
               .Returns("token");
        var tokenProviderMock = new Mock<IAuthTokenProvider>();

        var store = new Mock<IUserStore<User>>();
        var userManagerMock = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        userManagerMock.Setup(um => um.IsInRoleAsync(It.IsAny<User>(), "Admin")).ReturnsAsync(false);
        userManagerMock.Setup(um => um.GetSecurityStampAsync(It.IsAny<User>())).ReturnsAsync((User u) => u.SecurityStamp ?? "stamp");
        var alphaVantageSecretProtectorMock = new Mock<IAlphaVantageSecretProtector>();

        var controller = new UserSettingsController(db, current, logger, localizer, jwtMock.Object, tokenProviderMock.Object, userManagerMock.Object, alphaVantageSecretProtectorMock.Object);
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, current.UserId.ToString()) }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, db, current);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnDefaults()
    {
        var (controller, _, _) = Create();
        var result = await controller.GetImportSplitAsync(CancellationToken.None) as OkObjectResult;
        Assert.NotNull(result);
        var dto = result!.Value as ImportSplitSettingsDto;
        Assert.NotNull(dto);
        Assert.Equal(ImportSplitMode.MonthlyOrFixed, dto!.Mode);
        Assert.Equal(250, dto.MaxEntriesPerDraft);
        Assert.Equal(250, dto.MonthlySplitThreshold);
        Assert.Equal(8, dto.MinEntriesPerDraft); // new default
        Assert.Equal(MassImportDialogPolicy.OnMissingInformation, dto.MassImportDialogPolicy);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistValues_IncludingMinEntries()
    {
        var (controller, db, _) = Create();
        var req = new ImportSplitSettingsUpdateRequest(
            Mode: ImportSplitMode.MonthlyOrFixed,
            MaxEntriesPerDraft: 300,
            MonthlySplitThreshold: 350,
            MinEntriesPerDraft: 5,
            MassImportDialogPolicy: MassImportDialogPolicy.AlwaysConfirm);
        var resp = await controller.UpdateImportSplitAsync(req, CancellationToken.None);
        Assert.IsType<NoContentResult>(resp);

        var user = await db.Users.SingleAsync();
        Assert.Equal(ImportSplitMode.MonthlyOrFixed, user.ImportSplitMode);
        Assert.Equal(300, user.ImportMaxEntriesPerDraft);
        Assert.Equal(5, user.ImportMinEntriesPerDraft);
        Assert.Equal(MassImportDialogPolicy.AlwaysConfirm, user.MassImportDialogPolicy);
    }

    [Fact]
    public async Task UpdateAsync_ShouldValidateThreshold()
    {
        var (controller, db, _) = Create();
        var req = new ImportSplitSettingsUpdateRequest(
            Mode: ImportSplitMode.MonthlyOrFixed,
            MaxEntriesPerDraft: 300,
            MonthlySplitThreshold: 100,
            MinEntriesPerDraft: 8,
            MassImportDialogPolicy: MassImportDialogPolicy.OnMissingInformation);
        var resp = await controller.UpdateImportSplitAsync(req, CancellationToken.None);
        var obj = Assert.IsType<ObjectResult>(resp);
        var details = Assert.IsType<ValidationProblemDetails>(obj.Value);
        Assert.True(details.Errors.ContainsKey(nameof(req.MonthlySplitThreshold)));

        var user = await db.Users.SingleAsync();
        Assert.Equal(250, user.ImportMaxEntriesPerDraft); // unchanged
        Assert.Equal(8, user.ImportMinEntriesPerDraft);   // unchanged default
    }

    [Fact]
    public async Task UpdateAsync_ShouldAllowFixedSizeWithoutThreshold_AndPersistMinEntries()
    {
        var (controller, db, _) = Create();
        var req = new ImportSplitSettingsUpdateRequest(
            Mode: ImportSplitMode.FixedSize,
            MaxEntriesPerDraft: 400,
            MonthlySplitThreshold: null,
            MinEntriesPerDraft: 3,
            MassImportDialogPolicy: MassImportDialogPolicy.AlwaysConfirm);
        var resp = await controller.UpdateImportSplitAsync(req, CancellationToken.None);
        Assert.IsType<NoContentResult>(resp);

        var user = await db.Users.SingleAsync();
        Assert.Equal(ImportSplitMode.FixedSize, user.ImportSplitMode);
        Assert.Equal(400, user.ImportMaxEntriesPerDraft);
        Assert.Equal(3, user.ImportMinEntriesPerDraft);
        Assert.Equal(MassImportDialogPolicy.AlwaysConfirm, user.MassImportDialogPolicy);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenMinEntriesGreaterThanMax()
    {
        var (controller, db, _) = Create();
        var req = new ImportSplitSettingsUpdateRequest(
            Mode: ImportSplitMode.Monthly,
            MaxEntriesPerDraft: 50,
            MonthlySplitThreshold: null,
            MinEntriesPerDraft: 60,
            MassImportDialogPolicy: MassImportDialogPolicy.OnMissingInformation);
        var resp = await controller.UpdateImportSplitAsync(req, CancellationToken.None);
        var obj = Assert.IsType<ObjectResult>(resp);
        var details = Assert.IsType<ValidationProblemDetails>(obj.Value);
        Assert.True(details.Errors.ContainsKey(nameof(req.MinEntriesPerDraft)));

        var user = await db.Users.SingleAsync();
        Assert.Equal(250, user.ImportMaxEntriesPerDraft); // unchanged
        Assert.Equal(8, user.ImportMinEntriesPerDraft);   // unchanged
    }
}
