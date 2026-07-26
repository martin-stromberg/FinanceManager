#pragma warning disable CS1591
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    [HttpGet("api/health")]
    public IActionResult Get() => Ok(new { status = "ok" });
}
#pragma warning restore CS1591
