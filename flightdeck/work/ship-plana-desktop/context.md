# Product and delivery context

## Product

Plana Desktop is a Windows companion that combines an expressive Spine character with useful user-configured Actions. It should feel present and playful without blocking normal desktop work.

## Confirmed behavior

- The selected valid Character Pack renders in a transparent, frameless, always-on-top window; missing or invalid selections fall back to bundled Plana.
- Empty areas pass mouse input to applications behind the companion; visible or designated interactive areas accept clicks and dragging.
- Configurable Interactions such as click and double-click can invoke Actions; right-click is reserved for the Context Menu, and idle belongs to Automation.
- Built-in Actions may play animations, open HTTP(S) URLs, launch applications, or execute bounded commands.
- Declarative Action Packs extend the Action catalog without loading executable code into the desktop process.
- Capability grants are scoped per Action Pack; selected Actions may require confirmation on every run.
- Future executable Plugins must run outside the desktop process through a narrow IPC interface.
- Action Packs expose host capabilities for opening HTTP(S) URLs, files, folders, applications, bounded commands, and explicitly interpreted scripts; relative paths resolve from the pack directory.
- The host UI supports persisted live language switching. The first release ships English and Simplified Chinese; later languages remain additive culture resources without UI or domain-logic changes.
- Executable Plugin support is now a delivery requirement rather than an unspecified future extension.
- Users can define searchable project-launcher Actions with an executable, project folder, and ordered argument array. Windows Terminal is the first supported preset, including `{folder}` substitution and optional direct Codex startup.
- Users can create personal typed Actions without authoring an Action Pack. These Actions are persisted in the synthetic `Your Actions` pack and follow the same capability and argument-boundary rules.
- Configurable Interactions are deliberate user gestures only: click and double-click in the first release. Right-click is host-owned Context Menu navigation. Idle is Automation and may eventually invoke only Ambient Actions unless a distinct background-execution grant is designed.
- `Your Actions` is the primary path for personal automation. Action Packs are portable import/export bundles; executable Plugins run when discovered unless the user disables them, and have no official catalog in the first release.
- The Companion window exposes a two-row Quick Toolbar above the model: search/settings, then configurable Tool Group/Action/run. The toolbar has its own hit area; the renderer keeps its original size and other transparent areas remain click-through.
- AI responses and Interactions express character behavior through a semantic Character Performance Intent: Emotion, Gesture, and speaking state. The selected Character Pack's model-specific mapping owns Spine expression states, gesture sequences, and idle recovery; callers do not name Spine animations.
- Declarative Character Packs contain Spine assets, normalized hit geometry, layout, and model-specific Character Performance mappings. They contain no code and contribute no Actions.
- AI conversation is attached to the Companion rather than exposed as a Control Center page: Host-owned native input and Speech Bubble windows sit below/near the model for normal focus, IME, and physical-pixel text. Godot renders only the character. Settings retains provider/model/API configuration only.
- Conversation-entry copy is Character-neutral. Selecting another Character Pack changes appearance and performance mapping, but does not yet change the configured AI persona.

## Technology and repository constraints

- Windows is the primary and currently exclusive platform.
- Reusable modules target .NET 10. WinUI 3 owns ordinary application UI and configuration; the native Host embeds a narrow WPF transient-UI module for Windows 10 per-pixel-alpha Quick Launch and Companion Dock text/IME. `TransientUiHost` owns a dedicated STA thread and WPF Dispatcher; every Host-to-WPF public operation marshals through it. The global hotkey uses its own Host-thread WinForms HWND and never borrows a WPF window handle. Quick Launch activation temporarily attaches the previous foreground thread to the WPF thread before setting foreground, active, HWND, and WPF keyboard focus; logical WPF focus alone is insufficient. Quick Launch is a one-shot window: outside/Escape dismissal closes its HWND and clears WPF focus, while each activation creates a fresh Window and IME context from the retained catalog configuration. A supervised Godot 4 process owns transparent Spine rendering and dynamically loads validated Character Pack paths; one serialized Host lifecycle owns start/stop/recovery so switching cannot orphan Renderer processes. The .NET Companion host owns tray, persistence, Windows pass-through, recovery, and the semantic command seam.
- Historical WPF/WebView implementations are archived for reference only and do not participate in builds, publishing, or runtime fallback.
- Keep `Plana.Core` free of WPF and Windows dependencies.
- Treat the Core module's small interface as the primary test surface.
- The source repository must build independently without referencing sibling projects.
- Renderer assets, model attribution, and third-party runtime license notices remain with their bundled files.
- Prefer explicit executable and argument arrays; do not accept opaque shell command strings as the normal Action format.
- Keep executable Plugins isolated in a separate Plugin Host process and expose only versioned protocol operations and capability-mediated host requests.
- The machine defines a `TargetPath` environment variable for Adobe. Run builds through `build.ps1`, which removes it only for the child build process.

## Definition of usable

- A user can run the published executable and see, move, hide, restore, and quit the companion.
- Placement, scale, topmost preference, and interaction bindings survive restart.
- A control center lets users inspect and change companion settings, Interaction bindings, Action Packs, and capability grants.
- Broken packs or failed Actions produce recoverable messages and do not terminate the companion.
- Users can import Action Pack and developer-preview Plugin folders, open their managed library directories, and reload discovery from the control center.
- Release tests, publish, renderer startup, and click-through smoke checks pass from the self-contained repository.
- Installation, first run, Action Pack authoring, troubleshooting, and third-party notices are documented.

## External runtime requirements

- The Companion Host requires the .NET 10 Desktop Runtime; the Control Center uses Windows App SDK deployment.
- The published Host requires its bundled Godot executable and Renderer project. Missing files are a startup error, never a fallback trigger.
- Public Windows releases expose one elevated `Plana-Desktop-x64-Setup.exe`; users do not unpack or invoke PowerShell scripts. The installed Companion and Control Center request administrator elevation at process startup so application Actions can launch elevated executables without error 740.
