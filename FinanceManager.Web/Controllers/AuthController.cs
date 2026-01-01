using FinanceManager.Application.Users;
using FinanceManager.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Handles user authentication: login, registration and logout using JWT cookie tokens.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserAuthService _auth;
    private readonly IAuthTokenProvider _tokenProvider;
    private const string AuthCookieName = "FinanceManager.Auth";

    /// <summary>
    /// Creates a new instance of <see cref="AuthController"/>.
    /// </summary>
    /// <param name="auth">Service handling user authentication operations (login/register).</param>
    /// <param name="tokenProvider">Token provider used to clear in-memory tokens on logout.</param>
    public AuthController(IUserAuthService auth, IAuthTokenProvider tokenProvider)
    { _auth = auth; _tokenProvider = tokenProvider; }

    /// <summary>
    /// Authenticates a user with username and password, returning a JWT (cookie) and user info.
    /// </summary>
    /// <param name="request">Login request payload containing username, password and optional localization hints.</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>
    /// HTTP 200 with an <see cref="AuthOkResponse"/> when authentication succeeds.
    /// HTTP 400 when the request model is invalid.
    /// HTTP 401 when authentication fails (invalid credentials).
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the request contains invalid or malformed data.</exception>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _auth.LoginAsync(new LoginCommand(request.Username, request.Password, ip, request.PreferredLanguage, request.TimeZoneId), ct);
        if (!result.Success)
        {
            return Unauthorized(new ApiErrorDto(result.Error!));
        }

        // Set cookie with explicit expiry that matches token expiry
        Response.Cookies.Append(AuthCookieName, result.Value!.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            Expires = new DateTimeOffset(result.Value.ExpiresUtc)
        });

        return Ok(new AuthOkResponse(result.Value.Username, result.Value.IsAdmin, result.Value.ExpiresUtc));
    }

    /// <summary>
    /// Registers a new user account and returns a JWT (cookie) for immediate authentication.
    /// </summary>
    /// <param name="request">Registration request payload containing username, password and optional localization hints.</param>
    /// <param name="ct">Cancellation token used to cancel the operation.</param>
    /// <returns>
    /// HTTP 200 with an <see cref="AuthOkResponse"/> when registration succeeds and a token is issued.
    /// HTTP 400 when the request model is invalid.
    /// HTTP 409 when a user with the same username already exists.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when the request contains invalid or malformed data.</exception>
    /// <exception cref="InvalidOperationException">Thrown when registration cannot proceed due to a conflicting existing user.</exception>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }
        var result = await _auth.RegisterAsync(new RegisterUserCommand(request.Username, request.Password, request.PreferredLanguage, request.TimeZoneId), ct);
        if (!result.Success)
        {
            return Conflict(new ApiErrorDto(result.Error!));
        }

        Response.Cookies.Append(AuthCookieName, result.Value!.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            Expires = new DateTimeOffset(result.Value.ExpiresUtc)
        });

        return Ok(new AuthOkResponse(result.Value.Username, result.Value.IsAdmin, result.Value.ExpiresUtc));
    }

    /// <summary>
    /// Logs the current user out by clearing the auth cookie and clearing the in-memory token cache where applicable.
    /// </summary>
    /// <returns>HTTP 200 when logout completed.</returns>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        if (Request.Cookies.ContainsKey(AuthCookieName))
        {
            Response.Cookies.Delete(AuthCookieName, new CookieOptions
            {
                Path = "/",
                Secure = Request.IsHttps,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax
            });
        }
        if (_tokenProvider is JwtCookieAuthTokenProvider concrete)
        {
            concrete.Clear();
        }
        return Ok();
    }
}
