using FinanceManager.Application.Statements;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace FinanceManager.Infrastructure.Statements.Reader
{
    /// <summary>
    /// Base PDF statement file reader that extracts text content from a PDF and delegates
    /// parsing to the template-based parser implemented in <see cref="TemplateStatementFileReader"/>.
    /// </summary>
    /// <remarks>
    /// This reader uses the iText7 library to extract text from PDF pages. The extracted text is normalized
    /// to LF newlines and yielded line-by-line. Implementations of <see cref="TemplateStatementFileReader"/>
    /// consume the lines and apply template-based parsing to produce statement movements.
    /// </remarks>
    public abstract class PDFStatementFilereader : TemplateStatementFileReader, IStatementFileReader
    {
        /// <summary>
        /// Reads and extracts textual content from the provided PDF bytes and returns an enumerable of lines.
        /// </summary>
        /// <param name="fileBytes">Byte array containing the PDF file content. The method expects a valid PDF format.</param>
        /// <returns>An enumerable of text lines extracted from the PDF. Lines are normalized to use LF ("\n") as newline separator.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileBytes"/> is <c>null</c>.</exception>
        /// <exception cref="System.IO.IOException">Thrown on I/O errors while reading the PDF stream.</exception>
        /// <exception cref="iText.Kernel.Pdf.Exception">Thrown when the PDF cannot be parsed by the underlying iText library (malformed PDF).</exception>
        /// <remarks>
        /// The method opens the PDF data using iText and reads text page-by-page. It attempts to trim duplicated
        /// content that occurs when pages duplicate a header region by comparing the start of the current page with
        /// the last page's content. The iText reader is closed in a finally block to ensure resources are released.
        /// Callers should handle exceptions appropriately; this method does not swallow parsing errors.
        /// </remarks>
        protected override IEnumerable<string> ReadContent(byte[] fileBytes)
        {
            using var ms = new MemoryStream(fileBytes, false);
            PdfReader iTextReader = new PdfReader(ms);
            try
            {
                PdfDocument pdfDoc = new PdfDocument(iTextReader);
                int numberofpages = pdfDoc.GetNumberOfPages();
                ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();
                var totalContent = "";
                var lastContent = "";
                for (int pageNo = 1; pageNo <= numberofpages; pageNo++)
                {
                    var page = pdfDoc.GetPage(pageNo);
                    var pageContent = PdfTextExtractor.GetTextFromPage(page, strategy).Replace("\r\n", "\n").Replace("\r", "\n");
                    var currentContent = pageContent;
                    if (!string.IsNullOrWhiteSpace(lastContent) && pageContent.StartsWith(lastContent))
                        pageContent = pageContent.Remove(0, lastContent.Length).TrimStart('\n');
                    lastContent = currentContent;
                    totalContent += pageContent;
                }

                var pageLines = totalContent.TrimEnd('\n').Split('\n');
                foreach (var line in pageLines)
                    yield return line;
            }
            finally
            {
                iTextReader.Close();
            }
        }
    }
}
