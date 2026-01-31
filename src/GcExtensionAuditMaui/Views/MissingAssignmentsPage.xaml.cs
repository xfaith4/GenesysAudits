using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class MissingAssignmentsPage : ContentPage
{
    public MissingAssignmentsPage(MissingAssignmentsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}

