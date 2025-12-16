using System.ComponentModel.DataAnnotations;

namespace FinanceManager.Shared.Dtos.Contacts;

/// <summary>
/// Strategy for handling competing values when merging two contacts.
/// </summary>
public enum MergePreference
{
    DestinationFirst,
    SourceFirst
}

/// <summary>
/// Request payload to merge the current contact into a target contact.
/// </summary>
public sealed record ContactMergeRequest([Required] Guid TargetContactId, MergePreference Preference = MergePreference.DestinationFirst);
