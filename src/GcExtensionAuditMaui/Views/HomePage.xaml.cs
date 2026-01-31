using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class HomePage : ContentPage
{
    public HomePage(ConnectionViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

