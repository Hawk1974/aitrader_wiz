using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class TopologyServiceTests
{
    [Fact]
    public void DeriveTargets_CreatesWslTarget_ForWindowsComputerConfiguredForWsl()
    {
        var computers = new[]
        {
            new ComputerDefinition
            {
                Id = "computer_1",
                Label = "Operator PC",
                OperatingSystem = OperatingSystemKind.Windows,
                UsesWslBackend = true,
            }
        };

        var targets = TopologyService.DeriveTargets(computers);

        Assert.Equal(2, targets.Count);
        Assert.Contains(targets, target => target.Kind == RuntimeTargetKind.Windows);
        Assert.Contains(targets, target => target.Kind == RuntimeTargetKind.Wsl);
    }

    [Fact]
    public void ValidateTopology_RejectsInvalidRoleCompatibility()
    {
        var state = new WizardState
        {
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "Linux Box",
                    OperatingSystem = OperatingSystemKind.Linux,
                }
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "computer_1-linux",
                    DisplayName = "Linux Box Linux",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_1",
                    IsPrimaryDesktop = true,
                    Roles = [RoleKind.HermesDesktop],
                }
            ]
        };

        var errors = TopologyService.ValidateTopology(state);

        Assert.Contains(errors, error => error.Contains("cannot host Hermes Desktop", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTopology_RequiresExactlyOneAuthoritativeBackend()
    {
        var state = BuildValidState();
        state.Targets[0].IsAuthoritativeBackend = false;

        var errors = TopologyService.ValidateTopology(state);

        Assert.Contains(errors, error => error.Contains("authoritative", StringComparison.OrdinalIgnoreCase));
    }

    private static WizardState BuildValidState()
    {
        return new WizardState
        {
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
            }
        };
    }
}
