namespace FinanceManager.Web.Services
{
    /// <summary>
    /// Provides query methods to retrieve postings filtered by various entities (contact, account, savings plan, security).
    /// Implementations encapsulate paging, filtering and any authorization/context handling required by the application.
    /// </summary>
    public interface IPostingsQueryService
    {
        /// <summary>
        /// Retrieves a paged list of postings for the specified contact.
        /// </summary>
        /// <param name="contactId">Identifier of the contact whose postings should be returned.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for paging.</param>
        /// <param name="q">Optional search term used to filter postings.</param>
        /// <param name="from">Optional inclusive start date for date range filtering.</param>
        /// <param name="to">Optional inclusive end date for date range filtering.</param>
        /// <param name="currentUserId">Identifier of the current user used for authorization/context.</param>
        /// <param name="ct">Cancellation token used to cancel the operation.</param>
        /// <returns>A task that resolves to a read-only list of <see cref="PostingServiceDto"/> results.</returns>
        /// <exception cref="OperationCanceledException">When the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        Task<IReadOnlyList<PostingServiceDto>> GetContactPostingsAsync(Guid contactId, int skip, int take, string? q, DateTime? from, DateTime? to, Guid currentUserId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a paged list of postings for the specified account.
        /// </summary>
        /// <param name="accountId">Identifier of the account whose postings should be returned.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for paging.</param>
        /// <param name="q">Optional search term used to filter postings.</param>
        /// <param name="from">Optional inclusive start date for date range filtering.</param>
        /// <param name="to">Optional inclusive end date for date range filtering.</param>
        /// <param name="currentUserId">Identifier of the current user used for authorization/context.</param>
        /// <param name="ct">Cancellation token used to cancel the operation.</param>
        /// <returns>A task that resolves to a read-only list of <see cref="PostingServiceDto"/> results.</returns>
        /// <exception cref="OperationCanceledException">When the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        Task<IReadOnlyList<PostingServiceDto>> GetAccountPostingsAsync(Guid accountId, int skip, int take, string? q, DateTime? from, DateTime? to, Guid currentUserId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a paged list of postings for the specified savings plan.
        /// </summary>
        /// <param name="planId">Identifier of the savings plan whose postings should be returned.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for paging.</param>
        /// <param name="q">Optional search term used to filter postings.</param>
        /// <param name="from">Optional inclusive start date for date range filtering.</param>
        /// <param name="to">Optional inclusive end date for date range filtering.</param>
        /// <param name="currentUserId">Identifier of the current user used for authorization/context.</param>
        /// <param name="ct">Cancellation token used to cancel the operation.</param>
        /// <returns>A task that resolves to a read-only list of <see cref="PostingServiceDto"/> results.</returns>
        /// <exception cref="OperationCanceledException">When the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        Task<IReadOnlyList<PostingServiceDto>> GetSavingsPlanPostingsAsync(Guid planId, int skip, int take, string? q, DateTime? from, DateTime? to, Guid currentUserId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves a paged list of postings for the specified security.
        /// </summary>
        /// <param name="securityId">Identifier of the security whose postings should be returned.</param>
        /// <param name="skip">Number of items to skip for paging.</param>
        /// <param name="take">Number of items to take for paging.</param>
        /// <param name="from">Optional inclusive start date for date range filtering.</param>
        /// <param name="to">Optional inclusive end date for date range filtering.</param>
        /// <param name="currentUserId">Identifier of the current user used for authorization/context.</param>
        /// <param name="ct">Cancellation token used to cancel the operation.</param>
        /// <returns>A task that resolves to a read-only list of <see cref="PostingServiceDto"/> results.</returns>
        /// <exception cref="OperationCanceledException">When the operation is cancelled via the provided <paramref name="ct"/>.</exception>
        Task<IReadOnlyList<PostingServiceDto>> GetSecurityPostingsAsync(Guid securityId, int skip, int take, DateTime? from, DateTime? to, Guid currentUserId, CancellationToken ct = default);
    }
}
