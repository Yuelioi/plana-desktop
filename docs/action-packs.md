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
      "id": "example.open-notes",
      "label": "Open notes",
      "kind": "file.open",
      "parameters": { "path": "notes.txt" },
      "capabilities": ["file.open"]
    },
    {
      "id": "example.open-pack-folder",
      "label": "Open this pack folder",
      "kind": "folder.open",
      "parameters": { "path": "." },
      "capabilities": ["folder.open"]
    },
    {
      "id": "example.run-script",
      "label": "Run helper script",
      "kind": "script.run",
      "parameters": {
        "interpreter": "powershell.exe",
        "script": "scripts/helper.ps1",
        "arg.0": "example"
      },
      "capabilities": ["script.run"],
      "requiresConfirmation": true
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

Relative `path` and `script` values are resolved from the directory containing that pack's `manifest.json`. Script actions require an explicit interpreter and pass the script path plus each numbered argument as distinct process arguments. Plana does not bypass PowerShell execution policy or silently install an interpreter.

## Capabilities

- `url.open`: open an HTTP or HTTPS URL in the default browser.
- `file.open`: open an existing file with its Windows-associated application.
- `folder.open`: open an existing folder in the Windows shell.
- `process.launch`: start an executable without capturing its output.
- `command.run`: start an executable, capture output, enforce a timeout, and report a non-zero exit as failure.
- `script.run`: run an existing pack-relative script through an explicitly named interpreter, capture output, and enforce a timeout.

Capability access is granted per Action Pack. Set `requiresConfirmation` when an action should ask on every execution even after its pack has been authorized.

## Code plugins

Action Packs cannot load DLLs or call arbitrary host methods. Executable Plugins run outside the desktop process through the versioned Plugin Host protocol documented in `plugin-system.md`.

## Localization

The first manifest schema uses one required English `name` and `label`. Host UI strings are loaded from culture-specific resource dictionaries; the first release ships English and Simplified Chinese with live persisted switching. Localized pack metadata will be added in a later schema without changing Action identifiers or capability names.

## Windows Terminal project launchers

The control center's Actions page can create searchable project launchers without editing a manifest. The default uses `wt.exe` with two distinct arguments, `-d` and `{folder}`. `{folder}` is replaced with the configured project path.

To open a new tab in the project and immediately start Codex, enter these arguments one per line:

```text
new-tab
-d
{folder}
codex
```

Optional Windows Terminal arguments such as `-w`, `0`, `-p`, and a profile name remain separate lines. Plana persists and launches every line as one process argument; it never concatenates them into a shell command.

## User-created Actions

Users do not need to author a pack for personal automation. The control center's Actions page has an `Add custom Action` editor for HTTP(S) URLs, files, folders, applications, bounded commands, and explicitly interpreted scripts. Each personal Action is persisted under the synthetic `Your Actions` pack and can be searched, run, bound to an Interaction, or invoked from the Companion toolbar.

File, folder, and script paths entered through the editor must be absolute and already exist. Arguments remain one entry per line. Command and script Actions always require confirmation on every run.
