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

    [Fact]
    public void ApplyDefaultTargetAssignments_AssignsDesktopAndBackendFromDerivedTopology()
    {
        var state = new WizardState
        {
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "Test",
                    OperatingSystem = OperatingSystemKind.Windows,
                    UsesWslBackend = true,
                }
            ]
        };
        state.Targets = TopologyService.DeriveTargets(state.Computers);

        TopologyService.ApplyDefaultTargetAssignments(state);

        var windowsTarget = Assert.Single(state.Targets.Where(target => target.Kind == RuntimeTargetKind.Windows));
        var wslTarget = Assert.Single(state.Targets.Where(target => target.Kind == RuntimeTargetKind.Wsl));
        Assert.Contains(RoleKind.HermesDesktop, windowsTarget.Roles);
        Assert.True(windowsTarget.IsPrimaryDesktop);
        Assert.Contains(RoleKind.HermesBackend, wslTarget.Roles);
        Assert.True(wslTarget.IsAuthoritativeBackend);
    }

    [Fact]
    public void NormalizeTargetAssignments_RemovesIncompatibleRolesAndFlags()
    {
        var targets = new List<RuntimeTarget>
        {
            new()
            {
                Id = "windows",
                DisplayName = "Windows",
                Kind = RuntimeTargetKind.Windows,
                ComputerId = "computer_1",
                Roles = [RoleKind.HermesBackend, RoleKind.HermesDesktop],
                IsPrimaryDesktop = true,
                IsAuthoritativeBackend = true,
            },
            new()
            {
                Id = "wsl",
                DisplayName = "WSL",
                Kind = RuntimeTargetKind.Wsl,
                ComputerId = "computer_1",
                Roles = [RoleKind.HermesBackend, RoleKind.HermesDesktop],
                IsPrimaryDesktop = true,
                IsAuthoritativeBackend = true,
            }
        };

        TopologyService.NormalizeTargetAssignments(targets);

        var windowsTarget = targets[0];
        var wslTarget = targets[1];
        Assert.DoesNotContain(RoleKind.HermesBackend, windowsTarget.Roles);
        Assert.False(windowsTarget.IsAuthoritativeBackend);
        Assert.DoesNotContain(RoleKind.HermesDesktop, wslTarget.Roles);
        Assert.False(wslTarget.IsPrimaryDesktop);
    }

    [Fact]
    public void NormalizeTargetAssignments_KeepsOnlyOneBackendAndOneDesktopRole()
    {
        var targets = new List<RuntimeTarget>
        {
            new()
            {
                Id = "wsl",
                DisplayName = "WSL",
                Kind = RuntimeTargetKind.Wsl,
                ComputerId = "computer_1",
                Roles = [RoleKind.HermesBackend],
                IsAuthoritativeBackend = true,
            },
            new()
            {
                Id = "linux",
                DisplayName = "Linux",
                Kind = RuntimeTargetKind.Linux,
                ComputerId = "computer_2",
                Roles = [RoleKind.HermesBackend],
                IsAuthoritativeBackend = true,
            },
            new()
            {
                Id = "windows",
                DisplayName = "Windows",
                Kind = RuntimeTargetKind.Windows,
                ComputerId = "computer_1",
                Roles = [RoleKind.HermesDesktop],
                IsPrimaryDesktop = true,
            },
            new()
            {
                Id = "mac",
                DisplayName = "macOS",
                Kind = RuntimeTargetKind.MacOs,
                ComputerId = "computer_3",
                Roles = [RoleKind.HermesDesktop],
                IsPrimaryDesktop = true,
            }
        };

        TopologyService.NormalizeTargetAssignments(targets);

        Assert.Single(targets.Where(target => target.Roles.Contains(RoleKind.HermesBackend)));
        Assert.Single(targets.Where(target => target.Roles.Contains(RoleKind.HermesDesktop)));
        Assert.Single(targets.Where(target => target.IsAuthoritativeBackend));
        Assert.Single(targets.Where(target => target.IsPrimaryDesktop));
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
