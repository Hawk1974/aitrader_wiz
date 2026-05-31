namespace AiTrader.Wiz.Core;

public sealed record HermesProviderOption(
    string Key,
    string DisplayName,
    string BaseUrlLabel,
    string ModelLabel,
    string ApiKeyLabel,
    string DefaultBaseUrl,
    bool RequiresApiKey,
    string ValidationMode,
    string HelperText);

public static class HermesProviderCatalog
{
    public static IReadOnlyList<HermesProviderOption> All { get; } =
    [
        new("openai", "OpenAI API", "Base URL", "Model Name", "API Key", "https://api.openai.com/v1", true, "openai_compatible", "Use OpenAI's API endpoint and the exact model name the Hermes backend should use."),
        new("anthropic", "Anthropic Claude", "Base URL", "Model Name", "API Key", "https://api.anthropic.com", true, "anthropic", "Use Anthropic's API endpoint and the Claude model name for the Hermes backend."),
        new("xai", "xAI API", "Base URL", "Model Name", "API Key", "https://api.x.ai/v1", true, "openai_compatible", "Use xAI's direct API endpoint and Grok model name."),
        new("xai-oauth", "xAI Grok OAuth", "Base URL", "Model Name", "OAuth Token / API Key", "https://api.x.ai/v1", true, "openai_compatible", "Use the xAI OAuth-backed endpoint if that is how Hermes is authenticated."),
        new("gemini", "Google AI Studio", "Base URL", "Model Name", "API Key", "https://generativelanguage.googleapis.com/v1beta/openai", true, "openai_compatible", "Use Google's OpenAI-compatible Gemini endpoint and model name."),
        new("google-gemini-cli", "Google Gemini (OAuth)", "Base URL", "Model Name", "OAuth Token / API Key", "https://generativelanguage.googleapis.com/v1beta/openai", true, "openai_compatible", "Use the Gemini OAuth-backed endpoint if Hermes is configured that way."),
        new("openrouter", "OpenRouter", "Base URL", "Model Name", "API Key", "https://openrouter.ai/api/v1", true, "openai_compatible", "Use OpenRouter when Hermes should route through the OpenRouter provider layer."),
        new("nous", "Nous Portal", "Base URL", "Model Name", "Portal Token / API Key", string.Empty, false, "no_http_validation", "Hermes can attach to Nous Portal without a separate direct base URL in this wizard."),
        new("nous-api", "Nous API", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Use only if your Hermes backend is configured for a direct Nous API flow."),
        new("copilot", "GitHub Copilot", "Base URL", "Model Name", "GitHub Token", string.Empty, false, "no_http_validation", "Hermes handles the Copilot transport directly; this wizard records the chosen provider."),
        new("copilot-acp", "GitHub Copilot ACP", "Base URL", "Model Name", "GitHub Token", string.Empty, false, "no_http_validation", "Hermes handles the Copilot ACP transport directly; this wizard records the chosen provider."),
        new("lmstudio", "LM Studio", "Base URL", "Model ID", "API Key (optional)", "http://127.0.0.1:1234/v1", false, "openai_compatible", "Use LM Studio only when the backend machine hosts the local model server."),
        new("custom", "Custom OpenAI-Compatible", "Base URL", "Model Name", "API Key (optional)", string.Empty, false, "openai_compatible", "Use this for any other OpenAI-compatible endpoint not listed above."),
        new("deepseek", "DeepSeek", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports DeepSeek, but this wizard only records the selection unless you provide a known direct endpoint."),
        new("qwen-oauth", "Qwen OAuth", "Base URL", "Model Name", "OAuth Token / API Key", string.Empty, false, "no_http_validation", "Hermes supports Qwen OAuth directly; this wizard records the provider choice."),
        new("zai", "Z.AI / GLM", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Z.AI / GLM; this wizard records the provider choice unless you have a direct endpoint."),
        new("kimi-coding", "Kimi / Kimi Coding Plan", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Kimi Coding; this wizard records the provider choice unless you have a direct endpoint."),
        new("kimi-coding-cn", "Kimi / Moonshot (China)", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Moonshot China; this wizard records the provider choice unless you have a direct endpoint."),
        new("minimax", "MiniMax", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports MiniMax; this wizard records the provider choice unless you have a direct endpoint."),
        new("minimax-oauth", "MiniMax (OAuth)", "Base URL", "Model Name", "OAuth Token / API Key", string.Empty, false, "no_http_validation", "Hermes supports MiniMax OAuth; this wizard records the provider choice."),
        new("minimax-cn", "MiniMax (China)", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports MiniMax China; this wizard records the provider choice."),
        new("nvidia", "NVIDIA NIM", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports NVIDIA NIM; this wizard records the provider choice unless you have a direct endpoint."),
        new("huggingface", "Hugging Face", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Hugging Face; this wizard records the provider choice unless you have a direct endpoint."),
        new("ollama-cloud", "Ollama Cloud", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Ollama Cloud; this wizard records the provider choice unless you have a direct endpoint."),
        new("azure-foundry", "Azure Foundry", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Azure Foundry; this wizard records the provider choice unless you have a direct endpoint."),
        new("bedrock", "AWS Bedrock", "Base URL", "Model Name", "AWS Credentials / Token", string.Empty, false, "no_http_validation", "Hermes supports AWS Bedrock through its own provider flow; this wizard records the provider choice."),
        new("novitaai", "NovitaAI", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports NovitaAI in its provider layer; this wizard records the provider choice."),
        new("xiaomi", "Xiaomi MiMo", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Xiaomi MiMo; this wizard records the provider choice."),
        new("arcee", "Arcee AI", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Arcee AI; this wizard records the provider choice."),
        new("gmi", "GMI Cloud", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports GMI Cloud; this wizard records the provider choice."),
        new("stepfun", "StepFun Step Plan", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports StepFun; this wizard records the provider choice."),
        new("kilocode", "Kilo Code", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Kilo Code; this wizard records the provider choice."),
        new("opencode-zen", "OpenCode Zen", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports OpenCode Zen; this wizard records the provider choice."),
        new("opencode-go", "OpenCode Go", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports OpenCode Go; this wizard records the provider choice."),
        new("alibaba", "Alibaba", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Alibaba; this wizard records the provider choice."),
        new("alibaba-coding-plan", "Alibaba Coding Plan", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Alibaba Coding Plan; this wizard records the provider choice."),
        new("tencent-tokenhub", "Tencent TokenHub", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Hermes supports Tencent TokenHub; this wizard records the provider choice.")
    ];

    public static HermesProviderOption Find(string key) =>
        All.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? new HermesProviderOption(string.Empty, "Select a provider", "Base URL", "Model Name", "API Key", string.Empty, false, "no_http_validation", "Select the Hermes backend provider from the dropdown.");
}
