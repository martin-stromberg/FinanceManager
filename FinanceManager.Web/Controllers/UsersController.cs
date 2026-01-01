using FinanceManager.Application.Users;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// User management (read-only placeholder). Forwards calls to services and catches unexpected exceptions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class UsersController : ControllerBase
{
    private readonly IUserReadService _userReadService;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="userReadService">Service used to read user information.</param>
    /// <param name="logger">Logger instance for this controller.</param>
    public UsersController(IUserReadService userReadService, ILogger<UsersController> logger)
    {
        _userReadService = userReadService;
        _logger = logger;
    }

    /// <summary>
    /// Returns true if any user exists in the system.
    /// </summary>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> which contains a 200 OK response with an <see cref="AnyUsersResponse"/> value when successful.
    /// In case of cancellation a ProblemDetails response with status code 499 (Client Closed Request) is returned.
    /// For unexpected errors a 500 Internal Server Error ProblemDetails response is returned.
    /// </returns>
    /// <remarks>
    /// This is a minimal endpoint; more administration endpoints (list, create, lock, etc.) will be added later.
    /// </remarks>
    /// <exception cref="OperationCanceledException">When the provided cancellation token is triggered. This is handled and mapped to a 499 response.</exception>
    /// <exception cref="Exception">An unexpected exception that is logged and results in a 500 response.</exception>
    [HttpGet("exists")]
    [ProducesResponseType(typeof(AnyUsersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HasAnyUsersAsync(CancellationToken ct)
    {
        try
        {
            bool any = await _userReadService.HasAnyUsersAsync(ct);
            return Ok(new AnyUsersResponse(any));
        }
        catch (OperationCanceledException)
        {
            // Let canceled requests bubble up as 499/Client Closed or generic cancellation.
            _logger.LogInformation("HasAnyUsersAsync cancelled");
            return Problem(statusCode: StatusCodes.Status499ClientClosedRequest, title: "Request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking for existing users");
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error", detail: ex.Message);
        }
    }
}
