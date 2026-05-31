using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class RenderingServiceTests
{
    [Fact]
    public void RenderClientIntakeYaml_ContainsExpectedSections()
    {
        var state = BuildState();

        var yaml = RenderingService.RenderClientIntakeYaml(state);

        Assert.Contains("altrader_client_intake:", yaml, StringComparison.Ordinal);
        Assert.Contains("client_identity:", yaml, StringComparison.Ordinal);
        Assert.Contains("runtime_targets:", yaml, StringComparison.Ordinal);
        Assert.Contains("alpaca_paper:", yaml, StringComparison.Ordinal);
        Assert.Contains("cash_allocation_policy:", yaml, StringComparison.Ordinal);
        Assert.Contains("hermes_ai_provider:", yaml, StringComparison.Ordinal);
        Assert.Contains("docker_available:", yaml, StringComparison.Ordinal);
        Assert.Contains("access_mode:", yaml, StringComparison.Ordinal);
        Assert.Contains("service_placements:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderAll_UsesDeterministicTargetFileNames()
    {
        var state = BuildState();

        var artifacts = RenderingService.RenderAll(state);

        Assert.Contains(artifacts, artifact => artifact.FileName == "TARGET_01_LINUX_AUTHORITATIVE.md");
        Assert.Contains(artifacts, artifact => artifact.FileName == "TARGET_02_WINDOWS_PRIMARY.md");
    }

    [Fact]
    public void RenderInstruction_StatesPaperModeAndGoLiveBoundary()
    {
        var state = BuildState();

        var markdown = RenderingService.RenderInstruction(state);

        Assert.Contains("paper mode only", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GO LIVE", markdown, StringComparison.Ordinal);
        Assert.Contains("Do not assume Docker unless", markdown, StringComparison.Ordinal);
        Assert.Contains("full non-secret AlTrader stand-up package", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderSetupAiExecutionRules_RequiresLoggingAndEscalationRules()
    {
        var state = BuildState();

        var markdown = RenderingService.RenderSetupAiExecutionRules(state);

        Assert.Contains("Log every meaningful action", markdown, StringComparison.Ordinal);
        Assert.Contains("Known and solvable from this package", markdown, StringComparison.Ordinal);
        Assert.Contains("STANDUP_EXECUTION_SUMMARY.md", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderClientSummary_ContainsManualCommandsAndCashAllocation()
    {
        var state = BuildState();

        var markdown = RenderingService.RenderClientSummary(state);

        Assert.Contains("paper trade kickoff", markdown, StringComparison.Ordinal);
        Assert.Contains("GO LIVE", markdown, StringComparison.Ordinal);
        Assert.Contains("Cash Allocation Policy", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderApprovalSummary_RedactsSecrets()
    {
        var state = BuildState();

        var markdown = RenderingService.RenderApprovalSummary(state);

        Assert.Contains("Provider API Key: [redacted]", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("api-key", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftSafeCopy_ClearsSecretsButPreservesNonSecretFields()
    {
        var state = BuildState();
        state.AlpacaPaper.ApiKey = "paper-key";
        state.AlpacaPaper.SecretKey = "paper-secret";
        state.Telegram.BotToken = "token";
        state.AgentMail.ApiKey = "agentmail";
        state.ValidationResults.Add(new ValidationRecord { Key = "paper", Status = ValidationStatus.Passed, Message = "ok" });

        var sanitized = WizardStateSanitizer.CreateDraftSafeCopy(state);

        Assert.Equal(string.Empty, sanitized.HermesAiProvider.ApiKey);
        Assert.Equal(string.Empty, sanitized.AlpacaPaper.ApiKey);
        Assert.Equal(string.Empty, sanitized.AlpacaPaper.SecretKey);
        Assert.Equal(string.Empty, sanitized.Telegram.BotToken);
        Assert.Equal(string.Empty, sanitized.AgentMail.ApiKey);
        Assert.Empty(sanitized.ValidationResults);
        Assert.Equal(state.ClientIdentity.DeploymentName, sanitized.ClientIdentity.DeploymentName);
        Assert.Equal(state.Telegram.ChatId, sanitized.Telegram.ChatId);
    }

    [Fact]
    public void RenderTargetStandup_ContainsAccessAndPlacementInstructions()
    {
        var state = BuildState();

        var markdown = RenderingService.RenderTargetStandup(state, state.Targets.Single(target => target.IsAuthoritativeBackend), 1);

        Assert.Contains("Preferred access mode is", markdown, StringComparison.Ordinal);
        Assert.Contains("Follow Service Placement", markdown, StringComparison.Ordinal);
        Assert.Contains("Telegram Integration", markdown, StringComparison.Ordinal);
    }

    private static WizardState BuildState()
    {
        var state = new WizardState
        {
            ClientIdentity = new ClientIdentity
            {
                ClientName = "Example Client",
                MainContactName = "Alex",
                MainContactEmail = "alex@example.com",
                DeploymentName = "Pilot",
            },
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "Windows",
                    OperatingSystem = OperatingSystemKind.Windows,
                    AccessMode = AccessMode.DirectLocal,
                },
                new ComputerDefinition
                {
                    Id = "computer_2",
                    Label = "Linux",
                    OperatingSystem = OperatingSystemKind.Linux,
                    DockerAvailable = true,
                    AccessMode = AccessMode.Ssh,
                    AccessHostOrIp = "100.64.0.2",
                    AccessUsername = "hawk",
                    AccessPort = 22,
                },
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "computer_2-linux",
                    DisplayName = "Linux",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_2",
                    Roles = [RoleKind.HermesBackend],
                    IsAuthoritativeBackend = true,
                },
                new RuntimeTarget
                {
                    Id = "computer_1-windows",
                    DisplayName = "Windows",
                    Kind = RuntimeTargetKind.Windows,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesDesktop],
                    IsPrimaryDesktop = true,
                }
            ],
            HermesAiProvider = new HermesAiProviderConfiguration
            {
                ProviderKey = "openai",
                BaseUrl = "https://api.openai.com/v1",
                ModelName = "gpt-4.1",
                ApiKey = "api-key",
            },
            Connectivity = new ConnectivityConfiguration
            {
                RequiresTailscale = true,
            }
        };

        TopologyService.ApplyDefaultDeploymentModel(state);
        return state;
    }
}
