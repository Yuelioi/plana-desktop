# Plana Desktop

A Windows desktop companion powered by the existing Plana Spine renderer and a native C# host.

The repository is self-contained for builds. Renderer code, the Spine runtime files, model data, and their attribution/license notices live under `src/Plana.Desktop/Renderer`.

## Status

The validated WPF/WebView2 prototype is being promoted into the production architecture. The first production milestone includes transparent rendering, native hit testing, persisted placement, interaction bindings, and declarative Action Packs.

## Architecture

- `src/Plana.Core`: Action catalog, Action Pack loading, capability checks, and dispatch.
- `src/Plana.Desktop`: WPF shell, WebView2 renderer, Windows action adapters, tray, and settings.
- `tests/Plana.Core.Tests`: behavior tests across the Core module interface.

## Action Packs

An Action Pack is a directory containing `manifest.json`. It can contribute actions implemented by the host:

- `pet.animation`
- `url.open`
- `process.launch`
- `command.run`

Opening URLs, launching applications, and executing commands are capabilities. Packs must declare them and users can disable a pack without uninstalling the application. Executable code plugins are intentionally deferred to a separate out-of-process Plugin Host.

## Build

```powershell
.\build.ps1
.\build.ps1 -Publish
```

The build script removes a machine-level `TargetPath` environment variable from its process because some Adobe installations define it and unintentionally override MSBuild's project output path.

The framework-dependent Windows package is written to `artifacts\win-x64`. Run `Plana.Desktop.exe`; the application requires the .NET 8 Desktop Runtime and WebView2 Runtime.
