namespace GcExtensionAuditMaui.Services;

public sealed class OutputPathService
{
    public string GetNewOutputFolder()
    {
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

#if WINDOWS
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var baseDir = string.IsNullOrWhiteSpace(docs) ? FileSystem.AppDataDirectory : docs;
        var outDir = Path.Combine(baseDir, "GcExtensionAudit", ts);
#else
        var outDir = Path.Combine(FileSystem.AppDataDirectory, "GcExtensionAudit", ts);
#endif

        Directory.CreateDirectory(outDir);
        return outDir;
    }

    public string GetDefaultLogPath()
    {
        var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");

#if WINDOWS
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir)) { baseDir = FileSystem.AppDataDirectory; }
#else
        var baseDir = FileSystem.AppDataDirectory;
#endif

        var logDir = Path.Combine(baseDir, "AGenesysToolKit", "Logs", "ExtensionAudit");
        Directory.CreateDirectory(logDir);
        return Path.Combine(logDir, $"GcExtensionAuditMaui_{ts}.log");
    }
}

