using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class DryRunPage : ContentPage
{
    public DryRunPage(DryRunViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

