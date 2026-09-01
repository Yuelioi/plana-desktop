# Product and delivery context

## Product

Plana Desktop is a Windows companion that combines an expressive Spine character with useful user-configured Actions. It should feel present and playful without blocking normal desktop work.

## Confirmed behavior

- Plana renders in a transparent, frameless, always-on-top window.
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
- AI responses and Interactions express character behavior through a semantic Character Performance Intent: Emotion, Gesture, and speaking state. The Plana model planner owns the mapping to numbered Spine expression states, gesture sequences, and idle recovery; callers do not name Spine animations.
- AI conversation is attached to the Companion rather than exposed as a Control Center page: Host-owned native input and Speech Bubble windows sit below/near the model for normal focus, IME, and physical-pixel text. Godot renders only the character. Settings retains provider/model/API configuration only.

## Technology and repository constraints

- Windows is the primary and currently exclusive platform.
- Reusable modules target .NET 10. WinUI 3 owns ordinary application UI, AI text/IME, and configuration; a supervised Godot 4 process owns transparent Spine rendering, while the .NET Companion host owns tray, persistence, Windows pass-through, recovery, and the semantic command seam.
- Keep the WPF host runnable only as the behavioral migration baseline until parity checks pass.
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

- The current WPF baseline is framework-dependent and requires the .NET 8 Desktop Runtime; the migration target is .NET 10 plus Windows App SDK deployment.
- WebView2 Runtime is required for the renderer.
