# Desktop-to-Spark Connection

Step One: Confirm Tailscale path
Description: Verify the Windows workstation can reach the Spark over the tailnet.
Instructions: Give the local AI this prompt: "Confirm Tailscale is installed and connected on Windows. Report the Windows Tailscale hostname, verify reachability to the Spark Tailscale hostname or IP, and preserve the exact hostname that Hermes Desktop should use."
Issues Prompt to AI: "The Windows workstation cannot reach the Spark over Tailscale. Diagnose the tailnet path, preserve the first failing hop, and fix Tailscale connectivity before changing Hermes Desktop settings."

Step Two: Configure the primary remote control path
Description: Use SSH-over-Tailscale as the primary Desktop control path for the Spark backend.
Instructions: Give the local AI this prompt: "Configure Hermes Desktop to use the Spark over SSH on Tailscale as the primary remote path. Record the SSH username, hostname, and any required tunnel details."
Issues Prompt to AI: "SSH-over-Tailscale failed from Hermes Desktop. Determine whether the problem is SSH auth, hostname resolution, firewall, or tunnel settings, and repair that exact issue."

Step Three: Use the Hermes API path only when needed
Description: Keep the API path secondary and non-public.
Instructions: Give the local AI this prompt: "If Hermes Desktop also needs a direct API path, configure it only over the tailnet or through SSH tunneling, keep API auth enabled, and document it as the secondary control path."
Issues Prompt to AI: "The API path is failing or insecure. Verify binding, auth, and tunnel behavior, then reconfigure it so it is reachable only over the approved remote path."
