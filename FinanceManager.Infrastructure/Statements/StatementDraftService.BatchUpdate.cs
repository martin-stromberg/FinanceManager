// Application DTOs consolidated into shared contracts; keep using shared DTOs only
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FinanceManager.Shared.Dtos.Statements;
using System.Globalization;
using System.Resources;
using System.Reflection;

namespace FinanceManager.Infrastructure.Statements;

public sealed partial class StatementDraftService
{
    /// <summary>
    /// Applies a batch of entry updates atomically. Validates inputs and applies all changes in a single DB transaction.
    /// Returns per-entry field errors when validation fails; in that case no changes are committed.
    /// </summary>
    /// <inheritdoc />
    public async Task<(bool Success, FinanceManager.Shared.Dtos.Statements.BatchUpdateSuccessResponseDto? SuccessResponse, FinanceManager.Shared.Dtos.Statements.BatchUpdateErrorResponseDto? ErrorResponse)> ApplyBatchEntryUpdatesAsync(Guid draftId, Guid ownerUserId, FinanceManager.Shared.Dtos.Statements.BatchUpdateRequestDto request, CancellationToken ct)
    {
        static string Loc(string key, string fallback)
        {
            try
            {
                var rm = new ResourceManager("FinanceManager.Infrastructure.Statements.Resources.StatementDraftService", Assembly.GetExecutingAssembly());
                var v = rm.GetString(key, CultureInfo.CurrentCulture);
                return string.IsNullOrEmpty(v) ? fallback : v;
            }
            catch
            {
                return fallback;
            }
        }

        if (request == null) throw new ArgumentNullException(nameof(request));

        _logger?.Log(LogLevel.Information, "User {User} requested batch update for draft {DraftId} with {Count} updates", ownerUserId, draftId, request.Updates?.Count ?? 0);

        // Load draft with entries and check ownership
        var draft = await _db.StatementDrafts.Include(d => d.Entries).FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (draft == null || draft.OwnerUserId != ownerUserId)
        {
            _logger?.Log(LogLevel.Warning, "Unauthorized batch update attempt for draft {DraftId} by user {User}", draftId, ownerUserId);
            throw new UnauthorizedAccessException();
        }

        var errors = new List<FinanceManager.Shared.Dtos.Statements.EntryErrorDto>();

        // Map entries for quick lookup
        var entryMap = draft.Entries.ToDictionary(e => e.Id, e => e);

        // gather proposed changes per entry (do not apply yet)
        // Added ValutaDate and BookingDescription so quick-edit changes for these fields are persisted
        var proposed = new Dictionary<Guid, (DateTime? BookingDate, DateTime? ValutaDate, decimal? Amount, string? Subject, string? BookingDescription, string? RecipientName, StatementDraftEntryStatus? Status)>();

        foreach (var upd in request.Updates)
        {
            var entryErrors = new List<FinanceManager.Shared.Dtos.Statements.FieldErrorDto>();
            if (!entryMap.TryGetValue(upd.EntryId, out var entry))
            {
                entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = string.Empty, Message = "Entry not found in draft" });
                errors.Add(new FinanceManager.Shared.Dtos.Statements.EntryErrorDto { EntryId = upd.EntryId, FieldErrors = entryErrors });
                continue;
            }

            // Business rule: editing restrictions (e.g., already-booked) are enforced by UpdateEntryCoreAsync; collect changes here.

            // start from current values
            DateTime? newBooking = entry.BookingDate;
            DateTime? newValuta = entry.ValutaDate;
            decimal? newAmount = entry.Amount;
            string? newSubject = entry.Subject;
            string? newBookingDesc = entry.BookingDescription;
            string? newRecipient = entry.RecipientName;
            StatementDraftEntryStatus? newStatus = (StatementDraftEntryStatus?)entry.Status;

            foreach (var kv in upd.Fields)
            {
                var key = kv.Key ?? string.Empty;
                var val = kv.Value;
                switch (key)
                {
                    case "BookingDate":
                        if (val == null)
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = Loc("BatchUpdate_BookingDateRequired", "Booking date is required") });
                            break;
                        }
                        DateTime parsedDate;
                        if (val is DateTime dtVal)
                        {
                            parsedDate = dtVal;
                        }
                        else
                        {
                            string? s = null;
                            if (val is System.Text.Json.JsonElement jeDate && jeDate.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                s = jeDate.GetString();
                            }
                            else
                            {
                                s = Convert.ToString(val, CultureInfo.InvariantCulture);
                            }

                            // Accept only strict date formats (reject invalid calendar dates)
                            var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "yyyy/MM/dd" };
                            if (string.IsNullOrWhiteSpace(s) || !DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                            {
                                entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid date format" });
                                break;
                            }
                        }

                        // schedule change
                        newBooking = parsedDate.Date;
                        break;
                    case "ValutaDate":
                        if (val == null)
                        {
                            // Treat explicit null as an invalid date input (client-side parse failed)
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid date format" });
                            break;
                        }
                        DateTime parsedValuta;
                        if (val is DateTime dv)
                        {
                            parsedValuta = dv;
                        }
                        else
                        {
                            string? s2 = null;
                            if (val is System.Text.Json.JsonElement jeVal && jeVal.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                s2 = jeVal.GetString();
                            }
                            else
                            {
                                s2 = Convert.ToString(val, CultureInfo.InvariantCulture);
                            }

                            var formats2 = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "yyyy/MM/dd" };
                            if (string.IsNullOrWhiteSpace(s2) || !DateTime.TryParseExact(s2.Trim(), formats2, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedValuta))
                            {
                                entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid date format" });
                                break;
                            }
                        }
                        newValuta = parsedValuta.Date;
                        break;
                    case "Amount":
                        if (val == null)
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Amount is required" });
                            break;
                        }
                        try
                        {
                            decimal dec;
                            if (val is System.Text.Json.JsonElement je)
                            {
                                if (je.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    // try get decimal directly
                                    try { dec = je.GetDecimal(); }
                                    catch { dec = Convert.ToDecimal(je.GetDouble(), CultureInfo.InvariantCulture); }
                                }
                                else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var se = je.GetString() ?? string.Empty;
                                    dec = Convert.ToDecimal(se, CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid amount" });
                                    break;
                                }
                            }
                            else
                            {
                                dec = Convert.ToDecimal(val, CultureInfo.InvariantCulture);
                            }

                            if (dec == 0m)
                            {
                                entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Amount must not be zero" });
                            }
                            else
                            {
                                newAmount = dec;
                            }
                        }
                        catch
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid amount" });
                        }
                        break;
                    case "Subject":
                        var subjText = Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
                        if (subjText.Length > 1000)
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Subject too long" });
                        }
                        else
                        {
                            newSubject = subjText;
                        }
                        break;
                    case "BookingDescription":
                        var bd = Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
                        if (bd.Length > 1000)
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Booking description too long" });
                        }
                        else
                        {
                            newBookingDesc = bd;
                        }
                        break;
                    case "RecipientName":
                        var r = Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
                        if (r.Length > 250)
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Recipient name too long" });
                        }
                        else
                        {
                            newRecipient = string.IsNullOrWhiteSpace(r) ? null : r;
                        }
                        break;
                    case "Status":
                        if (val == null)
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Status is required" });
                            break;
                        }
                        try
                        {
                            if (val is System.Text.Json.JsonElement jeStatus)
                            {
                                if (jeStatus.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var sVal = jeStatus.GetString() ?? string.Empty;
                                    if (Enum.TryParse<StatementDraftEntryStatus>(sVal, true, out var st)) newStatus = st;
                                    else entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid status value" });
                                }
                                else if (jeStatus.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    var intVal = jeStatus.GetInt32();
                                    if (Enum.IsDefined(typeof(StatementDraftEntryStatus), intVal)) newStatus = (StatementDraftEntryStatus)intVal;
                                    else entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid status value" });
                                }
                                else
                                {
                                    entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid status value" });
                                }
                            }
                            else if (val is string sVal)
                            {
                                if (Enum.TryParse<StatementDraftEntryStatus>(sVal, true, out var st)) newStatus = st;
                                else entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid status value" });
                            }
                            else
                            {
                                var intVal = Convert.ToInt32(val);
                                if (Enum.IsDefined(typeof(StatementDraftEntryStatus), intVal)) newStatus = (StatementDraftEntryStatus)intVal;
                                else entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid status value" });
                            }
                        }
                        catch
                        {
                            entryErrors.Add(new FinanceManager.Shared.Dtos.Statements.FieldErrorDto { Field = key, Message = "Invalid status value" });
                        }
                        break;
                    default:
                        // ignore unknown fields per API guidance
                        _logger?.Log(LogLevel.Debug, "Ignoring unknown field '{Field}' in batch update for entry {EntryId}", key, upd.EntryId);
                        break;
                }
            }

            if (entryErrors.Count > 0)
            {
                errors.Add(new FinanceManager.Shared.Dtos.Statements.EntryErrorDto { EntryId = upd.EntryId, FieldErrors = entryErrors });
            }
            else
            {
                proposed[upd.EntryId] = (newBooking, newValuta, newAmount, newSubject, newBookingDesc, newRecipient, newStatus);
            }
        }

        if (errors.Count > 0)
        {
            var errResp = new FinanceManager.Shared.Dtos.Statements.BatchUpdateErrorResponseDto { Errors = errors };
            return (false, null, errResp);
        }

        // All validations passed -> apply changes in transaction
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var kv in proposed)
            {
                var entryId = kv.Key;
                var p = kv.Value;
                // load current entry DTO values for parameters not changed
                var existing = await GetDraftEntryAsync(draftId, entryId, ct);
                if (existing == null) continue; // should not happen

                var bookingDate = p.BookingDate ?? existing.BookingDate;
                var valutaDate = p.ValutaDate ?? existing.ValutaDate;
                var amount = p.Amount ?? existing.Amount;
                var subject = p.Subject ?? existing.Subject ?? string.Empty;
                var bookingDesc = p.BookingDescription ?? existing.BookingDescription ?? string.Empty;
                var recipient = p.RecipientName ?? existing.RecipientName;

                var updatedEntry = await UpdateEntryCoreAsync(draftId, entryId, ownerUserId, bookingDate, valutaDate, amount, subject, recipient, null, bookingDesc, ct);

                // If caller requested an explicit status change (e.g., mark as AlreadyBooked), apply it here.
                if (p.Status.HasValue)
                {
                    try
                    {
                        var ent = await _db.StatementDraftEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.DraftId == draftId, ct);
                        if (ent != null)
                        {
                            // Apply requested status change using domain methods (setters are private)
                            if (p.Status.Value == StatementDraftEntryStatus.AlreadyBooked)
                            {
                                ent.MarkAlreadyBooked();
                            }
                            else if (p.Status.Value == StatementDraftEntryStatus.Accounted)
                            {
                                // If we already have a contact assignment, mark accounted; otherwise mark for manual check
                                if (ent.ContactId.HasValue)
                                    ent.MarkAccounted(ent.ContactId.Value);
                                else
                                    ent.MarkNeedsCheck();
                            }
                            else if (p.Status.Value == StatementDraftEntryStatus.Announced)
                            {
                                // ResetOpen will set to Announced when the entry was originally announced
                                ent.ResetOpen();
                            }
                            else if (p.Status.Value == StatementDraftEntryStatus.Open)
                            {
                                ent.MarkNeedsCheck();
                            }

                            _db.StatementDraftEntries.Update(ent);
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to apply status change for entry {EntryId} in draft {DraftId}", entryId, draftId);
                        // swallow per-entry status apply failure; overall update should have succeeded or reported earlier validation errors
                    }
                }
            }

            await tx.CommitAsync(ct);

            // return updated draft snapshot
            var updated = await GetDraftAsync(draftId, ownerUserId, ct);
            // Map to StatementDraftDetailDto expected by shared contract
            FinanceManager.Shared.Dtos.Statements.StatementDraftDetailDto? detail = null;
            if (updated != null)
            {
                // Map available fields from StatementDraftDto to the detailed DTO. Prev/Next and extra maps are not available here and kept null.
                detail = new FinanceManager.Shared.Dtos.Statements.StatementDraftDetailDto(
                    updated.DraftId,
                    updated.OriginalFileName ?? string.Empty,
                    updated.Description,
                    updated.DetectedAccountId,
                    updated.Status,
                    updated.TotalAmount,
                    updated.IsSplitDraft,
                    updated.ParentDraftId,
                    updated.ParentEntryId,
                    updated.ParentEntryAmount,
                    updated.UploadGroupId,
                    updated.Entries ?? new List<FinanceManager.Shared.Dtos.Statements.StatementDraftEntryDto>(),
                    PrevInUpload: null,
                    NextInUpload: null
                );
            }
            var success = new FinanceManager.Shared.Dtos.Statements.BatchUpdateSuccessResponseDto { UpdatedDraft = detail };
            _logger?.Log(LogLevel.Information, "Batch update applied for draft {DraftId} by user {User}", draftId, ownerUserId);
            return (true, success, null);
        }
        catch (Exception ex)
        {
            _logger?.Log(LogLevel.Error, ex, "Failed to commit batch update for draft {DraftId}", draftId);
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
