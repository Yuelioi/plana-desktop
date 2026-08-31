# .NET 10 and WinUI 3 migration

## Deliverable

A side-by-side .NET 10 hybrid application: WinUI 3 for ordinary product UI and a native Win32/Composition Companion for transparent rendering and pointer behavior, with no regression in the existing Core, Action, Plugin, settings, or renderer contracts.

## Current

Paused. The WinUI control center remains a useful direction, but the WebView2 Composition Companion is rejected as the production architecture after repeated runtime failures across cursor ownership, drag, keyboard focus, context-menu lifetime, settings refresh, and mixed native/web toolbar interaction.

The clean Release build passes 28 tests with zero warnings/errors. The clean native publish runs from `artifacts/native-win-x64`; the control-center MSIX is under `artifacts/control-center`; the WPF rollback publish is isolated under `artifacts/legacy-win-x64`. Runtime checks verified single instances, Plugin Host startup, protocol redirects, contention-safe settings reload/save, live scale changes, complete process exit, and native hit tests (`HTTRANSPARENT` corner, `HTCLIENT` model and toolbar). Final screenshots are under `artifacts/native-companion-final.png` and `artifacts/control-center-settings-final.png`.

## Next

Research mature desktop-pet implementations and run isolated architecture proofs before replacing the Companion. Do not add more features to the current native/WebView toolbar.

## Acceptance checks

- `Plana.Core`, Core tests, `Plana.PluginHost`, and the example Plugin build and test on .NET 10 without UI dependencies.
- A separate WinUI 3 app runs Settings, language switching, and Action search using existing settings and Core interfaces.
- A small `ICompanionSurface` seam separates control-center callers from the native Companion implementation.
- Native prototype proves transparent Spine rendering, topmost/drag, DPI, and click-through without using a transparent WinUI WebView.
- WPF baseline stays runnable until side-by-side parity checks pass.
- Existing JSON settings migrate additively; no SQLite dependency is introduced.

## Decisions

- WinUI 3 is the application UI framework, not the transparent renderer window.
- Companion transparency and hit testing belong to native Win32/Composition.
- Migration replaces modules at their seams; it does not layer WinUI controls into the existing WPF visual tree.
- JSON settings are shared through `Plana.Core.Settings`; WinUI and WPF do not keep separate settings models.
- Repository builds clear the machine-level After Effects `TargetPath` variable so MSBuild cannot mistake `AfterFX.exe` for project output.
- `plana://settings` and `plana://actions?query=...` are the stable activation boundary from the existing Companion into the packaged WinUI control center; the WPF UI remains a fallback when the protocol is unavailable.
- `Plana.Core.Companion.ICompanionSurface` is the UI-neutral control seam. `Plana.Companion.Native` proves a .NET 10 raw Win32 layered topmost surface with no WinUI dependency, per-monitor DPI handling, persisted size/position/scale, native drag, and transparent-region hit-through.
- Settings reads allow atomic replacement and saves retry brief sharing violations, preventing the native watcher from racing WinUI scale/settings writes.
- Settings storage never captures a UI synchronization context. Native reload uses timestamp polling plus `PostMessage`, and WebView toolbar refresh remains asynchronous, preventing sync-over-async deadlocks in the Win32 message loop.
- Action/Tool Group IDs use the same `user.action.*` and `user.launcher.*` identities in WinUI and the native toolbar. Legacy raw IDs are normalized on load. Tool Group editing explicitly loads existing membership and provides New, Save, and Delete states.
- Settings is the first ordinary NavigationView tab; technical framework copy is not shown in product UI. With no configured Tool Group the Companion toolbar shows only placeholders and no Pack Actions.
