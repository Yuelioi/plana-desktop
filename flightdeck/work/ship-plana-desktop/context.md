# Product and delivery context

## Product

Plana Desktop is a Windows companion that combines an expressive Spine character with useful user-configured Actions. It should feel present and playful without blocking normal desktop work.

## Confirmed behavior

- Plana renders in a transparent, frameless, always-on-top window.
- Empty areas pass mouse input to applications behind the companion; visible or designated interactive areas accept clicks and dragging.
- Interactions such as click, double-click, right-click, and idle events can invoke Actions.
- Built-in Actions may play animations, open HTTP(S) URLs, launch applications, or execute bounded commands.
- Declarative Action Packs extend the Action catalog without loading executable code into the desktop process.
- Capability grants are scoped per Action Pack; selected Actions may require confirmation on every run.
- Future executable Plugins must run outside the desktop process through a narrow IPC interface.

## Technology and repository constraints

- Windows is the primary and currently exclusive platform.
- Use C# with .NET 8 and WPF for the host; use WebView2CompositionControl for the bundled Spine renderer.
- Keep `Plana.Core` free of WPF and Windows dependencies.
- Treat the Core module's small interface as the primary test surface.
- The source repository must build independently without referencing sibling projects.
- Renderer assets, model attribution, and third-party runtime license notices remain with their bundled files.
- Prefer explicit executable and argument arrays; do not accept opaque shell command strings as the normal Action format.
- The machine defines a `TargetPath` environment variable for Adobe. Run builds through `build.ps1`, which removes it only for the child build process.

## Definition of usable

- A user can run the published executable and see, move, hide, restore, and quit the companion.
- Placement, scale, topmost preference, and interaction bindings survive restart.
- A control center lets users inspect and change companion settings, Interaction bindings, Action Packs, and capability grants.
- Broken packs or failed Actions produce recoverable messages and do not terminate the companion.
- Release tests, publish, renderer startup, and click-through smoke checks pass from the self-contained repository.
- Installation, first run, Action Pack authoring, troubleshooting, and third-party notices are documented.

## External runtime requirements

- The current published build is framework-dependent and requires the .NET 8 Desktop Runtime.
- WebView2 Runtime is required for the renderer.
