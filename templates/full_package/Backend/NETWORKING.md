# Spark-to-Desktop Networking

Step One: Confirm Tailscale is installed and connected
Description: Ensure both the Windows Desktop and the Spark are on the same tailnet.
Instructions: Give the local AI this prompt: "Confirm Tailscale is installed and connected on the Spark. Report the Spark Tailscale hostname and IP address and confirm it can reach the Windows Desktop over the tailnet."
Issues Prompt to AI: "Tailscale is not connected or the tailnet path is failing. Fix the Tailscale connectivity issue first and do not change Hermes network settings until the tailnet path is healthy."

Step Two: Prefer SSH over Tailscale
Description: Use SSH over the tailnet as the primary Desktop-to-Spark control path.
Instructions: Give the local AI this prompt: "Configure and validate SSH over Tailscale from the Windows Desktop to the Spark. Record the hostname, username, and any tunnel details that Hermes Desktop will use."
Issues Prompt to AI: "SSH over Tailscale failed. Determine whether the failure is DNS, key auth, password auth, firewall, or SSH service state, repair that specific issue, and report the exact working SSH command."

Step Three: Use Hermes API server only as needed
Description: Keep the Hermes API server non-public and narrow in scope.
Instructions: Give the local AI this prompt: "If Hermes API server access is required, bind it only to the tailnet or keep it localhost-only behind SSH tunneling. Set and document the API server authentication key and do not expose Hermes publicly."
Issues Prompt to AI: "The Hermes API server path is failing or insecure. Verify binding address, auth key, and tunnel path, then reconfigure the API server to be reachable only over the approved control path."
