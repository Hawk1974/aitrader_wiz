# Backend Standup

Step One: Read The Bundle And The Intake Form
Description: Start from the standup bundle and the client-filled intake file instead of improvising installation steps.
Instructions: Give the Linux Spark AI this prompt: "Read `CLIENT_INTAKE.yaml`, `Backend\\README.md`, `Backend\\CONFIGURATION.md`, `Backend\\AUTOMATION.md`, `Backend\\NETWORKING.md`, and `Backend\\VALIDATION.md`. Use those files as the only source of truth for standing up Hermes Agent and AlTrader on this Linux Spark machine."
Issues Prompt to AI: "The backend standup source of truth is unclear. Re-read the standup bundle docs and restate the exact files that govern backend install, configuration, automation, networking, and validation."

Step Two: Confirm The Spark Backend Identity
Description: Make sure this machine is the Linux DGX Spark backend and not the Windows Desktop workstation.
Instructions: Give the AI this prompt: "Confirm this machine is the Linux backend from `CLIENT_INTAKE.yaml`. Report hostname, username, architecture, and whether it matches the DGX Spark role."
Issues Prompt to AI: "The machine role does not match the client intake. Stop and verify whether this is the Linux Spark backend before making any runtime or secret changes."

Step Three: Build The Canonical Backend Paths
Description: Create and verify the directories that Hermes Agent, AlTrader, logs, models, and backups will use.
Instructions: Give the AI this prompt: "Create and validate the runtime paths from `CLIENT_INTAKE.yaml`: Hermes home, AlTrader repo path, Hermes data path, AlTrader data path, models path, logs path, and backups path. Apply safe ownership and permissions for the Hermes service user."
Issues Prompt to AI: "One or more backend runtime directories are missing or have bad permissions. Repair only the broken directories and report the final ownership and permissions."

Step Four: Install Or Update Hermes Agent On Linux
Description: The Spark backend is the backend authority and must run Hermes Agent directly.
Instructions: Give the AI this prompt: "Install or update Hermes Agent on this Linux Spark machine using the supported Linux ARM64-compatible method. Preserve an existing install if present. Report the final version and executable path."
Issues Prompt to AI: "Hermes Agent install or update failed on Linux. Diagnose architecture, Python, package, and path issues, then complete a working backend install without switching away from Linux ARM64."

Step Five: Install The AlTrader Backend Payload
Description: Copy the versioned backend source files from this bundle into the canonical AlTrader repo path on Linux.
Instructions: Give the AI this prompt: "Copy `Backend\\repo_payload` into the target AlTrader repo path from `CLIENT_INTAKE.yaml`. Preserve the relative structure. Verify `scripts`, `config`, `docs`, `schemas`, `tests`, and `hermes` all land correctly."
Issues Prompt to AI: "The backend payload copy failed or the destination structure is wrong. Recreate the destination tree and verify the expected top-level folders exist after the copy."

Step Six: Apply The Hermes Runtime Templates
Description: Install the backend Alvin root files, profiles, and Hermes scripts from the bundle onto the Linux backend.
Instructions: Give the AI this prompt: "Copy `Backend\\runtime_templates\\root_hermes`, `Backend\\runtime_templates\\profiles`, and `Backend\\runtime_templates\\hermes_scripts` into the Linux Hermes home from `CLIENT_INTAKE.yaml`. Preserve backups of any existing files first."
Issues Prompt to AI: "The backend runtime templates could not be applied cleanly. Compare live files against bundle templates, preserve backups, and apply only the canonical AlTrader runtime files."

Step Seven: Configure Secrets And Provider Values
Description: Read the client form and store Telegram, AgentMail, Alpaca, LM Studio, and backend runtime values on Linux only.
Instructions: Give the AI this prompt: "Use `CLIENT_INTAKE.yaml` and `Backend\\CONFIGURATION.md` to write the backend environment and config files. Store Alpaca, Telegram, and AgentMail values only on Linux. Configure LM Studio and the selected model endpoint from the intake form."
Issues Prompt to AI: "A required backend value is missing or invalid. Identify the exact missing field name from `CLIENT_INTAKE.yaml`, explain what it is used for, and stop only that part of the configuration until it is supplied."

Step Eight: Register Backend Automation
Description: Install the scheduled backend behavior so AlTrader can run as a product instead of a manual demo.
Instructions: Give the AI this prompt: "Use `Backend\\AUTOMATION.md` and the Hermes scripts from the bundle to register the backend jobs for High Marshal, Archivist, Steward, source monitoring, and active trade supervision. Report the exact resulting schedules."
Issues Prompt to AI: "Backend automation registration failed or one or more jobs are missing. Reconcile the expected AlTrader schedules against the live Hermes cron list and register only the missing or broken jobs."

Step Nine: Configure Tailscale Connectivity
Description: Make the Linux Spark reachable from the Windows Desktop over Tailscale without exposing Hermes publicly.
Instructions: Give the AI this prompt: "Use `CLIENT_INTAKE.yaml` and `Backend\\NETWORKING.md` to configure Tailscale-backed access. Prefer SSH-over-Tailscale as primary. If the Hermes API is enabled, bind it only to the tailnet or keep it behind SSH tunneling."
Issues Prompt to AI: "The Windows machine cannot reach the Linux backend over Tailscale. Validate tailnet connectivity, SSH access, hostname or IP values from the intake form, and any local firewall rules before changing Hermes runtime settings."

Step Ten: Validate The Full Backend
Description: Prove the backend can run AlTrader correctly before handing it off to the Desktop machine.
Instructions: Give the AI this prompt: "Run the validation flow from `Backend\\VALIDATION.md`. Confirm Hermes Agent works, AlTrader files are present, Alpaca paper credentials validate, Telegram and AgentMail config read correctly, Kanban can be created, and scheduled jobs exist."
Issues Prompt to AI: "Backend validation failed. Record the first failing step, preserve the exact error, repair only that dependency or configuration issue, and rerun the validation until the backend passes."
