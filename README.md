# Plana Desktop

A Windows desktop companion built with .NET 10, WinUI 3, raw Win32, Windows Composition, WebView2, and the existing Plana Spine renderer.

The repository is self-contained for builds. Renderer code, the Spine runtime files, model data, and their attribution/license notices live under `src/Plana.Desktop/Renderer`.

## Status

The production application uses a native transparent Companion and a packaged WinUI 3 control center. It includes AI conversation through a local Codex subscription or an OpenAI-compatible API, user-created Actions, configurable tool groups and interactions, Action Packs, out-of-process Plugins, English/Simplified-Chinese UI, persisted placement/scale, startup integration, and a compact collapsible toolbar above the character. The former WPF host remains only as a legacy fallback build.

## Architecture

- `src/Plana.Core`: settings, Action/Pack contracts, Plugin protocol/runtime, and the Companion surface seam.
- `src/Plana.ControlCenter`: packaged .NET 10 WinUI 3 Settings, tabular Actions, Tool Groups, and Extensions UI.
- `src/Plana.Companion.Native`: .NET 10 raw Win32/Composition Companion, Spine renderer, toolbar, tray, hit testing, and Windows adapters.
- `src/Plana.PluginHost`: out-of-process executable Plugin supervisor.
- `src/Plana.Desktop`: retained .NET 8 WPF legacy fallback.
- `tests/Plana.Core.Tests`: behavior tests across the Core module interface.

## Action Packs

An Action Pack is a directory containing `manifest.json`. It can contribute actions implemented by the host:

- `pet.animation`
- `url.open`
- `process.launch`
- `command.run`

Opening URLs, launching applications, and executing commands are typed capabilities. Packs declare them and users can disable a pack without uninstalling it. Executable Plugins run through the separate Plugin Host and contribute Actions to the same catalog.

## Build

```powershell
.\build.ps1
.\build.ps1 -Publish
```

The build script removes a machine-level `TargetPath` environment variable from its process because some Adobe installations define it and unintentionally override MSBuild's project output path.

The current framework-dependent Companion is written to `artifacts\native-win-x64`. Install the generated control-center MSIX under `artifacts\control-center`, then run `artifacts\native-win-x64\Plana.Desktop.exe`. The application requires the .NET 10 Desktop Runtime and WebView2 Runtime. The former host is published to `artifacts\legacy-win-x64` for rollback only.
