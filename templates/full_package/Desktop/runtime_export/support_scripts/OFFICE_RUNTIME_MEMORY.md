# Hermes Desktop Office Runtime Memory

## Desktop Office Ownership Rule

When troubleshooting Hermes Desktop Office, do not treat a browser-working `http://localhost:3000/office` page as proof that the desktop app is healthy.

The desktop app considers Office healthy only when all of these are true:

- Hermes Desktop owns the Office dev server on port `3000`.
- Hermes Desktop owns the Hermes adapter process for port `18789`.
- `C:\Users\hawkc\.hermes\claw3d-dev.pid` exists and points to the live Office server process.
- `C:\Users\hawkc\.hermes\claw3d-adapter.pid` exists and points to the live adapter launch process started from the Hermes Office runtime.

## Anti-Pattern

Do not fix this by only making the browser version work.

Examples of incomplete fixes:

- manually starting `node server/index.js --dev`
- manually starting `node server/hermes-gateway-adapter.js`
- leaving the desktop app without its expected pid files

Those can make the browser look correct while Hermes Desktop still reports Office as stopped.

## Correct Recovery Order

1. Stop stray manual Office and adapter processes.
2. Ensure port `3000` is free for Hermes Desktop.
3. Ensure port `18789` is free for the Hermes Office adapter that Hermes Desktop expects.
4. Start the Office server and adapter in the Hermes Desktop ownership model.
5. Verify both pid files exist under `C:\Users\hawkc\.hermes\`.
6. Only then trust the desktop UI state.

## Important Clarification

- A browser does not "take" port `3000`. Browsers are clients, not listeners.
- Multiple clients can use the same Office server on `3000`.
- The failure happens when Hermes tries to start a second Office server or a second adapter while the first one is still bound.
- There is no safe automatic "route desktop to another port but keep the same output" behavior here because the desktop app is configured to expect a specific local Office endpoint and adapter endpoint.

## Repair Script

If Hermes Desktop shows Office as stopped while Hermes Office is already listening, run:

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\hawkc\.hermes\repair-office-runtime.ps1
```

What it does:

- verifies that port `3000` is owned by the Hermes Office dev server
- verifies that port `18789` is owned by the Hermes adapter, or starts the adapter if missing
- recreates `claw3d-dev.pid`
- recreates `claw3d-adapter.pid`
- rewrites `claw3d-port`
- rewrites `claw3d-ws-url`

Use this repair instead of manually starting extra Office or adapter processes.
