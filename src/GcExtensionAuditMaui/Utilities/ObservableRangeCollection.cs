using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace GcExtensionAuditMaui.Utilities;

public sealed class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public void ReplaceRange(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
    }
}

