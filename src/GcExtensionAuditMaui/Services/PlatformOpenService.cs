using System.Diagnostics;

namespace GcExtensionAuditMaui.Services;

public sealed class PlatformOpenService
{
    public Task OpenFileAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return Task.CompletedTask; }

#if WINDOWS
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
#else
        return Launcher.Default.OpenAsync(new OpenFileRequest("Open file", new ReadOnlyFile(path)));
#endif
    }

    public Task OpenFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) { return Task.CompletedTask; }

#if WINDOWS
        Process.Start(new ProcessStartInfo("explorer.exe", folderPath) { UseShellExecute = true });
        return Task.CompletedTask;
#else
        // No consistent “open folder” on mobile; open the folder location if the platform supports it.
        return Launcher.Default.OpenAsync(folderPath);
#endif
    }
}
