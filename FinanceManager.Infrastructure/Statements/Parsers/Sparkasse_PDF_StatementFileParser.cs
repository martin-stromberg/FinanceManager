using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Shared.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Statement file reader implementation for Sparkasse PDF exports.
    /// Initially based on the ING template – will be adapted later to Sparkasse specifics.
    /// </summary>
    public class Sparkasse_PDF_StatementFileParser : TemplateStatementFileParser
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Sparkasse_PDF_StatementFileParser"/>.
        /// </summary>
        /// <param name="logger">Logger instance used for parsing diagnostics.</param>
        public Sparkasse_PDF_StatementFileParser(ILogger<Sparkasse_PDF_StatementFileParser> logger) : base(_Templates, logger)
        {
        }

        private static readonly string[] _Templates = new string[]
        {
            // Initially reuse ING template; adapt later for Sparkasse specifics
            @" 
<template>
  <section name='AccountInfo' type='keyvalue' separator=':' endKeyword='--------------------------'>
    <key name='IBAN' variable='BankAccountNo' mode='always'/>
  </section>
  <section name='table' type='table' containsheader='false' fieldSeparator='#None#' endKeyword='--------------------------'>
    <!-- Single regexp to match Sparkasse table lines like: 02.01|Eigenleistung lfd. Jahr|139,00+ -->
    <regExp pattern='^(?&lt;PostingDate&gt;\d{2}\.\d{2}(?:\.\d{4})?)\|(?&lt;Description&gt;[^|]+)\|(?&lt;Amount&gt;[+\-]?\d{1,3}(?:\.\d{3})*,\d{2}[+\-]?)$' multiplier='1'/>
  </section>
  <section name='BlockEnd' type='ignore'/>
  <replacements>
    <replace from='Ueberweisung' to='Überweisung'/>
    <replace from='Terminueberw.' to='Terminüberweisung'/>
  </replacements>
</template>"
        };


        /// <summary>
        /// Determines whether the specified statement file is supported by this parser.
        /// </summary>
        /// <param name="statementFile">Statement file to evaluate.</param>
        /// <returns>True if the file is a Sparkasse PDF; otherwise false.</returns>
        protected override bool CanParse(IStatementFile statementFile)
        {
            return new Type[] { typeof(Sparkasse_PDF_StatementFile) }.Any(t => t.IsAssignableFrom(statementFile.GetType()));
        }

        private bool _salutationFound = false;
        private string _contactName = string.Empty;
        private DateTime _referenceDate = DateTime.MinValue;

        /// <summary>
        /// Performs preprocessing on a line of text before it is parsed, allowing for custom handling based on the
        /// current parsing mode.
        /// </summary>
        /// <remarks>Override this method to implement custom logic that should occur before each line is
        /// parsed. This can be used to detect or extract specific information from the input lines based on the current
        /// parsing context.</remarks>
        /// <param name="line">A reference to the line of text to be processed before parsing. The value may be modified by the method.</param>
        protected override void OnBeforeParseLine(ref string line)
        {
            base.OnBeforeParseLine(ref line);
            switch(CurrentMode)
            {
                case ParseMode.KeyValue:
                    if (line.StartsWith("Herrn") || line.StartsWith("Frau"))
                    {
                        _salutationFound = true;
                    }
                    else if (_salutationFound)
                    {
                        _contactName = line;
                        _salutationFound = false;
                    }

                    if (line.StartsWith("per "))
                    {
                        _referenceDate = DateTime.Parse(line.Remove(0, 4).Split('|').First());
                    }
                    break;
            }
        }

        /// <summary>
        /// Processes a found record and sanitizes PDF table derived values.
        /// </summary>
        /// <param name="rec">Record to process.</param>
        /// <returns>Enumerable with processed record(s).</returns>
        protected override IEnumerable<StatementMovement> ProcessFoundRecord(StatementMovement rec)
        {
            rec.Subject = ClearPDFTableValue(rec.Subject);
            rec.PostingDescription = ClearPDFTableValue(rec.PostingDescription ?? rec.Subject);
            CheckSetSelfCounterparty(rec);
            rec.Counterparty = ClearPDFTableValue(rec.Counterparty);
            ExtractPostingdescriptionFromCounterparty(rec);
            rec.BookingDate = CorrectDate(rec.BookingDate);
            rec.ValutaDate = CorrectDate(rec.ValutaDate, rec.BookingDate);
            rec.IsPreview = (rec.BookingDate == DateTime.MinValue)
                    || (rec.BookingDate > DateTime.Today);
            return base.ProcessFoundRecord(rec);
        }

        private DateTime CorrectDate(DateTime bookingDate, DateTime? secondDate = null)
        {
            if (_referenceDate != DateTime.MinValue && (bookingDate > _referenceDate))
                return new DateTime(_referenceDate.Year, bookingDate.Month, bookingDate.Day);
            else if (bookingDate != DateTime.MinValue)
                return bookingDate;
            else if (secondDate != null)
                return secondDate.Value;
            else
                return DateTime.MinValue;
        }

        private void CheckSetSelfCounterparty(StatementMovement rec)
        {
            if ((rec.Subject?? "").Contains("Eigenleistung"))
                rec.Counterparty = rec.Counterparty ?? _contactName;
        }

        private static readonly string[] PostingDescriptions = {
            "Lastschrift-Einzug",
            "Lastschrift",
            "Dauerauftrag/Terminüberweisung",
            "Gutschrift/Dauerauftrag",
            "Überweisung",
            "Gutschrift",
            "Zins/Dividende WP",
            "Echtzeitüberweisung",
            "Gehalt/Rente",
            "Wertpapierkauf",
            "Kapitalertragsteuer",
            "Solidaritätszuschlag",
            "Zinsertrag" };
        private void ExtractPostingdescriptionFromCounterparty(StatementMovement rec)
        {
            if (!string.IsNullOrWhiteSpace(rec.PostingDescription))
                return;
            foreach (var description in PostingDescriptions)
            {
                if (rec.Subject.StartsWith(description))
                {
                    rec.PostingDescription = description;
                    rec.Subject = rec.Subject.Remove(0, description.Length).TrimStart();
                }
                if (rec.Counterparty.StartsWith(description))
                {
                    rec.PostingDescription = description;
                    rec.Counterparty = rec.Counterparty.Remove(0, description.Length).TrimStart();
                }
            }
        }

        private string ClearPDFTableValue(string value)
        {
            return value?.Replace("|", " ") ?? string.Empty;
        }
    }
}
