using FinanceManager.Application.WellKnown;
using FinanceManager.Shared.Dtos.Admin;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Exposes public well-known endpoints and admin configuration endpoints.
/// </summary>
[ApiController]
public sealed class WellKnownController : ControllerBase
{
    private readonly IWellKnownSettingsService _service;

    /// <summary>Creates a new controller instance.</summary>
    /// <param name="service">The service.</param>
    public WellKnownController(IWellKnownSettingsService service)
    {
        _service = service;
    }

    /// <summary>Redirects to the configured change-password page (RFC 8615 well-known URI).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpGet("/.well-known/change-password")]
    [AllowAnonymous]
    public async Task<IActionResult> GetChangePasswordRedirectAsync(CancellationToken ct)
        => Redirect(await _service.GetChangePasswordUrlAsync(ct));

    /// <summary>Returns the current admin settings.</summary>
    /// <returns>The result.</returns>
    [HttpGet("api/admin/well-known")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<IActionResult> GetSettingsAsync(CancellationToken ct) => Ok(await _service.GetAsync(ct));

    /// <summary>Updates the current admin settings.</summary>
    /// <param name="request">Request payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPut("api/admin/well-known")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
    public async Task<IActionResult> UpdateSettingsAsync([FromBody] WellKnownSettingsUpdateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await _service.UpdateAsync(request, ct);
        return NoContent();
    }
}
