using System.Security.Cryptography;
using System.Text;

namespace FinanceManager.Web.Mcp;

/// <summary>
/// Deterministic audit anchors of the write-class tool adapters: the domain enforces a real,
/// non-empty record id on every field delta (<c>Guards.NotEmpty</c> of
/// <c>McpAuditEntryChange</c>), but some effects have no database row to point at — the update
/// settings live in a file (<c>settings.json</c>), an install run is a process-level side
/// effect. For those the fachlich nächste anchor is a stable, deterministic id derived from
/// the well-known record key (or, for install runs, the version being installed) so audit
/// drill-down queries still resolve the touched "record" reproducibly.
/// </summary>
internal static class McpAuditAnchors
{
    /// <summary>
    /// Derives a deterministic <see cref="Guid"/> from a stable anchor key: the first 16 bytes
    /// of the SHA-256 hash of the UTF-8 key. Equal keys always yield the same id — the anchor
    /// is a stable pseudo record id, not a random placeholder.
    /// </summary>
    /// <param name="key">Stable anchor key (e.g. <c>"UpdateSettings"</c> or
    /// <c>"UpdateInstallation:1.3.0"</c>).</param>
    /// <returns>The deterministic anchor id.</returns>
    public static Guid StableId(string key)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16]);
}
