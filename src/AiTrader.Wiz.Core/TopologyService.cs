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
        else if (backendTargets[0].Kind is not (RuntimeTargetKind.Linux or RuntimeTargetKind.Wsl))
        {
            errors.Add("The authoritative backend target must be Linux or WSL.");
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

    public static void ApplyDefaultDeploymentModel(WizardState state)
    {
        ApplyDefaultTargetAssignments(state);

        foreach (var computer in state.Computers)
        {
            EnsureServicePlacements(computer);

            var targets = state.Targets.Where(target => target.ComputerId == computer.Id).ToList();
            var hasDesktop = targets.Any(target => target.Roles.Contains(RoleKind.HermesDesktop));
            var hasBackend = targets.Any(target => target.Roles.Contains(RoleKind.HermesBackend));
            var isAuthoritativeBackend = targets.Any(target => target.IsAuthoritativeBackend);

            SetDefaultPlacement(computer, ServiceKind.HermesDesktop, hasDesktop ? ServicePlacementMode.HostNative : ServicePlacementMode.NotOnThisComputer);
            SetDefaultPlacement(computer, ServiceKind.HermesBackend, hasBackend
                ? (computer.DockerAvailable ? ServicePlacementMode.DockerContainer : ServicePlacementMode.HostNative)
                : ServicePlacementMode.NotOnThisComputer);
            SetDefaultPlacement(computer, ServiceKind.TelegramIntegration, isAuthoritativeBackend ? DefaultBackendServicePlacement(computer) : ServicePlacementMode.NotOnThisComputer);
            SetDefaultPlacement(computer, ServiceKind.AgentMailIntegration, isAuthoritativeBackend ? DefaultBackendServicePlacement(computer) : ServicePlacementMode.NotOnThisComputer);
            SetDefaultPlacement(computer, ServiceKind.AlpacaIntegration, isAuthoritativeBackend ? DefaultBackendServicePlacement(computer) : ServicePlacementMode.NotOnThisComputer);
            SetDefaultPlacement(computer, ServiceKind.Tailscale,
                state.Connectivity.RequiresTailscale && (hasDesktop || isAuthoritativeBackend)
                    ? DefaultBackendServicePlacement(computer)
                    : ServicePlacementMode.NotOnThisComputer);
        }
    }

    public static void ApplyDefaultTargetAssignments(WizardState state)
    {
        foreach (var target in state.Targets)
        {
            target.Roles.Clear();
            target.IsPrimaryDesktop = false;
            target.IsAuthoritativeBackend = false;
        }

        var backendTarget = state.Targets.FirstOrDefault(target => target.Kind is RuntimeTargetKind.Wsl or RuntimeTargetKind.Linux);
        if (backendTarget is not null)
        {
            backendTarget.Roles.Add(RoleKind.HermesBackend);
            backendTarget.IsAuthoritativeBackend = true;
        }

        var desktopTarget = state.Targets.FirstOrDefault(target => target.Kind is RuntimeTargetKind.Windows or RuntimeTargetKind.MacOs);
        if (desktopTarget is not null)
        {
            desktopTarget.Roles.Add(RoleKind.HermesDesktop);
            desktopTarget.IsPrimaryDesktop = true;
        }
    }

    private static ServicePlacementMode DefaultBackendServicePlacement(ComputerDefinition computer) =>
        computer.DockerAvailable ? ServicePlacementMode.DockerContainer : ServicePlacementMode.HostNative;

    private static void EnsureServicePlacements(ComputerDefinition computer)
    {
        foreach (var service in DeploymentCatalog.SupportedServices)
        {
            if (computer.ServicePlacements.All(existing => existing.Service != service))
            {
                computer.ServicePlacements.Add(new ServicePlacement
                {
                    Service = service,
                    PlacementMode = ServicePlacementMode.NotOnThisComputer,
                });
            }
        }
    }

    private static void SetDefaultPlacement(ComputerDefinition computer, ServiceKind service, ServicePlacementMode placementMode)
    {
        var placement = computer.ServicePlacements.First(existing => existing.Service == service);
        if (placement.PlacementMode == ServicePlacementMode.NotOnThisComputer || placement.PlacementMode == ServicePlacementMode.HostNative)
        {
            placement.PlacementMode = placementMode;
        }
    }
}
