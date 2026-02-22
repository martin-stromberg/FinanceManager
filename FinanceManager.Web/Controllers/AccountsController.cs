using FinanceManager.Application;
using FinanceManager.Application.Accounts;
using FinanceManager.Application.Contacts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;

namespace FinanceManager.Web.Controllers;

/// <summary>
/// Provides CRUD operations for bank accounts owned by the current user, including linking to a bank contact and symbol management.
/// </summary>
[ApiController]
[Route("api/accounts")]
[Produces(MediaTypeNames.Application.Json)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _accounts;
    private readonly IContactService _contacts;
    private readonly ICurrentUserService _current;
    private readonly ILogger<AccountsController> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="AccountsController"/>.
    /// </summary>
    /// <param name="accounts">Service for account operations.</param>
    /// <param name="contacts">Service for contact operations used to create or lookup bank contacts.</param>
    /// <param name="current">Service providing information about the current user.</param>
    /// <param name="logger">Logger instance for the controller.</param>
    public AccountsController(IAccountService accounts, IContactService contacts, ICurrentUserService current, ILogger<AccountsController> logger)
    {
        _accounts = accounts;
        _contacts = contacts;
        _current = current;
        _logger = logger;
    }

    /// <summary>
    /// Returns a (paged) list of accounts owned by the current user. Optional filter by bank contact id.
    /// </summary>
    /// <param name="skip">Number of items to skip for paging.</param>
    /// <param name="take">Maximum number of items to return (clamped to 1..200).</param>
    /// <param name="bankContactId">Optional bank contact identifier to filter the accounts.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>HTTP 200 with a list of <see cref="AccountDto"/> matching the criteria.</returns>
    /// <exception cref="Exception">May throw on unexpected server errors which are translated to HTTP 500.</exception>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync([FromQuery] int skip = 0, [FromQuery] int take = 100, [FromQuery] Guid? bankContactId = null, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        try
        {
            var list = await _accounts.ListAsync(_current.UserId, skip, take, ct);
            if (bankContactId.HasValue)
            {
                list = list.Where(a => a.BankContactId == bankContactId.Value).ToList();
            }
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List accounts failed");
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets a single account by id. The account must belong to the current user.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// HTTP 200 with <see cref="AccountDto"/> when found; HTTP 404 when the account does not exist or is not owned by the current user.
    /// </returns>
    /// <exception cref="Exception">May throw on unexpected server errors which are translated to HTTP 500.</exception>
    [HttpGet("{id:guid}", Name = "GetAccount")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var dto = await _accounts.GetAsync(id, _current.UserId, ct);
            return dto is null ? NotFound() : Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get account {AccountId} failed", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Creates a new account for the current user. Either an existing bank contact id or a new bank contact name must be provided; when none are provided a bank contact will be auto-created.
    /// </summary>
    /// <param name="req">Account creation payload containing name, type, optional IBAN, bank contact info and optional symbol attachment id.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// HTTP 201 with the created <see cref="AccountDto"/> on success.
    /// HTTP 400 when provided arguments are invalid.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when provided input values are invalid; translated to HTTP 400 with error details.</exception>
    /// <exception cref="Exception">Unexpected server errors are translated to HTTP 500.</exception>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] AccountCreateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            Guid bankContactId;
            if (!string.IsNullOrWhiteSpace(req.NewBankContactName))
            {
                var createdContact = await _contacts.CreateAsync(_current.UserId, req.NewBankContactName.Trim(), ContactType.Bank, null, null, null, ct);
                bankContactId = createdContact.Id;
            }
            else if (req.BankContactId.HasValue)
            {
                bankContactId = req.BankContactId.Value;
            }
            else
            {
                // Auto-create a bank contact based on the account name or IBAN if none provided
                var contactName = !string.IsNullOrWhiteSpace(req.Name) ? req.Name.Trim() : (string.IsNullOrWhiteSpace(req.Iban) ? "Bankkonto" : req.Iban!.Trim());
                var createdContact = await _contacts.CreateAsync(_current.UserId, contactName, ContactType.Bank, null, null, null, ct);
                bankContactId = createdContact.Id;
            }

            var account = await _accounts.CreateAsync(_current.UserId, req.Name.Trim(), req.Type, req.Iban?.Trim(), bankContactId, req.SavingsPlanExpectation, req.SecurityProcessingEnabled, ct);
            if (req.SymbolAttachmentId.HasValue)
            {
                await _accounts.SetSymbolAttachmentAsync(account.Id, _current.UserId, req.SymbolAttachmentId.Value, ct);
                account = await _accounts.GetAsync(account.Id, _current.UserId, ct) ?? account;
            }
            return CreatedAtRoute("GetAccount", new { id = account.Id }, account);
        }
        catch (ArgumentException ex)
        {
            var code = !string.IsNullOrWhiteSpace(ex.ParamName) ? $"Err_Invalid_{ex.ParamName}" : "Err_InvalidArgument";
            return BadRequest(new { error = code, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create account failed");
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Updates an existing account (including optional bank contact change and symbol assignment).
    /// </summary>
    /// <param name="id">Identifier of the account to update.</param>
    /// <param name="req">Update payload containing new values.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// HTTP 200 with the updated <see cref="AccountDto"/> when successful; HTTP 404 when the account does not exist; HTTP 400 when input is invalid.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when provided input values are invalid; translated to HTTP 400.</exception>
    /// <exception cref="Exception">Unexpected server errors are translated to HTTP 500.</exception>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] AccountUpdateRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            Guid bankContactId;
            if (!string.IsNullOrWhiteSpace(req.NewBankContactName))
            {
                var createdContact = await _contacts.CreateAsync(_current.UserId, req.NewBankContactName.Trim(), ContactType.Bank, null, null, null, ct);
                bankContactId = createdContact.Id;
            }
            else if (req.BankContactId.HasValue)
            {
                bankContactId = req.BankContactId.Value;
            }
            else
            {
                return BadRequest(new { error = "Bank contact required (existing or new)" });
            }
            var updated = await _accounts.UpdateAsync(id, _current.UserId, req.Name.Trim(), req.Iban?.Trim(), bankContactId, req.SavingsPlanExpectation, req.SecurityProcessingEnabled, ct);
            if (updated is null) return NotFound();
            if (req.SymbolAttachmentId.HasValue)
            {
                await _accounts.SetSymbolAttachmentAsync(id, _current.UserId, req.SymbolAttachmentId.Value, ct);
                updated = await _accounts.GetAsync(id, _current.UserId, ct) ?? updated;
            }
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            var code = !string.IsNullOrWhiteSpace(ex.ParamName) ? $"Err_Invalid_{ex.ParamName}" : "Err_InvalidArgument";
            return BadRequest(new { error = code, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update account {AccountId} failed", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Deletes an account owned by the current user.
    /// </summary>
    /// <param name="id">Identifier of the account to delete.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>HTTP 204 when deletion succeeded; HTTP 404 when the account does not exist.</returns>
    /// <exception cref="Exception">Thrown on unexpected server errors which are translated to HTTP 500.</exception>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var ok = await _accounts.DeleteAsync(id, _current.UserId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete account {AccountId} failed", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Assigns an existing attachment as symbol for the account.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="attachmentId">Attachment identifier to assign as symbol.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>HTTP 204 when symbol assignment succeeds; HTTP 404 when account or attachment not found; HTTP 400 when input invalid.</returns>
    /// <exception cref="ArgumentException">Thrown when provided identifiers are invalid; translated to HTTP 404/400 as used in the controller.</exception>
    [HttpPost("{id:guid}/symbol/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetSymbolAsync(Guid id, Guid attachmentId, CancellationToken ct)
    {
        try
        {
            await _accounts.SetSymbolAttachmentAsync(id, _current.UserId, attachmentId, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "SetSymbol failed for account {AccountId}", id);
            var code = !string.IsNullOrWhiteSpace(ex.ParamName) ? $"Err_Invalid_{ex.ParamName}" : "Err_NotFound";
            return NotFound(new { error = code, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetSymbol failed for account {AccountId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }

    /// <summary>
    /// Clears any symbol attachment from the account.
    /// </summary>
    /// <param name="id">Account identifier.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>HTTP 204 when the symbol was cleared; HTTP 404 when the account or resource was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when provided identifiers are invalid; translated to HTTP 404/400 as used in the controller.</exception>
    [HttpDelete("{id:guid}/symbol")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearSymbolAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await _accounts.SetSymbolAttachmentAsync(id, _current.UserId, null, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "ClearSymbol failed for account {AccountId}", id);
            var code = !string.IsNullOrWhiteSpace(ex.ParamName) ? $"Err_Invalid_{ex.ParamName}" : "Err_NotFound";
            return NotFound(new { error = code, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ClearSymbol failed for account {AccountId}", id);
            return Problem("Unexpected error", statusCode: 500);
        }
    }
}
