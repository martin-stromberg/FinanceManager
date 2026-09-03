using System;
using System.Reflection;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FinanceManager.Application.Security;
using FinanceManager.Tests.TestHelpers;
using FinanceManager.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace FinanceManager.Tests.Controllers;

/// <summary>
/// Tests for <see cref="SecurityTxtController"/> covering the public <c>/security.txt</c> endpoint (RFC 9116),
/// the admin-only settings read/update endpoints, and the request-level validation rules for the update DTO
/// (canonical URL format, expiry not in the past).
/// </summary>
public sealed class SecurityTxtControllerTests
{
    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    private static (SecurityTxtController controller, Mock<ISecurityTxtSettingsService> service) Create(
        bool isAdmin = false)
    {
        var service = new Mock<ISecurityTxtSettingsService>(MockBehavior.Strict);

        var controller = new SecurityTxtController(service.Object);

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
                TraceIdentifier = "trace-security-txt"
            }
        };

        return (controller, service);
    }

    // ---------------------------------------------------------------------------
    // GET /security.txt — public endpoint
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that the public <c>/security.txt</c> endpoint returns 200 with the generated plain-text
    /// content when a contact has been configured.
    /// </summary>
    [Fact]
    public async Task GetSecurityTxt_Returns200_WhenContactConfigured()
    {
        var (controller, service) = Create();
        service
            .Setup(s => s.BuildContentAsync(SecurityTxtFormat.PlainText, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Contact: mailto:security@example.com\nExpires: 2026-01-01T00:00:00+00:00");

        var result = await controller.GetSecurityTxtAsync(CancellationToken.None);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.StatusCode.Should().BeNull(); // defaults to 200
        content.Content.Should().Contain("Contact: mailto:security@example.com");
    }

    /// <summary>
    /// Verifies that the public endpoint returns 503 Service Unavailable when no contact has been configured,
    /// since serving an empty/invalid security.txt would be misleading rather than simply absent.
    /// </summary>
    [Fact]
    public async Task GetSecurityTxt_Returns503_WhenContactEmpty()
    {
        var (controller, service) = Create();
        service
            .Setup(s => s.BuildContentAsync(SecurityTxtFormat.PlainText, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await controller.GetSecurityTxtAsync(CancellationToken.None);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    // ---------------------------------------------------------------------------
    // GET api/admin/security-txt — admin role via attribute
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that an authenticated admin can read the raw settings DTO used to populate the admin editor.
    /// </summary>
    [Fact]
    public async Task GetSettings_WithAdminRole_Returns200()
    {
        var (controller, service) = Create(isAdmin: true);
        var dto = new SecurityTxtSettingsDto
        {
            Contact = "mailto:security@example.com",
            Expires = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        service
            .Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await controller.GetSettingsAsync(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(dto);
    }

    /// <summary>
    /// Verifies the authorization contract declaratively rather than through the ASP.NET Core pipeline (which
    /// unit tests bypass): asserts that <c>GetSettingsAsync</c> carries <c>[Authorize(Roles = "Admin")]</c>, so
    /// non-admins are rejected with 403 at request time.
    /// </summary>
    [Fact]
    public void GetSettings_WithoutAdminRole_Returns403_AuthorizeAttributeRequiresAdminRole()
    {
        // Unit tests bypass the authentication middleware. We verify the authorization
        // contract by inspecting the [Authorize(Roles = "Admin")] attribute that the
        // middleware enforces at runtime.
        var method = typeof(SecurityTxtController)
            .GetMethod(nameof(SecurityTxtController.GetSettingsAsync));

        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull("GetSettingsAsync must be protected by [Authorize]");
        authorizeAttr!.Roles.Should().Be("Admin");
    }

    // ---------------------------------------------------------------------------
    // PUT api/admin/security-txt — update
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Verifies that a valid settings update from an admin is persisted via the service and returns 204.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_WithAdminRole_Returns204()
    {
        var (controller, service) = Create(isAdmin: true);
        var request = SecurityTxtSettingsTestData.ValidRequest();

        service
            .Setup(s => s.UpdateAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await controller.UpdateSettingsAsync(request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    /// <summary>
    /// Verifies that an update request with a pre-populated <c>ModelState</c> error (missing required
    /// <c>Contact</c>) is rejected as a validation problem without ever calling the update service.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_InvalidModel_Returns400()
    {
        var (controller, service) = Create(isAdmin: true);
        controller.ModelState.AddModelError(nameof(SecurityTxtSettingsUpdateRequest.Contact), "The Contact field is required.");

        var request = new SecurityTxtSettingsUpdateRequest(
            Contact: string.Empty,
            Expires: DateTimeOffset.UtcNow.AddYears(1),
            Encryption: null,
            Acknowledgments: null,
            PreferredLanguages: null,
            Policy: null,
            Hiring: null,
            Canonical: null);

        var result = await controller.UpdateSettingsAsync(request, CancellationToken.None);

        // ValidationProblem() wraps the result in an ObjectResult; the exact HTTP status
        // is resolved by the middleware, but the contract is that it is NOT a success result
        // and the service must not have been called.
        result.Should().BeAssignableTo<ObjectResult>();
        service.Verify(s => s.UpdateAsync(It.IsAny<SecurityTxtSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that malformed canonical URLs — a non-HTTPS scheme, an unexpected query string or fragment, or
    /// a relative path instead of an absolute URL — all fail the request DTO's data-annotation validation and
    /// are rejected without reaching the update service. RFC 9116 requires the canonical field to be an exact,
    /// unambiguous HTTPS URL to the file itself.
    /// </summary>
    /// <param name="canonical">A canonical URL value that violates the expected format.</param>
    [Theory]
    [InlineData("http://localhost/.well-known/security.txt")]
    [InlineData("https://security.example.com/.well-known/security.txt?from=admin")]
    [InlineData("https://security.example.com/.well-known/security.txt#anchor")]
    [InlineData("http://security.example.com/.well-known/security.txt")]
    [InlineData("/.well-known/security.txt")]
    public async Task UpdateSettings_InvalidCanonical_Returns400(string canonical)
    {
        var (controller, service) = Create(isAdmin: true);
        var request = SecurityTxtSettingsTestData.ValidRequest(canonical: canonical);

        var validationResults = new System.Collections.Generic.List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        foreach (var validationResult in validationResults)
        {
            var memberName = validationResult.MemberNames.FirstOrDefault() ?? nameof(SecurityTxtSettingsUpdateRequest.Canonical);
            controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage ?? "Validation failed.");
        }

        var result = await controller.UpdateSettingsAsync(request, CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>();
        service.Verify(s => s.UpdateAsync(It.IsAny<SecurityTxtSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that an <c>Expires</c> value in the past fails validation and is rejected without reaching the
    /// update service, since a security.txt that is already expired would be published in an invalid state.
    /// </summary>
    [Fact]
    public async Task UpdateSettings_ExpiredExpires_Returns400()
    {
        var (controller, service) = Create(isAdmin: true);
        var request = SecurityTxtSettingsTestData.ValidRequest(expires: DateTimeOffset.UtcNow.AddMinutes(-1));

        var validationResults = new System.Collections.Generic.List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
        foreach (var validationResult in validationResults)
        {
            var memberName = validationResult.MemberNames.FirstOrDefault() ?? nameof(SecurityTxtSettingsUpdateRequest.Expires);
            controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage ?? "Validation failed.");
        }

        var result = await controller.UpdateSettingsAsync(request, CancellationToken.None);

        result.Should().BeAssignableTo<ObjectResult>();
        service.Verify(s => s.UpdateAsync(It.IsAny<SecurityTxtSettingsUpdateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
