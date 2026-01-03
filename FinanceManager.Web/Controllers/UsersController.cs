using FinanceManager.Application.Users;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using FinanceManager.Application.Demo;

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
    private readonly IDemoDataService _demoDataService;
    private readonly ILogger<UsersController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    /// <param name="userReadService">Service used to read user information.</param>
    /// <param name="demoDataService">Service responsible for creating demo data for a user.</param>
    /// <param name="logger">Logger instance for this controller.</param>
    public UsersController(IUserReadService userReadService, IDemoDataService demoDataService, ILogger<UsersController> logger)
    {
        _userReadService = userReadService;
        _demoDataService = demoDataService;
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

    /// <summary>
    /// Creates demo data for the specified user. This is intended for development and demo scenarios only.
    /// The method will schedule or execute creation of various domain entities under the provided user account.
    /// </summary>
    /// <param name="userId">Identifier of the user for whom demo data should be created.</param>
    /// <param name="createPostings">When true, statement imports and entries (postings) will also be created as part of the demo data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 202 Accepted when the request was accepted for processing.
    /// 400 Bad Request when the <paramref name="userId"/> is empty.
    /// 500 Internal Server Error for unexpected errors.
    /// </returns>
    [HttpPost("demo/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateDemoDataAsync(Guid userId, [FromBody] DemoRequest req, CancellationToken ct)
    {
        if (userId == Guid.Empty) return BadRequest("userId required");
        try
        {
            await _demoDataService.CreateDemoDataAsync(userId, req.createPostings, ct);
            return Accepted();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CreateDemoDataAsync cancelled for {UserId}", userId);
            return Problem(statusCode: StatusCodes.Status499ClientClosedRequest, title: "Request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating demo data for user {UserId}", userId);
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error", detail: ex.Message);
        }
    }
}
