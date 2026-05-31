# Windows Desktop Standup

Step One: Confirm the Desktop machine role
Description: Verify this machine is the Windows Hermes Desktop workstation and not the Spark backend.
Instructions: Give the local AI this prompt: "Confirm this is the Windows workstation that will run Hermes Desktop only. Do not configure Alpaca, AgentMail, or Telegram secrets here. Report the Windows username, the Tailscale hostname, and whether Hermes Desktop is already installed."
Issues Prompt to AI: "The machine role is unclear or Hermes Desktop cannot be located. Determine whether this is the Windows operator workstation, locate the current Hermes Desktop installation, and stop before changing any trading or messaging secrets."

Step Two: Back up the current Hermes Desktop runtime
Description: Preserve the current `%USERPROFILE%\\.hermes` state before applying the AlTrader runtime files.
Instructions: Give the local AI this prompt: "Back up the current Hermes Desktop runtime home at `%USERPROFILE%\\.hermes` into a timestamped sibling backup folder. Preserve `config.yaml`, `.env`, `claw3d-runtime.json`, `profiles`, `scripts`, and `logs` if they exist. Do not delete the original."
Issues Prompt to AI: "The Hermes runtime home is missing or partially corrupted. Create a safe backup of whatever exists, record what is missing, and continue without deleting existing state."

Step Three: Install or verify Hermes Desktop
Description: Ensure Hermes Desktop is installed and launches on Windows.
Instructions: Give the local AI this prompt: "Verify Hermes Desktop is installed on Windows. If it is missing, install the current supported Windows build. Launch it once, confirm the application starts, then close it cleanly before applying runtime files."
Issues Prompt to AI: "Hermes Desktop installation or first launch failed. Diagnose the installer path, permissions, and antivirus interference, repair the install, and report the exact executable path that will be used."

Step Four: Apply the Alvin root runtime files
Description: Copy the exported root Hermes runtime files from this bundle into the Windows Hermes home so the Desktop presents the correct Alvin identity and root runtime behavior.
Instructions: Give the local AI this prompt: "Copy the contents of `runtime_export\\root_hermes` from this bundle into `%USERPROFILE%\\.hermes`. Preserve backups. Overwrite only the runtime identity and markdown files that are explicitly included in the bundle."
Issues Prompt to AI: "The root Hermes runtime files could not be copied or conflict with existing files. Compare the bundle files with the current `%USERPROFILE%\\.hermes` files, preserve a backup, and apply only the required Alvin runtime files."

Step Five: Apply the Desktop runtime snapshot and profiles
Description: Install the Desktop-visible AlTrader agent roster, Office runtime snapshot, and profile files used by Office, Kanban, and schedule views.
Instructions: Give the local AI this prompt: "Copy `runtime_export\\claw3d_runtime\\claw3d-runtime.json` into `%USERPROFILE%\\.hermes\\claw3d-runtime.json`. Then copy all folders under `runtime_export\\profiles` into `%USERPROFILE%\\.hermes\\profiles`, preserving backups of existing matching profiles. Ensure the agents `highmarshal`, `scryer`, `runesmith`, `augur`, `coinmaster`, `warden`, `overlord`, `gatekeeper`, `tracker`, `bard`, `chirurgeon`, `archivist`, and `steward` exist with their markdown and memory files."
Issues Prompt to AI: "The Desktop profile import failed or the expected agent folders are missing. Reconcile the bundle profile export against `%USERPROFILE%\\.hermes\\profiles`, restore any missing agent folders, and verify each required agent has its markdown and memory files."

Step Six: Apply Desktop support scripts
Description: Install the local support scripts used for Office repair, runtime maintenance, and local Desktop runtime alignment.
Instructions: Give the local AI this prompt: "Copy the files under `runtime_export\\support_scripts` into `%USERPROFILE%\\.hermes`. Preserve backups if files already exist. These scripts are Desktop-local support files and are not the backend trading authority."
Issues Prompt to AI: "Support scripts could not be applied or Windows blocked them. Unblock the copied files if needed, verify PowerShell execution access for local support scripts, and report which scripts were restored."

Step Seven: Connect the Desktop to the Spark over Tailscale
Description: Configure the Windows Desktop to communicate with the Spark backend over the tailnet.
Instructions: Give the local AI this prompt: "Using the files in `connection_docs`, configure Hermes Desktop to reach the Spark backend over Tailscale. Prefer SSH-over-Tailscale as the primary control path. If the Spark API server is intentionally enabled on the tailnet, record that as the secondary path. Do not store Spark trading secrets on this Windows machine."
Issues Prompt to AI: "The Desktop cannot reach the Spark over Tailscale. Validate tailnet connectivity, confirm the Spark hostname or Tailscale IP, test SSH reachability, and report the exact failing hop before changing Hermes Desktop settings."

Step Eight: Validate Office, Kanban, schedules, and agent visibility
Description: Confirm the Desktop shows the AlTrader runtime correctly after the import and remote connection setup.
Instructions: Give the local AI this prompt: "Launch Hermes Desktop and validate that Office loads, Kanban renders, the AlTrader agents appear, and the schedule views are coherent with the imported runtime. Confirm Alvin is the root identity and High Marshal is the orchestrator."
Issues Prompt to AI: "The Desktop app opens but Office, Kanban, schedules, or agent visibility is wrong. Compare the live Desktop runtime against the exported `claw3d-runtime.json` and profile files from this bundle, identify the mismatch, and repair only the mismatched Desktop runtime components."

Step Nine: Confirm Desktop is non-authoritative for broker and notification secrets
Description: Verify the Windows workstation does not hold the Spark-only Telegram, AgentMail, or Alpaca credentials.
Instructions: Give the local AI this prompt: "Audit the Windows Desktop Hermes runtime and confirm that Telegram, AgentMail, and Alpaca paper-trading secrets are not stored here unless they are strictly required for Desktop-only testing. Report any trading or notification secrets found on the Desktop machine."
Issues Prompt to AI: "Trading or notification secrets were found on the Desktop workstation. Identify each secret location, report it, and prepare a remediation plan that moves those secrets to the Spark backend without breaking Desktop connectivity."
