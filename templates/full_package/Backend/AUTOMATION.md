# Backend Automation Commands

Use these commands from the Spark backend Hermes environment after the helper scripts have been copied into the Hermes scripts directory.

Step One: Register High Marshal kickoff
Description: Create the weekday morning High Marshal kickoff job.
Instructions: Give the local AI this prompt: "Run the Hermes cron command that creates the High Marshal job: `hermes cron create '0 9 * * 1-5' --name 'AlTrader High Marshal 09:00 Local' --deliver local --script 'altrader_highmarshal_kickoff.py' --no-agent --workdir '/opt/hermes/altrader'`."
Issues Prompt to AI: "The High Marshal cron registration failed. Verify the Hermes executable path, the script path, and the backend workdir, then register the High Marshal job again and report the exact error if it still fails."

Step Two: Register Archivist closeout
Description: Create the weekday late-day Archivist archive job.
Instructions: Give the local AI this prompt: "Run the Hermes cron command that creates the Archivist job: `hermes cron create '0 17 * * 1-5' --name 'AlTrader Archivist 17:00 Local' --deliver local --script 'altrader_archivist_archive.py' --no-agent --workdir '/opt/hermes/altrader'`."
Issues Prompt to AI: "The Archivist cron registration failed. Verify the Hermes executable path, the script path, and the backend workdir, then register the Archivist job again and report the exact error if it still fails."

Step Three: Register Steward recovery
Description: Create the periodic Steward recovery job.
Instructions: Give the local AI this prompt: "Run the Hermes cron command that creates the Steward job: `hermes cron create '*/10 * * * *' --name 'AlTrader Steward 10min' --deliver local --script 'altrader_steward_recovery.py' --no-agent --workdir '/opt/hermes/altrader'`."
Issues Prompt to AI: "The Steward cron registration failed. Verify the Hermes executable path, the script path, and the backend workdir, then register the Steward job again and report the exact error if it still fails."
