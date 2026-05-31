# Backend Configuration Guide

Step One: Create backend-only environment files
Description: Define the files that will hold Spark-only runtime secrets and operational configuration.
Instructions: Give the local AI this prompt: "Create the backend environment files `/srv/hermes/env/hermes.env` and `/srv/hermes/env/altrader.env` if they do not already exist. Set permissions so only the intended service user can read them."
Issues Prompt to AI: "The backend environment files could not be created or secured. Repair the directory and permission model so the Hermes service user can read the files and other users cannot."

Step Two: Configure Telegram
Description: Apply the Telegram bot token and channel settings only on the Spark backend.
Instructions: Give the local AI this prompt: "Collect the Telegram bot token, the intended chat or home channel identifier, and any related routing values from the operator. Store them only in the backend environment files and validate that Alvin can send a test Telegram message from the Spark."
Issues Prompt to AI: "Telegram configuration failed. Identify whether the problem is the bot token, chat identifier, gateway/runtime path, or network reachability, and request only the missing or invalid Telegram input."

Step Three: Configure AgentMail
Description: Apply the AgentMail API credentials used for backend email delivery.
Instructions: Give the local AI this prompt: "Collect the AgentMail API key, sender identity, and destination email policy from the operator. Store them only in the backend environment files and validate that the Spark can perform a test send through the AlTrader reporting path."
Issues Prompt to AI: "AgentMail configuration failed. Determine whether the API key, sender address, delivery path, or report writer configuration is wrong, and request only the exact missing or invalid value."

Step Four: Configure Alpaca paper trading
Description: Apply the Alpaca paper-trading credentials used by the backend broker context and order submission paths.
Instructions: Give the local AI this prompt: "Collect the Alpaca paper endpoint, API key, and secret from the operator. Store them only in the backend environment files and validate paper account access without placing a live trade."
Issues Prompt to AI: "Alpaca paper configuration failed. Determine whether the endpoint, key, secret, or network access is wrong, preserve the broker response, and request only the exact missing or invalid input."

Step Five: Configure AI provider access
Description: Apply the model/provider settings that Hermes Agent and AlTrader will use on the Spark.
Instructions: Give the local AI this prompt: "Collect the provider configuration required for the Spark backend. If LM Studio on the Spark is intended to serve models locally, document the base URL and model IDs. If external OpenAI-compatible providers are used, store only the required API values on the Spark."
Issues Prompt to AI: "Provider configuration failed. Identify whether the issue is the local LM Studio endpoint, model name, remote API key, or base URL, and request only the missing provider detail."

Step Six: Configure schedule and timezone policy
Description: Ensure the Spark scheduler uses the intended trading automation timezone and market-day rules.
Instructions: Give the local AI this prompt: "Set and document the Spark timezone policy used for AlTrader automation. If the operator has not specified otherwise, recommend a fixed trading timezone such as `America/New_York`. Then validate that the High Marshal, Archivist, and Steward schedules align with that policy and with the market-day guard."
Issues Prompt to AI: "The schedule or timezone policy is ambiguous or incorrect. Stop before changing cron. Determine the intended authoritative timezone for trading automation and revalidate the schedules against that timezone."
