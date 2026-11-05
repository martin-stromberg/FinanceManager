using FinanceManager.Domain.Mcp;

namespace FinanceManager.Application.Mcp;

/// <summary>
/// A scope tuple (Datentyp × Rechteart) without filter — the unit used by the F3 matrix and
/// the level default profiles.
/// </summary>
/// <param name="DataType">The classified data type.</param>
/// <param name="Right">The right kind granted on the data type.</param>
public readonly record struct McpScopeTuple(McpDataType DataType, McpRightKind Right);

/// <summary>
/// Server-side truth of the F3 scope matrix (berechtigungsmodell.md, scope-und-toolkatalog.md):
/// which (Datentyp × Rechteart) combinations exist at all. Used to validate individual scope
/// profiles and to derive the default profiles of the access levels. The UI reads this matrix
/// via <c>GET /api/mcp-accesses/levels</c>/<c>…/options</c> and never duplicates the rules.
/// </summary>
/// <remarks>
/// Normative edges: booking exists only on statement drafts; postings are write-protected
/// (created solely via booking, removed solely via reversal = Löschung); statement imports and
/// account types carry no write path at all.
/// </remarks>
public static class McpScopeMatrix
{
    private static readonly McpDataType[] AllDataTypes = Enum.GetValues<McpDataType>();

    private static readonly HashSet<McpDataType> NoWriteTypes =
    [
        McpDataType.AccountTypes,
        McpDataType.StatementImports
    ];

    /// <summary>
    /// Determines whether the scope tuple exists in the F3 matrix ("✓"-cell).
    /// </summary>
    /// <param name="dataType">The data type.</param>
    /// <param name="right">The right kind.</param>
    /// <returns><see langword="true"/> when the combination is a valid scope.</returns>
    public static bool IsValidCombination(McpDataType dataType, McpRightKind right)
        => right switch
        {
            // Administration is an installation scope without suggestion/completion
            // semantics and without a delete/book path (fachkonzept.md §7.2): reading the
            // admin surfaces, modifying admin-editable settings/effects and creating a
            // backup snapshot are the only grantable rights.
            _ when dataType == McpDataType.Administration
                => right is McpRightKind.Read or McpRightKind.Modify or McpRightKind.Create,
            // Reading and suggestion apply to every classified data type.
            McpRightKind.Read or McpRightKind.Suggest => true,
            // Completion/modify/create apply to all writable types; postings stay write-
            // protected (invariant: they are created via booking only), account types and
            // committed imports have no write path at all.
            McpRightKind.Complete or McpRightKind.Modify or McpRightKind.Create
                => !NoWriteTypes.Contains(dataType) && dataType != McpDataType.Postings,
            // Booking exists exclusively on statement drafts.
            McpRightKind.Book => dataType == McpDataType.StatementDrafts,
            // Deletion (incl. reversal of postings, archiving semantics) applies to all types
            // that may be created or that participate in the ledger as postings. Statement
            // imports are revision-safe documents and account types are enum values only.
            McpRightKind.Delete => !NoWriteTypes.Contains(dataType),
            _ => false
        };

    /// <summary>
    /// Lists the valid right kinds of a data type in declaration order.
    /// </summary>
    /// <param name="dataType">The data type.</param>
    /// <returns>The valid right kinds.</returns>
    public static IReadOnlyList<McpRightKind> RightsFor(McpDataType dataType)
        => Enum.GetValues<McpRightKind>().Where(r => IsValidCombination(dataType, r)).ToList();

    /// <summary>
    /// Builds the complete set of valid scope tuples (the materialized F3 matrix).
    /// </summary>
    /// <returns>All valid (Datentyp × Rechteart) tuples.</returns>
    public static IReadOnlyList<McpScopeTuple> AllValidTuples()
        => AllDataTypes
            .SelectMany(t => RightsFor(t).Select(r => new McpScopeTuple(t, r)))
            .ToList();
}

/// <summary>
/// Server-side default scope profiles of the access levels (berechtigungsmodell.md §3.1).
/// Levels other than <see cref="McpAccessLevel.Individuell"/> persist no scope rows — their
/// effective profile is derived here at runtime so it evolves with the catalog.
/// </summary>
public static class McpLevelProfiles
{
    private static readonly McpDataType[] AllDataTypes = Enum.GetValues<McpDataType>();

    // Freigabestufe Sachbearbeiter: Vervollständigung/Änderung/Erstellung auf
    // Kontoauszugsentwürfen und vervollständigenden Stammdaten (Kontakte inkl. AliasName,
    // Kategorie-Zuordnungen = Kontaktgruppen).
    private static readonly McpDataType[] SachbearbeiterWriteTypes =
    [
        McpDataType.StatementDrafts,
        McpDataType.Contacts,
        McpDataType.ContactCategories
    ];

    /// <summary>
    /// Derives the default scope profile (without filters) of an access level.
    /// </summary>
    /// <param name="level">The access level.</param>
    /// <param name="isAdmin">
    /// Whether the access owner carries the administrative role. The
    /// <see cref="McpDataType.Administration"/> installation scope is granted to admins only —
    /// for a non-admin owner the tuples are removed from the profile so the standard level
    /// does not advertise a scope the admin gate of the fm_admin_* tools would deny anyway.
    /// </param>
    /// <returns>The effective scope tuples of the level; empty for unknown levels.</returns>
    public static IReadOnlyList<McpScopeTuple> GetDefaultScopes(McpAccessLevel level, bool isAdmin)
    {
        var scopes = new List<McpScopeTuple>();
        switch (level)
        {
            case McpAccessLevel.Analyse:
                scopes.AddRange(AllDataTypes.Select(t => new McpScopeTuple(t, McpRightKind.Read)));
                break;
            case McpAccessLevel.Assistent:
                foreach (var t in AllDataTypes)
                {
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Read));
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Suggest));
                }
                break;
            case McpAccessLevel.Sachbearbeiter:
                foreach (var t in AllDataTypes)
                {
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Read));
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Suggest));
                }
                foreach (var t in SachbearbeiterWriteTypes)
                {
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Complete));
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Modify));
                    scopes.Add(new McpScopeTuple(t, McpRightKind.Create));
                }
                break;
            case McpAccessLevel.Buchhalter:
                scopes.AddRange(McpScopeMatrix.AllValidTuples());
                break;
            case McpAccessLevel.Individuell:
            default:
                break;
        }
        if (!isAdmin)
        {
            scopes.RemoveAll(t => t.DataType == McpDataType.Administration);
        }
        return scopes;
    }
}
