using FinanceManager.Infrastructure.Reports;
using Xunit;

namespace FinanceManager.Tests.Reports;

/// <summary>
/// Tests for <see cref="ExportFileHelper.CsvEscape"/>: quoting, newline neutralization and
/// neutralization of cell values that spreadsheets would interpret as formulas.
/// </summary>
public sealed class ExportFileHelperTests
{
    /// <summary>Ordinary text is quoted unchanged.</summary>
    [Fact]
    public void CsvEscape_PlainValue_IsQuoted()
    {
        Assert.Equal("\"Alpha\"", ExportFileHelper.CsvEscape("Alpha"));
    }

    /// <summary>Null and empty inputs yield an empty field.</summary>
    /// <param name="value">The input value to escape.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CsvEscape_NullOrEmpty_YieldsEmpty(string? value)
    {
        Assert.Equal(string.Empty, ExportFileHelper.CsvEscape(value));
    }

    /// <summary>Values starting with a spreadsheet-sensitive prefix are neutralized with a leading apostrophe.</summary>
    /// <param name="input">The dangerous cell value.</param>
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+cmd")]
    [InlineData("-10")]
    [InlineData("@SUM(A1)")]
    [InlineData("\t=x")]
    public void CsvEscape_FormulaPrefix_IsPrefixed(string input)
    {
        var result = ExportFileHelper.CsvEscape(input);
        Assert.StartsWith("\"'", result, StringComparison.Ordinal);
    }

    /// <summary>Newlines are still neutralized and double-quotes doubled.</summary>
    [Fact]
    public void CsvEscape_NewlinesAndQuotes_AreNeutralized()
    {
        Assert.Equal("\"a \"\"b\"\"c\"", ExportFileHelper.CsvEscape("a\n\"b\"\rc"));
    }
}
