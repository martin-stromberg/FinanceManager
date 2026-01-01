namespace FinanceManager.Domain.Statements;

/// <summary>
/// Represents a single import operation of a bank statement file for a specific account.
/// Contains metadata about the import (format, original filename, timestamps and totals).
/// </summary>
public sealed class StatementImport : Entity, IAggregateRoot
{
    /// <summary>
    /// Parameterless constructor for ORM/deserialization.
    /// </summary>
    private StatementImport() { }

    /// <summary>
    /// Creates a new <see cref="StatementImport"/> for the specified account and file.
    /// </summary>
    /// <param name="accountId">Identifier of the account the statement belongs to. Must not be <see cref="Guid.Empty"/>.</param>
    /// <param name="format">Detected import format.</param>
    /// <param name="originalFileName">Original uploaded file name. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="accountId"/> is empty or <paramref name="originalFileName"/> is null/whitespace (see guard helpers).</exception>
    public StatementImport(Guid accountId, ImportFormat format, string originalFileName)
    {
        AccountId = Guards.NotEmpty(accountId, nameof(accountId));
        Format = format;
        OriginalFileName = Guards.NotNullOrWhiteSpace(originalFileName, nameof(originalFileName));
        ImportedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Identifier of the account this import belongs to.
    /// </summary>
    /// <value>The account GUID.</value>
    public Guid AccountId { get; private set; }

    /// <summary>
    /// Detected import format (CSV, MT940, etc.).
    /// </summary>
    /// <value>The import format.</value>
    public ImportFormat Format { get; private set; }

    /// <summary>
    /// Timestamp (UTC) when the file was imported.
    /// </summary>
    /// <value>The import time in UTC.</value>
    public DateTime ImportedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Original uploaded file name as provided by the user.
    /// </summary>
    /// <value>Original file name string.</value>
    public string OriginalFileName { get; private set; } = null!;

    /// <summary>
    /// Number of entries parsed from the imported statement file.
    /// </summary>
    /// <value>Total number of parsed entries.</value>
    public int TotalEntries { get; private set; }
}