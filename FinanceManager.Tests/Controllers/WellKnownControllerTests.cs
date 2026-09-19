using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Application.WellKnown;
using FinanceManager.Shared.Dtos.Admin;
using FinanceManager.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="WellKnownController"/> covering the anonymous
/// <c>/.well-known/change-password</c> redirect endpoint and the admin-only settings
/// read/update endpoints including the request-level URL validation.
/// </summary>
public sealed class WellKnownControllerTests
{
    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    private static (WellKnownController controller, Mock<IWellKnownSettingsService> service) Create(
        bool isAdmin = false)
    {
        var service = new Mock<IWellKnownSettingsService>(MockBehavior.Strict);

        var controller = new WellKnownController(service.Object);

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "testuser")
            },
            authenticationType: "test");

        if (isAdmin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                TraceIdentifier = "trace-well-known"
            }
        };

        return (controller, service);
    }

    // ---------------------------------------------------------------------------
    // GET /.well-known/change-password — public endpoint
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the public endpoint issues an HTTP 302 redirect to the configured local path.
    /// </summary>
    [Fact]
    public async Task GetChangePassword_Returns302_WithConfiguredLocalUrl()
    {
        var (controller, service) = Create();
        service
            .Setup(s => s.GetChangePasswordUrlAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("/change-password");

        var result = await controller.GetChangePasswordRedirectAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Permanent.Should().BeFalse();
        redirect.Url.Should().Be("/change-password");
    }

    /// <summary>
    /// Verifies that an admin-configured absolute URL is used verbatim — the endpoint intentionally
    /// permits external targets because the value is administrator-controlled.
    /// </summary>
    [Fact]
    public async Task GetChangePassword_Returns302_WithConfiguredExternalUrl()
    {
        var (controller, service) = Create();
        service
            .Setup(s => s.GetChangePasswordUrlAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://idp.example.com/account/password");

        var result = await controller.GetChangePasswordRedirectAsync(CancellationToken.None);

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("https://idp.example.com/account/password");
    }

    /// <summary>
    /// Verifies that the public redirect action carries <c>[AllowAnonymous]</c> — password managers
    /// must be able to discover the change-password URL without a session.
    /// </summary>
    [Fact]
    public void GetChangePassword_AllowAnonymousAttributePresent()
    {
        var method = typeof(WellKnownController)
            .GetMethod(nameof(WellKnownController.GetChangePasswordRedirectAsync));

        method.Should().NotBeNull();
        method!.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull(
            "GetChangePasswordRedirectAsync must be reachable without authentication");
    }

    // ---------------------------------------------------------------------------
    // GET api/admin/well-known — admin role via attribute
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that an authenticated admin can read the raw settings DTO used to populate the admin editor.
    /// </summary>
    [Fact]
    public async Task GetSettings_WithAdminRole_Returns200()
    {
        var (controller, service) = Create(isAdmin: true);
        var dto = new WellKnownSettingsDto { ChangePasswordUrl = "/change-password" };

        service
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await controller.GetSettingsAsync(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(dto);
    }

    /// <summary>
    /// Verifies the authorization contract declaratively rather than through the ASP.NET Core pipeline
    /// (which unit tests bypass): asserts that <c>GetSettingsAsync</c> carries
    /// <c>[Authorize(Roles = "Admin")]</c>, so non-admins are rejected with 403 at request time.
    /// </summary>
    [Fact]
    public void GetSettings_WithoutAdminRole_Returns403_AuthorizeAttributeRequiresAdminRole()
    {
        var method = typeof(WellKnownController)
            .GetMethod(nameof(WellKnownController.GetSettingsAsync));

        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull("GetSettingsAsync must be protected by [Authorize]");
        authorizeAttr!.Roles.Should().Be("Admin");
    }

    // ---------------------------------------------------------------------------
    // PUT api/admin/well-known — update
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a valid settings update from an admin is persisted via the service and returns 204.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_WithAdminRole_Returns204()
    {
        var (controller, service) = Create(isAdmin: true);
        var request = new WellKnownSettingsUpdateRequest("/account/password");

        service
            .Setup(s => s.UpdateAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.UpdateSettingsAsync(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Verifies that an invalid URL value fails the request DTO's data-annotation validation and is
    /// rejected without reaching the update service.
    /// </summary>
    /// <param name="url">A change-password URL that violates the expected format.</param>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("foo")]
    [InlineData("//host/path")]
    [InlineData("ftp://host/path")]
    public async Task UpdateSettings_InvalidUrl_Returns400(string url)
    {
        var (controller, service) = Create(isAdmin: true);
        var request = new WellKnownSettingsUpdateRequest(url);

        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        validationResults.Should().NotBeEmpty();
        foreach (var validationResult in validationResults)
        {
            var memberName = validationResult.MemberNames.FirstOrDefault() ?? nameof(WellKnownSettingsUpdateRequest.ChangePasswordUrl);
            controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage ?? "Validation failed.");
        }

        var result = await controller.UpdateSettingsAsync(request, CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>();
        service.Verify(s => s.UpdateAsync(It.IsAny<WellKnownSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
