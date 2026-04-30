using FinanceManager.Shared;
using FinanceManager.Web.ViewModels.Common;
using FinanceManager.Web.ViewModels.Postings.Common;

namespace FinanceManager.Web.ViewModels.Postings
{
    /// <summary>
    /// List view model for postings related to a specific security. Provides paging and lookup
    /// of posting service DTOs filtered by the configured security id.
    /// Overrides column layout to include the security-specific "Anzahl" (quantity) column.
    /// </summary>
    public sealed class SecurityPostingsListViewModel : BasePostingsListViewModel
    {
        /// <summary>
        /// Identifier of the security for which postings are queried.
        /// </summary>
        private readonly Guid _securityId;

        /// <summary>
        /// Initializes a new instance of <see cref="SecurityPostingsListViewModel"/> for the specified security.
        /// Sets column layout: Date, Valuta, Kind, Quantity, Amount, Subject, Description.
        /// </summary>
        /// <param name="services">Service provider used by the base view model.</param>
        /// <param name="securityId">Identifier of the security whose postings should be listed.</param>
        public SecurityPostingsListViewModel(IServiceProvider services, Guid securityId) : base(services)
        {
            _securityId = securityId;
            AllowRangeFiltering = true;

            // Override columns: reorder and add Quantity between Kind and Amount.
            // Recipient is omitted as it is not meaningful for security postings.
            Columns = new[]
            {
                new ListColumn("date",        "Datum",              Align: ListColumnAlign.Left,  Width: "8rem"),
                new ListColumn("valuta",      "Valuta",             Align: ListColumnAlign.Left,  Width: "8rem"),
                new ListColumn("kind",        "Art",                Align: ListColumnAlign.Left,  Width: "9rem"),
                new ListColumn("quantity",    "Anzahl",             Align: ListColumnAlign.Right, Width: "7rem"),
                new ListColumn("amount",      "Betrag",             Align: ListColumnAlign.Right, Width: "10rem"),
                new ListColumn("subject",     "Verwendungszweck",   Width: "22%"),
                new ListColumn("description", "Beschreibung")
            };
        }

        /// <summary>
        /// Queries a page of postings for the configured security.
        /// </summary>
        /// <param name="api">API client used to perform the query.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for the page.</param>
        /// <param name="search">Search term to filter postings (may be empty).</param>
        /// <param name="from">Optional start date for range filtering.</param>
        /// <param name="to">Optional end date for range filtering.</param>
        /// <returns>
        /// A task that resolves to a read-only list of <see cref="PostingServiceDto"/> when the query succeeds,
        /// or <c>null</c> when an error occurs.
        /// </returns>
        protected override async Task<IReadOnlyList<PostingServiceDto>?> QueryPageAsync(IApiClient api, int skip, int take, string search, DateTime? from, DateTime? to)
        {
            try
            {
                var list = await api.Postings_GetSecurityAsync(_securityId, skip, take, from, to);
                return list?.ToList();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds list records with the security-specific column order:
        /// Date, Valuta, Kind, Quantity, Amount, Subject, Description.
        /// The Quantity cell is left empty when the value is <c>null</c> or zero
        /// (e.g. for dividends, taxes and fees that carry no share count).
        /// </summary>
        protected override void BuildRecords()
        {
            Columns = Columns ?? Array.Empty<ListColumn>();
            Records = Items.Select(i =>
            {
                var navItem = new PostingListItem(i);

                var kindText = (i.Kind == PostingKind.Security && i.SecuritySubType.HasValue)
                    ? $"Security-{i.SecuritySubType}"
                    : i.Kind.ToString();

                // Show quantity only when it is non-null and non-zero; use up to 6 significant
                // decimal places without trailing zeros (e.g. "12,5" instead of "12,500000").
                var quantityText = (i.Quantity.HasValue && i.Quantity.Value != 0m)
                    ? i.Quantity.Value.ToString("0.######")
                    : string.Empty;

                return new ListRecord(new[]
                {
                    new ListCell(ListCellKind.Text,     Text: i.BookingDate.ToString("d")),
                    new ListCell(ListCellKind.Text,     Text: i.ValutaDate.ToString("d")),
                    new ListCell(ListCellKind.Text,     Text: kindText),
                    new ListCell(ListCellKind.Text,     Text: quantityText),
                    new ListCell(ListCellKind.Currency, Amount: i.Amount),
                    new ListCell(ListCellKind.Text,     Text: i.Subject ?? string.Empty),
                    new ListCell(ListCellKind.Text,     Text: i.Description ?? string.Empty)
                }, navItem);
            }).ToList();
        }
    }
}
