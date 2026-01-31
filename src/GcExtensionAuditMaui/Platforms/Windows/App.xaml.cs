using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GcExtensionAuditMaui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor.
	public App()
	{
		this.InitializeComponent();
	}
	#pragma warning restore CS8618

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
