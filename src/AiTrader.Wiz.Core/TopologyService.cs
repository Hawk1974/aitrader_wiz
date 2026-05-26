namespace AiTrader.Wiz.Core;

public static class TopologyService
{
    public static List<RuntimeTarget> DeriveTargets(IReadOnlyList<ComputerDefinition> computers)
    {
        var results = new List<RuntimeTarget>();
        foreach (var computer in computers)
        {
            switch (computer.OperatingSystem)
            {
                case OperatingSystemKind.Windows:
                    results.Add(new RuntimeTarget
                    {
                        Id = $"{computer.Id}-windows",
                        ComputerId = computer.Id,
                        DisplayName = $"{computer.Label} Windows",
                        Kind = RuntimeTargetKind.Windows,
                    });
                    if (computer.UsesWslBackend)
                    {
                        results.Add(new RuntimeTarget
                        {
                            Id = $"{computer.Id}-wsl",
                            ComputerId = computer.Id,
                            DisplayName = $"{computer.Label} WSL",
                            Kind = RuntimeTargetKind.Wsl,
                        });
                    }
                    break;
                case OperatingSystemKind.Linux:
                    results.Add(new RuntimeTarget
                    {
                        Id = $"{computer.Id}-linux",
                        ComputerId = computer.Id,
                        DisplayName = $"{computer.Label} Linux",
                        Kind = RuntimeTargetKind.Linux,
                    });
                    break;
                case OperatingSystemKind.MacOs:
                    results.Add(new RuntimeTarget
                    {
                        Id = $"{computer.Id}-macos",
                        ComputerId = computer.Id,
                        DisplayName = $"{computer.Label} macOS",
                        Kind = RuntimeTargetKind.MacOs,
                    });
                    break;
            }
        }

        return results;
    }

    public static IReadOnlyList<string> ValidateTopology(WizardState state)
    {
        var errors = new List<string>();
        if (state.Computers.Count is < 1 or > 2)
        {
            errors.Add("The wizard supports one or two physical computers.");
        }

        if (state.Targets.Count == 0)
        {
            errors.Add("At least one runtime target must exist.");
            return errors;
        }

        foreach (var target in state.Targets)
        {
            if (target.Roles.Contains(RoleKind.HermesBackend) && !IsBackendCompatible(target.Kind))
            {
                errors.Add($"{target.DisplayName} cannot host Hermes Backend.");
            }

            if (target.Roles.Contains(RoleKind.HermesDesktop) && !IsDesktopCompatible(target.Kind))
            {
                errors.Add($"{target.DisplayName} cannot host Hermes Desktop.");
            }
        }

        var backendTargets = state.Targets.Where(t => t.Roles.Contains(RoleKind.HermesBackend)).ToList();
        if (backendTargets.Count != 1)
        {
            errors.Add("Exactly one authoritative backend target must be selected.");
        }
        else if (!backendTargets[0].IsAuthoritativeBackend)
        {
            errors.Add("The backend target must be marked authoritative.");
        }

        var desktopTargets = state.Targets.Where(t => t.Roles.Contains(RoleKind.HermesDesktop)).ToList();
        if (desktopTargets.Count < 1)
        {
            errors.Add("At least one Hermes Desktop target must be selected.");
        }
        else if (desktopTargets.Count(t => t.IsPrimaryDesktop) != 1)
        {
            errors.Add("Exactly one primary desktop target must be selected.");
        }

        return errors;
    }

    public static bool IsBackendCompatible(RuntimeTargetKind kind) =>
        kind is RuntimeTargetKind.Linux or RuntimeTargetKind.Wsl;

    public static bool IsDesktopCompatible(RuntimeTargetKind kind) =>
        kind is RuntimeTargetKind.Windows or RuntimeTargetKind.MacOs;
}

