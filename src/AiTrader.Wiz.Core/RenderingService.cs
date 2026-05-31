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
            new() { FileName = "SETUP_AI_EXECUTION_RULES.md", Content = RenderSetupAiExecutionRules(state) },
            new() { FileName = "PACKAGE_MANIFEST.md", Content = RenderPackageManifest(state) },
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
            AppendScalar(sb, 3, "docker_available", computer.DockerAvailable.ToString().ToLowerInvariant());
            AppendScalar(sb, 3, "access_mode", computer.AccessMode.ToString());
            AppendScalar(sb, 3, "access_host_or_ip", computer.AccessHostOrIp);
            AppendScalar(sb, 3, "access_port", computer.AccessPort.ToString());
            AppendScalar(sb, 3, "access_username", computer.AccessUsername);
            sb.AppendLine("      service_placements:");
            foreach (var placement in computer.ServicePlacements.OrderBy(item => item.Service))
            {
                sb.AppendLine("        -");
                AppendScalar(sb, 5, "service", placement.Service.ToString());
                AppendScalar(sb, 5, "display_name", DeploymentCatalog.GetServiceDisplayName(placement.Service));
                AppendScalar(sb, 5, "placement_mode", placement.PlacementMode.ToString());
            }
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

        AppendMap(sb, 1, "hermes_ai_provider", new Dictionary<string, string>
        {
            ["provider_mode"] = state.HermesAiProvider.ProviderKey,
            ["base_url"] = state.HermesAiProvider.BaseUrl,
            ["api_key_if_required"] = state.HermesAiProvider.ApiKey,
            ["model_name"] = state.HermesAiProvider.ModelName,
        });

        AppendMap(sb, 1, "lm_studio_backend", new Dictionary<string, string>
        {
            ["used"] = string.Equals(state.HermesAiProvider.ProviderKey, "lmstudio", StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant(),
            ["lm_studio_base_url"] = string.Equals(state.HermesAiProvider.ProviderKey, "lmstudio", StringComparison.OrdinalIgnoreCase) ? state.HermesAiProvider.BaseUrl : string.Empty,
            ["lm_studio_port"] = string.Equals(state.HermesAiProvider.ProviderKey, "lmstudio", StringComparison.OrdinalIgnoreCase) ? ExtractPort(state.HermesAiProvider.BaseUrl) : string.Empty,
            ["model_id"] = string.Equals(state.HermesAiProvider.ProviderKey, "lmstudio", StringComparison.OrdinalIgnoreCase) ? state.HermesAiProvider.ModelName : string.Empty,
            ["api_type"] = string.Equals(state.HermesAiProvider.ProviderKey, "lmstudio", StringComparison.OrdinalIgnoreCase) ? "openai-compatible" : string.Empty,
            ["api_key_if_used"] = string.Equals(state.HermesAiProvider.ProviderKey, "lmstudio", StringComparison.OrdinalIgnoreCase) ? state.HermesAiProvider.ApiKey : string.Empty,
        });

        AppendMap(sb, 1, "alpaca_paper", new Dictionary<string, string>
        {
            ["account_name"] = state.AlpacaPaper.AccountName,
            ["api_key"] = state.AlpacaPaper.ApiKey,
            ["secret_key"] = state.AlpacaPaper.SecretKey,
            ["base_url"] = NormalizeAlpacaBaseUrl(state.AlpacaPaper.BaseUrl),
        });

        AppendMap(sb, 1, "alpaca_live", new Dictionary<string, string>
        {
            ["enabled"] = state.AlpacaLive.Enabled.ToString().ToLowerInvariant(),
            ["api_key"] = state.AlpacaLive.ApiKey,
            ["secret_key"] = state.AlpacaLive.SecretKey,
            ["base_url"] = NormalizeAlpacaBaseUrl(state.AlpacaLive.BaseUrl),
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

        AppendMap(sb, 1, "cash_allocation_policy", new Dictionary<string, string>
        {
            ["start_of_day_cash_basis"] = state.CashAllocation.StartOfDayCashBasis.ToString("0.##"),
            ["protected_reserve_input_mode"] = state.CashAllocation.ProtectedReserve.Mode.ToString(),
            ["protected_reserve_input_value"] = state.CashAllocation.ProtectedReserve.Value.ToString("0.##"),
            ["protected_reserve_percent_effective"] = CashAllocationCalculator.CalculatePercentValue(state.CashAllocation.ProtectedReserve, state.CashAllocation.StartOfDayCashBasis).ToString("0.####"),
            ["per_ticker_allocation_input_mode"] = state.CashAllocation.PerTickerAllocation.Mode.ToString(),
            ["per_ticker_allocation_input_value"] = state.CashAllocation.PerTickerAllocation.Value.ToString("0.##"),
            ["per_ticker_allocation_percent_effective"] = CashAllocationCalculator.CalculatePercentValue(state.CashAllocation.PerTickerAllocation, state.CashAllocation.StartOfDayCashBasis).ToString("0.####"),
            ["max_ranked_candidates_per_cycle"] = state.CashAllocation.MaxRankedCandidatesPerCycle.ToString(),
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
        sb.AppendLine("## Manual Commands");
        sb.AppendLine($"- High Marshal kickoff: `{WizardStaticContent.HighMarshalFullKickoff}`");
        sb.AppendLine($"- High Marshal shortcut: `{WizardStaticContent.HighMarshalShortKickoff}`");
        sb.AppendLine($"- Overlord live staging: `{WizardStaticContent.OverlordGoLive}`");
        sb.AppendLine();
        sb.AppendLine("## Package Model");
        sb.AppendLine("- This export is the full non-secret stand-up package for the setup AI.");
        sb.AppendLine("- No second static AlTrader repo zip is required.");
        sb.AppendLine("- The setup AI must follow `SETUP_AI_EXECUTION_RULES.md` while it installs, configures, validates, and logs the deployment.");
        sb.AppendLine();
        sb.AppendLine("## Cash Allocation Policy");
        sb.AppendLine($"- Start-of-day cash basis: ${state.CashAllocation.StartOfDayCashBasis:0,0.00}");
        sb.AppendLine($"- Protected reserve input: {FormatAllocationInput(state.CashAllocation.ProtectedReserve)} ({CashAllocationCalculator.CalculatePercentValue(state.CashAllocation.ProtectedReserve, state.CashAllocation.StartOfDayCashBasis):0.####}% effective)");
        sb.AppendLine($"- Per-ticker allocation input: {FormatAllocationInput(state.CashAllocation.PerTickerAllocation)} ({CashAllocationCalculator.CalculatePercentValue(state.CashAllocation.PerTickerAllocation, state.CashAllocation.StartOfDayCashBasis):0.####}% effective)");
        sb.AppendLine($"- Max ranked candidates per cycle: {state.CashAllocation.MaxRankedCandidatesPerCycle}");
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
        sb.AppendLine("This folder is the full non-secret AlTrader stand-up package for the setup AI.");
        sb.AppendLine();
        sb.AppendLine("## Scope");
        sb.AppendLine("- Use this package as the primary non-secret source of truth for stand-up.");
        sb.AppendLine("- Do not require a second static AlTrader repo zip.");
        sb.AppendLine("- Treat the generated files in this package as deployment-specific instructions that override generic template examples in the bundled payload.");
        sb.AppendLine("- Stand up AlTrader in paper mode only.");
        sb.AppendLine("- Do not enable live trading during standup.");
        sb.AppendLine("- Live trading remains disabled until Overlord handles `GO LIVE`.");
        sb.AppendLine();
        sb.AppendLine("## File Precedence");
        sb.AppendLine("1. `INSTRUCTION.md`");
        sb.AppendLine("2. `SETUP_AI_EXECUTION_RULES.md`");
        sb.AppendLine("3. `CLIENT_INTAKE.yaml`");
        sb.AppendLine("4. `VALIDATION_SUMMARY.md`");
        sb.AppendLine("5. `TARGET_*.md`");
        sb.AppendLine("6. `PACKAGE_MANIFEST.md`");
        sb.AppendLine("7. All remaining bundled payload files");
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
        sb.AppendLine("## Access and Placement Rules");
        sb.AppendLine("- Use the per-computer `access_mode` and `access_*` values from `CLIENT_INTAKE.yaml` before guessing how to reach a machine.");
        sb.AppendLine("- Do not assume Docker unless the computer and service placement explicitly say Docker is used there.");
        sb.AppendLine("- Do not assume host-native placement unless the service placement explicitly says `host_native`.");
        sb.AppendLine("- If a machine is isolated and the wizard output does not contain the required SSH or Tailscale access details, stop and ask the user for the missing access information.");
        sb.AppendLine("- Do not guess credentials, routes, or container placement.");
        sb.AppendLine("- Follow `SETUP_AI_EXECUTION_RULES.md` for step logging, retries, and escalation behavior.");
        sb.AppendLine();
        sb.AppendLine("## Secrets");
        sb.AppendLine("- Alpaca, Telegram, and AgentMail secrets belong on the authoritative backend target only.");
        sb.AppendLine("- The desktop target must not become the long-term holder of backend secrets.");
        sb.AppendLine($"- Hermes AI provider selected for this deployment: `{HermesProviderCatalog.Find(state.HermesAiProvider.ProviderKey).DisplayName}`.");
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
        AppendSecretLine(sb, "HERMES_AI_PROVIDER_API_KEY", state.HermesAiProvider.ApiKey);
        return sb.ToString();
    }

    public static string RenderSetupAiExecutionRules(WizardState state)
    {
        var backendTarget = state.Targets.SingleOrDefault(target => target.IsAuthoritativeBackend);
        var desktopTarget = state.Targets.SingleOrDefault(target => target.IsPrimaryDesktop);
        var sb = new StringBuilder();
        sb.AppendLine("# Setup AI Execution Rules");
        sb.AppendLine();
        sb.AppendLine("This file defines required behavior for the setup AI that consumes this stand-up package.");
        sb.AppendLine();
        sb.AppendLine("## Hard Rules");
        sb.AppendLine("- Log every meaningful action before and after it runs.");
        sb.AppendLine("- Log the target machine or runtime target for each action.");
        sb.AppendLine("- Log the files rendered, copied, edited, or configured.");
        sb.AppendLine("- Log every validation check, its pass/fail result, and any retry that follows.");
        sb.AppendLine("- Log every blocker, exact failure message, and every clarification request made to the user.");
        sb.AppendLine("- Never guess secrets, target identity, access paths, or container placement.");
        sb.AppendLine();
        sb.AppendLine("## Resolution Rule");
        sb.AppendLine("- Known and solvable from this package: resolve automatically and log the action.");
        sb.AppendLine("- Missing required data or unresolved ambiguity: stop, log the blocker, and ask the user.");
        sb.AppendLine("- Unsafe or contradictory state: stop, log the contradiction, and ask the user unless this package explicitly defines the correction.");
        sb.AppendLine();
        sb.AppendLine("## Log Output Expectations");
        sb.AppendLine("- Use a durable execution log directory named `standup_logs` at the package working directory or an equivalent durable workspace chosen by the setup AI.");
        sb.AppendLine("- Create per-phase log entries for backend, desktop, networking, automation, integrations, and validation.");
        sb.AppendLine("- Produce a final summary artifact named `STANDUP_EXECUTION_SUMMARY.md` that lists completed steps, failed steps, skipped steps, user questions asked, and remaining blockers.");
        sb.AppendLine();
        sb.AppendLine("## Required Logged Fields");
        sb.AppendLine("- step name");
        sb.AppendLine("- timestamp");
        sb.AppendLine("- target machine or runtime target");
        sb.AppendLine("- command or action");
        sb.AppendLine("- rendered inputs used");
        sb.AppendLine("- result");
        sb.AppendLine("- retries and reason");
        sb.AppendLine("- blocker details");
        sb.AppendLine();
        sb.AppendLine("## Deployment Context");
        if (backendTarget is not null)
        {
            sb.AppendLine($"- Authoritative backend target: `{backendTarget.DisplayName}`");
        }
        if (desktopTarget is not null)
        {
            sb.AppendLine($"- Primary desktop target: `{desktopTarget.DisplayName}`");
        }
        sb.AppendLine($"- Hermes AI provider: `{HermesProviderCatalog.Find(state.HermesAiProvider.ProviderKey).DisplayName}`");
        sb.AppendLine("- Paper trading only during stand-up.");
        sb.AppendLine("- Live trading remains disabled until a later explicit `GO LIVE` workflow.");
        return sb.ToString();
    }

    public static string RenderPackageManifest(WizardState state)
    {
        var sb = new StringBuilder();
        var generatedFiles = new List<string>
        {
            "CLIENT_INTAKE.yaml",
            "CLIENT_SUMMARY.md",
            "INSTRUCTION.md",
            "SETUP_AI_EXECUTION_RULES.md",
            "PACKAGE_MANIFEST.md",
            "VALIDATION_SUMMARY.md",
            "SECRETS_STATUS.md",
        };

        var orderedTargets = state.Targets.OrderBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < orderedTargets.Count; index++)
        {
            generatedFiles.Add($"TARGET_{index + 1:00}_{ToTargetSlug(orderedTargets[index])}.md");
        }

        sb.AppendLine("# Package Manifest");
        sb.AppendLine();
        sb.AppendLine("## Generated Files");
        foreach (var artifact in generatedFiles.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- `{artifact}`");
        }
        sb.AppendLine();
        sb.AppendLine("## Bundled Payload Directories");
        sb.AppendLine("- `Backend/repo_payload`");
        sb.AppendLine("- `Backend/runtime_templates`");
        sb.AppendLine("- `Desktop/runtime_templates`");
        sb.AppendLine("- `Desktop/connection_docs`");
        sb.AppendLine();
        sb.AppendLine("## Setup AI Expectations");
        sb.AppendLine("- Use this package as the complete non-secret handoff.");
        sb.AppendLine("- Read `INSTRUCTION.md` first.");
        sb.AppendLine("- Follow `SETUP_AI_EXECUTION_RULES.md` throughout the stand-up.");
        sb.AppendLine("- Use `TARGET_*.md` files for target-specific sequencing.");
        return sb.ToString();
    }

    public static string RenderTargetStandup(WizardState state, RuntimeTarget target, int index)
    {
        var computer = state.Computers.FirstOrDefault(item => item.Id == target.ComputerId);
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
        sb.AppendLine("Issues Prompt to AI: The target role scope is unclear. Re-read `INSTRUCTION.md`, `SETUP_AI_EXECUTION_RULES.md`, `CLIENT_INTAKE.yaml`, and this target file, then restate the exact allowed responsibilities for this target before proceeding.");
        sb.AppendLine();
        sb.AppendLine("Step Three: Follow Access Rules");
        if (computer is not null)
        {
            sb.AppendLine($"Description: Reach `{computer.Label}` using the configured access model.");
            sb.AppendLine($"Instructions: Preferred access mode is `{computer.AccessMode}`. Host/IP: `{computer.AccessHostOrIp}`. SSH user: `{computer.AccessUsername}`. SSH port: `{computer.AccessPort}`.");
            sb.AppendLine("Issues Prompt to AI: If the configured access mode requires SSH or Tailscale details and they are missing or unusable, stop and ask the user for the missing access path instead of guessing.");
            sb.AppendLine();
            sb.AppendLine("Step Four: Follow Service Placement");
            sb.AppendLine("Description: Only stand up the services explicitly assigned to this computer.");
            sb.AppendLine("Instructions:");
            foreach (var placement in computer.ServicePlacements.Where(item => item.PlacementMode != ServicePlacementMode.NotOnThisComputer))
            {
                sb.AppendLine($"- `{DeploymentCatalog.GetServiceDisplayName(placement.Service)}` => `{placement.PlacementMode}`");
            }
            if (computer.ServicePlacements.All(item => item.PlacementMode == ServicePlacementMode.NotOnThisComputer))
            {
                sb.AppendLine("- No AlTrader-related services are assigned to this computer.");
            }
            sb.AppendLine("Issues Prompt to AI: If Docker is not explicitly assigned for a service, do not containerize it. If host-native is not explicitly assigned, do not assume host-native placement.");
            sb.AppendLine();
        }
        sb.AppendLine("Step Five: Respect Secret Placement");
        sb.AppendLine("Description: Keep backend secrets only on the authoritative backend target.");
        sb.AppendLine("Instructions: If this target is not the authoritative backend, do not make it the long-term holder of Alpaca, Telegram, or AgentMail secrets.");
        sb.AppendLine("Issues Prompt to AI: A backend secret is about to be stored on a non-backend target. Stop, report the secret placement issue, and move the plan back to the authoritative backend target.");
        sb.AppendLine();
        sb.AppendLine("Step Six: Log Every Phase");
        sb.AppendLine("Description: Maintain a durable execution trace for this target.");
        sb.AppendLine("Instructions: Before and after each action on this target, update the execution log described in `SETUP_AI_EXECUTION_RULES.md` with the step name, target, files touched, command or action, and result.");
        sb.AppendLine("Issues Prompt to AI: If logging is not active or the log location is unclear, stop and correct the logging path before continuing.");
        return sb.ToString();
    }

    private static void AppendSecretLine(StringBuilder sb, string key, string value)
    {
        sb.AppendLine($"- `{key}`: {(string.IsNullOrWhiteSpace(value) ? "missing" : "provided")}");
    }

    private static string FormatAllocationInput(AllocationInput input)
    {
        return input.Mode == AllocationEntryMode.Percent
            ? $"{input.Value:0.##}%"
            : $"${input.Value:0,0.00}";
    }

    private static string ToTargetSlug(RuntimeTarget target)
    {
        var kind = target.Kind.ToString().ToUpperInvariant();
        var primaryPart = target.IsPrimaryDesktop ? "_PRIMARY" : string.Empty;
        var backendPart = target.IsAuthoritativeBackend ? "_AUTHORITATIVE" : string.Empty;
        return $"{kind}{backendPart}{primaryPart}";
    }

    private static string NormalizeAlpacaBaseUrl(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return trimmed.EndsWith("/v2", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/v2";
    }

    private static string ExtractPort(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && !uri.IsDefaultPort)
        {
            return uri.Port.ToString();
        }

        return string.Empty;
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
