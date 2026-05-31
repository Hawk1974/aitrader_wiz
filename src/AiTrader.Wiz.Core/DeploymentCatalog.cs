namespace AiTrader.Wiz.Core;

public sealed record EnumOption<T>(T Value, string DisplayName);

public static class DeploymentCatalog
{
    public static readonly IReadOnlyList<EnumOption<AccessMode>> AccessModeOptions =
    [
        new(AccessMode.DirectLocal, "Direct / Local"),
        new(AccessMode.Tailscale, "Tailscale"),
        new(AccessMode.Ssh, "SSH"),
        new(AccessMode.NotNeeded, "Not Needed"),
    ];

    public static readonly IReadOnlyList<EnumOption<ServicePlacementMode>> ServicePlacementOptions =
    [
        new(ServicePlacementMode.HostNative, "Host Native"),
        new(ServicePlacementMode.DockerContainer, "Docker Container"),
        new(ServicePlacementMode.ExternalOnly, "External Only"),
        new(ServicePlacementMode.NotOnThisComputer, "Not On This Computer"),
    ];

    public static readonly IReadOnlyList<ServiceKind> SupportedServices =
    [
        ServiceKind.HermesDesktop,
        ServiceKind.HermesBackend,
        ServiceKind.Tailscale,
        ServiceKind.TelegramIntegration,
        ServiceKind.AgentMailIntegration,
        ServiceKind.AlpacaIntegration,
    ];

    public static string GetAccessModeDisplayName(AccessMode mode) => mode switch
    {
        AccessMode.DirectLocal => "Direct / Local",
        AccessMode.Tailscale => "Tailscale",
        AccessMode.Ssh => "SSH",
        AccessMode.NotNeeded => "Not Needed",
        _ => mode.ToString(),
    };

    public static string GetServiceDisplayName(ServiceKind service) => service switch
    {
        ServiceKind.HermesDesktop => "Hermes Desktop",
        ServiceKind.HermesBackend => "Hermes Backend",
        ServiceKind.Tailscale => "Tailscale",
        ServiceKind.TelegramIntegration => "Telegram Integration",
        ServiceKind.AgentMailIntegration => "AgentMail Integration",
        ServiceKind.AlpacaIntegration => "Alpaca Integration",
        _ => service.ToString(),
    };

    public static string GetPlacementDisplayName(ServicePlacementMode mode) => mode switch
    {
        ServicePlacementMode.HostNative => "Host Native",
        ServicePlacementMode.DockerContainer => "Docker Container",
        ServicePlacementMode.ExternalOnly => "External Only",
        ServicePlacementMode.NotOnThisComputer => "Not On This Computer",
        _ => mode.ToString(),
    };
}
