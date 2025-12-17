namespace FinanceManager.Web.ViewModels.Common
{
    /// <summary>
    /// Provides information whether a ViewModel can accept symbol attachments and for which entity.
    /// </summary>
    public interface ISymbolAssignableCard
    {
        Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType);
    }
}
