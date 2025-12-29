using System;
using Microsoft.AspNetCore.Components;
using FinanceManager.Shared.Dtos.Postings;

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

        if (string.Equals(t.Name, "UserAdminDto", StringComparison.OrdinalIgnoreCase)
            || (t.FullName != null && t.FullName.EndsWith(".UserAdminDto", StringComparison.OrdinalIgnoreCase)))
        {
            var pid = t.GetProperty("Id")?.GetValue(result);
            if (pid is Guid gid)
            {
                nav.NavigateTo($"/card/users/{gid}");
                return true;
            }
            return false;
        }
        if (string.Equals(t.Name, "StatementDraftEntryDto", StringComparison.OrdinalIgnoreCase)
            || (t.FullName != null && t.FullName.EndsWith(".StatementDraftEntryDto", StringComparison.OrdinalIgnoreCase)))
        {
            var pid = t.GetProperty("Id")?.GetValue(result);
            if (pid is Guid gid)
            {
                nav.NavigateTo($"/card/statement-drafts/entries/{gid}");
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

    /// <summary>
    /// Navigate to the creation page for the provided kind segment (e.g. "securities", "accounts").
    /// An optional subKind segment may be included in the generated URL.
    /// Throws NotSupportedException when kind is empty or invalid.
    /// Returns true when navigation was performed.
    /// </summary>
    public static bool NavigateToModelCreate(this NavigationManager nav, string kind, string? subKind = null)
    {
        if (nav == null) throw new ArgumentNullException(nameof(nav));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));

        // normalize kind (remove leading/trailing slashes and whitespace)
        var k = kind.Trim();
        if (k.StartsWith("/")) k = k.TrimStart('/');
        if (k.EndsWith("/")) k = k.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(k)) throw new ArgumentException("Kind must be a non-empty path segment.", nameof(kind));

        var sb = new System.Text.StringBuilder();
        sb.Append("/card/");
        sb.Append(k);

        if (!string.IsNullOrWhiteSpace(subKind))
        {
            var s = subKind.Trim();
            if (s.StartsWith("/")) s = s.TrimStart('/');
            if (s.EndsWith("/")) s = s.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(s))
            {
                sb.Append('/');
                sb.Append(s);
            }
        }

        sb.Append('/');
        sb.Append($"{Guid.Empty}");

        nav.NavigateTo(sb.ToString());
        return true;
    }

    public static bool NavigateToModelCategories(this NavigationManager nav, string kind)
    {
        if (nav == null) throw new ArgumentNullException(nameof(nav));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(kind));

        var k = kind.Trim().ToLowerInvariant();
        if (k.StartsWith("/")) k = k.TrimStart('/');
        if (k.EndsWith("/")) k = k.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(k)) throw new ArgumentException("Kind must be a non-empty path segment.", nameof(kind));

        var sb = new System.Text.StringBuilder();
        sb.Append("/list/");
        sb.Append($"{k}/categories");

        nav.NavigateTo(sb.ToString());
        return true;
    }

    /// <summary>
    /// Backwards-compatible overload accepting PostingKind; forwards to string-based method.
    /// </summary>
    public static bool NavigateToModelCreate(this NavigationManager nav, PostingKind kind, string? subKind = null)
    {
        if (nav == null) throw new ArgumentNullException(nameof(nav));

        var path = MapPostingKindToPath(kind);
        if (string.IsNullOrWhiteSpace(path)) throw new NotSupportedException($"No route mapping defined for PostingKind '{kind}'.");

        return NavigateToModelCreate(nav, path, subKind);
    }

    private static string? MapPostingKindToPath(PostingKind kind)
    {
        return kind switch
        {
            PostingKind.Contact => "contacts",
            PostingKind.SavingsPlan => "savings-plans",
            PostingKind.Security => "securities",
            PostingKind.Bank => "accounts",
            _ => null
        };
    }
}
