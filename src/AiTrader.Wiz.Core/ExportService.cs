using System.IO.Compression;

namespace AiTrader.Wiz.Core;

public static class ExportService
{
    public static string ExportOverlay(WizardState state, string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        foreach (var artifact in RenderingService.RenderAll(state))
        {
            File.WriteAllText(Path.Combine(directoryPath, artifact.FileName), artifact.Content);
        }

        return directoryPath;
    }

    public static string ExportOverlayZip(WizardState state, string zipPath)
    {
        var staging = Path.Combine(Path.GetTempPath(), "aitrader_wiz_export", Guid.NewGuid().ToString("N"));
        ExportOverlay(state, staging);
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(staging, zipPath);
        Directory.Delete(staging, true);
        return zipPath;
    }
}

