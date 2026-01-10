using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Shared.Extensions;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Xml;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Statement file reader for Wüstenrot PDF statements.
    /// Extends the PDF parser with custom handling for records that span multiple lines
    /// (main line + additional information lines).
    /// </summary>
    public class Wuestenrot_StatementFileParser : TemplateStatementFileParser
    {
        /// <summary>
        /// Initializes a new instance of the Wuestenrot_StatementFileReader class using the default templates.
        /// </summary>
        /// <param name="logger">Logger instance for logging parser operations and errors.</param>
        public Wuestenrot_StatementFileParser(ILogger<Wuestenrot_StatementFileParser> logger)
            :base(_Templates, logger)
        {

        }
        /// <summary>
        /// XML templates used to control parsing of Wüstenrot statement text.
        /// </summary>
        private static readonly string[] _Templates = new string[]
        {
            @"
<template>
  <section name='Title' type='ignore' endKeyword='Kontoauszug'></section>
  <section name='AccountInfo' type='keyvalue' separator=':|' endKeyword='Anfangssaldo'>
    <key name='Kontonummer' variable='BankAccountNo' mode='always'/>
    <key name='IBAN' variable='BankAccountNo' mode='onlywhenempty'/>
  </section>
  <section name='table' type='table' containsheader='false' fieldSeparator='#None#' endKeyword='Endsaldo'>
    <regExp pattern='(?&lt;PostingDate&gt;\d{2}\.\d{2}\.\d{4})\s+(?&lt;PostingDescription&gt;. +?)\s+(?&lt;ValutaDate&gt;\d{2}\.\d{2}\.\d{4})\s+(?&lt;Amount&gt;-?\s?\d{1,3}(?:\.\d{3})*,\d{2})' multiplier='1'/>
    <regExp pattern='(?&lt;PostingDate&gt;\d{2}\.\d{2}\.\d{4})\|(?&lt;PostingDescription&gt;[^|]+)\|(?&lt;ValutaDate&gt;\d{2}\.\d{2}\.\d{4})\|(?&lt;Amount&gt;-?\s?\d{1,3}(?:\.\d{3})*,\d{2})' multiplier='1'/>
    <regExp pattern='(?&lt;PostingDate&gt;\d{2}\.\d{2}\.\d{4})\|(?&lt;Category&gt;[^|]+)\|(?&lt;PostingDescription&gt;[^|]+)\|(?&lt;ValutaDate&gt;\d{2}\.\d{2}\.\d{4})\|(?&lt;Amount&gt;-?\s?\d{1,3}(?:\.\d{3})*,\d{2})' multiplier='1'/>
    <regExp type='additional' maxoccur='2' pattern='(?&lt;SourceName&gt;[\x20-\x7E]+)' />
  </section>
  <section name='BlockEnd' type='ignore'/>
</template>"
        };
        /// <summary>
        /// Determines whether the specified statement file can be parsed by this parser.
        /// </summary>
        /// <remarks>This method checks whether the provided statement file is of a type recognized by
        /// this parser. Only files of type Wuestenrot_PDF_StatementFile are supported.</remarks>
        /// <param name="statementFile">The statement file to evaluate for compatibility. Cannot be null.</param>
        /// <returns>true if the statement file is of a supported type and can be parsed; otherwise, false.</returns>
        protected override bool CanParse(IStatementFile statementFile)
        {
            return new Type[] { typeof(Wuestenrot_PDF_StatementFile) }.Contains(statementFile.GetType());
        }
    }
}
