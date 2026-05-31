using System.Net;
using System.Net.Http;
using System.Text;

using AiTrader.Wiz.Core;

namespace AiTrader.Wiz.UnitTests;

public sealed class ValidationServiceTests
{
    [Fact]
    public async Task ValidateAlpacaPaperAsync_Fails_WhenCredentialsMissing()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.ValidateAlpacaPaperAsync(new AlpacaPaperConfiguration());

        Assert.Equal(ValidationStatus.FailedBlocking, result.Status);
    }

    [Fact]
    public async Task ValidateHermesAiProviderAsync_Passes_ForOpenAiCompatibleProvider()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json") })));

        var result = await service.ValidateHermesAiProviderAsync(new HermesAiProviderConfiguration
        {
            ProviderKey = "openai",
            BaseUrl = "https://api.openai.com/v1",
            ModelName = "gpt-4.1",
            ApiKey = "api-key",
        });

        Assert.Equal(ValidationStatus.Passed, result.Status);
    }

    [Fact]
    public async Task ValidateHermesAiProviderAsync_Fails_WhenProviderMissing()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.ValidateHermesAiProviderAsync(new HermesAiProviderConfiguration());

        Assert.Equal(ValidationStatus.FailedBlocking, result.Status);
    }

    [Fact]
    public void ValidateConnectivity_Warns_WhenBootstrapNotComplete()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

        var result = service.ValidateConnectivity(new ConnectivityConfiguration
        {
            BackendTargetHostOrIp = "100.100.100.2",
            DesktopTargetHostOrIp = "100.100.100.3",
            RequiresTailscale = true,
            BootstrapComplete = false,
        });

        Assert.Equal(ValidationStatus.PassedWithWarning, result.Status);
    }

    [Fact]
    public void ValidateDeploymentModel_Fails_WhenDockerPlacementHasNoDocker()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        var state = new WizardState
        {
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "Desktop",
                    OperatingSystem = OperatingSystemKind.Windows,
                    DockerAvailable = false,
                    AccessMode = AccessMode.DirectLocal,
                    ServicePlacements =
                    [
                        new ServicePlacement { Service = ServiceKind.HermesDesktop, PlacementMode = ServicePlacementMode.DockerContainer },
                    ],
                },
                new ComputerDefinition
                {
                    Id = "computer_2",
                    Label = "Backend",
                    OperatingSystem = OperatingSystemKind.Linux,
                    DockerAvailable = false,
                    AccessMode = AccessMode.Ssh,
                    AccessHostOrIp = "100.64.0.2",
                    AccessUsername = "hawk",
                    AccessPort = 22,
                    ServicePlacements =
                    [
                        new ServicePlacement { Service = ServiceKind.HermesBackend, PlacementMode = ServicePlacementMode.HostNative },
                        new ServicePlacement { Service = ServiceKind.TelegramIntegration, PlacementMode = ServicePlacementMode.HostNative },
                        new ServicePlacement { Service = ServiceKind.AgentMailIntegration, PlacementMode = ServicePlacementMode.HostNative },
                        new ServicePlacement { Service = ServiceKind.AlpacaIntegration, PlacementMode = ServicePlacementMode.HostNative },
                    ],
                }
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "desktop",
                    DisplayName = "Desktop",
                    Kind = RuntimeTargetKind.Windows,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesDesktop],
                    IsPrimaryDesktop = true,
                },
                new RuntimeTarget
                {
                    Id = "backend",
                    DisplayName = "Backend",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_2",
                    Roles = [RoleKind.HermesBackend],
                    IsAuthoritativeBackend = true,
                }
            ]
        };

        var result = service.ValidateDeploymentModel(state);

        Assert.Equal(ValidationStatus.FailedBlocking, result.Status);
        Assert.Contains("Docker is not enabled", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDeploymentModel_Fails_WhenSshAccessIsIncomplete()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        var state = BuildValidDeploymentState();
        state.Computers[1].AccessHostOrIp = string.Empty;

        var result = service.ValidateDeploymentModel(state);

        Assert.Equal(ValidationStatus.FailedBlocking, result.Status);
        Assert.Contains("SSH host, username, or port is incomplete", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDeploymentModel_Passes_ForWindowsAndWslReferenceTopology()
    {
        var service = new ValidationService(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));
        var state = BuildValidDeploymentState();

        var result = service.ValidateDeploymentModel(state);

        Assert.Equal(ValidationStatus.Passed, result.Status);
    }

    private static WizardState BuildValidDeploymentState()
    {
        var state = new WizardState
        {
            Connectivity = new ConnectivityConfiguration
            {
                RequiresTailscale = true,
            },
            Computers =
            [
                new ComputerDefinition
                {
                    Id = "computer_1",
                    Label = "Desktop",
                    OperatingSystem = OperatingSystemKind.Windows,
                    UsesWslBackend = true,
                    DockerAvailable = false,
                    AccessMode = AccessMode.DirectLocal,
                },
                new ComputerDefinition
                {
                    Id = "computer_2",
                    Label = "Backend",
                    OperatingSystem = OperatingSystemKind.Linux,
                    DockerAvailable = true,
                    AccessMode = AccessMode.Ssh,
                    AccessHostOrIp = "100.64.0.2",
                    AccessUsername = "hawk",
                    AccessPort = 22,
                }
            ],
            Targets =
            [
                new RuntimeTarget
                {
                    Id = "desktop",
                    DisplayName = "Desktop",
                    Kind = RuntimeTargetKind.Windows,
                    ComputerId = "computer_1",
                    Roles = [RoleKind.HermesDesktop],
                    IsPrimaryDesktop = true,
                },
                new RuntimeTarget
                {
                    Id = "backend",
                    DisplayName = "Backend",
                    Kind = RuntimeTargetKind.Linux,
                    ComputerId = "computer_2",
                    Roles = [RoleKind.HermesBackend],
                    IsAuthoritativeBackend = true,
                }
            ]
        };

        TopologyService.ApplyDefaultDeploymentModel(state);
        return state;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
