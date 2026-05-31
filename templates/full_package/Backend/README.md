# Spark Backend Standup

Step One: Confirm the Spark machine role
Description: Verify this machine is the NVIDIA DGX Spark backend host for Hermes Agent and AlTrader.
Instructions: Give the local AI this prompt: "Confirm this machine is the DGX Spark backend host. Report the current hostname, architecture, Ubuntu base details, Tailscale hostname, and whether Hermes Agent is already installed."
Issues Prompt to AI: "The machine role or architecture is unclear. Determine whether this is the DGX Spark backend, confirm it is ARM64 or aarch64 Linux, and stop before installing x86_64 desktop artifacts."

Step Two: Prepare canonical directories
Description: Create and validate the target directory layout used by Hermes Agent and AlTrader on the Spark.
Instructions: Give the local AI this prompt: "Create and validate these directories if they do not already exist: `/opt/hermes/`, `/opt/hermes/altrader/`, `/srv/hermes/`, `/srv/hermes/altrader/`, `/srv/models/`, `/var/log/hermes/`, and `/srv/backups/`. Report ownership and permissions for each."
Issues Prompt to AI: "The canonical directories could not be created or permissions are wrong. Repair ownership and permissions so the Hermes service user can read and write the required paths without weakening the system unnecessarily."

Step Three: Install or update Hermes Agent
Description: Ensure the Spark backend has a current Hermes Agent install compatible with the AlTrader runtime.
Instructions: Give the local AI this prompt: "Install or update Hermes Agent on the Spark under `/opt/hermes/` using the current supported Linux ARM64-compatible method. Preserve the existing install if it already exists, and report the final executable path and version."
Issues Prompt to AI: "Hermes Agent installation or update failed on the Spark. Diagnose package manager, Python environment, architecture, and path issues, then complete a working ARM64-compatible Hermes Agent install."

Step Four: Copy the AlTrader backend payload
Description: Install the backend scripts, configs, Hermes docs, and runtime source files from this bundle into the Spark target paths.
Instructions: Give the local AI this prompt: "Copy the contents of `repo_payload` from this bundle into `/opt/hermes/altrader/`. Preserve the folder structure exactly. Ensure the `scripts`, `config`, `hermes`, and root wrapper files land in the same relative paths on the Spark."
Issues Prompt to AI: "The backend payload copy failed or the target paths are inconsistent. Recreate the target directory structure under `/opt/hermes/altrader/`, copy the bundle contents again, and verify the expected files exist at the documented relative paths."

Step Five: Apply the backend Hermes runtime templates
Description: Install the Alvin root runtime files, agent profiles, and Hermes helper scripts on the Spark so the backend behavior aligns with AlTrader.
Instructions: Give the local AI this prompt: "Copy the contents of `runtime_templates\\root_hermes` into the Spark Hermes home, copy `runtime_templates\\profiles` into the Spark Hermes profiles directory, and copy `runtime_templates\\hermes_scripts` into the Spark Hermes scripts directory. Preserve backups of any existing files first."
Issues Prompt to AI: "The backend runtime template import failed or existing files conflict. Compare the bundle runtime templates with the live Spark Hermes home, preserve backups, and apply the canonical AlTrader-specific files without deleting unrelated system files."

Step Six: Configure Telegram, AgentMail, Alpaca, and provider settings
Description: Apply the backend-only secrets and provider values required for notifications, paper trading, and AI runtime behavior.
Instructions: Give the local AI this prompt: "Open `CONFIGURATION.md` from this bundle and follow it exactly. Collect the Telegram, AgentMail, Alpaca, Tailscale, and provider values from the operator. Store them only on the Spark in the documented backend environment files. Do not place these secrets on the Windows Desktop workstation."
Issues Prompt to AI: "A required Telegram, AgentMail, Alpaca, or provider value is missing or invalid. Identify the exact missing field, stop secret-dependent configuration changes, and request only the specific missing input from the operator."

Step Seven: Register the backend Hermes cron jobs
Description: Install the AlTrader automation jobs so High Marshal, Archivist, and Steward run on the Spark backend.
Instructions: Give the local AI this prompt: "Use the scripts in `runtime_templates\\hermes_scripts` and the commands documented in `AUTOMATION.md` to register the backend Hermes cron jobs for High Marshal, Archivist, and Steward. Confirm the jobs exist and report their schedules."
Issues Prompt to AI: "Cron registration failed or the jobs do not appear in Hermes. Check the Hermes home path, script installation path, workdir, and cron command syntax, then re-register the jobs and report the exact resulting job list."

Step Eight: Enable Tailscale-backed remote access
Description: Make the Spark reachable from the Windows Desktop over Tailscale without exposing Hermes publicly.
Instructions: Give the local AI this prompt: "Using `NETWORKING.md`, configure the Spark so Hermes is reachable from the Windows Desktop over Tailscale. Prefer SSH-over-Tailscale as primary. If the Hermes API server is enabled, bind it only to the tailnet or keep it localhost-only behind SSH tunneling."
Issues Prompt to AI: "The Desktop cannot reach the Spark over Tailscale. Validate tailnet status, SSH reachability, API binding choices, firewall rules, and the configured hostname or Tailscale IP before changing any higher-level Hermes settings."

Step Nine: Validate the backend runtime
Description: Confirm the Spark backend can run AlTrader with Kanban, scheduling, notifications, and paper-trading support.
Instructions: Give the local AI this prompt: "Run the validation steps in `VALIDATION.md`. Confirm the AlTrader board can be created, specialist tasks can progress, scheduled jobs are present, Telegram and AgentMail config are readable, and Alpaca paper credentials validate without placing a live trade."
Issues Prompt to AI: "Backend validation failed. Identify the first failing validation step, preserve the exact error, and repair only the backend dependency or config that caused that step to fail before rerunning the validation sequence."
