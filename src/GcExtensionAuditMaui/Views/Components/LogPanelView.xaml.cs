using System.Collections.Specialized;
using GcExtensionAuditMaui.Models.Logging;
using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views.Components;

public partial class LogPanelView : ContentView
{
    private DashboardViewModel? _vm;

    public LogPanelView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.LogEntries.CollectionChanged -= OnLogEntriesChanged;
        }

        _vm = BindingContext as DashboardViewModel;
        if (_vm is not null)
        {
            _vm.LogEntries.CollectionChanged += OnLogEntriesChanged;
        }
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_vm is null) { return; }
        if (!_vm.AutoScrollLog || !_vm.IsLogExpanded) { return; }
        if (_vm.LogEntries.Count == 0) { return; }

        var last = _vm.LogEntries[_vm.LogEntries.Count - 1];
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                LogList.ScrollTo(last, position: ScrollToPosition.End, animate: false);
            }
            catch
            {
                // Ignore scroll failures during layout.
            }
        });
    }
}
