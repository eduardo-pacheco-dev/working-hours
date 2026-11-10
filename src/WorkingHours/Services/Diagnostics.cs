using System.Diagnostics;
using System.Text;

namespace WorkingHours.Services;

/// <summary>Minimal crash log so failures outside the dispatcher are not silent.</summary>
public static class Diagnostics
{
    private static readonly object Gate = new();

    public static void Log(Exception? exception, string? context = null)
    {
        if (exception is null)
            return;

        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkingHours");
            Directory.CreateDirectory(directory);

            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}");
            builder.AppendLine(exception.ToString());
            builder.AppendLine(new string('-', 80));

            lock (Gate)
            {
                File.AppendAllText(Path.Combine(directory, "error.log"), builder.ToString());
            }
        }
        catch
        {
            // Logging must never throw.
        }
    }
}
