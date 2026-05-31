namespace AiTrader.Wiz.Core;

public static class WizardStateFactory
{
    public static WizardState CreateDefault()
    {
        var state = new WizardState();
        state.Computers.Add(new ComputerDefinition
        {
            Id = "computer_1",
            Label = "Windows Workstation",
            OperatingSystem = OperatingSystemKind.Windows,
            AccessMode = AccessMode.DirectLocal,
        });

        state.Computers.Add(new ComputerDefinition
        {
            Id = "computer_2",
            Label = "Linux Spark Backend",
            OperatingSystem = OperatingSystemKind.Linux,
            AccessMode = AccessMode.Ssh,
        });

        state.Targets = TopologyService.DeriveTargets(state.Computers);
        TopologyService.ApplyDefaultDeploymentModel(state);
        return state;
    }
}
