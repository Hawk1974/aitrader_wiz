using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace AiTrader.Wiz.Core;

public sealed class ValidationService(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<ValidationRecord> ValidateAlpacaPaperAsync(AlpacaPaperConfiguration config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.SecretKey))
        {
            return Failed("alpaca_paper", "Alpaca paper credentials are missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{NormalizeAlpacaBaseUrl(config.BaseUrl)}/account");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("APCA-API-KEY-ID", config.ApiKey);
        request.Headers.Add("APCA-API-SECRET-KEY", config.SecretKey);
        return await SendJsonValidationAsync("alpaca_paper", request, cancellationToken);
    }

    public async Task<ValidationRecord> ValidateAlpacaLiveAsync(AlpacaLiveConfiguration config, CancellationToken cancellationToken = default)
    {
        if (!config.Enabled)
        {
            return Skipped("alpaca_live", "Live trading validation skipped because live config is disabled.");
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.SecretKey))
        {
            return Failed("alpaca_live", "Alpaca live credentials are missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{NormalizeAlpacaBaseUrl(config.BaseUrl)}/account");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("APCA-API-KEY-ID", config.ApiKey);
        request.Headers.Add("APCA-API-SECRET-KEY", config.SecretKey);
        return await SendJsonValidationAsync("alpaca_live", request, cancellationToken);
    }

    public async Task<ValidationRecord> ValidateTelegramAsync(TelegramConfiguration config, CancellationToken cancellationToken = default)
    {
        if (!config.Enabled)
        {
            return Skipped("telegram", "Telegram validation skipped because Telegram is disabled.");
        }

        if (string.IsNullOrWhiteSpace(config.BotToken) || string.IsNullOrWhiteSpace(config.ChatId))
        {
            return Failed("telegram", "Telegram bot token or chat id is missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.telegram.org/bot{config.BotToken}/getMe");
        return await SendJsonValidationAsync("telegram", request, cancellationToken);
    }

    public async Task<ValidationRecord> ValidateAgentMailAsync(AgentMailConfiguration config, CancellationToken cancellationToken = default)
    {
        if (!config.Enabled)
        {
            return Skipped("agentmail", "AgentMail validation skipped because email delivery is disabled.");
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.FromId) || string.IsNullOrWhiteSpace(config.RecipientEmail))
        {
            return Failed("agentmail", "AgentMail fields are incomplete.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.agentmail.to/v0/inboxes/{config.FromId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        return await SendJsonValidationAsync("agentmail", request, cancellationToken);
    }

    public async Task<ValidationRecord> ValidateHermesAiProviderAsync(HermesAiProviderConfiguration config, CancellationToken cancellationToken = default)
    {
        var provider = HermesProviderCatalog.Find(config.ProviderKey);
        if (string.IsNullOrWhiteSpace(provider.Key))
        {
            return Failed("hermes_ai_provider", "An AI provider must be selected for the backend target.");
        }

        return provider.ValidationMode switch
        {
            "openai_compatible" => await ValidateOpenAiCompatibleProviderAsync(
                "hermes_ai_provider",
                string.IsNullOrWhiteSpace(config.BaseUrl) ? provider.DefaultBaseUrl : config.BaseUrl,
                config.ModelName,
                config.ApiKey,
                requireApiKey: provider.RequiresApiKey,
                cancellationToken),
            "anthropic" => await ValidateAnthropicProviderAsync(config, provider, cancellationToken),
            "no_http_validation" => ValidateRecordedProvider(config, provider),
            _ => Failed("hermes_ai_provider", "The selected AI provider is not supported by the wizard."),
        };
    }

    public ValidationRecord ValidateConnectivity(ConnectivityConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.BackendTargetHostOrIp) || string.IsNullOrWhiteSpace(config.DesktopTargetHostOrIp))
        {
            return Failed("connectivity", "Connectivity fields are incomplete.");
        }

        if (config.RequiresTailscale && !config.BootstrapComplete)
        {
            return Warning("connectivity", "Bootstrap is not yet complete. Export may continue with warnings.");
        }

        return Passed("connectivity", "Connectivity details look complete.");
    }

    public ValidationRecord ValidateDeploymentModel(WizardState state)
    {
        var errors = new List<string>();
        var backendComputerId = state.Targets.SingleOrDefault(target => target.IsAuthoritativeBackend)?.ComputerId;

        foreach (var computer in state.Computers)
        {
            foreach (var placement in computer.ServicePlacements)
            {
                if (placement.PlacementMode == ServicePlacementMode.DockerContainer && !computer.DockerAvailable)
                {
                    errors.Add($"{computer.Label} assigns {DeploymentCatalog.GetServiceDisplayName(placement.Service)} to Docker but Docker is not enabled for that computer.");
                }
            }

            if (computer.AccessMode == AccessMode.Ssh)
            {
                if (string.IsNullOrWhiteSpace(computer.AccessHostOrIp) ||
                    string.IsNullOrWhiteSpace(computer.AccessUsername) ||
                    computer.AccessPort <= 0)
                {
                    errors.Add($"{computer.Label} uses SSH access but SSH host, username, or port is incomplete.");
                }
            }
            else if (computer.AccessMode == AccessMode.Tailscale && string.IsNullOrWhiteSpace(computer.AccessHostOrIp))
            {
                errors.Add($"{computer.Label} uses Tailscale access but the Tailscale host or IP is missing.");
            }
        }

        if (!string.IsNullOrWhiteSpace(backendComputerId))
        {
            var backendComputer = state.Computers.FirstOrDefault(computer => computer.Id == backendComputerId);
            if (backendComputer is not null)
            {
                foreach (var service in new[]
                         {
                             ServiceKind.HermesBackend,
                             ServiceKind.TelegramIntegration,
                             ServiceKind.AgentMailIntegration,
                             ServiceKind.AlpacaIntegration,
                         })
                {
                    if (GetPlacementMode(backendComputer, service) == ServicePlacementMode.NotOnThisComputer)
                    {
                        errors.Add($"{backendComputer.Label} must explicitly place {DeploymentCatalog.GetServiceDisplayName(service)} because it is the authoritative backend computer.");
                    }
                }

                if (state.Connectivity.RequiresTailscale && GetPlacementMode(backendComputer, ServiceKind.Tailscale) == ServicePlacementMode.NotOnThisComputer)
                {
                    errors.Add($"{backendComputer.Label} must explicitly place Tailscale because the deployment requires Tailscale.");
                }
            }
        }

        foreach (var service in GetRequiredServices(state))
        {
            if (state.Computers.All(computer => GetPlacementMode(computer, service) == ServicePlacementMode.NotOnThisComputer))
            {
                errors.Add($"{DeploymentCatalog.GetServiceDisplayName(service)} is required by the chosen topology but is not assigned to any computer.");
            }
        }

        return errors.Count == 0
            ? Passed("deployment_model", "Docker, access, and service placement settings look complete.")
            : Failed("deployment_model", string.Join(" ", errors));
    }

    public static ValidationRecord ClassifyTopology(WizardState state)
    {
        var errors = TopologyService.ValidateTopology(state).ToList();
        if (string.IsNullOrWhiteSpace(state.HermesAiProvider.ProviderKey))
        {
            errors.Add("Exactly one backend AI provider must be selected on the Targets page.");
        }

        return errors.Count == 0
            ? Passed("topology", "Topology is valid.")
            : Failed("topology", string.Join(" ", errors));
    }

    public ValidationRecord ValidateInputConventions(WizardState state)
    {
        var warnings = new List<string>();

        WarnIfQuestionableEmail(warnings, "Main contact email", state.ClientIdentity.MainContactEmail);
        WarnIfQuestionableEmail(warnings, "AgentMail recipient email", state.AgentMail.RecipientEmail);
        WarnIfQuestionableEmail(warnings, "AgentMail sender address or inbox id", state.AgentMail.FromId, allowInboxId: true);

        WarnIfQuestionableUrl(warnings, "Hermes AI provider base URL", state.HermesAiProvider.BaseUrl);
        WarnIfQuestionableUrl(warnings, "Alpaca paper base URL", state.AlpacaPaper.BaseUrl);
        WarnIfQuestionableUrl(warnings, "Optional Alpaca live base URL", state.AlpacaLive.BaseUrl, ignoreWhenBlank: !state.AlpacaLive.Enabled);

        WarnIfQuestionableHost(warnings, "Backend target host or IP", state.Connectivity.BackendTargetHostOrIp);
        WarnIfQuestionableHost(warnings, "Desktop target host or IP", state.Connectivity.DesktopTargetHostOrIp);

        foreach (var computer in state.Computers)
        {
            if (string.IsNullOrWhiteSpace(computer.Label))
            {
                warnings.Add($"{computer.Id} does not have a recognizable computer name.");
            }

            if (computer.AccessMode is AccessMode.Ssh or AccessMode.Tailscale)
            {
                WarnIfQuestionableHost(warnings, $"{computer.Label} access host or IP", computer.AccessHostOrIp);
            }
        }

        WarnIfQuestionableTelegramChatId(warnings, state.Telegram.ChatId);

        return warnings.Count == 0
            ? Passed("input_format", "Structured input fields look reasonable.")
            : Warning("input_format", string.Join(" ", warnings));
    }

    private async Task<ValidationRecord> ValidateOpenAiCompatibleProviderAsync(
        string key,
        string baseUrl,
        string modelName,
        string apiKey,
        bool requireApiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(modelName))
        {
            return Failed(key, "The selected AI provider requires both a base URL and a model name.");
        }

        if (requireApiKey && string.IsNullOrWhiteSpace(apiKey))
        {
            return Failed(key, "The selected AI provider requires an API key.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/models");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return await SendJsonValidationAsync(key, request, cancellationToken);
    }

    private async Task<ValidationRecord> ValidateAnthropicProviderAsync(HermesAiProviderConfiguration config, HermesProviderOption provider, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? provider.DefaultBaseUrl : config.BaseUrl;
        if (string.IsNullOrWhiteSpace(config.ModelName))
        {
            return Failed("hermes_ai_provider", "Claude requires a model name.");
        }

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Failed("hermes_ai_provider", "Claude requires an API key.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/v1/models");
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        return await SendJsonValidationAsync("hermes_ai_provider", request, cancellationToken);
    }

    private static ValidationRecord ValidateRecordedProvider(HermesAiProviderConfiguration config, HermesProviderOption provider)
    {
        if (string.IsNullOrWhiteSpace(config.ModelName))
        {
            return Failed("hermes_ai_provider", $"{provider.DisplayName} requires a model name to be recorded in the export.");
        }

        return Passed("hermes_ai_provider", $"{provider.DisplayName} recorded for Hermes backend. Direct endpoint validation is not available in the wizard for this provider.");
    }

    private async Task<ValidationRecord> SendJsonValidationAsync(string key, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failed(key, $"Validation failed with status {(int)response.StatusCode}: {Truncate(content)}");
            }

            return Passed(key, $"Validation succeeded: {Summarize(content)}");
        }
        catch (Exception ex)
        {
            return Warning(key, $"Validation could not complete: {ex.Message}");
        }
    }

    private static string Summarize(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return $"JSON object with {doc.RootElement.EnumerateObject().Count()} fields.";
            }
        }
        catch
        {
        }

        return Truncate(content);
    }

    private static string Truncate(string value) => value.Length > 180 ? value[..180] : value;

    private static string NormalizeAlpacaBaseUrl(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "https://paper-api.alpaca.markets/v2";
        }

        return trimmed.EndsWith("/v2", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/v2";
    }

    private static ValidationRecord Passed(string key, string message) => new() { Key = key, Status = ValidationStatus.Passed, Message = message };
    private static ValidationRecord Warning(string key, string message) => new() { Key = key, Status = ValidationStatus.PassedWithWarning, Message = message };
    private static ValidationRecord Failed(string key, string message) => new() { Key = key, Status = ValidationStatus.FailedBlocking, Message = message };
    private static ValidationRecord Skipped(string key, string message) => new() { Key = key, Status = ValidationStatus.Skipped, Message = message };

    private static void WarnIfQuestionableEmail(List<string> warnings, string fieldName, string value, bool allowInboxId = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (allowInboxId && !value.Contains('@'))
        {
            return;
        }

        if (!Regex.IsMatch(value.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            warnings.Add($"{fieldName} does not look like a standard email address.");
        }
    }

    private static void WarnIfQuestionableUrl(List<string> warnings, string fieldName, string value, bool ignoreWhenBlank = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            warnings.Add($"{fieldName} does not look like a valid HTTP or HTTPS URL.");
        }
    }

    private static void WarnIfQuestionableHost(List<string> warnings, string fieldName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains(' '))
        {
            warnings.Add($"{fieldName} contains spaces and may not be a valid host name or IP address.");
        }
    }

    private static void WarnIfQuestionableTelegramChatId(List<string> warnings, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Regex.IsMatch(value.Trim(), @"^-?\d+$"))
        {
            warnings.Add("Telegram Chat ID does not look like a standard numeric Telegram chat id.");
        }
    }

    private static IReadOnlyList<ServiceKind> GetRequiredServices(WizardState state)
    {
        var required = new List<ServiceKind>();
        if (state.Targets.Any(target => target.Roles.Contains(RoleKind.HermesDesktop)))
        {
            required.Add(ServiceKind.HermesDesktop);
        }

        if (state.Targets.Any(target => target.Roles.Contains(RoleKind.HermesBackend)))
        {
            required.Add(ServiceKind.HermesBackend);
            required.Add(ServiceKind.TelegramIntegration);
            required.Add(ServiceKind.AgentMailIntegration);
            required.Add(ServiceKind.AlpacaIntegration);
        }

        if (state.Connectivity.RequiresTailscale)
        {
            required.Add(ServiceKind.Tailscale);
        }

        return required.Distinct().ToArray();
    }

    private static ServicePlacementMode GetPlacementMode(ComputerDefinition computer, ServiceKind service) =>
        computer.ServicePlacements.FirstOrDefault(placement => placement.Service == service)?.PlacementMode
        ?? ServicePlacementMode.NotOnThisComputer;
}
