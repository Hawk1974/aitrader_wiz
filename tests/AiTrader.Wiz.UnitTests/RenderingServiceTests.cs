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

    private static WizardState BuildState()
    {
        return new WizardState
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
                new ComputerDefinition { Id = "computer_1", Label = "Windows", OperatingSystem = OperatingSystemKind.Windows },
                new ComputerDefinition { Id = "computer_2", Label = "Linux", OperatingSystem = OperatingSystemKind.Linux },
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "computer_2-linux",
                    DisplayName = "Linux",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_2",
                    Roles = [RoleKind.HermesBackend, RoleKind.LmStudio],
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
            ]
        };
    }
}
