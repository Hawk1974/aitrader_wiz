using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.ContractTests;

public sealed class OutputContractTests
{
    [Fact]
    public void ClientIntakeYaml_ContainsMandatoryTopLevelSections()
    {
        var yaml = RenderingService.RenderClientIntakeYaml(SampleState());

        Assert.Contains("altrader_client_intake:", yaml, StringComparison.Ordinal);
        Assert.Contains("client_identity:", yaml, StringComparison.Ordinal);
        Assert.Contains("computers:", yaml, StringComparison.Ordinal);
        Assert.Contains("runtime_targets:", yaml, StringComparison.Ordinal);
        Assert.Contains("connectivity:", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public void InstructionMarkdown_ContainsPrecedenceAndBringUpOrder()
    {
        var markdown = RenderingService.RenderInstruction(SampleState());

        Assert.Contains("File Precedence", markdown, StringComparison.Ordinal);
        Assert.Contains("Bring-Up Order", markdown, StringComparison.Ordinal);
        Assert.Contains("backend target", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderAll_IncludesAllRequiredOverlayFiles()
    {
        var files = RenderingService.RenderAll(SampleState()).Select(artifact => artifact.FileName).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("CLIENT_INTAKE.yaml", files);
        Assert.Contains("CLIENT_SUMMARY.md", files);
        Assert.Contains("INSTRUCTION.md", files);
        Assert.Contains("VALIDATION_SUMMARY.md", files);
        Assert.Contains("SECRETS_STATUS.md", files);
        Assert.Contains("TARGET_01_LINUX_AUTHORITATIVE.md", files);
        Assert.Contains("TARGET_02_WINDOWS_PRIMARY.md", files);
    }

    private static WizardState SampleState() =>
        new()
        {
            ClientIdentity = new ClientIdentity
            {
                ClientName = "Client",
                MainContactName = "Operator",
                MainContactEmail = "operator@example.com",
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
                    Id = "backend-linux",
                    DisplayName = "Linux",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_2",
                    Roles = [RoleKind.HermesBackend, RoleKind.LmStudio],
                    IsAuthoritativeBackend = true,
                },
                new RuntimeTarget
                {
                    Id = "desktop-windows",
                    DisplayName = "Windows",
                    Kind = RuntimeTargetKind.Windows,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesDesktop],
                    IsPrimaryDesktop = true,
                }
            ],
            ValidationResults = [new ValidationRecord { Key = "topology", Status = ValidationStatus.Passed, Message = "ok" }]
        };
}
