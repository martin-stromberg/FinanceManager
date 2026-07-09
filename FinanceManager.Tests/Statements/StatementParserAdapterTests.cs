using FinanceManager.Application.Statements;
using FinanceManager.Infrastructure.Statements.Files;
using FinanceManager.Infrastructure.Statements.Parsers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace FinanceManager.Tests.Statements;

public sealed class StatementParserAdapterTests
{
    private sealed class FakeIngPdfStatementFile : ING_PDF_StatementFile
    {
        private readonly IReadOnlyList<string> _lines;
        public FakeIngPdfStatementFile(IReadOnlyList<string> lines) => _lines = lines;
        public override IEnumerable<string> ReadContent() => _lines;
    }

    private sealed class FakeLineStatementFile : IStatementFile
    {
        private readonly IReadOnlyList<string> _lines;
        public FakeLineStatementFile(string fileName, IReadOnlyList<string> lines) { FileName = fileName; _lines = lines; }
        public string FileName { get; }
        public bool Load(string fileName, byte[] fileBytes) => true;
        public IEnumerable<string> ReadContent() => _lines;
    }

    private static byte[] CreateIngCsvBytes(string content)
        => Encoding.UTF8.GetBytes(content);

    [Fact]
    public void Parse_ShouldReturnNull_WhenFileNotRecognized()
    {
        var parser = new ING_CSV_StatementFileParser(NullLogger<ING_CSV_StatementFileParser>.Instance);
        var fakeFile = new FakeLineStatementFile("test.csv", new[] { "no content here" });

        var result = parser.Parse(fakeFile);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_BackupJson_ShouldReturnSingleElementList_WhenValid()
    {
        var parser = new Backup_JSON_StatementFileParser();
        var fakeFile = new FakeLineStatementFile("backup.ndjson", new[]
        {
            "{\"Type\":\"Backup\",\"Version\":2}",
            "{ \"BankAccounts\": [{\"IBAN\": \"\"}], \"BankAccountLedgerEntries\": [], \"BankAccountJournalLines\": [{\"Id\": 1,\"PostingDate\": \"2017-07-15T00:00:00\",\"ValutaDate\": \"2017-07-15T00:00:00\",\"PostingDescription\": \"Lastschrift\",\"SourceName\": \"GEZ\",\"Description\": \"GEZ Gebuehr\",\"CurrencyCode\": \"EUR\",\"Amount\": -97.95,\"CreatedAt\": \"2017-07-16T12:33:42.000041\"}] }"
        });

        var result = parser.Parse(fakeFile);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<StatementParseResult>>(result);
        Assert.Single(result);
    }

    [Theory]
    [InlineData("test.pdf")]
    [InlineData("test.csv")]
    [InlineData("test.ndjson")]
    public void Parse_ShouldReturnNull_ForTemplateParsers_WhenFileTypeDoesNotMatch(string fileName)
    {
        var fakeFile = new FakeLineStatementFile(fileName, new[] { "some content" });

        IStatementFileParser[] parsers =
        [
            new Barclays_PDF_StatementFileParser(NullLogger<Barclays_PDF_StatementFileParser>.Instance),
            new Wuestenrot_StatementFileParser(NullLogger<Wuestenrot_StatementFileParser>.Instance),
            new Sparkasse_PDF_StatementFileParser(NullLogger<Sparkasse_PDF_StatementFileParser>.Instance),
            new ING_PDF_StatementFileParser(NullLogger<ING_PDF_StatementFileParser>.Instance),
            new ING_CSV_StatementFileParser(NullLogger<ING_CSV_StatementFileParser>.Instance),
        ];

        foreach (var parser in parsers)
        {
            var result = parser.Parse(fakeFile);
            Assert.Null(result);
        }
    }

    [Fact]
    public void Parse_IngCsv_ShouldReturnSingleElementList_WhenValidSingleBlockContent()
    {
        var csv =
            "Bank;ING\r\n" +
            "\r\n" +
            "IBAN;DE11100000000000\r\n" +
            "Kontoname;Testkonto\r\n" +
            "Kunde;Testinhaber\r\n" +
            "Zeitraum;01.01.2023 - 31.01.2023\r\n" +
            "Saldo;100,00;EUR\r\n" +
            "\r\n" +
            "Sortierung;Datum absteigend\r\n" +
            "\r\n" +
            "\r\n" +
            "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
            "01.01.2023;01.01.2023;TestPerson;Überweisung;Testzweck;100,00;EUR;-50,00;EUR\r\n";

        var ingFile = new ING_Csv_StatementFile(NullLogger<ING_Csv_StatementFile>.Instance);
        var loaded = ingFile.Load("test.csv", CreateIngCsvBytes(csv));
        Assert.True(loaded, "ING_Csv_StatementFile should load the CSV");

        var parser = new ING_CSV_StatementFileParser(NullLogger<ING_CSV_StatementFileParser>.Instance);

        var result = parser.Parse(ingFile);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<StatementParseResult>>(result);
        Assert.Single(result);
    }

    [Fact]
    public void Parse_ShouldReturnMultipleResults_ForCollectionAccountCSV()
    {
        var csv =
            "Bank;ING\r\n" +
            "\r\n" +
            "IBAN;DE11100000000000\r\n" +
            "Kontoname;Konto1\r\n" +
            "Kunde;User1\r\n" +
            "Zeitraum;01.01.2023 - 31.01.2023\r\n" +
            "Saldo;100,00;EUR\r\n" +
            "\r\n" +
            "Sortierung;Datum absteigend\r\n" +
            "\r\n" +
            "\r\n" +
            "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
            "01.01.2023;01.01.2023;Person1;Überweisung;Test1;100,00;EUR;-50,00;EUR\r\n" +
            "\r\n" +
            "Bank;ING\r\n" +
            "\r\n" +
            "IBAN;DE22200000000000\r\n" +
            "Kontoname;Konto2\r\n" +
            "Kunde;User2\r\n" +
            "Zeitraum;01.01.2023 - 31.01.2023\r\n" +
            "Saldo;200,00;EUR\r\n" +
            "\r\n" +
            "Sortierung;Datum absteigend\r\n" +
            "\r\n" +
            "\r\n" +
            "Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung\r\n" +
            "02.01.2023;02.01.2023;Person2;Überweisung;Test2;200,00;EUR;-100,00;EUR\r\n";

        var ingFile = new ING_Csv_StatementFile(NullLogger<ING_Csv_StatementFile>.Instance);
        var loaded = ingFile.Load("sammel.csv", CreateIngCsvBytes(csv));
        Assert.True(loaded, "ING_Csv_StatementFile should load the multi-block CSV");

        var parser = new ING_CSV_StatementFileParser(NullLogger<ING_CSV_StatementFileParser>.Instance);

        var result = parser.Parse(ingFile);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyList<StatementParseResult>>(result);
        Assert.True(result!.Count > 1, "Collection account CSV should produce multiple StatementParseResult instances");
    }

    [Fact]
    public void Parse_IngPdfSparbriefTemplate_ShouldReturnExpectedIbanAndMovements()
    {
        var file = new FakeIngPdfStatementFile(new[]
        {
            "ING-DiBa AG · 60628 Frankfurt am Main",
            "Kontoauszug|2026",
            "Valuta|Vorgang|Euro",
            "Sparbrief (Laufzeit bis 29.04.2026)",
            "IBAN DE11 1111 1111 1111 1111 11 / Kontonummer|1111111111",
            "30.12.2025|alter Saldo|12.623,22",
            "29.04.2026|Zinsgutschrift|83,45",
            "29.04.2026|Kontolöschung|-12.706,67",
            "29.04.2026|neuer Saldo|0,00",
            "Wichtige Informationen für Sie:"
        });
        var parser = new ING_PDF_StatementFileParser(NullLogger<ING_PDF_StatementFileParser>.Instance);

        var result = parser.Parse(file);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("DE11111111111111111111", result![0].Header.IBAN);
        Assert.Equal(2, result[0].Movements.Count);
        var zinsgutschrift = Assert.Single(result[0].Movements.Where(m => m.PostingDescription == "Zinsgutschrift"));
        Assert.Equal(new DateTime(2026, 4, 29), zinsgutschrift.BookingDate);
        Assert.Equal(83.45m, zinsgutschrift.Amount);

        var kontoloeschung = Assert.Single(result[0].Movements.Where(m => m.PostingDescription == "Kontolöschung"));
        Assert.Equal(new DateTime(2026, 4, 29), kontoloeschung.BookingDate);
        Assert.Equal(-12706.67m, kontoloeschung.Amount);
    }

    [Fact]
    public void Parse_IngPdfLegacyTemplate_ShouldStillParse()
    {
        var file = new FakeIngPdfStatementFile(new[]
        {
            "ING-DiBa AG · 60628 Frankfurt am Main",
            "IBAN|DE12123412341234123412",
            "Buchung|Buchung",
            "29.04.2026|Zinsgutschrift|83,45",
            "Abschluss für Konto"
        });
        var parser = new ING_PDF_StatementFileParser(NullLogger<ING_PDF_StatementFileParser>.Instance);

        var result = parser.Parse(file);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("DE12123412341234123412", result![0].Header.IBAN);
        Assert.Single(result[0].Movements);
        Assert.Equal("Zinsgutschrift", result[0].Movements.First().Counterparty);
        Assert.Equal(83.45m, result[0].Movements.First().Amount);
    }
}
