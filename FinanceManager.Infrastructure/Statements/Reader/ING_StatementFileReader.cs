using FinanceManager.Application.Statements;
using System.Text;

namespace FinanceManager.Infrastructure.Statements.Reader
{
    /// <summary>
    /// Statement file reader for ING statement exports using template-based parsing.
    /// Provides templates used by the base <see cref="TemplateStatementFileReader"/> to detect account
    /// information and table columns. The reader exposes a UTF-8 content reader suitable for PDF-to-text
    /// conversions that produce UTF-8 encoded bytes.
    /// </summary>
    public class ING_StatementFileReader : TemplateStatementFileReader, IStatementFileReader
    {
        /// <summary>
        /// Legacy templates kept for backwards compatibility with older statement layouts.
        /// </summary>
        private string[] OldTemplates = new string[]
        {
            @"
<template>
  <section name='recordSet' type='dynTable' recordLength='84' fieldSeparator='#None#' removeDuplicates='true'>
    <field name='Buchung' variable='PostingDate' type='date' length='11'/>
    <field name='Valuta' variable='ValutaDate' type='date' length='11'/>
    <field name='Auftraggeber/Empfänger' variable='SourceName' length='23'/>
    <field name='Ort' variable='' length='14'/>
    <field name='Land' variable='' length='3'/>
    <field name='Karte' variable='' length='15'/>
    <field name='Betrag' variable='Amount' type='decimal'/>    
  </section>
</template>",
        };
        /// <summary>
        /// Templates used by the parsing engine to recognize ING statement layout variations.
        /// Each template contains sections and field mappings consumed by the base template parser.
        /// </summary>
        private string[] _Templates = new string[]
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
    <field name='Kategorie' variable='Category'/>
    <field name='Verwendungszweck' variable='Description'/>
    <field name='Saldo' variable=''/>
    <field name='Währung' variable='CurrencyCode'/>
    <field name='Betrag' variable='Amount'/>
    <field name='Währung' variable='CurrencyCode'/>
  </section>
</template>",
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
        /// Returns the templates that the base parser should use to detect the ING layout.
        /// </summary>
        protected override string[] Templates => _Templates;

        /// <summary>
        /// Reads the provided file bytes and returns an enumerable of text lines.
        /// This implementation normalizes common newline variants (CRLF, CR) to LF and splits on LF.
        /// </summary>
        /// <param name="fileBytes">The raw file bytes to convert to textual content.</param>
        /// <returns>An enumerable of lines extracted from the input bytes.</returns>
        /// <remarks>
        /// The method assumes the incoming bytes are UTF-8 encoded. If the bytes use a different
        /// encoding, callers should convert them to UTF-8 before invoking the parser or override this method.
        /// </remarks>
        protected override IEnumerable<string> ReadContent(byte[] fileBytes)
        {
            return Encoding.UTF8.GetString(fileBytes)
                .Replace("\r\n", "\n") // Windows to Unix
                .Replace("\r", "\n")   // Mac to Unix
                .Split('\n')
                .AsEnumerable();
        }
    }
}
