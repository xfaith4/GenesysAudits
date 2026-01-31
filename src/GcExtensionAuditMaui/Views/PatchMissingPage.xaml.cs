using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class PatchMissingPage : ContentPage
{
    public PatchMissingPage(PatchMissingViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

