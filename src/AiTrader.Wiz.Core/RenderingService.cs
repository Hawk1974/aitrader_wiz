using System.Text;
using System.Text.Json;

namespace AiTrader.Wiz.Core;

public static class RenderingService
{
    public static IReadOnlyList<ExportArtifact> RenderAll(WizardState state)
    {
        var artifacts = new List<ExportArtifact>
        {
            new() { FileName = "CLIENT_INTAKE.yaml", Content = RenderClientIntakeYaml(state) },
            new() { FileName = "CLIENT_SUMMARY.md", Content = RenderClientSummary(state) },
            new() { FileName = "INSTRUCTION.md", Content = RenderInstruction(state) },
            new() { FileName = "VALIDATION_SUMMARY.md", Content = RenderValidationSummary(state) },
            new() { FileName = "SECRETS_STATUS.md", Content = RenderSecretsStatus(state) },
        };

        var orderedTargets = state.Targets.OrderBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < orderedTargets.Count; index++)
        {
            var target = orderedTargets[index];
            artifacts.Add(new ExportArtifact
            {
                FileName = $"TARGET_{index + 1:00}_{ToTargetSlug(target)}.md",
                Content = RenderTargetStandup(state, target, index + 1),
            });
        }

        return artifacts;
    }

    public static string RenderClientIntakeYaml(WizardState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("altrader_client_intake:");
        AppendMap(sb, 1, "client_identity", new Dictionary<string, string>
        {
            ["client_name"] = state.ClientIdentity.ClientName,
            ["main_contact_name"] = state.ClientIdentity.MainContactName,
            ["main_contact_email"] = state.ClientIdentity.MainContactEmail,
            ["deployment_name"] = state.ClientIdentity.DeploymentName,
            ["notes"] = state.ClientIdentity.Notes,
        });

        sb.AppendLine("  computers:");
        foreach (var computer in state.Computers)
        {
            sb.AppendLine("    -");
            AppendScalar(sb, 3, "id", computer.Id);
            AppendScalar(sb, 3, "label", computer.Label);
            AppendScalar(sb, 3, "operating_system", computer.OperatingSystem.ToString());
            AppendScalar(sb, 3, "uses_wsl_backend", computer.UsesWslBackend.ToString().ToLowerInvariant());
        }

        sb.AppendLine("  runtime_targets:");
        foreach (var target in state.Targets)
        {
            sb.AppendLine("    -");
            AppendScalar(sb, 3, "id", target.Id);
            AppendScalar(sb, 3, "display_name", target.DisplayName);
            AppendScalar(sb, 3, "kind", target.Kind.ToString());
            AppendScalar(sb, 3, "computer_id", target.ComputerId);
            AppendScalar(sb, 3, "is_primary_desktop", target.IsPrimaryDesktop.ToString().ToLowerInvariant());
            AppendScalar(sb, 3, "is_authoritative_backend", target.IsAuthoritativeBackend.ToString().ToLowerInvariant());
            sb.AppendLine("      roles:");
            foreach (var role in target.Roles)
            {
                sb.AppendLine($"        - {ToYamlScalar(role.ToString())}");
            }
        }

        AppendMap(sb, 1, "connectivity", new Dictionary<string, string>
        {
            ["tailnet_name"] = state.Connectivity.TailnetName,
            ["backend_target_host_or_ip"] = state.Connectivity.BackendTargetHostOrIp,
            ["desktop_target_host_or_ip"] = state.Connectivity.DesktopTargetHostOrIp,
            ["ssh_username"] = state.Connectivity.SshUsername,
            ["ssh_port"] = state.Connectivity.SshPort.ToString(),
            ["requires_tailscale"] = state.Connectivity.RequiresTailscale.ToString().ToLowerInvariant(),
            ["bootstrap_complete"] = state.Connectivity.BootstrapComplete.ToString().ToLowerInvariant(),
        });

        AppendMap(sb, 1, "backend", new Dictionary<string, string>
        {
            ["hermes_home"] = state.Backend.HermesHome,
            ["repo_path"] = state.Backend.RepoPath,
            ["data_path"] = state.Backend.DataPath,
            ["altrader_data_path"] = state.Backend.AlTraderDataPath,
            ["models_path"] = state.Backend.ModelsPath,
            ["logs_path"] = state.Backend.LogsPath,
            ["backups_path"] = state.Backend.BackupsPath,
            ["timezone"] = state.Backend.Timezone,
        });

        AppendMap(sb, 1, "lm_studio", new Dictionary<string, string>
        {
            ["base_url"] = state.LmStudio.BaseUrl,
            ["model_id"] = state.LmStudio.ModelId,
            ["colocated_with_backend"] = state.LmStudio.ColocatedWithBackend.ToString().ToLowerInvariant(),
        });

        AppendMap(sb, 1, "alpaca_paper", new Dictionary<string, string>
        {
            ["account_name"] = state.AlpacaPaper.AccountName,
            ["api_key"] = state.AlpacaPaper.ApiKey,
            ["secret_key"] = state.AlpacaPaper.SecretKey,
            ["base_url"] = state.AlpacaPaper.BaseUrl,
        });

        AppendMap(sb, 1, "alpaca_live", new Dictionary<string, string>
        {
            ["enabled"] = state.AlpacaLive.Enabled.ToString().ToLowerInvariant(),
            ["api_key"] = state.AlpacaLive.ApiKey,
            ["secret_key"] = state.AlpacaLive.SecretKey,
            ["base_url"] = state.AlpacaLive.BaseUrl,
        });

        AppendMap(sb, 1, "telegram", new Dictionary<string, string>
        {
            ["enabled"] = state.Telegram.Enabled.ToString().ToLowerInvariant(),
            ["bot_token"] = state.Telegram.BotToken,
            ["chat_id"] = state.Telegram.ChatId,
        });

        AppendMap(sb, 1, "agentmail", new Dictionary<string, string>
        {
            ["enabled"] = state.AgentMail.Enabled.ToString().ToLowerInvariant(),
            ["api_key"] = state.AgentMail.ApiKey,
            ["from_id"] = state.AgentMail.FromId,
            ["recipient_email"] = state.AgentMail.RecipientEmail,
        });

        return sb.ToString();
    }

    public static string RenderClientSummary(WizardState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Client Summary");
        sb.AppendLine();
        sb.AppendLine($"- Client: {state.ClientIdentity.ClientName}");
        sb.AppendLine($"- Deployment: {state.ClientIdentity.DeploymentName}");
        sb.AppendLine($"- Main contact: {state.ClientIdentity.MainContactName} <{state.ClientIdentity.MainContactEmail}>");
        sb.AppendLine($"- Computers: {state.Computers.Count}");
        sb.AppendLine($"- Runtime targets: {state.Targets.Count}");
        sb.AppendLine();
        sb.AppendLine("## Runtime Targets");
        foreach (var target in state.Targets)
        {
            sb.AppendLine($"- {target.DisplayName}: {string.Join(", ", target.Roles)}");
        }
        return sb.ToString();
    }

    public static string RenderInstruction(WizardState state)
    {
        var backendTarget = state.Targets.SingleOrDefault(t => t.IsAuthoritativeBackend);
        var desktopTarget = state.Targets.SingleOrDefault(t => t.IsPrimaryDesktop);
        var sb = new StringBuilder();
        sb.AppendLine("# Instruction");
        sb.AppendLine();
        sb.AppendLine("This folder is the client-specific overlay for the static AlTrader repo zip.");
        sb.AppendLine();
        sb.AppendLine("## Scope");
        sb.AppendLine("- Use these files together with the static AlTrader repo zip.");
        sb.AppendLine("- Do not rewrite or replace the static repo bundle.");
        sb.AppendLine("- Stand up AlTrader in paper mode only.");
        sb.AppendLine("- Do not enable live trading during standup.");
        sb.AppendLine("- Live trading remains disabled until Overlord handles `GO LIVE`.");
        sb.AppendLine();
        sb.AppendLine("## File Precedence");
        sb.AppendLine("1. `INSTRUCTION.md`");
        sb.AppendLine("2. `CLIENT_INTAKE.yaml`");
        sb.AppendLine("3. `VALIDATION_SUMMARY.md`");
        sb.AppendLine("4. `TARGET_*.md`");
        sb.AppendLine("5. Static docs from the AlTrader repo zip");
        sb.AppendLine("6. All remaining repo files");
        sb.AppendLine();
        sb.AppendLine("## Bring-Up Order");
        if (backendTarget is not null)
        {
            sb.AppendLine($"1. Stand up the backend target: `{backendTarget.DisplayName}`.");
        }
        if (desktopTarget is not null)
        {
            sb.AppendLine($"2. Stand up the desktop target: `{desktopTarget.DisplayName}`.");
        }
        if (state.Connectivity.RequiresTailscale)
        {
            sb.AppendLine("3. If targets are on separate machines, complete Tailscale bootstrap before desktop-to-backend connection work.");
        }
        sb.AppendLine();
        sb.AppendLine("## Secrets");
        sb.AppendLine("- Alpaca, Telegram, and AgentMail secrets belong on the authoritative backend target only.");
        sb.AppendLine("- The desktop target must not become the long-term holder of backend secrets.");
        return sb.ToString();
    }

    public static string RenderValidationSummary(WizardState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Validation Summary");
        sb.AppendLine();
        foreach (var record in state.ValidationResults)
        {
            sb.AppendLine($"- `{record.Key}`: `{record.Status}` - {record.Message}");
        }
        return sb.ToString();
    }

    public static string RenderSecretsStatus(WizardState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Secrets Status");
        sb.AppendLine();
        AppendSecretLine(sb, "ALPACA_PAPER_API_KEY", state.AlpacaPaper.ApiKey);
        AppendSecretLine(sb, "ALPACA_PAPER_SECRET_KEY", state.AlpacaPaper.SecretKey);
        AppendSecretLine(sb, "ALPACA_LIVE_API_KEY", state.AlpacaLive.ApiKey);
        AppendSecretLine(sb, "ALPACA_LIVE_SECRET_KEY", state.AlpacaLive.SecretKey);
        AppendSecretLine(sb, "TELEGRAM_BOT_TOKEN", state.Telegram.BotToken);
        AppendSecretLine(sb, "AGENTMAIL_API_KEY", state.AgentMail.ApiKey);
        return sb.ToString();
    }

    public static string RenderTargetStandup(WizardState state, RuntimeTarget target, int index)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Target {index:00}: {target.DisplayName}");
        sb.AppendLine();
        sb.AppendLine($"Step One: Confirm Target Identity");
        sb.AppendLine($"Description: Confirm that this runtime target is `{target.DisplayName}` on the expected host.");
        sb.AppendLine($"Instructions: Read `CLIENT_INTAKE.yaml`, confirm the target kind is `{target.Kind}`, confirm the host computer id is `{target.ComputerId}`, and verify this target will host: {string.Join(", ", target.Roles)}.");
        sb.AppendLine("Issues Prompt to AI: The runtime target does not match the client intake. Stop, report the mismatch, and do not apply the wrong target instructions to the wrong host.");
        sb.AppendLine();
        sb.AppendLine("Step Two: Apply Role Scope");
        sb.AppendLine($"Description: Configure only the roles assigned to this target: {string.Join(", ", target.Roles)}.");
        sb.AppendLine($"Instructions: Follow only the setup actions relevant to `{target.DisplayName}` and ignore unrelated roles.");
        sb.AppendLine("Issues Prompt to AI: The target role scope is unclear. Re-read `INSTRUCTION.md`, `CLIENT_INTAKE.yaml`, and this target file, then restate the exact allowed responsibilities for this target before proceeding.");
        sb.AppendLine();
        sb.AppendLine("Step Three: Respect Secret Placement");
        sb.AppendLine("Description: Keep backend secrets only on the authoritative backend target.");
        sb.AppendLine("Instructions: If this target is not the authoritative backend, do not make it the long-term holder of Alpaca, Telegram, or AgentMail secrets.");
        sb.AppendLine("Issues Prompt to AI: A backend secret is about to be stored on a non-backend target. Stop, report the secret placement issue, and move the plan back to the authoritative backend target.");
        return sb.ToString();
    }

    private static void AppendSecretLine(StringBuilder sb, string key, string value)
    {
        sb.AppendLine($"- `{key}`: {(string.IsNullOrWhiteSpace(value) ? "missing" : "provided")}");
    }

    private static string ToTargetSlug(RuntimeTarget target)
    {
        var kind = target.Kind.ToString().ToUpperInvariant();
        var primaryPart = target.IsPrimaryDesktop ? "_PRIMARY" : string.Empty;
        var backendPart = target.IsAuthoritativeBackend ? "_AUTHORITATIVE" : string.Empty;
        return $"{kind}{backendPart}{primaryPart}";
    }

    private static void AppendMap(StringBuilder sb, int indentLevel, string key, Dictionary<string, string> values)
    {
        sb.AppendLine($"{new string(' ', indentLevel * 2)}{key}:");
        foreach (var pair in values)
        {
            AppendScalar(sb, indentLevel + 1, pair.Key, pair.Value);
        }
    }

    private static void AppendScalar(StringBuilder sb, int indentLevel, string key, string value)
    {
        sb.AppendLine($"{new string(' ', indentLevel * 2)}{key}: {ToYamlScalar(value)}");
    }

    private static string ToYamlScalar(string value)
    {
        return JsonSerializer.Serialize(value ?? string.Empty);
    }
}

