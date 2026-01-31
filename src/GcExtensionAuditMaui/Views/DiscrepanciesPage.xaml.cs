using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class DiscrepanciesPage : ContentPage
{
    public DiscrepanciesPage(DiscrepanciesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

