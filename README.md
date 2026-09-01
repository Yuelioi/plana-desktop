# Plana Desktop

A Windows desktop companion built with .NET 10, WinUI 3, a supervised Godot 4 renderer, and the existing Plana Spine model.

The repository is self-contained for builds. Renderer code, the Spine runtime files, model data, and their attribution/license notices live under `src/Plana.Desktop/Renderer`.

## Status

The production application uses a transparent Godot Renderer supervised by a .NET Windows host plus a packaged WinUI 3 control center. It includes an IME-capable chat input below the character and a sharp native speech-bubble response surface using a local Codex subscription or an OpenAI-compatible API, semantic character expressions/gestures, unified Quick Launch, user-created Actions, configurable tool groups and interactions, Action Packs, out-of-process Plugins, English/Simplified-Chinese UI, persisted placement/scale, renderer crash recovery, and mouse pass-through. The former Native/WebView and WPF hosts remain as fallback code paths.

## Architecture

- `src/Plana.Core`: settings, Action/Pack contracts, Plugin protocol/runtime, and the Companion surface seam.
- `src/Plana.ControlCenter`: packaged .NET 10 WinUI 3 Chat, Settings, Actions, Tool Groups, and Extensions UI.
- `src/Plana.Companion.Native`: .NET 10 Companion host, tray, settings watcher, Windows window adapter, semantic control pipe, renderer supervision, and legacy WebView fallback.
- `src/Plana.Companion.Godot.Renderer`: production transparent Spine renderer, character animation queue, drag, and pointer events.
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

The framework-dependent Companion is written to `artifacts\native-win-x64`; run `artifacts\native-win-x64\Plana.Desktop.exe`. It starts the bundled Godot Renderer by default and requires the .NET 10 Desktop Runtime. The control-center MSIX is under `artifacts\control-center`; the currently generated local package must be signed before ordinary MSIX installation. A debug identity can run the latest Control Center from its build output. WebView2 is required only by fallback hosts.
