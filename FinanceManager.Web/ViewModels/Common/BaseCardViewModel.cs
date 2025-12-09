using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FinanceManager.Web.ViewModels.Common
{
    public abstract class BaseCardViewModel<TKeyValue> : BaseViewModel
    {
        public virtual Task InitializeAsync(System.Guid id) => LoadAsync(id);

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

        // Validate and upload a symbol file for this card's entity. Default implementation does nothing and returns null.
        public virtual Task<Guid?> ValidateSymbolAsync(System.IO.Stream stream, string fileName, string contentType)
        {
            return Task.FromResult<Guid?>(null);
        }

        // Reload the card data after changes (default no-op)
        public virtual Task ReloadAsync()
        {
            return Task.CompletedTask;
        }

        
    }
}
