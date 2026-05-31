# Desktop Standup

Step One: Read The Bundle And The Intake Form
Description: Start from the bundle docs and the client-filled intake form instead of asking the operator for backend secrets.
Instructions: Give the Windows AI this prompt: "Read `CLIENT_INTAKE.yaml`, `Desktop\\README.md`, and `Desktop\\CONNECTION.md`. Use those files as the only source of truth for standing up Hermes Desktop on this Windows machine."
Issues Prompt to AI: "The Desktop standup source of truth is unclear. Re-read the bundle files and restate which documents control Windows install, runtime import, and Spark connectivity."

Step Two: Confirm This Is The Windows Desktop Machine
Description: Prevent the AI from trying to build the Linux backend on the wrong computer.
Instructions: Give the AI this prompt: "Confirm this machine matches the Windows workstation values in `CLIENT_INTAKE.yaml`. Report hostname, username, and whether Hermes Desktop is already installed."
Issues Prompt to AI: "The current machine does not match the Windows Desktop role from the client intake. Stop before applying runtime files and verify the correct workstation."

Step Three: Back Up The Existing Windows Hermes Runtime
Description: Preserve `%USERPROFILE%\\.hermes` before importing the AlTrader Desktop runtime.
Instructions: Give the AI this prompt: "Back up the current `%USERPROFILE%\\.hermes` folder into a timestamped backup folder. Preserve the original in place."
Issues Prompt to AI: "The existing Windows Hermes runtime is missing or partially damaged. Back up whatever exists, record what is missing, and continue without deleting the original state."

Step Four: Install Or Verify Hermes Desktop
Description: The Windows machine must run Hermes Desktop and not the backend trading services.
Instructions: Give the AI this prompt: "Verify Hermes Desktop is installed on Windows. If it is missing, install the current supported Windows build, launch it once, confirm it opens, and close it before runtime import."
Issues Prompt to AI: "Hermes Desktop install or first launch failed. Diagnose the installer, permissions, and executable path issues, then complete a working Windows Desktop install."

Step Five: Import The Desktop Runtime
Description: Copy the Alvin root files, runtime snapshot, profiles, and support scripts from the bundle into the Windows Hermes home.
Instructions: Give the AI this prompt: "Follow `Desktop\\README.md`. Copy the exported root Hermes files, `claw3d-runtime.json`, agent profiles, and Desktop support scripts from the bundle into `%USERPROFILE%\\.hermes`, preserving backups of existing files first."
Issues Prompt to AI: "The Desktop runtime import failed or the expected agents are missing. Compare the imported files against the bundle and restore the required Alvin runtime snapshot and profile set."

Step Six: Connect Windows To The Linux Spark Over Tailscale
Description: The Desktop must reach the Spark backend over the tailnet and must not become the broker or secret authority.
Instructions: Give the AI this prompt: "Using `CLIENT_INTAKE.yaml` and `Desktop\\CONNECTION.md`, configure the Windows workstation to reach the Linux Spark over Tailscale. Prefer SSH-over-Tailscale as the control path. Do not store Alpaca, Telegram, or AgentMail secrets here."
Issues Prompt to AI: "The Windows Desktop cannot reach the Linux Spark over Tailscale. Validate the Tailscale connection, SSH reachability, and hostname or IP values from the client intake before changing Hermes Desktop settings."

Step Seven: Validate Office, Kanban, And Agent Visibility
Description: Confirm the imported runtime renders correctly in Hermes Desktop.
Instructions: Give the AI this prompt: "Launch Hermes Desktop and validate that Alvin is the root identity, the AlTrader agent roster is visible, Office loads, and Kanban can display the board after backend connectivity is available."
Issues Prompt to AI: "Hermes Desktop opens but Office, Kanban, or the agent roster is wrong. Reconcile the imported runtime snapshot and profiles against the bundle and repair only the mismatched Desktop files."

Step Eight: Verify The Windows Machine Is Not Holding Backend Secrets
Description: The Desktop machine should not become the long-term location for the backend trading and notification secrets.
Instructions: Give the AI this prompt: "Audit `%USERPROFILE%\\.hermes` and any Desktop-side config files for Alpaca, Telegram, or AgentMail secrets. Report any backend secrets found and prepare a remediation plan that moves them back to Linux."
Issues Prompt to AI: "Trading or notification secrets were found on the Windows workstation. Identify where they are stored, report them, and move them out of the Windows plan so Linux remains the backend authority."
