using System.Collections.Concurrent;
using System.IO;

namespace WinRadial.Core;

/// <summary>
/// Thread-safe file-based logging service.
/// Writes to %APPDATA%\WinRadial\logs\ with daily rotation and 7-day auto-prune.
/// </summary>
public sealed class LogService : IDisposable
{
    private readonly string _logDirectory;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly Timer _flushTimer;
    private readonly object _writeLock = new();
    private bool _disposed;

    public LogService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinRadial", "logs");

        Directory.CreateDirectory(_logDirectory);
        PruneOldLogs();

        // Flush every 500ms
        _flushTimer = new Timer(_ => Flush(), null, 500, 500);
    }

    public void Debug(string message) => Enqueue("DEBUG", message);
    public void Info(string message) => Enqueue("INFO", message);
    public void Warning(string message) => Enqueue("WARN", message);
    public void Error(string message) => Enqueue("ERROR", message);

    private void Enqueue(string level, string message)
    {
        if (_disposed) return;
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        _queue.Enqueue($"[{timestamp}] [{level}] {message}");
    }

    private void Flush()
    {
        if (_disposed || _queue.IsEmpty) return;

        var lines = new List<string>();
        while (_queue.TryDequeue(out var line))
        {
            lines.Add(line);
        }

        if (lines.Count == 0) return;

        lock (_writeLock)
        {
            try
            {
                var logFile = Path.Combine(_logDirectory, $"winradial-{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllLines(logFile, lines);
            }
            catch
            {
                // Can't log a logging failure — silently discard
            }
        }
    }

    private void PruneOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-7);
            foreach (var file in Directory.GetFiles(_logDirectory, "winradial-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Non-critical — ignore prune failures
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _flushTimer.Dispose();
        Flush(); // Final flush
    }
}
