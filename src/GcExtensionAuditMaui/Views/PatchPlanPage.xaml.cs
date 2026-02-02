using GcExtensionAuditMaui.ViewModels;

namespace GcExtensionAuditMaui.Views;

public partial class PatchPlanPage : ContentPage
{
    public PatchPlanPage(PatchPlanViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
