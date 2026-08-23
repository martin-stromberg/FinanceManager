namespace FinanceManager.Shared.Dtos.Statements;

/// <summary>
/// Request to create a preliminary (provisional) statement draft for a bank account.
/// </summary>
/// <param name="AccountId">Identifier of the bank account for which the preliminary draft is created.</param>
public sealed record CreatePreliminaryStatementDraftRequest(Guid AccountId);
