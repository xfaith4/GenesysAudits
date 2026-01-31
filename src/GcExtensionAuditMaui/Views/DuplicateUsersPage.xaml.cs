using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class DuplicateUsersPage : ContentPage
{
    public DuplicateUsersPage(DuplicateUsersViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

