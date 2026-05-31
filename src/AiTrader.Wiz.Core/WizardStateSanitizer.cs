namespace AiTrader.Wiz.Core;

public static class WizardStateSanitizer
{
    public static WizardState CreateDraftSafeCopy(WizardState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new WizardState
        {
            ClientIdentity = new ClientIdentity
            {
                ClientName = state.ClientIdentity.ClientName,
                MainContactName = state.ClientIdentity.MainContactName,
                MainContactEmail = state.ClientIdentity.MainContactEmail,
                DeploymentName = state.ClientIdentity.DeploymentName,
                Notes = state.ClientIdentity.Notes,
            },
            Computers = state.Computers.Select(CloneComputer).ToList(),
            Targets = state.Targets.Select(CloneTarget).ToList(),
            Connectivity = new ConnectivityConfiguration
            {
                TailnetName = state.Connectivity.TailnetName,
                BackendTargetHostOrIp = state.Connectivity.BackendTargetHostOrIp,
                DesktopTargetHostOrIp = state.Connectivity.DesktopTargetHostOrIp,
                SshUsername = state.Connectivity.SshUsername,
                SshPort = state.Connectivity.SshPort,
                RequiresTailscale = state.Connectivity.RequiresTailscale,
                BootstrapComplete = state.Connectivity.BootstrapComplete,
            },
            Backend = new BackendConfiguration
            {
                HermesHome = state.Backend.HermesHome,
                RepoPath = state.Backend.RepoPath,
                DataPath = state.Backend.DataPath,
                AlTraderDataPath = state.Backend.AlTraderDataPath,
                ModelsPath = state.Backend.ModelsPath,
                LogsPath = state.Backend.LogsPath,
                BackupsPath = state.Backend.BackupsPath,
                Timezone = state.Backend.Timezone,
            },
            HermesAiProvider = new HermesAiProviderConfiguration
            {
                ProviderKey = state.HermesAiProvider.ProviderKey,
                BaseUrl = state.HermesAiProvider.BaseUrl,
                ApiKey = string.Empty,
                ModelName = state.HermesAiProvider.ModelName,
            },
            AlpacaPaper = new AlpacaPaperConfiguration
            {
                AccountName = state.AlpacaPaper.AccountName,
                ApiKey = string.Empty,
                SecretKey = string.Empty,
                BaseUrl = state.AlpacaPaper.BaseUrl,
            },
            AlpacaLive = new AlpacaLiveConfiguration
            {
                Enabled = state.AlpacaLive.Enabled,
                ApiKey = string.Empty,
                SecretKey = string.Empty,
                BaseUrl = state.AlpacaLive.BaseUrl,
            },
            Telegram = new TelegramConfiguration
            {
                Enabled = state.Telegram.Enabled,
                BotToken = string.Empty,
                ChatId = state.Telegram.ChatId,
            },
            AgentMail = new AgentMailConfiguration
            {
                Enabled = state.AgentMail.Enabled,
                ApiKey = string.Empty,
                FromId = state.AgentMail.FromId,
                RecipientEmail = state.AgentMail.RecipientEmail,
            },
            CashAllocation = new CashAllocationConfiguration
            {
                StartOfDayCashBasis = state.CashAllocation.StartOfDayCashBasis,
                ProtectedReserve = new AllocationInput
                {
                    Mode = state.CashAllocation.ProtectedReserve.Mode,
                    Value = state.CashAllocation.ProtectedReserve.Value,
                },
                PerTickerAllocation = new AllocationInput
                {
                    Mode = state.CashAllocation.PerTickerAllocation.Mode,
                    Value = state.CashAllocation.PerTickerAllocation.Value,
                },
                MaxRankedCandidatesPerCycle = state.CashAllocation.MaxRankedCandidatesPerCycle,
            },
            ValidationResults = [],
        };
    }

    private static ComputerDefinition CloneComputer(ComputerDefinition computer) =>
        new()
        {
            Id = computer.Id,
            Label = computer.Label,
            OperatingSystem = computer.OperatingSystem,
            UsesWslBackend = computer.UsesWslBackend,
            DockerAvailable = computer.DockerAvailable,
            AccessMode = computer.AccessMode,
            AccessHostOrIp = computer.AccessHostOrIp,
            AccessPort = computer.AccessPort,
            AccessUsername = computer.AccessUsername,
            ServicePlacements = computer.ServicePlacements.Select(placement => new ServicePlacement
            {
                Service = placement.Service,
                PlacementMode = placement.PlacementMode,
            }).ToList(),
        };

    private static RuntimeTarget CloneTarget(RuntimeTarget target) =>
        new()
        {
            Id = target.Id,
            DisplayName = target.DisplayName,
            Kind = target.Kind,
            ComputerId = target.ComputerId,
            IsPrimaryDesktop = target.IsPrimaryDesktop,
            IsAuthoritativeBackend = target.IsAuthoritativeBackend,
            Roles = [.. target.Roles],
        };
}
