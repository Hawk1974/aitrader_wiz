# Client Intake

Step One: Fill Out The Client Intake Form
Description: This form is the only information the AI needs from the client before it can stand up AlTrader on the two-computer setup.
Instructions: Give the client `CLIENT_INTAKE.yaml` and tell them: "Fill in every blank you know. If you do not know a value, leave it blank and note that you need help finding it."
Issues Prompt to AI: "The client intake form is incomplete. Read `CLIENT_INTAKE.yaml`, identify only the missing required fields for backend standup, and ask for those specific values in plain English."

Step Two: Confirm The Two-Computer Rule
Description: Make sure the AI understands the hard deployment rule before it starts installing anything.
Instructions: Give the AI this prompt: "This deployment always uses two machines. Machine one is the Linux NVIDIA DGX Spark backend with LM Studio and Hermes Agent. Machine two is the Windows workstation with Hermes Desktop. The tie between them is Tailscale. Do not collapse this into a single-machine install."
Issues Prompt to AI: "The deployment plan drifted away from the required two-computer model. Rebuild the plan so the Linux Spark is the backend authority and the Windows machine is the Hermes Desktop workstation only."

Step Three: Confirm Which Secrets Stay On Linux Only
Description: Prevent accidental placement of trading or notification secrets on the Windows workstation.
Instructions: Give the AI this prompt: "Read `CLIENT_INTAKE.yaml` and enforce this rule: Alpaca, Telegram, and AgentMail secrets must live on the Linux Spark backend only. The Windows Hermes Desktop machine must not become the long-term holder of those secrets."
Issues Prompt to AI: "A secret was placed or planned for the Windows machine. Identify the secret, move it back into the Linux backend plan, and restate the storage rule before proceeding."

Step Four: Split The Intake Between Backend And Desktop
Description: The Linux AI needs the full form. The Windows AI only needs the Desktop and Tailscale portions plus the Spark connection details.
Instructions: Give the backend AI this prompt: "Use the full `CLIENT_INTAKE.yaml` file." Give the Windows AI this prompt: "Use only the Desktop, Tailscale, and connection fields from `CLIENT_INTAKE.yaml`. Do not request or store backend trading secrets."
Issues Prompt to AI: "The wrong machine is asking for or storing the wrong fields. Re-map the intake so the Linux backend gets full config and the Windows workstation gets connection-only values."

Step Five: Hand The Filled Form To The Standup AI
Description: Once the form is complete, it becomes the configuration source of truth for the AI that will perform the install.
Instructions: Give the AI this prompt: "Use the filled `CLIENT_INTAKE.yaml` as the source of truth while following `BACKEND_STANDUP.md` or `DESKTOP_STANDUP.md`. Do not invent missing values. Stop and ask only for fields that are still blank and required."
Issues Prompt to AI: "A required field is still blank during standup. Identify the exact field name from `CLIENT_INTAKE.yaml`, explain in one sentence what it is for, and pause until the operator provides it."
