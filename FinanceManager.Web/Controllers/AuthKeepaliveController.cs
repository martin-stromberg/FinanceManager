using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Provides an authenticated no-op endpoint that lets the JWT refresh middleware renew the auth cookie.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/auth/keepalive")]
public sealed class AuthKeepaliveController : ControllerBase
{
    /// <summary>
    /// Returns success for an authenticated session without loading or mutating domain data.
    /// </summary>
    /// <returns>HTTP 204 when the current session is authenticated.</returns>
    [HttpGet]
    public IActionResult Get()
    {
        return NoContent();
    }
}
