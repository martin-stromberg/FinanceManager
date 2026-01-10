using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// PDF statement file reader implementation for Barclays statement exports.
    /// Uses template-based parsing (regular expressions and table definitions) inherited from <see cref="PDFStatementFilereader"/>.
    /// The templates contained in this class target various Barclays PDF layouts and provide section and field mappings
    /// to build <see cref="StatementMovement"/> instances.
    /// </summary>
    public class Barclays_PDF_StatementFileParser : TemplateStatementFileParser
    {
        /// <summary>
        /// Initializes a new instance of the Barclays_StatementFileReader class using the predefined statement
        /// templates for Barclays bank statements.
        /// </summary>
        /// <param name="logger">Logger instance for logging parser operations and errors.</param>
        /// <remarks>Use this constructor to create a file reader specifically configured to parse
        /// Barclays bank statement files. The instance will be initialized with templates tailored for the Barclays
        /// statement format.</remarks>
        public Barclays_PDF_StatementFileParser(ILogger<Barclays_PDF_StatementFileParser> logger) : base(_Templates, logger)
        {
        }
        /// <summary>
        /// Array of XML templates (string literals) describing supported Barclays statement layouts.
        /// Each template is an XML document consumed by the base template parser.
        /// </summary>
        private static readonly string[] _Templates = {
            @"
<template>
  <section name='Block1' type='ignore' endKeyword='Allgemeine Umsätze'/>
  <section name='table' type='table' containsheader='false' fieldSeparator='#None#' endKeyword=''>
    <regExp pattern='(?&lt;PostingDate&gt;\d{2}\.\d{2}\.\d{4})\|(?&lt;ValutaDate&gt;\d{2}\.\d{2}\.\d{4})\|(?&lt;SourceName&gt;. +)\|(?&lt;Amount&gt;-?\s?\d{1,3}(?:\.\d{3})*,\d{2}-?)' multiplier='1'/>
  </section>  
</template>"
};
        /// <summary>
        /// Determines whether the specified statement file is of a supported type for parsing.
        /// </summary>
        /// <param name="statementFile">The statement file to evaluate for compatibility with the parser. Cannot be null.</param>
        /// <returns>true if the statement file is of a supported type; otherwise, false.</returns>
        protected override bool CanParse(IStatementFile statementFile)
        {
            return new Type[]{ typeof(Barclays_PDF_StatementFile) }.Contains(statementFile.GetType());
        }
    }
}
