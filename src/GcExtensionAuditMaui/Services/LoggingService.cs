using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using GcExtensionAuditMaui.Models.Logging;

namespace GcExtensionAuditMaui.Services;

public sealed class LoggingService
{
    private readonly ConcurrentQueue<LogEntry> _pendingUi = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    // Configuration constants
    private const int UiPumpIntervalMs = 300;
    private const int BatchInitialCapacity = 128;
    private const int MaxBatchSize = 256;

    private StreamWriter? _writer;
    private Task? _uiPump;
    private CancellationTokenSource? _uiCts;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public int MaxEntries { get; set; } = 2000;
    public string? LogPath { get; private set; }

    public void Initialize(string logPath)
    {
        LogPath = logPath;

        _uiCts = new CancellationTokenSource();
        _uiPump = Task.Run(() => UiPumpAsync(_uiCts.Token));

        try
        {
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true,
            };

            Log(LogLevel.Info, "Logging initialized", new { LogPath = logPath });
        }
        catch (Exception ex)
        {
            _writer = null;
            EnqueueUi(LogLevel.Warn, "Logging initialization failed (file logging disabled)", new
            {
                LogPath = logPath,
                ex = new { ex.Message, Type = ex.GetType().FullName },
            });
        }
    }

    public void ClearView()
    {
        MainThread.BeginInvokeOnMainThread(() => Entries.Clear());
    }

    public void Log(LogLevel level, string message, object? data = null, Exception? ex = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}",
            DataJson = data is null ? null : SerializeData(data),
        };

        try
        {
            _writer?.WriteLine(Format(entry));
        }
        catch (IOException)
        {
            // Intentionally ignore logging I/O failures to avoid cascading UI failures.
        }
        catch (ObjectDisposedException)
        {
            // Writer was disposed, ignore to avoid cascading failures.
        }

        _pendingUi.Enqueue(entry);
    }

    private void EnqueueUi(LogLevel level, string message, object? data = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            DataJson = data is null ? null : SerializeData(data),
        };

        _pendingUi.Enqueue(entry);
    }

    private async Task UiPumpAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(UiPumpIntervalMs));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            if (_pendingUi.IsEmpty) { continue; }

            var batch = new List<LogEntry>(BatchInitialCapacity);
            while (batch.Count < MaxBatchSize && _pendingUi.TryDequeue(out var e))
            {
                batch.Add(e);
            }

            if (batch.Count == 0) { continue; }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var e in batch)
                {
                    Entries.Add(e);
                }

                while (Entries.Count > MaxEntries)
                {
                    Entries.RemoveAt(0);
                }
            });
        }
    }

    private string SerializeData(object data)
    {
        try
        {
            var raw = JsonSerializer.Serialize(data, _jsonOptions);
            using var doc = JsonDocument.Parse(raw);
            return RedactJson(doc.RootElement);
        }
        catch
        {
            return "(data serialization failed)";
        }
    }

    private static string RedactJson(JsonElement root)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
        WriteRedacted(root, writer);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteRedacted(JsonElement el, Utf8JsonWriter writer)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            {
                writer.WriteStartObject();
                foreach (var p in el.EnumerateObject())
                {
                    writer.WritePropertyName(p.Name);
                    if (IsSensitiveKey(p.Name))
                    {
                        writer.WriteStringValue("***REDACTED***");
                    }
                    else
                    {
                        WriteRedacted(p.Value, writer);
                    }
                }
                writer.WriteEndObject();
                return;
            }
            case JsonValueKind.Array:
            {
                writer.WriteStartArray();
                foreach (var item in el.EnumerateArray())
                {
                    WriteRedacted(item, writer);
                }
                writer.WriteEndArray();
                return;
            }
            case JsonValueKind.String:
                writer.WriteStringValue(el.GetString());
                return;
            case JsonValueKind.Number:
                if (el.TryGetInt64(out var i64)) { writer.WriteNumberValue(i64); return; }
                if (el.TryGetDouble(out var d)) { writer.WriteNumberValue(d); return; }
                writer.WriteRawValue(el.GetRawText());
                return;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(el.GetBoolean());
                return;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                return;
            default:
                writer.WriteRawValue(el.GetRawText());
                return;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) { return false; }
        return key.Equals("authorization", StringComparison.OrdinalIgnoreCase)
               || key.Equals("access_token", StringComparison.OrdinalIgnoreCase)
               || key.Equals("access-token", StringComparison.OrdinalIgnoreCase)
               || key.Equals("token", StringComparison.OrdinalIgnoreCase)
               || key.Equals("password", StringComparison.OrdinalIgnoreCase)
               || key.Equals("client_secret", StringComparison.OrdinalIgnoreCase)
               || key.Equals("client-secret", StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(LogEntry e)
    {
        var ts = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{ts}] [{e.Level.ToString().ToUpperInvariant()}] {e.Message}";
        if (!string.IsNullOrWhiteSpace(e.DataJson))
        {
            line += $" | {e.DataJson}";
        }
        return line;
    }
}
