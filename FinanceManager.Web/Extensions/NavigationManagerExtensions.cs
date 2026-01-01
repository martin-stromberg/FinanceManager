using System;
using Microsoft.AspNetCore.Components;
using FinanceManager.Shared.Dtos.Postings;

namespace FinanceManager.Web.Extensions;

/// <summary>
/// Provides navigation helper extension methods for <see cref="NavigationManager"/> to navigate to model cards,
/// create pages and category lists based on model kinds and results.
/// </summary>
public static class NavigationManagerExtensions
{
    /// <summary>
    /// Inspect a result object and navigate to an appropriate page when possible.
    /// Recognized types include string (navigates to the URI), Guid (navigates to contact card),
    /// known DTO types with an Id property (ContactDto, UserAdminDto, StatementDraftEntryDto) and
    /// any object exposing a Guid-typed "Id" property (falls back to contact card path).
    /// </summary>
    /// <param name="nav">The <see cref="NavigationManager"/> used to perform navigation. Must not be <c>null</c>.</param>
    /// <param name="result">Arbitrary result object to inspect for navigation target. May be <c>null</c>.</param>
    /// <returns><c>true</c> when navigation was performed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nav"/> is <c>null</c>.</exception>
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
    /// An optional <paramref name="subKind"/> segment may be included in the generated URL.
    /// </summary>
    /// <param name="nav">The <see cref="NavigationManager"/> instance. Must not be <c>null</c>.</param>
    /// <param name="kind">Primary kind path segment (for example "contacts" or "securities"). Must be a non-empty path segment.</param>
    /// <param name="subKind">Optional sub-kind segment to include in the route (for example "prices" or "categories").</param>
    /// <returns><c>true</c> when navigation was performed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nav"/> or <paramref name="kind"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="kind"/> cannot be normalized to a valid path segment.</exception>
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

    /// <summary>
    /// Navigate to the category listing page for the specified model kind (e.g. "/list/contacts/categories").
    /// </summary>
    /// <param name="nav">The <see cref="NavigationManager"/> instance. Must not be <c>null</c>.</param>
    /// <param name="kind">Primary kind path segment (for example "contacts").</param>
    /// <returns><c>true</c> when navigation was performed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nav"/> or <paramref name="kind"/> is <c>null</c> or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="kind"/> cannot be normalized to a valid path segment.</exception>
    public static bool NavigateToModelCategories(this NavigationManager nav, string kind)
    {
        if (nav == null) throw new ArgumentNullException(nameof(nav));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentNullException(nameof(nav));

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
    /// Backwards-compatible overload accepting <see cref="PostingKind"/>; forwards to string-based method.
    /// </summary>
    /// <param name="nav">The <see cref="NavigationManager"/> instance. Must not be <c>null</c>.</param>
    /// <param name="kind">Posting kind enum value that will be mapped to a path segment.</param>
    /// <param name="subKind">Optional sub-kind to include in the created path.</param>
    /// <returns><c>true</c> when navigation was performed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nav"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provided <paramref name="kind"/> cannot be mapped to a route path.</exception>
    public static bool NavigateToModelCreate(this NavigationManager nav, PostingKind kind, string? subKind = null)
    {
        if (nav == null) throw new ArgumentNullException(nameof(nav));

        var path = MapPostingKindToPath(kind);
        if (string.IsNullOrWhiteSpace(path)) throw new NotSupportedException($"No route mapping defined for PostingKind '{kind}'.");

        return NavigateToModelCreate(nav, path, subKind);
    }

    /// <summary>
    /// Maps a <see cref="PostingKind"/> value to the corresponding primary route path segment.
    /// Returns <c>null</c> when no mapping exists for the supplied kind.
    /// </summary>
    /// <param name="kind">Posting kind to map.</param>
    /// <returns>Path segment string or <c>null</c> when unsupported.</returns>
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
