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
        });

        state.Computers.Add(new ComputerDefinition
        {
            Id = "computer_2",
            Label = "Linux Spark Backend",
            OperatingSystem = OperatingSystemKind.Linux,
        });

        state.Targets = TopologyService.DeriveTargets(state.Computers);
        return state;
    }
}
