using System.IO;
using System.Text;

namespace AiTrader.Wiz;

public static class VerboseLogger
{
    private const string ApplicationDataFolderName = "AlTraderConfigWizard";
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationDataFolderName,
        "logs");
    private static readonly string SessionId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
    private static readonly string LogPath = Path.Combine(LogDirectory, $"AlTraderConfigWizard_{SessionId}.log");

    public static string CurrentLogPath => LogPath;

    public static void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);
        Info("Verbose logger initialized.");
        Info($"Session log path: {LogPath}");
    }

    public static void Info(string message) => WriteLine("INFO", message);

    public static void Warn(string message) => WriteLine("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            WriteLine("ERROR", message);
            return;
        }

        WriteLine("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private static void WriteLine(string level, string message)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(
                LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
    }
}
