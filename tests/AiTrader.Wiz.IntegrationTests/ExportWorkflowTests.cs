using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.IntegrationTests;

public sealed class ExportWorkflowTests
{
    [Fact]
    public void ExportOverlay_WritesExpectedFiles_ForWindowsLinuxSplitSetup()
    {
        var state = SampleStates.WindowsLinuxSplit();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "aitrader_wiz_tests", Guid.NewGuid().ToString("N"));

        ExportService.ExportOverlay(state, outputDirectory);

        var files = Directory.GetFiles(outputDirectory).Select(Path.GetFileName).OrderBy(name => name).ToArray();
        Assert.Contains("CLIENT_INTAKE.yaml", files);
        Assert.Contains("INSTRUCTION.md", files);
        Assert.Contains("TARGET_01_LINUX_AUTHORITATIVE.md", files);
        Assert.Contains("TARGET_02_WINDOWS_PRIMARY.md", files);

        var yaml = File.ReadAllText(Path.Combine(outputDirectory, "CLIENT_INTAKE.yaml"));
        Assert.Contains("cash_allocation_policy:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportOverlay_WritesExpectedFiles_ForWindowsWslSingleComputerSetup()
    {
        var state = SampleStates.WindowsWslLocal();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "aitrader_wiz_tests", Guid.NewGuid().ToString("N"));

        ExportService.ExportOverlay(state, outputDirectory);

        var files = Directory.GetFiles(outputDirectory).Select(Path.GetFileName).OrderBy(name => name).ToArray();
        Assert.Contains("TARGET_01_WINDOWS_PRIMARY.md", files);
        Assert.Contains("TARGET_02_WSL_AUTHORITATIVE.md", files);
    }

    [Fact]
    public void ExportOverlayZip_CreatesZipArtifact()
    {
        var state = SampleStates.MacDesktopLinuxBackend();
        var zipPath = Path.Combine(Path.GetTempPath(), "aitrader_wiz_tests", $"{Guid.NewGuid():N}.zip");

        ExportService.ExportOverlayZip(state, zipPath);

        Assert.True(File.Exists(zipPath));
    }
}

internal static class SampleStates
{
    public static WizardState WindowsLinuxSplit() =>
        BaseState(RuntimeTargetKind.Windows, RuntimeTargetKind.Linux, includeWsl: false);

    public static WizardState WindowsWslLocal() =>
        BaseState(RuntimeTargetKind.Windows, RuntimeTargetKind.Wsl, includeWsl: true);

    public static WizardState MacDesktopLinuxBackend() =>
        BaseState(RuntimeTargetKind.MacOs, RuntimeTargetKind.Linux, includeWsl: false);

    private static WizardState BaseState(RuntimeTargetKind desktopKind, RuntimeTargetKind backendKind, bool includeWsl)
    {
        var state = new WizardState
        {
            ClientIdentity = new ClientIdentity
            {
                ClientName = "Client",
                MainContactName = "Operator",
                MainContactEmail = "operator@example.com",
                DeploymentName = "Pilot",
            },
            Connectivity = new ConnectivityConfiguration
            {
                TailnetName = "tailnet",
                BackendTargetHostOrIp = "100.64.0.2",
                DesktopTargetHostOrIp = "100.64.0.3",
                SshUsername = "hawk",
                RequiresTailscale = true,
                BootstrapComplete = true,
            },
            AlpacaPaper = new AlpacaPaperConfiguration
            {
                AccountName = "Paper",
                ApiKey = "paper-key",
                SecretKey = "paper-secret",
            },
            Telegram = new TelegramConfiguration
            {
                Enabled = true,
                BotToken = "bot-token",
                ChatId = "12345",
            },
            AgentMail = new AgentMailConfiguration
            {
                Enabled = true,
                ApiKey = "agentmail-key",
                FromId = "from-id",
                RecipientEmail = "operator@example.com",
            },
            LmStudio = new LmStudioConfiguration
            {
                BaseUrl = "http://spark:1234",
                ModelId = "qwen",
            },
        };

        state.Computers.Add(new ComputerDefinition
        {
            Id = "computer_1",
            Label = desktopKind == RuntimeTargetKind.MacOs ? "Mac Desktop" : "Windows Desktop",
            OperatingSystem = desktopKind == RuntimeTargetKind.MacOs ? OperatingSystemKind.MacOs : OperatingSystemKind.Windows,
            UsesWslBackend = includeWsl,
        });
        state.Computers.Add(new ComputerDefinition
        {
            Id = "computer_2",
            Label = "Backend",
            OperatingSystem = backendKind == RuntimeTargetKind.Linux ? OperatingSystemKind.Linux : OperatingSystemKind.Windows,
        });

        state.Targets.Add(new RuntimeTarget
        {
            Id = $"desktop-{desktopKind}",
            DisplayName = desktopKind.ToString(),
            Kind = desktopKind,
            ComputerId = "computer_1",
            Roles = [RoleKind.HermesDesktop],
            IsPrimaryDesktop = true,
        });
        state.Targets.Add(new RuntimeTarget
        {
            Id = $"backend-{backendKind}",
            DisplayName = backendKind.ToString(),
            Kind = backendKind,
            ComputerId = backendKind == RuntimeTargetKind.Wsl ? "computer_1" : "computer_2",
            Roles = [RoleKind.HermesBackend, RoleKind.LmStudio],
            IsAuthoritativeBackend = true,
        });
        return state;
    }
}
