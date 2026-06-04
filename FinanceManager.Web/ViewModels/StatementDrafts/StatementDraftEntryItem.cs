namespace FinanceManager.Web.ViewModels.StatementDrafts;

using FinanceManager.Shared.Dtos.Statements;

// Lightweight navigation-capable item for listing entries in embedded list
internal sealed class StatementDraftEntryItem : IListItemNavigation
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public DateTime BookingDate { get; set; }
    public DateTime? ValutaDate { get; set; }
    public decimal Amount { get; set; }
    public string? RecipientName { get; set; }
    public string? Subject { get; set; }
    public string? BookingDescription { get; set; }
    public StatementDraftEntryStatus Status { get; set; }
    public BudgetImpactEvaluationDto? BudgetImpact { get; set; }

    public string GetNavigateUrl() => $"/card/statement-drafts/entries/{Id}?draftId={DraftId}";
}
