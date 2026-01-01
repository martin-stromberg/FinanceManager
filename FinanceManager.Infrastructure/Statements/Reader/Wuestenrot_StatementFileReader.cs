using FinanceManager.Application.Statements;
using FinanceManager.Shared.Extensions;
using System.Text.RegularExpressions;
using System.Xml;

namespace FinanceManager.Infrastructure.Statements.Reader
{
    /// <summary>
    /// Statement file reader for Wüstenrot PDF statements.
    /// Extends the PDF parser with custom handling for records that span multiple lines
    /// (main line + additional information lines).
    /// </summary>
    public class Wuestenrot_StatementFileReader : PDFStatementFilereader, IStatementFileReader
    {
        /// <summary>
        /// XML templates used to control parsing of Wüstenrot statement text.
        /// </summary>
        private string[] _Templates = new string[]
        {
            @"
<template>
  <section name='Title' type='ignore' endKeyword='Nr.'></section>
  <section name='AccountInfo' type='keyvalue' separator=':' endKeyword='Anfangssaldo'>
    <key name='Kontonummer' variable='BankAccountNo' mode='always'/>
    <key name='IBAN' variable='BankAccountNo' mode='onlywhenempty'/>
  </section>
  <section name='table' type='table' containsheader='false' fieldSeparator='#None#' endKeyword='Endsaldo'>
    <regExp pattern='(?&lt;PostingDate&gt;\d{2}\.\d{2}\.\d{4})\s+(?&lt;PostingDescription&gt;.+?)\s+(?&lt;ValutaDate&gt;\d{2}\.\d{2}\.\d{4})\s+(?&lt;Amount&gt;-?\s?\d{1,3}(?:\.\d{3})*,\d{2})' multiplier='1'/>
    <regExp type='additional' maxoccur='2' pattern='(?&lt;SourceName&gt;[\x20-\x7E]+)' />
  </section>
  <section name='BlockEnd' type='ignore'/>
</template>"
        };

        /// <summary>
        /// Exposes the templates to the base <see cref="TemplateStatementFileReader"/> parser.
        /// </summary>
        protected override string[] Templates => _Templates;

        private StatementMovement _RecordDelay = null;
        private int _additionalRecordInformationCount = 0;

        /// <summary>
        /// Parses a table record line. Wüstenrot statements may spread one logical record across
        /// multiple PDF lines: the first matching line is delayed until any additional information
        /// lines are processed. This override implements that delay logic.
        /// </summary>
        /// <param name="line">The raw text line to parse.</param>
        /// <returns>
        /// A parsed <see cref="StatementMovement"/> when a complete logical record is available; otherwise <c>null</c>
        /// to indicate the line was consumed as part of a multi-line record or ignored.
        /// </returns>
        protected override StatementMovement ParseTableRecord(string line)
        {
            if (_RecordDelay is null)
            {
                var record = base.ParseTableRecord(line);
                if (record is null || record.BookingDate == DateTime.MinValue)
                    return record;
                _RecordDelay = record;
                return null;
            }
            else
            {
                return ParseWuestenrotRecord(line);
            }
        }

        /// <summary>
        /// Called when a table section ends. Ensure any delayed record is returned as the table footer
        /// or fallback to the base implementation.
        /// </summary>
        /// <returns>Optional footer/aggregate <see cref="StatementMovement"/> or <c>null</c>.</returns>
        protected override StatementMovement OnTableFinished()
        {
            return _RecordDelay ?? base.OnTableFinished();
        }

        /// <summary>
        /// Attempts to parse an additional information line for a previously delayed record.
        /// If the provided line contains further data for the delayed record the delayed record
        /// will be finalized and returned; otherwise <c>null</c> is returned and the parser
        /// continues consuming lines.
        /// </summary>
        /// <param name="line">The input text line containing additional information or the next record.</param>
        /// <returns>
        /// The finalized <see cref="StatementMovement"/> when the delayed record is completed; otherwise <c>null</c>.
        /// </returns>
        private StatementMovement ParseWuestenrotRecord(string line)
        {
            var isNextRecord = false;
            foreach (XmlNode Field in CurrentSection.ChildNodes)
            {
                switch (Field.Name)
                {
                    case "regExp":
                        isNextRecord = isNextRecord || OwnParseRegularExpression(line, Field);
                        break;
                }
            }
            if (!isNextRecord) return null;
            var outputRecord = ReturnCurrentDelayedRecord();
            _ = ParseTableRecord(line);
            return outputRecord;

        }

        /// <summary>
        /// Returns the currently delayed record and resets internal counters used for multi-line aggregation.
        /// </summary>
        /// <returns>The currently delayed <see cref="StatementMovement"/>.</returns>
        private StatementMovement ReturnCurrentDelayedRecord()
        {
            var outputRecord = _RecordDelay;
            _RecordDelay = null;
            _additionalRecordInformationCount = 0;
            return outputRecord;
        }

        /// <summary>
        /// Overrides the base implementation to skip 'additional' type regular expressions.
        /// The base parser will handle primary record patterns only.
        /// </summary>
        /// <param name="input">Input text to test against the regular expression.</param>
        /// <param name="field">Template XML node describing the regular expression.</param>
        protected override void ParseRegularExpression(string input, XmlNode field)
        {
            var type = field.Attributes.GetNamedItem("type")?.Value;
            if (type != "additional")
                base.ParseRegularExpression(input, field);
        }

        /// <summary>
        /// Custom regular-expression parsing used to process 'additional' lines that belong to the
        /// previously delayed record. Returns <c>true</c> when the line indicates the next full record
        /// or when the configured maximum occurrences have been reached.
        /// </summary>
        /// <param name="input">The input text line to evaluate.</param>
        /// <param name="field">Template XML node describing the regular expression and optional attributes <c>multiplier</c> and <c>maxoccur</c>.</param>
        /// <returns><c>true</c> when this line completes the delayed record; otherwise <c>false</c>.</returns>
        private bool OwnParseRegularExpression(string input, XmlNode field)
        {
            var pattern = field.Attributes["pattern"].Value;
            var type = field.Attributes.GetNamedItem("type")?.Value;
            var maxoccur = (field.Attributes.GetNamedItem("maxoccur")?.Value ?? "-").ToInt32();
            if (type != "additional")
            {
                var record = base.ParseTableRecord(input);
                if (record is not null)
                    return true;
                return false;
            }
            var regex = new Regex(pattern, RegexOptions.IgnorePatternWhitespace);
            var match = regex.Match(input);
            if (!int.TryParse(field.Attributes["multiplier"]?.Value, out int multiplier))
                multiplier = 1;
            if (match.Success)
            {
                foreach (var groupName in regex.GetGroupNames())
                {
                    if (int.TryParse(groupName, out _))
                        continue;

                    var value = match.Groups[groupName].Value;
                    if (string.IsNullOrEmpty(value))
                        continue;
                    ParseVariable(_RecordDelay, groupName, value, VariableMode.Always, multiplier);
                }
                _additionalRecordInformationCount++;
                if (maxoccur > 0 && _additionalRecordInformationCount >= maxoccur)
                    return true;
            }
            return false;
        }

    }
}
