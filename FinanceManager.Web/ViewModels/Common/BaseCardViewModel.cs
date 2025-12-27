using FinanceManager.Domain.Attachments;

namespace FinanceManager.Web.ViewModels.Common
{
    public abstract class BaseCardViewModel<TKeyValue> : BaseViewModel, ISymbolAssignableCard, ICardInitializable
    {
        private string? _initPrefill;
        private string? _initBack;
        public void SetInitValue(string? prefill) => _initPrefill = prefill;
        public void SetBackNavigation(string? backUrl) => _initBack = backUrl;
        public virtual Task InitializeAsync(System.Guid id) => LoadAsync(id);

        /// <summary>
        /// Optional embedded list view model that can be rendered together with the card (e.g. entries for a statement draft).
        /// When set, the UI may initialize and display the list below the card details.
        /// </summary>
        public BaseListViewModel? EmbeddedList { get; set; }

        // Note: derived classes can access the prefill/back values via these protected properties if needed
        protected string? InitPrefill => _initPrefill;
        protected string? InitBack => _initBack;

        public abstract Task LoadAsync(System.Guid id);

        public virtual Task<bool> SaveAsync() => Task.FromResult(true);

        public virtual Task<bool> DeleteAsync() => Task.FromResult(false);

        // New: single card record for the card view
        public virtual CardRecord? CardRecord { get; protected set; }

        // Pending field values stored by label key (not persisted until SaveAsync)
        protected readonly Dictionary<string, object?> _pendingFieldValues = new();

        protected BaseCardViewModel(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public IReadOnlyDictionary<string, object?> PendingFieldValues => _pendingFieldValues;
        public bool HasPendingChanges => _pendingFieldValues.Count > 0;


        public virtual AggregateBarChartViewModel? ChartViewModel => null;

        public virtual void ValidateFieldValue(CardField field, object? newValue)
        {
            if (field == null) return;
            _pendingFieldValues[field.LabelKey] = newValue;
            RaiseStateChanged();
        }
        public virtual void ValidateLookupField(CardField field, LookupItem? item)
        {
            if (field == null) return;
            if (item == null)
            {
                _pendingFieldValues.Remove(field.LabelKey);
            }
            else
            {
                _pendingFieldValues[field.LabelKey] = item;
            }
            RaiseStateChanged();
        }

        // Clear pending changes
        protected void ClearPendingChanges() => _pendingFieldValues.Clear();

        // Apply pending field overrides to a CardRecord instance. This mutates the given CardRecord's fields
        // so the UI can render the in-memory pending values without persisting them.
        public virtual CardRecord ApplyPendingValues(CardRecord record)
        {
            if (record == null) return record!;
            foreach (var f in record.Fields)
            {
                if (f == null) continue;
                if (!_pendingFieldValues.TryGetValue(f.LabelKey, out var v)) continue;
                // if pending value is a LookupItem, use its name and key
                if (v is LookupItem li)
                {
                    f.ValueId = li.Key;
                    f.Text = li.Name;
                    continue;
                }
                switch (v)
                {
                    case Guid g:
                        f.ValueId = g;
                        if (f.Kind == CardFieldKind.Symbol) f.SymbolId = g;
                        break;
                    case decimal d:
                        f.Amount = d;
                        break;
                    case string s:
                        // try parse decimal for currency fields
                        if (f.Kind == CardFieldKind.Currency && decimal.TryParse(s, out var dv))
                        {
                            f.Amount = dv;
                        }
                        else if (Guid.TryParse(s, out var gv))
                        {
                            f.ValueId = gv;
                            if (f.Kind == CardFieldKind.Symbol) f.SymbolId = gv;
                        }
                        else
                        {
                            f.Text = s;
                        }
                        break;
                    default:
                        f.Text = v?.ToString();
                        break;
                }
            }
            return record;
        }

        // --- Symbol upload handling (default implementation provided in base) ---
        /// <summary>
        /// Determine whether a symbol upload is permitted. Derived classes may override and throw exceptions
        /// or return false to disallow the upload.
        /// </summary>
        protected abstract bool IsSymbolUploadAllowed();

        /// <summary>
        /// Provides the Attachment parent kind and id to be used for uploading the symbol file.
        /// Implementations should return the appropriate AttachmentEntityKind and the (possibly Guid.Empty) parent id.
        /// </summary>
        protected abstract (AttachmentEntityKind Kind, Guid ParentId) GetSymbolParent();

        /// <summary>
        /// Called after a successful upload so the derived ViewModel can persist the new symbol attachment id
        /// (for example by calling the API to set the symbol on the entity) and update its Model/state.
        /// </summary>
        protected abstract Task AssignNewSymbolAsync(Guid? attachmentId);

        /// <summary>
        /// Validate and upload a symbol file for this card's entity. Base implementation performs permission
        /// check, uploads the file as a Symbol role attachment and then calls AssignNewSymbolAsync.
        /// </summary>
        public virtual async Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType)
        {
            try
            {
                // Allow derived classes to veto or raise an error
                var allowed = IsSymbolUploadAllowed();
                if (!allowed) return null;

                // Upload attachment using shared API client
                var api = ServiceProvider.GetRequiredService<Shared.IApiClient>();
                var parent = GetSymbolParent();
                var dto = await api.Attachments_UploadFileAsync((short)parent.Kind, parent.ParentId, stream, fileName, contentType, null, (short)FinanceManager.Domain.Attachments.AttachmentRole.Symbol);
                if (dto != null)
                {
                    await AssignNewSymbolAsync(dto.Id);
                    return dto.Id;
                }
                return null;
            }
            catch
            {
                // Fail silently to preserve previous behavior; derived implementations may log if desired.
                return null;
            }
        }

        // Reload the card data after changes (default no-op)
        public virtual Task ReloadAsync()
        {
            return Task.CompletedTask;
        }


    }
}
