namespace FinanceManager.Application.Security;

/// <summary>
/// Output format for security.txt rendering.
/// </summary>
public enum SecurityTxtFormat
{
    /// <summary>RFC 9116 plain text.</summary>
    PlainText = 0,
    /// <summary>Markdown rendering.</summary>
    Markdown = 1,
    /// <summary>HTML rendering.</summary>
    Html = 2
}
