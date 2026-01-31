namespace GcExtensionAuditMaui.Services;

public sealed class DialogService
{
    public Task AlertAsync(string title, string message, string cancel = "OK")
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null) { return; }
            await page.DisplayAlert(title, message, cancel);
        });

    public Task<bool> ConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null) { return false; }
            return await page.DisplayAlert(title, message, accept, cancel);
        });
}
