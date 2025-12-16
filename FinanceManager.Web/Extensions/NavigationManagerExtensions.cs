using System;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Web.Extensions;

public static class NavigationManagerExtensions
{
    /// <summary>
    /// Inspect a result object and navigate to an appropriate page when possible.
    /// Currently recognizes:
    /// - FinanceManager.Shared.Dtos.Contacts.ContactDto (navigates to /card/contacts/{id})
    /// - Guid (navigates to /card/contacts/{guid})
    /// - string (navigates to the string as URI)
    /// - any object with an "Id" property of type Guid (navigates to contact card)
    /// Returns true when a navigation was performed.
    /// </summary>
    public static bool NavigateToModelCard(this NavigationManager nav, object? result)
    {
        if (nav == null) throw new ArgumentNullException(nameof(nav));
        if (result == null) return false;

        // string -> navigate as-is
        if (result is string s)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                nav.NavigateTo(s);
                return true;
            }
            return false;
        }

        // Guid -> assume contact card
        if (result is Guid g)
        {
            nav.NavigateTo($"/card/contacts/{g}");
            return true;
        }

        // Try known concrete DTO type by name to avoid referencing shared DTO assembly here
        var t = result.GetType();
        if (string.Equals(t.Name, "ContactDto", StringComparison.OrdinalIgnoreCase)
            || (t.FullName != null && t.FullName.EndsWith(".ContactDto", StringComparison.OrdinalIgnoreCase)))
        {
            var pid = t.GetProperty("Id")?.GetValue(result);
            if (pid is Guid gid)
            {
                nav.NavigateTo($"/card/contacts/{gid}");
                return true;
            }
            return false;
        }

        // Fallback: try to find an Id property of type Guid
        var prop = t.GetProperty("Id");
        if (prop != null)
        {
            var val = prop.GetValue(result);
            if (val is Guid gid2)
            {
                nav.NavigateTo($"/card/contacts/{gid2}");
                return true;
            }
        }

        return false;
    }
}
