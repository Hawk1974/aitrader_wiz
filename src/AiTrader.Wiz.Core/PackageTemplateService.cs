using System.Text;

namespace AiTrader.Wiz.Core;

public static class PackageTemplateService
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cmd",
        ".config",
        ".css",
        ".env",
        ".example",
        ".html",
        ".ini",
        ".js",
        ".json",
        ".md",
        ".ps1",
        ".py",
        ".sh",
        ".sql",
        ".toml",
        ".txt",
        ".xml",
        ".yaml",
        ".yml",
    };

    public static string StageFullPackage(WizardState state, string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        CopyTemplatePayload(state, directoryPath);

        foreach (var artifact in RenderingService.RenderAll(state))
        {
            var artifactPath = Path.Combine(directoryPath, artifact.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            File.WriteAllText(artifactPath, artifact.Content, Utf8NoBom);
        }

        return directoryPath;
    }

    private static void CopyTemplatePayload(WizardState state, string directoryPath)
    {
        var templateRoot = ResolveTemplateRoot();
        foreach (var sourceFile in Directory.EnumerateFiles(templateRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(templateRoot, sourceFile);
            var mappedRelativePath = MapRelativePath(relativePath);
            var destinationPath = Path.Combine(directoryPath, mappedRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (IsTextFile(sourceFile))
            {
                var content = File.ReadAllText(sourceFile);
                File.WriteAllText(destinationPath, SanitizeText(content, state), Utf8NoBom);
            }
            else
            {
                File.Copy(sourceFile, destinationPath, overwrite: true);
            }
        }
    }

    private static string ResolveTemplateRoot()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDirectory, "templates", "full_package"),
        };

        var current = new DirectoryInfo(baseDirectory);
        for (var i = 0; i < 8 && current is not null; i++)
        {
            candidates.Add(Path.Combine(current.FullName, "templates", "full_package"));
            candidates.Add(Path.Combine(current.FullName, "aitrader_wiz", "templates", "full_package"));
            current = current.Parent;
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate the wizard full-package templates directory.");
    }

    private static string MapRelativePath(string relativePath)
    {
        var normalized = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        normalized = normalized.Replace(
            $"Desktop{Path.DirectorySeparatorChar}runtime_export",
            $"Desktop{Path.DirectorySeparatorChar}runtime_templates",
            StringComparison.OrdinalIgnoreCase);
        normalized = normalized.Replace(
            $"Desktop{Path.DirectorySeparatorChar}desktop_source_docs",
            $"Desktop{Path.DirectorySeparatorChar}connection_docs",
            StringComparison.OrdinalIgnoreCase);
        return normalized;
    }

    private static bool IsTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (TextFileExtensions.Contains(extension))
        {
            return true;
        }

        var fileName = Path.GetFileName(filePath);
        return string.Equals(fileName, ".env.example", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeText(string content, WizardState state)
    {
        var backendTarget = state.Targets.FirstOrDefault(target => target.IsAuthoritativeBackend);
        var desktopTarget = state.Targets.FirstOrDefault(target => target.IsPrimaryDesktop);
        var backendLabel = backendTarget?.DisplayName ?? "authoritative backend target";
        var desktopLabel = desktopTarget?.DisplayName ?? "primary desktop target";
        var desktopHost = string.IsNullOrWhiteSpace(state.Connectivity.DesktopTargetHostOrIp)
            ? desktopLabel
            : state.Connectivity.DesktopTargetHostOrIp;
        var backendHost = string.IsNullOrWhiteSpace(state.Connectivity.BackendTargetHostOrIp)
            ? backendLabel
            : state.Connectivity.BackendTargetHostOrIp;

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["C:\\Users\\hawkc\\Documents\\Codex\\2026-05-17\\what-is-the-proper-wasy-to\\AlTrader"] = state.Backend.RepoPath,
            ["C:\\Users\\hawkc\\Documents\\Codex\\2026-05-17\\what-is-the-proper-wasy-to"] = "{{WIZARD_WORKSPACE_ROOT}}",
            ["C:\\Users\\hawkc\\.hermes"] = "{{WINDOWS_HERMES_HOME}}",
            ["C:\\Users\\hawkc\\.openclaw"] = "{{WINDOWS_OPENCLAW_HOME}}",
            ["/home/hawkc/.hermes"] = state.Backend.HermesHome,
            ["/mnt/c/Users/hawkc/Documents/Codex/2026-05-17/what-is-the-proper-wasy-to/AlTrader/data"] = state.Backend.AlTraderDataPath,
            ["/mnt/c/Users/hawkc/Documents/Codex/2026-05-17/what-is-the-proper-wasy-to/AlTrader"] = state.Backend.RepoPath,
            ["DESKTOP-SQPK1F5"] = desktopHost,
            ["DGX Spark"] = backendLabel,
            ["Linux DGX Spark"] = backendLabel,
            ["Windows Desktop"] = desktopLabel,
            ["Martin C."] = state.ClientIdentity.MainContactName,
            ["Hawk / Martin C."] = state.ClientIdentity.MainContactName,
            ["Hawk"] = state.ClientIdentity.MainContactName,
            ["localhost:18789"] = "{{HERMES_DESKTOP_ADAPTER_URL}}",
            ["localhost:3000"] = "{{HERMES_OFFICE_URL}}",
            ["127.0.0.1:8642"] = "{{HERMES_GATEWAY_API}}",
            ["paper trade kickoff"] = "paper trade kickoff",
        };

        foreach (var replacement in replacements)
        {
            if (!string.IsNullOrWhiteSpace(replacement.Value))
            {
                content = content.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
            }
        }

        content = content.Replace("runtime_export", "runtime_templates", StringComparison.OrdinalIgnoreCase);
        content = content.Replace("desktop_source_docs", "connection_docs", StringComparison.OrdinalIgnoreCase);
        content = content.Replace("AlTrader_Standup.zip", "stand-up package zip", StringComparison.OrdinalIgnoreCase);
        return content;
    }
}
