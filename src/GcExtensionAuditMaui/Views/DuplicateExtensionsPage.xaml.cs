using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class DuplicateExtensionsPage : ContentPage
{
    public DuplicateExtensionsPage(DuplicateExtensionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

