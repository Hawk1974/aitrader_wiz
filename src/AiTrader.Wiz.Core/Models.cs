using System.Text.Json.Serialization;

namespace AiTrader.Wiz.Core;

public enum OperatingSystemKind
{
    Windows,
    Linux,
    MacOs,
}

public enum RuntimeTargetKind
{
    Windows,
    Linux,
    MacOs,
    Wsl,
}

public enum RoleKind
{
    HermesBackend,
    HermesDesktop,
    LmStudio,
}

public enum ValidationStatus
{
    Passed,
    PassedWithWarning,
    FailedBlocking,
    Skipped,
}

public sealed class ComputerDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public OperatingSystemKind OperatingSystem { get; set; }
    public bool UsesWslBackend { get; set; }
}

public sealed class RuntimeTarget
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public RuntimeTargetKind Kind { get; set; }
    public string ComputerId { get; set; } = string.Empty;
    public bool IsPrimaryDesktop { get; set; }
    public bool IsAuthoritativeBackend { get; set; }
    public List<RoleKind> Roles { get; set; } = [];
}

public sealed class ClientIdentity
{
    public string ClientName { get; set; } = string.Empty;
    public string MainContactName { get; set; } = string.Empty;
    public string MainContactEmail { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class ConnectivityConfiguration
{
    public string TailnetName { get; set; } = string.Empty;
    public string BackendTargetHostOrIp { get; set; } = string.Empty;
    public string DesktopTargetHostOrIp { get; set; } = string.Empty;
    public string SshUsername { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public bool RequiresTailscale { get; set; } = true;
    public bool BootstrapComplete { get; set; }
}

public sealed class BackendConfiguration
{
    public string HermesHome { get; set; } = "/opt/hermes";
    public string RepoPath { get; set; } = "/opt/hermes/altrader";
    public string DataPath { get; set; } = "/srv/hermes";
    public string AlTraderDataPath { get; set; } = "/srv/hermes/altrader";
    public string ModelsPath { get; set; } = "/srv/models";
    public string LogsPath { get; set; } = "/var/log/hermes";
    public string BackupsPath { get; set; } = "/srv/backups";
    public string Timezone { get; set; } = "America/New_York";
}

public sealed class LmStudioConfiguration
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public bool ColocatedWithBackend { get; set; } = true;
}

public sealed class AlpacaPaperConfiguration
{
    public string AccountName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://paper-api.alpaca.markets";
}

public sealed class AlpacaLiveConfiguration
{
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.alpaca.markets";
}

public sealed class TelegramConfiguration
{
    public bool Enabled { get; set; } = true;
    public string BotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
}

public sealed class AgentMailConfiguration
{
    public bool Enabled { get; set; } = true;
    public string ApiKey { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
}

public sealed class ValidationRecord
{
    public string Key { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ValidationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class WizardState
{
    public ClientIdentity ClientIdentity { get; set; } = new();
    public List<ComputerDefinition> Computers { get; set; } = [];
    public List<RuntimeTarget> Targets { get; set; } = [];
    public ConnectivityConfiguration Connectivity { get; set; } = new();
    public BackendConfiguration Backend { get; set; } = new();
    public LmStudioConfiguration LmStudio { get; set; } = new();
    public AlpacaPaperConfiguration AlpacaPaper { get; set; } = new();
    public AlpacaLiveConfiguration AlpacaLive { get; set; } = new();
    public TelegramConfiguration Telegram { get; set; } = new();
    public AgentMailConfiguration AgentMail { get; set; } = new();
    public List<ValidationRecord> ValidationResults { get; set; } = [];
}

public sealed class ExportArtifact
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

