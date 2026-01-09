using FinanceManager.Application.Statements;

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
        /// <remarks>Use this constructor to create a file reader specifically configured to parse
        /// Barclays bank statement files. The instance will be initialized with templates tailored for the Barclays
        /// statement format.</remarks>
        public Barclays_PDF_StatementFileParser() : base(_Templates)
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

    }
}
