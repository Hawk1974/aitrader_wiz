using System.IO.Compression;

namespace AiTrader.Wiz.Core;

public static class ExportService
{
    public static string ExportOverlay(WizardState state, string directoryPath)
    {
        return ExportStandupPackage(state, directoryPath);
    }

    public static string ExportOverlayZip(WizardState state, string zipPath)
    {
        return ExportStandupPackageZip(state, zipPath);
    }

    public static string ExportStandupPackage(WizardState state, string directoryPath)
    {
        return PackageTemplateService.StageFullPackage(state, directoryPath);
    }

    public static string ExportStandupPackageZip(WizardState state, string zipPath)
    {
        var staging = Path.Combine(Path.GetTempPath(), "aitrader_wiz_export", Guid.NewGuid().ToString("N"));
        ExportStandupPackage(state, staging);
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
