# Backend Validation

Step One: Validate Hermes Agent health
Description: Confirm the Spark Hermes Agent is installed, starts cleanly, and reports its version.
Instructions: Give the local AI this prompt: "Validate the Hermes Agent install on the Spark. Report the executable path, version, and whether the gateway, cron, and dashboard or API server services are reachable."
Issues Prompt to AI: "Hermes Agent health validation failed. Identify the first failing subsystem, preserve the exact command output, and repair only that subsystem before rerunning the validation."

Step Two: Validate AlTrader graph creation
Description: Confirm High Marshal can create the `altrader` board graph from the backend.
Instructions: Give the local AI this prompt: "From `/opt/hermes/altrader`, run the graph creation helper for the `altrader` board, confirm tasks are created, and report the board contents."
Issues Prompt to AI: "The AlTrader graph could not be created or the board is inconsistent. Check the board name, helper script path, and Hermes Kanban state, then repair the graph creation path."

Step Three: Validate deterministic stage execution
Description: Confirm deterministic wrappers can run the specialist stages without relying on improvised tool names.
Instructions: Give the local AI this prompt: "Validate that `run_altrader_stage.py` can execute at least one deterministic stage against the current board and report the stage artifact path it produced."
Issues Prompt to AI: "Deterministic stage execution failed. Identify whether the problem is the script path, stage id, workspace root, or Hermes board state, and repair that specific issue."

Step Four: Validate notifications and paper broker access
Description: Confirm the backend can send Telegram, send AgentMail, and authenticate to Alpaca paper trading.
Instructions: Give the local AI this prompt: "Validate Telegram send, AgentMail send, and Alpaca paper account access from the Spark. Do not place a live trade. Preserve exact delivery or broker artifacts as validation evidence."
Issues Prompt to AI: "Notification or Alpaca validation failed. Preserve the exact provider response, identify which integration failed first, and repair only that integration before rerunning the validation sequence."
