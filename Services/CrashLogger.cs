using System;
using System.IO;
using System.Text;

namespace MasterWirelessUtility.Services;

public static class CrashLogger
{
    public static string LogDirectory
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MasterWirelessUtility",
                "Logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string CurrentLogFile => Path.Combine(LogDirectory, "MasterWirelessUtility_crash.log");

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Error(Exception ex, string? note = null)
    {
        Write("ERROR", note ?? ex.Message, ex);
    }

    public static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("============================================================");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Level: {level}");
            sb.AppendLine($"Message: {message}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"Machine: {Environment.MachineName}");
            sb.AppendLine($"User: {Environment.UserName}");
            sb.AppendLine($"App Base: {AppContext.BaseDirectory}");
            if (ex != null)
            {
                sb.AppendLine("Exception:");
                sb.AppendLine(ex.ToString());
            }
            sb.AppendLine();

            File.AppendAllText(CurrentLogFile, sb.ToString());
        }
        catch
        {
            // Never let logging crash the app.
        }
    }
}
