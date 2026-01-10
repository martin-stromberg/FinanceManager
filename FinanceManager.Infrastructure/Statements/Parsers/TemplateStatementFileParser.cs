using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Shared.Extensions;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace FinanceManager.Infrastructure.Statements.Parsers
{
    /// <summary>
    /// Base class for template-driven statement file readers.
    /// Implementations provide a set of XML templates and a mechanism to read text content from a file bytes array.
    /// The template controls how lines are parsed into statement header data and individual movements.
    /// </summary>
    public abstract class TemplateStatementFileParser: BaseStatementFileParser
    {
        /// <summary>
        /// Initializes a new instance of the TemplateStatementFileReader class with the specified templates.
        /// </summary>
        /// <param name="templates">An array of template strings to be used by the file reader. Cannot be null.</param>
        /// <param name="logger">Logger instance for logging parsing activities and errors.</param>
        protected TemplateStatementFileParser(string[] templates, ILogger logger)
            :base(logger)
        {
            Templates = templates;
        }

        /// <summary>
        /// Determines whether the specified statement file can be parsed by this parser.
        /// </summary>
        /// <param name="statementFile">The statement file to evaluate for compatibility with this parser. Cannot be null.</param>
        /// <returns>true if the statement file can be parsed; otherwise, false.</returns>
        protected abstract bool CanParse(IStatementFile statementFile);

        /// <summary>
        /// Parses the specified statement file and attempts to extract structured statement data.
        /// </summary>
        /// <remarks>This method tries multiple parsing templates in sequence until one successfully
        /// parses the file. If none of the templates match the file format, the method returns null. The returned
        /// StatementParseResult includes all successfully parsed statement movements and header information.</remarks>
        /// <param name="statementFile">The statement file to parse. Must provide access to the file's content in a supported format.</param>
        /// <returns>A StatementParseResult containing the parsed statement header and movements if parsing succeeds; otherwise,
        /// null if the file could not be parsed with any available template.</returns>
        public override StatementParseResult? Parse(IStatementFile statementFile)
        {
            if (!CanParse(statementFile))
                return null;
            LogInformation($"Starting parse of statement file: {statementFile.FileName} ({GetType().Name})");
            var DraftId = Guid.NewGuid();
            XmlDoc = new XmlDocument();
            var fileContent = statementFile.ReadContent().ToList();
            if (!fileContent.Any() ) 
            {
                LogWarning("Statement file content is empty.");
                return null;
            }
            List<Exception> ErrorList = new List<Exception>();
            for (int idx = Templates.GetLowerBound(0); idx <= Templates.GetUpperBound(0); idx++)
            {
                LogInformation($"Trying template #{idx + 1} of {Templates.Length}...");
                XmlDoc.LoadXml(Templates[idx]);
                try
                {
                    CurrentMode = ParseMode.None;
                    CurrentSection = null;
                    GlobalDraftData = new StatementHeader();
                    GlobalLineData = new StatementMovement();
                    var resultList = new List<StatementMovement>();
                    EntryNo = 0;
                    foreach (var line in fileContent)
                    {
                        LogDebug($"Parsing line: {line}");
                        foreach (var record in ParseNextLine(line).Where(rec => rec is not null).SelectMany(rec => ProcessFoundRecord(rec)))
                        {
                            EntryNo++;
                            record.EntryNumber = EntryNo;
                            resultList.Add(record);
                        }
                    }
                    CurrentMode = ParseMode.None;
                    ErrorList.Clear();
                    if (resultList.Any())
                        return new StatementParseResult(GlobalDraftData, resultList);
                    LogInformation($"Template #{idx + 1} did not yield any records.");
                }
                catch (Exception ex)
                {
                    LogWarning(ex, $"Template #{idx + 1} parsing failed: {ex.Message}");
                    ErrorList.Add(ex);
                }
            }
            return null;
        }
        /// <summary>
        /// Processes a found statement movement record and returns one or more resulting records for further handling.
        /// </summary>
        /// <remarks>Derived classes can override this method to implement custom processing logic, such
        /// as filtering, transforming, or splitting the input record into multiple results.</remarks>
        /// <param name="rec">The statement movement record to process. Cannot be null.</param>
        /// <returns>An enumerable collection of <see cref="StatementMovement"/> objects resulting from processing the input
        /// record. The collection contains at least the input record.</returns>
        protected virtual IEnumerable<StatementMovement> ProcessFoundRecord(StatementMovement rec)
        {
            yield return rec;
        }

        /// <summary>
        /// Parses the specified statement file and returns detailed parsing results.
        /// </summary>
        /// <param name="statementFile">The statement file to be parsed. Cannot be null.</param>
        /// <returns>A <see cref="StatementParseResult"/> containing the details of the parsed statement, or <see
        /// langword="null"/> if parsing fails or the file is not supported.</returns>
        public override StatementParseResult? ParseDetails(IStatementFile statementFile)
        {
            return Parse(statementFile);
        }

        /// <summary>
        /// Template XML documents used for parsing. Implementations must provide one or more templates.
        /// </summary>
        protected string[] Templates { get; init; }

        private enum ParseMode
        {

            None,
            Ignore,
            KeyValue,
            Table,
            TableHeader,
            DynamicTable
        }

    ;

        /// <summary>
        /// Controls how template variables are applied when multiple values are encountered.
        /// <list type="bullet">
        /// <item><description><see cref="Always"/> - Always overwrite the target value.</description></item>
        /// <item><description><see cref="OnlyWhenEmpty"/> - Only set the target when it is currently empty.</description></item>
        /// </list>
        /// </summary>
        protected enum VariableMode
        {

            /// <summary>
            /// Always assign the parsed value, overwriting any existing value.
            /// </summary>
            Always,
            /// <summary>
            /// Assign the parsed value only when the existing value is empty or null.
            /// </summary>
            OnlyWhenEmpty

        }

    ;

        private VariableMode GetVariableMode(string text)
        {
            switch (text)
            {
                case "always":
                    return VariableMode.Always;
                case "onlywhenempty":
                    return VariableMode.OnlyWhenEmpty;
                default:
                    throw new ApplicationException("Unknown variable mode!");
            }
        }

        private ParseMode CurrentMode = ParseMode.None;
        private string[] EndKeywords = null;
        private string TableFieldSeparator = ";";
        private bool RemoveDuplicates = false;
        private int TableRecordLength = 0;
        private string[] IgnoreRecordKeywords = null;
        private bool StopOnError = true;
        /// <summary>
        /// The currently active XML section node from the loaded template used to control parsing behaviour.
        /// Implementations may inspect this node when extending parsing logic.
        /// </summary>
        protected XmlNode CurrentSection = null;
        private StatementHeader GlobalDraftData;
        private StatementMovement GlobalLineData;
        private StatementMovement RecordLineData = null;
        private XmlDocument XmlDoc;
        private int EntryNo = 0;


        private IEnumerable<StatementMovement> ParseNextLine(string line)
        {
            switch (CurrentMode)
            {
                case ParseMode.None:
                    if (CurrentSection == null)
                        CurrentSection = XmlDoc.DocumentElement.FirstChild;
                    else
                        CurrentSection = CurrentSection.NextSibling;
                    InitSection();
                    foreach (var record in ParseNextLine(line))
                        yield return record;
                    break;
                case ParseMode.Ignore:
                    if (line.Length == 0)
                        CurrentMode = ParseMode.None;
                    else if (EndKeywords is null)
                        yield return null;
                    else if (!EndKeywords.Any(kw => line.Contains(kw)))
                        yield return null;
                    else
                        CurrentMode = ParseMode.None;
                    break;
                case ParseMode.KeyValue:
                    if (line.Length == 0)
                        CurrentMode = ParseMode.None;
                    else if (EndKeywords is not null && EndKeywords.Any(kw => line.Contains(kw)))
                        CurrentMode = ParseMode.None;
                    else
                        ParseKeyValue(line, CurrentSection);
                    break;
                case ParseMode.TableHeader:
                    CurrentMode = ParseMode.Table;
                    break;
                case ParseMode.Table:
                    if (line.Length == 0)
                        CurrentMode = ParseMode.None;
                    else if ((EndKeywords is not null) && EndKeywords.Where(ek => !string.IsNullOrWhiteSpace(ek)).Any(kw => line.Contains(kw)))
                    {
                        yield return ReturnCurrentDelayedRecord();
                        yield return OnTableFinished();
                        CurrentMode = ParseMode.None;
                        foreach (var record in ParseNextLine(line))
                            yield return record;
                    }
                    else
                    {
                        var record = ParseTableRecord(line);
                                                if (record is not null && record.IsError)
                        {
                            CurrentMode = ParseMode.None;
                            foreach (var record2 in ParseNextLine(line))
                                yield return record2;
                        }
                        else
                            yield return record;
                    }
                    break;
                case ParseMode.DynamicTable:
                    if ((EndKeywords is not null) && EndKeywords.Any(kw => line.Contains(kw)))
                    {
                        CurrentMode = ParseMode.None;
                        foreach (var record in ParseNextLine(line))
                            yield return record;
                    }
                    else
                        yield return ParseDynamicTableRecord(line);
                    break;
            }
        }
        /// <summary>
        /// Called when a table section has finished and allows derived classes to return a final aggregate record
        /// (for example a subtotal row). The default implementation returns <c>null</c>.
        /// </summary>
        /// <returns>An optional <see cref="StatementMovement"/> representing the table footer/aggregate, or <c>null</c>.</returns>
        protected virtual StatementMovement OnTableFinished()
        {
            return null;
        }
        private void InitSection()
        {
            if (CurrentSection is null)
            {
                CurrentMode = ParseMode.Ignore;
                EndKeywords = null;
                return;
            }
            switch (CurrentSection.Attributes["type"].Value)
            {
                case "ignore":
                    CurrentMode = ParseMode.Ignore;
                    break;
                case "keyvalue":
                    CurrentMode = ParseMode.KeyValue;
                    break;
                case "table":
                    {
                        CurrentMode = ParseMode.Table;
                        if (CurrentSection.Attributes["containsheader"].Value == "true")
                            CurrentMode = ParseMode.TableHeader;
                        TableFieldSeparator = CurrentSection.Attributes["fieldSeparator"]?.Value;
                        if (string.IsNullOrEmpty(TableFieldSeparator))
                            TableFieldSeparator = ";";

                        var queryIgnore = from XmlNode cn in CurrentSection.ChildNodes
                                          where cn.Name == "ignore"
                                          select cn.Attributes["keyword"].Value;
                        IgnoreRecordKeywords = queryIgnore.ToArray();
                        if (!bool.TryParse(CurrentSection.Attributes["removeDuplicates"]?.Value, out RemoveDuplicates))
                            RemoveDuplicates = false;
                        StopOnError = CurrentSection.Attributes["stopOnError"]?.Value == "true";
                    }
                    break;
                case "dynTable":
                    {
                        CurrentMode = ParseMode.DynamicTable;
                        if (!int.TryParse(CurrentSection.Attributes["recordLength"]?.Value, out TableRecordLength))
                            TableRecordLength = 0;
                        TableFieldSeparator = CurrentSection.Attributes["fieldSeparator"]?.Value;
                        if (string.IsNullOrEmpty(TableFieldSeparator))
                            TableFieldSeparator = ";";
                        var queryIgnore = from XmlNode cn in CurrentSection.ChildNodes
                                          where cn.Name == "ignore"
                                          select cn.Attributes["keyword"].Value;
                        IgnoreRecordKeywords = queryIgnore.ToArray();
                        if (!bool.TryParse(CurrentSection.Attributes["removeDuplicates"]?.Value, out RemoveDuplicates))
                            RemoveDuplicates = false;
                    }
                    break;
                default:
                    throw new ApplicationException("unknown section type!");
            }
            EndKeywords = CurrentSection.Attributes["endKeyword"]?.Value?.Split('|');
        }

        private void ParseKeyValue(string line, XmlNode currentSection)
        {
            var separator = currentSection.Attributes.GetNamedItem("separator")?.Value ?? ";";
            string[] Values = line.Split(separator);
            foreach (XmlNode Key in CurrentSection.ChildNodes)
            {
                var fieldCount = Key.Attributes["name"].Value.Split(separator).Length;
                var name = string.Join(separator, Values.Take(fieldCount));
                if (name.EndsWith(Key.Attributes["name"].Value))
                {
                    string VariableName = Key.Attributes["variable"].Value;
                    ParseVariable(VariableName, Values.Skip(fieldCount).FirstOrDefault(), true, GetVariableMode(Key.Attributes["mode"].Value), 1);
                }
            }
        }
        /// <summary>
        /// Parses a variable value and applies it to either the current record or the global draft header.
        /// </summary>
        /// <param name="line">The target record instance where the parsed value will be applied.</param>
        /// <param name="Name">Variable name as defined in the template.</param>
        /// <param name="Value">String representation of the value to parse.</param>
        /// <param name="mode">The variable mode that controls assignment behaviour.</param>
        /// <param name="multiplier">Multiplier applied to numeric amounts (useful for sign handling).</param>
        /// <exception cref="FormatException">Thrown when date or numeric parsing fails for the provided value.</exception>
        protected void ParseVariable(StatementMovement line, string Name, string Value, VariableMode mode, int multiplier)
        {
            switch (Name)
            {
                case "BankAccountNo":
                    GlobalDraftData.AccountNumber = (string.IsNullOrWhiteSpace(GlobalDraftData.AccountNumber) || mode == VariableMode.Always) ? Value.Replace(" ", string.Empty) : GlobalDraftData.AccountNumber;
                    break;
                case "PostingDate":
                    line.BookingDate = DateTime.Parse(Value, new CultureInfo("de-DE"));
                    break;
                case "ValutaDate":
                    line.ValutaDate = DateTime.Parse(Value, new CultureInfo("de-DE"));
                    break;
                case "SourceName":
                    line.Counterparty = ApplyTextReplacements($"{line.Counterparty} {Value}".Trim());
                    break;
                case "PostingDescription":
                    line.PostingDescription = ApplyTextReplacements(Value);
                    break;
                case "Description":
                    line.Subject = ApplyTextReplacements(Value);
                    break;
                case "CurrencyCode":
                    line.CurrencyCode = Value;
                    break;
                case "Amount":
                    line.Amount = decimal.Parse(Value.Replace(" ", ""), new CultureInfo("de-DE")) * multiplier;
                    break;
            }
        }

        private string? ApplyTextReplacements(string inputText)
        {
            var node = XmlDoc.DocumentElement.ChildNodes.OfType<XmlNode>().FirstOrDefault(node => node.Name == "replacements");
            if (node is null)
                return inputText;
            foreach (var subNode in node.ChildNodes.OfType<XmlNode>())
            {
                if (string.Compare(subNode.Name, "replace", true) != 0)
                    continue;
                var search = subNode.Attributes.GetNamedItem("from")?.Value;
                var replace = subNode.Attributes.GetNamedItem("to")?.Value;
                if (string.IsNullOrWhiteSpace(search))
                    continue;
                inputText = inputText.Replace(search, replace);
            }
            return inputText;
        }

        /// <summary>
        /// Parses a variable defined by the template and applies it either to the global draft header (when <paramref name="global"/> is <c>true</c>)
        /// or to the current record. This is a convenience overload that delegates to <see cref="ParseVariable(StatementMovement,string,string,VariableMode,int)"/>
        /// </summary>
        /// <param name="Name">Variable name.</param>
        /// <param name="Value">String value to parse.</param>
        /// <param name="global">When <c>true</c> the value is applied to the draft header; otherwise to the current record.</param>
        /// <param name="mode">Variable assignment mode.</param>
        /// <param name="multiplier">Multiplier for numeric values.</param>
        /// <exception cref="FormatException">Thrown when date or numeric parsing fails for the provided value.</exception>
        protected void ParseVariable(string Name, string Value, bool global, VariableMode mode, int multiplier)
        {
            StatementMovement line = global ? GlobalLineData : RecordLineData;
            ParseVariable(line, Name, Value, mode, multiplier);
        }

        private StatementMovement ParseDynamicTableRecord(string line)
        {
            if (TableRecordLength > 0 && line.Length != TableRecordLength)
                return null;
            try
            {
                return ParseTableRecord(line);
            }
            catch (FormatException) { return null; }
        }

        private StatementMovement _RecordDelay = null;
        private int _additionalRecordInformationCount = 0;
        /// <summary>
        /// Parses a single table record line and maps template-defined fields onto a <see cref="StatementMovement"/> instance.
        /// The default implementation reads fields defined in the current XML section and supports regular-expression based fields.
        /// </summary>
        /// <param name="line">The input text line containing table columns.</param>
        /// <returns>The parsed <see cref="StatementMovement"/>, or <c>null</c> when the line should be ignored.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the template expects more fields than present and StopOnError is <c>false</c>.</exception>
        /// <exception cref="FormatException">Thrown when numeric/date parsing fails for field values.</exception>
        protected virtual StatementMovement ParseTableRecord(string line)
        {
            if (_RecordDelay is null)
            {
                var record = InternalParseTableRecord(line);
                if (record is null || record.BookingDate == DateTime.MinValue)
                    return record;
                if (!HasAdditionalRowsDefined())
                    return record;
                _RecordDelay = record;
                return null;
            }
            else
                return ParseSecondRow(line);
        }
        private bool HasAdditionalRowsDefined()
        {
            foreach (XmlNode Field in CurrentSection.ChildNodes)
            {
                if (Field.Name == "regExp" && Field.Attributes.GetNamedItem("type")?.Value == "additional")
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Attempts to parse the second row that contains additional information for a previously delayed record.
        /// </summary>
        /// <param name="line">The second-line input from the table section.</param>
        /// <returns>The completed <see cref="StatementMovement"/> when parsing succeeded; otherwise <c>null</c>.</returns>
        private StatementMovement ParseSecondRow(string line)
        {
            var isNextRecord = false;
            foreach (XmlNode Field in CurrentSection.ChildNodes)
            {
                switch (Field.Name)
                {
                    case "regExp":
                        isNextRecord = isNextRecord || SecondRowParseRegularExpression(line, Field);
                        break;
                }
            }
            if (!isNextRecord) return null;
            var outputRecord = ReturnCurrentDelayedRecord();
            _ = ParseTableRecord(line);
            return outputRecord;

        }
        /// <summary>
        /// Returns the currently delayed record and resets the internal delay state.
        /// </summary>
        /// <returns>The delayed <see cref="StatementMovement"/> instance.</returns>
        private StatementMovement ReturnCurrentDelayedRecord()
        {
            var outputRecord = _RecordDelay;
            _RecordDelay = null;
            _additionalRecordInformationCount = 0;
            return outputRecord;
        }
        private StatementMovement InternalParseTableRecord(string line)
        { 
            if ((IgnoreRecordKeywords is not null) && IgnoreRecordKeywords.Any(kw => line.Contains(kw)))
                return null;
            string[] Values = line.Split(TableFieldSeparator);
            RecordLineData = new StatementMovement() { };
            int FieldIdx = Values.GetLowerBound(0);
            try
            {
                var regExpRecordCount = false;
                foreach (XmlNode Field in CurrentSection.ChildNodes)
                {
                    switch (Field.Name)
                    {
                        case "field":
                            FieldIdx = ParseField(Values, FieldIdx, Field);
                            break;
                        case "regExp":
                            regExpRecordCount = regExpRecordCount || ParseRegularExpression(line, Field);
                            break;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                if (StopOnError)
                    return new StatementMovement() { IsError = true };
                throw;
            }
            return FinishRecord();
        }

        /// <summary>
        /// Applies a regular expression defined in the template to the input string and maps named capture groups to variables.
        /// </summary>
        /// <param name="input">Input text to run the regular expression against.</param>
        /// <param name="field">XML node containing attributes <c>pattern</c> and optional <c>multiplier</c> and <c>type</c>.</param>
        /// <exception cref="ArgumentException">Thrown when the configured regular expression pattern is invalid.</exception>
        protected virtual bool ParseRegularExpression(string input, XmlNode field)
        {
            var type = field.Attributes.GetNamedItem("type")?.Value;
            if (type == "additional")
                return false;

            var pattern = field.Attributes["pattern"].Value;
            var regex = new Regex(pattern, RegexOptions.IgnorePatternWhitespace);
            var match = regex.Match(input);
            if (!int.TryParse(field.Attributes["multiplier"]?.Value, out int multiplier))
                multiplier = 1;
            if (!match.Success)
                return false;
            foreach (var groupName in regex.GetGroupNames())
            {
                if (int.TryParse(groupName, out _))
                    continue;

                var value = match.Groups[groupName].Value;
                if (string.IsNullOrEmpty(value))
                    continue;
                ParseVariable(groupName, value, false, VariableMode.Always, multiplier);
            }
            return true;
        }
        /// <summary>
        /// Custom parsing for "additional" regular expression fields. When an additional pattern matches it augments the delayed record's fields.
        /// </summary>
        /// <param name="input">The input text to match.</param>
        /// <param name="field">XML node defining the regex pattern and multiplier.</param>
        /// <returns><c>true</c> when the input completed parsing for the delayed record (no further additional lines required); otherwise <c>false</c>.</returns>
        private bool SecondRowParseRegularExpression(string input, XmlNode field)
        {
            var pattern = field.Attributes["pattern"].Value;
            var type = field.Attributes.GetNamedItem("type")?.Value;
            var maxoccur = (field.Attributes.GetNamedItem("maxoccur")?.Value ?? "-").ToInt32();
            if (type != "additional")
            {
                var record = InternalParseTableRecord(input);
                if (record is not null && record.Amount != 0)
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
        private int ParseField(string[] Values, int FieldIdx, XmlNode Field)
        {
            string VariableName = Field.Attributes["variable"]?.Value;
            int.TryParse(Field.Attributes["length"]?.Value, out int fieldLength);
            if (!int.TryParse(Field.Attributes["multiplier"]?.Value, out int multiplier))
                multiplier = 1;
            if (TableFieldSeparator == "#None#")
            {
                if (fieldLength == 0)
                    fieldLength = Values[0].Length;
                var currentValue = Values[0].Substring(0, fieldLength);
                Values[0] = Values[0].Remove(0, fieldLength);
                ParseVariable(VariableName, currentValue, false, VariableMode.Always, multiplier);
            }
            else
            {
                ParseVariable(VariableName, Values[FieldIdx], false, VariableMode.Always, multiplier);
                FieldIdx += 1;
            }

            return FieldIdx;
        }

        /// <summary>
        /// Returns whether the current record contains meaningful data (non-empty subject, non-zero amount or booking date set).
        /// </summary>
        /// <returns><c>true</c> when the current record contains data; otherwise <c>false</c>.</returns>
        protected bool IsRecordSet()
        {
            if (RecordLineData is null) return false;
            if (RecordLineData.Amount == 0 && string.IsNullOrWhiteSpace(RecordLineData.Subject) && RecordLineData.BookingDate == DateTime.MinValue)
                return false;
            return true;
        }

        /// <summary>
        /// Finalizes the current record by returning it if set and resetting the internal state. If the record is empty, <c>null</c> is returned.
        /// </summary>
        /// <returns>The finalized <see cref="StatementMovement"/>, or <c>null</c> when nothing to return.</returns>
        private StatementMovement FinishRecord()
        {
            try
            {
                if (!IsRecordSet())
                    return null;

                RecordLineData.IsPreview = (RecordLineData.BookingDate == DateTime.MinValue)
                    || (RecordLineData.BookingDate > DateTime.Today);
                return RecordLineData;

            }
            finally
            {
                RecordLineData = null;
            }
        }
    }
}
