using FinanceManager.Application.Notifications;
using FinanceManager.Domain.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Provides metadata endpoints for holiday provider information: available providers, supported countries and subdivisions.
/// Used by UI components to configure notification settings and holiday-aware scheduling.
/// </summary>
[ApiController]
[Route("api/meta")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class MetaHolidaysController : ControllerBase
{
    private readonly IHolidaySubdivisionService _service;

    /// <summary>
    /// Static list of supported ISO country codes used as a fallback for client UI.
    /// </summary>
    private static readonly string[] Countries = new[]
    {
        "DE","US","GB","AT","CH","FR","ES","IT","NL","BE","DK","SE","NO","FI","IE","PL","CZ","HU","PT"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="MetaHolidaysController"/>.
    /// </summary>
    /// <param name="service">Service used to resolve available subdivisions for a given provider and country.</param>
    public MetaHolidaysController(IHolidaySubdivisionService service)
    {
        _service = service;
    }

    /// <summary>
    /// Returns the available holiday provider kinds as string names of the <see cref="HolidayProviderKind"/> enum.
    /// </summary>
    /// <returns>200 OK with an array of provider names (strings).</returns>
    // GET api/meta/holiday-providers
    [HttpGet("holiday-providers")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public IActionResult GetProviders()
    {
        var values = Enum.GetNames(typeof(HolidayProviderKind));
        return Ok(values);
    }

    /// <summary>
    /// Returns the list of supported ISO country codes for holiday data that the UI can present to the user.
    /// </summary>
    /// <returns>200 OK with an array of ISO country codes (strings).</returns>
    // GET api/meta/holiday-countries
    [HttpGet("holiday-countries")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public IActionResult GetCountries() => Ok(Countries);

    /// <summary>
    /// Returns subdivision codes (state/region) for a given provider + country combination.
    /// The provider argument is parsed case-insensitively to <see cref="HolidayProviderKind"/>.
    /// </summary>
    /// <param name="provider">Provider kind name (case-insensitive) matching <see cref="HolidayProviderKind"/>.</param>
    /// <param name="country">ISO country code for which subdivisions should be returned.</param>
    /// <param name="ct">Cancellation token used to cancel the lookup operation.</param>
    /// <returns>
    /// 200 OK with an array of subdivision codes when the provider and country are recognized; an empty array when invalid input was supplied.
    /// </returns>
    /// <exception cref="OperationCanceledException">May be thrown when the operation is canceled via the provided <paramref name="ct"/>.</exception>
    // GET api/meta/holiday-subdivisions?provider=...&country=...
    [HttpGet("holiday-subdivisions")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubdivisions([FromQuery] string provider, [FromQuery] string country, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(provider))
        {
            return Ok(Array.Empty<string>());
        }
        if (!Enum.TryParse<HolidayProviderKind>(provider, ignoreCase: true, out var kind))
        {
            return Ok(Array.Empty<string>());
        }
        var list = await _service.GetSubdivisionsAsync(kind, country, ct);
        return Ok(list);
    }
}
