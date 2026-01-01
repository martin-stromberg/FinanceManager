using System;
using FinanceManager.Shared.Dtos.Statements;

namespace FinanceManager.Shared.Dtos.Statements
{
    /// <summary>
    /// Result payload for assigning/clearing a split draft on an entry.
    /// </summary>
    public sealed record StatementDraftSetEntrySplitDraftResultDto(StatementDraftEntryDto Entry, decimal? SplitSum, decimal? Difference);
}
