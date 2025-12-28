namespace FinanceManager.Domain.Attachments;

public enum AttachmentEntityKind : short
{
    None = -1,
    StatementDraftEntry = 0,
    StatementEntry = 1,
    Contact = 2,
    SavingsPlan = 3,
    Security = 4,
    Account = 5,
    StatementImport = 6,
    Posting = 7,
    StatementDraft = 8, 
    ContactCategory = 9,
    SavingsPlanCategory = 10, 
    SecurityCategory = 11
    
}
