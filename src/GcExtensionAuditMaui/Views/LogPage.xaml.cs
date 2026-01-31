using System.Collections.Specialized;
using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class LogPage : ContentPage
{
    private readonly LogViewModel _vm;

    public LogPage(LogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        _vm.Entries.CollectionChanged += EntriesOnCollectionChanged;
    }

    private void EntriesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_vm.AutoScroll) { return; }
        if (_vm.Entries.Count == 0) { return; }

        try
        {
            LogList.ScrollTo(_vm.Entries[^1], position: ScrollToPosition.End, animate: false);
        }
        catch
        {
            // Ignore scroll failures.
        }
    }
}

