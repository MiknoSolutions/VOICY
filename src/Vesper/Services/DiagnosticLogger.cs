using System.IO;
using System.Text;

namespace Vesper.Services;

/// <summary>
/// Simple file logger for diagnostics. Writes to vesper-log.txt next to the executable.
/// </summary>
public static class DiagnosticLogger
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "vesper-log.txt");
    private static readonly object Lock = new();

    public static void Clear()
    {
        try { File.Delete(LogPath); } catch { }
    }

    public static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch { /* logging should never crash the app */ }
    }

    public static void LogEnvironment()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== VESPER Diagnostic Log ===");
        sb.AppendLine($"Time: {DateTime.Now:O}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
        sb.AppendLine($"CLR: {Environment.Version}");
        sb.AppendLine($"Processors: {Environment.ProcessorCount}");
        sb.AppendLine($"BaseDirectory: {AppContext.BaseDirectory}");
        sb.AppendLine($"TEMP: {Path.GetTempPath()}");
        sb.AppendLine($"User: {Environment.UserName}");
        sb.AppendLine($"Non-ASCII in path: {ContainsNonAscii(AppContext.BaseDirectory)}");
        sb.AppendLine($"Non-ASCII in TEMP: {ContainsNonAscii(Path.GetTempPath())}");
        Log(sb.ToString());
    }

    public static void LogException(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"EXCEPTION in {context}:");
        var current = ex;
        int depth = 0;
        while (current != null && depth < 5)
        {
            sb.AppendLine($"  [{depth}] {current.GetType().Name}: {current.Message}");
            if (depth == 0 && current.StackTrace != null)
                sb.AppendLine($"  StackTrace: {current.StackTrace}");
            current = current.InnerException;
            depth++;
        }
        Log(sb.ToString());
    }

    private static bool ContainsNonAscii(string s)
    {
        foreach (var c in s)
            if (c > 127) return true;
        return false;
    }
}
