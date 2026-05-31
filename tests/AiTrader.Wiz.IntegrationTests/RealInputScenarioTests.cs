using System.Net.Http;
using System.Text.Json;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.IntegrationTests;

public sealed class RealInputScenarioTests
{
    private static readonly string SecretsFilePath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "tests", ".local", "real_input_secrets.json"));

    [Fact]
    public void RealInputState_ExportPackage_UsesExpectedWindowsWslTopologyAndProvider()
    {
        if (!CanRunRealInputScenario())
        {
            return;
        }

        var state = BuildStateFromEnvironment();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "aitrader_wiz_real_input", Guid.NewGuid().ToString("N"));

        ExportService.ExportOverlay(state, outputDirectory);

        var yaml = File.ReadAllText(Path.Combine(outputDirectory, "CLIENT_INTAKE.yaml"));
        Assert.Contains("provider_mode: \"openai\"", yaml, StringComparison.Ordinal);
        Assert.Contains("base_url: \"https://api.openai.com/v1\"", yaml, StringComparison.Ordinal);
        Assert.Contains("backend_target_host_or_ip: \"localhost\"", yaml, StringComparison.Ordinal);
        Assert.Contains("desktop_target_host_or_ip: \"DESKTOP-SQPK1F5\"", yaml, StringComparison.Ordinal);
        Assert.Contains("operating_system: \"Windows\"", yaml, StringComparison.Ordinal);
        Assert.Contains("kind: \"Wsl\"", yaml, StringComparison.Ordinal);
        Assert.Contains("access_mode: \"DirectLocal\"", yaml, StringComparison.Ordinal);
        Assert.Contains("service_placements:", yaml, StringComparison.Ordinal);

        var files = Directory.GetFiles(outputDirectory).Select(Path.GetFileName).OrderBy(name => name).ToArray();
        Assert.Contains("SETUP_AI_EXECUTION_RULES.md", files);
        Assert.Contains("PACKAGE_MANIFEST.md", files);
        Assert.Contains("TARGET_01_WINDOWS_PRIMARY.md", files);
        Assert.Contains("TARGET_02_WSL_AUTHORITATIVE.md", files);
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Backend")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Desktop")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Backend", "repo_payload")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Backend", "runtime_templates")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Desktop", "runtime_templates")));
        Assert.True(Directory.Exists(Path.Combine(outputDirectory, "Desktop", "connection_docs")));
    }

    [Fact]
    public async Task RealInputState_LiveValidations_PassForConfiguredServices()
    {
        if (!CanRunRealInputScenario())
        {
            return;
        }

        var state = BuildStateFromEnvironment();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var validationService = new ValidationService(httpClient);

        var topology = ValidationService.ClassifyTopology(state);
        var deploymentModel = validationService.ValidateDeploymentModel(state);
        var connectivity = validationService.ValidateConnectivity(state.Connectivity);
        var alpacaPaper = await validationService.ValidateAlpacaPaperAsync(state.AlpacaPaper);
        var telegram = await validationService.ValidateTelegramAsync(state.Telegram);
        var agentMail = await validationService.ValidateAgentMailAsync(state.AgentMail);
        var aiProvider = await validationService.ValidateHermesAiProviderAsync(state.HermesAiProvider);
        var alpacaLive = await validationService.ValidateAlpacaLiveAsync(state.AlpacaLive);

        Assert.Equal(ValidationStatus.Passed, topology.Status);
        Assert.Equal(ValidationStatus.Passed, deploymentModel.Status);
        Assert.Equal(ValidationStatus.Passed, connectivity.Status);
        Assert.Equal(ValidationStatus.Passed, alpacaPaper.Status);
        Assert.Equal(ValidationStatus.Passed, telegram.Status);
        Assert.Equal(ValidationStatus.Passed, agentMail.Status);
        Assert.Equal(ValidationStatus.Passed, aiProvider.Status);
        Assert.Equal(ValidationStatus.Skipped, alpacaLive.Status);
    }

    private static WizardState BuildStateFromEnvironment()
    {
        var secrets = LoadLocalSecrets();
        var openAiApiKey = GetRequiredValue(secrets, "AITRADER_TEST_OPENAI_API_KEY", fallback: "OPENAI_API_KEY");
        var telegramBotToken = GetRequiredValue(secrets, "AITRADER_TEST_TELEGRAM_BOT_TOKEN");
        var telegramChatId = GetRequiredValue(secrets, "AITRADER_TEST_TELEGRAM_CHAT_ID");
        var alpacaPaperApiKey = GetRequiredValue(secrets, "AITRADER_TEST_ALPACA_PAPER_API_KEY");
        var alpacaPaperSecret = GetRequiredValue(secrets, "AITRADER_TEST_ALPACA_PAPER_SECRET_KEY");
        var agentMailApiKey = GetRequiredValue(secrets, "AITRADER_TEST_AGENTMAIL_API_KEY");
        var agentMailFromId = GetRequiredValue(secrets, "AITRADER_TEST_AGENTMAIL_FROM_ID");
        var reportRecipient = GetValue(secrets, "AITRADER_TEST_REPORT_EMAIL") ?? "hawk.contreras@gmail.com";

        var state = new WizardState
        {
            ClientIdentity = new ClientIdentity
            {
                ClientName = "hawkc",
                MainContactName = "hawkc",
                MainContactEmail = reportRecipient,
                DeploymentName = "AlTrader",
            },
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "DESKTOP-SQPK1F5",
                    OperatingSystem = OperatingSystemKind.Windows,
                    UsesWslBackend = true,
                    AccessMode = AccessMode.DirectLocal,
                }
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "computer_1-windows",
                    DisplayName = "DESKTOP-SQPK1F5 Windows",
                    Kind = RuntimeTargetKind.Windows,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesDesktop],
                    IsPrimaryDesktop = true,
                },
                new RuntimeTarget
                {
                    Id = "computer_1-wsl",
                    DisplayName = "DESKTOP-SQPK1F5 WSL",
                    Kind = RuntimeTargetKind.Wsl,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesBackend],
                    IsAuthoritativeBackend = true,
                }
            ],
            Connectivity = new ConnectivityConfiguration
            {
                TailnetName = string.Empty,
                BackendTargetHostOrIp = "localhost",
                DesktopTargetHostOrIp = "DESKTOP-SQPK1F5",
                SshUsername = "hawkc",
                SshPort = 22,
                RequiresTailscale = false,
                BootstrapComplete = true,
            },
            Backend = new BackendConfiguration
            {
                HermesHome = "/home/hawkc/.hermes",
                RepoPath = "/mnt/c/Users/hawkc/Documents/Codex/2026-05-17/what-is-the-proper-wasy-to/AlTrader",
                DataPath = "/mnt/c/Users/hawkc/Documents/Codex/2026-05-17/what-is-the-proper-wasy-to/AlTrader/data",
                AlTraderDataPath = "/mnt/c/Users/hawkc/Documents/Codex/2026-05-17/what-is-the-proper-wasy-to/AlTrader/data",
                ModelsPath = string.Empty,
                LogsPath = string.Empty,
                BackupsPath = string.Empty,
                Timezone = "America/Los_Angeles",
            },
            HermesAiProvider = new HermesAiProviderConfiguration
            {
                ProviderKey = "openai",
                BaseUrl = "https://api.openai.com/v1",
                ModelName = "gpt-4.1",
                ApiKey = openAiApiKey,
            },
            AlpacaPaper = new AlpacaPaperConfiguration
            {
                AccountName = "Paper",
                ApiKey = alpacaPaperApiKey,
                SecretKey = alpacaPaperSecret,
                BaseUrl = "https://paper-api.alpaca.markets/v2",
            },
            Telegram = new TelegramConfiguration
            {
                Enabled = true,
                BotToken = telegramBotToken,
                ChatId = telegramChatId,
            },
            AgentMail = new AgentMailConfiguration
            {
                Enabled = true,
                ApiKey = agentMailApiKey,
                FromId = agentMailFromId,
                RecipientEmail = reportRecipient,
            },
            AlpacaLive = new AlpacaLiveConfiguration
            {
                Enabled = false,
            }
        };

        TopologyService.ApplyDefaultDeploymentModel(state);
        return state;
    }

    private static bool CanRunRealInputScenario()
    {
        var secrets = LoadLocalSecrets();
        return RequiredKeys.All(key =>
            !string.IsNullOrWhiteSpace(GetValue(secrets, key)) ||
            (key == "AITRADER_TEST_OPENAI_API_KEY" && !string.IsNullOrWhiteSpace(GetValue(secrets, "OPENAI_API_KEY"))));
    }

    private static Dictionary<string, string> LoadLocalSecrets()
    {
        if (!File.Exists(SecretsFilePath))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(SecretsFilePath);
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        return values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetRequiredValue(IReadOnlyDictionary<string, string> secrets, string key, string? fallback = null)
    {
        var value = GetValue(secrets, key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            var fallbackValue = GetValue(secrets, fallback);
            if (!string.IsNullOrWhiteSpace(fallbackValue))
            {
                return fallbackValue;
            }
        }

        throw new InvalidOperationException($"Missing required real-input secret: {key}.");
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> secrets, string key)
    {
        if (secrets.TryGetValue(key, out var fileValue) && !string.IsNullOrWhiteSpace(fileValue))
        {
            return fileValue.Trim();
        }

        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static readonly string[] RequiredKeys =
    [
        "AITRADER_TEST_OPENAI_API_KEY",
        "AITRADER_TEST_TELEGRAM_BOT_TOKEN",
        "AITRADER_TEST_TELEGRAM_CHAT_ID",
        "AITRADER_TEST_ALPACA_PAPER_API_KEY",
        "AITRADER_TEST_ALPACA_PAPER_SECRET_KEY",
        "AITRADER_TEST_AGENTMAIL_API_KEY",
        "AITRADER_TEST_AGENTMAIL_FROM_ID",
    ];
}
