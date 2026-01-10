using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using Microsoft.Extensions.Logging;
using System.Text;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Statement file reader for ING statement exports using template-based parsing.
    /// Provides templates used by the base <see cref="TemplateStatementFileReader"/> to detect account
    /// information and table columns. The reader exposes a UTF-8 content reader suitable for PDF-to-text
    /// conversions that produce UTF-8 encoded bytes.
    /// </summary>
    public class ING_CSV_StatementFileParser : TemplateStatementFileParser
    {
        /// <summary>
        /// Templates used by the parsing engine to recognize ING statement layout variations.
        /// Each template contains sections and field mappings consumed by the base template parser.
        /// </summary>
        private static string[] _Templates = new string[]
        {
            @"
<template>
  <section name='Title' type='ignore'>
  </section>
  <section name='AccountInfo' type='keyvalue'>
    <key name='IBAN' variable='BankAccountNo' mode='always'/>
    <key name='Kunde' variable='BankAccountNo' mode='onlywhenempty'/>
  </section>
  <section name='Sortierung ' type='ignore'>
  </section>
  <section name='BlaBla' type='ignore'>
  </section>
  <section name='table' type='table' containsheader='true'>
    <field name='Buchung' variable='PostingDate'/>
    <field name='Valuta' variable='ValutaDate'/>
    <field name='Auftraggeber/Empfänger' variable='SourceName'/>
    <field name='Buchungstext' variable='PostingDescription'/>
    <field name='Verwendungszweck' variable='Description'/>
    <field name='Saldo' variable=''/>
    <field name='Währung' variable='CurrencyCode'/>
    <field name='Betrag' variable='Amount'/>
    <field name='Währung' variable='CurrencyCode'/>
  </section>
</template>"
        };
        /// <summary>
        /// Initializes a new instance of the ING_StatementFileReader class using the predefined templates.
        /// </summary>
        /// <param name="logger">Logger instance for logging parser operations and errors.</param>
        public ING_CSV_StatementFileParser(ILogger<ING_CSV_StatementFileParser> logger) : base(_Templates, logger)
        {
        }
        /// <summary>
        /// Determines whether the specified statement file is of a supported type and can be parsed by this parser.
        /// </summary>
        /// <param name="statementFile">The statement file to evaluate for compatibility with this parser. Cannot be null.</param>
        /// <returns>true if the statement file is of a supported type and can be parsed; otherwise, false.</returns>
        protected override bool CanParse(IStatementFile statementFile)
        {
            return new Type[] { typeof(ING_Csv_StatementFile) }.Any(t => t.IsAssignableFrom(statementFile.GetType()));
        }
    }
}
