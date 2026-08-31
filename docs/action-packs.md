# Action Packs

Action Packs add useful operations without loading executable plugin code into Plana Desktop. Put each pack in its own directory under `%LOCALAPPDATA%\PlanaDesktop\packs` and restart the application.

## Manifest

```json
{
  "schemaVersion": 1,
  "id": "example.productivity",
  "name": "Productivity examples",
  "version": "1.0.0",
  "publisher": "Example",
  "actions": [
    {
      "id": "example.open-website",
      "label": "Open website",
      "kind": "url.open",
      "parameters": {
        "url": "https://example.com"
      },
      "capabilities": ["url.open"]
    },
    {
      "id": "example.open-notepad",
      "label": "Open Notepad",
      "kind": "process.launch",
      "parameters": {
        "executable": "notepad.exe"
      },
      "capabilities": ["process.launch"]
    },
    {
      "id": "example.list-directory",
      "label": "List a directory",
      "kind": "command.run",
      "parameters": {
        "executable": "cmd.exe",
        "arg.0": "/c",
        "arg.1": "dir",
        "arg.2": "{appData}"
      },
      "capabilities": ["command.run"],
      "requiresConfirmation": true
    }
  ]
}
```

Arguments are separate manifest entries (`arg.0`, `arg.1`, and so on), not one shell command string. This preserves Windows argument boundaries and makes capability review meaningful.

## Capabilities

- `url.open`: open an HTTP or HTTPS URL in the default browser.
- `process.launch`: start an executable without capturing its output.
- `command.run`: start an executable, capture output, enforce a timeout, and report a non-zero exit as failure.

Capability access is granted per Action Pack. Set `requiresConfirmation` when an action should ask on every execution even after its pack has been authorized.

## Code plugins

Action Packs cannot load DLLs or call arbitrary host methods. A future code-plugin protocol will run plugins outside the desktop process and expose capabilities through a narrow IPC contract.
