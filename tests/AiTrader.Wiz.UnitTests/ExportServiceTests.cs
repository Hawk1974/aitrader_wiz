using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class ExportServiceTests
{
    [Fact]
    public void ExportStandupPackage_StagesBundledPayloadAndSanitizesLocalMachineReferences()
    {
        var state = BuildState();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "aitrader_wiz_package_test", Guid.NewGuid().ToString("N"));

        ExportService.ExportStandupPackage(state, outputDirectory);

        Assert.True(File.Exists(Path.Combine(outputDirectory, "SETUP_AI_EXECUTION_RULES.md")));
        Assert.True(File.Exists(Path.Combine(outputDirectory, "PACKAGE_MANIFEST.md")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Backend", "repo_payload")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Backend", "runtime_templates")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Desktop", "runtime_templates")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Desktop", "connection_docs")));
        Assert.False(Directory.Exists(Path.Combine(outputDirectory, "Desktop", "runtime_export")));

        var textFiles = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .Where(path => new[] { ".md", ".yaml", ".yml", ".json", ".ps1", ".py", ".txt", ".toml" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var file in textFiles)
        {
            var content = File.ReadAllText(file);
            Assert.DoesNotContain(@"C:\Users\hawkc", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DESKTOP-SQPK1F5", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static WizardState BuildState()
    {
        var state = new WizardState
        {
            ClientIdentity = new ClientIdentity
            {
                ClientName = "Example Client",
                MainContactName = "Alex Operator",
                MainContactEmail = "alex@example.com",
                DeploymentName = "Pilot",
            },
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "Primary Desktop",
                    OperatingSystem = OperatingSystemKind.Windows,
                    AccessMode = AccessMode.DirectLocal,
                },
                new ComputerDefinition
                {
                    Id = "computer_2",
                    Label = "Authoritative Backend",
                    OperatingSystem = OperatingSystemKind.Linux,
                    DockerAvailable = true,
                    AccessMode = AccessMode.Ssh,
                    AccessHostOrIp = "100.64.0.5",
                    AccessUsername = "hermes",
                    AccessPort = 22,
                },
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "computer_2-linux",
                    DisplayName = "Authoritative Backend",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_2",
                    Roles = [RoleKind.HermesBackend],
                    IsAuthoritativeBackend = true,
                },
                new RuntimeTarget
                {
                    Id = "computer_1-windows",
                    DisplayName = "Primary Desktop",
                    Kind = RuntimeTargetKind.Windows,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesDesktop],
                    IsPrimaryDesktop = true,
                }
            ],
            Connectivity = new ConnectivityConfiguration
            {
                BackendTargetHostOrIp = "100.64.0.5",
                DesktopTargetHostOrIp = "primary-desktop",
                RequiresTailscale = true,
                SshUsername = "hermes",
                SshPort = 22,
            },
            Backend = new BackendConfiguration
            {
                HermesHome = "/opt/hermes",
                RepoPath = "/opt/hermes/altrader",
                DataPath = "/srv/hermes",
                AlTraderDataPath = "/srv/hermes/altrader",
                ModelsPath = "/srv/models",
                LogsPath = "/var/log/hermes",
                BackupsPath = "/srv/backups",
                Timezone = "America/Los_Angeles",
            },
            HermesAiProvider = new HermesAiProviderConfiguration
            {
                ProviderKey = "openai",
                BaseUrl = "https://api.openai.com/v1",
                ModelName = "gpt-4.1",
                ApiKey = "api-key",
            }
        };

        TopologyService.ApplyDefaultDeploymentModel(state);
        return state;
    }
}
