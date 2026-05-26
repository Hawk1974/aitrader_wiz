using System.Net.Http.Headers;
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

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl.TrimEnd('/')}/v2/account");
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

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl.TrimEnd('/')}/v2/account");
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

    public async Task<ValidationRecord> ValidateLmStudioAsync(LmStudioConfiguration config, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.BaseUrl) || string.IsNullOrWhiteSpace(config.ModelId))
        {
            return Failed("lm_studio", "LM Studio base URL or model id is missing.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl.TrimEnd('/')}/v1/models");
        return await SendJsonValidationAsync("lm_studio", request, cancellationToken);
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

    public static ValidationRecord ClassifyTopology(WizardState state)
    {
        var errors = TopologyService.ValidateTopology(state);
        return errors.Count == 0
            ? Passed("topology", "Topology is valid.")
            : Failed("topology", string.Join(" ", errors));
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

    private static ValidationRecord Passed(string key, string message) => new() { Key = key, Status = ValidationStatus.Passed, Message = message };
    private static ValidationRecord Warning(string key, string message) => new() { Key = key, Status = ValidationStatus.PassedWithWarning, Message = message };
    private static ValidationRecord Failed(string key, string message) => new() { Key = key, Status = ValidationStatus.FailedBlocking, Message = message };
    private static ValidationRecord Skipped(string key, string message) => new() { Key = key, Status = ValidationStatus.Skipped, Message = message };
}

