# Control center

## Deliverable

A normal taskbar-visible settings window, opened from the tray, that removes the need to edit JSON manually for everyday configuration.

## Current

`DesktopSettings` stores placement, topmost preference, Interaction bindings, and Action Pack capability grants. `ActionPackLoader` returns manifests from `%LOCALAPPDATA%\PlanaDesktop\packs`, but pack enablement and load errors are not represented as user-facing state. The tray menu is generated from the current Action catalog and has no Settings entry.

## Next

Extend the persisted settings and pack-loading result with pack enablement and diagnostic state. Then create a single-instance control-center window that the tray can show or focus.

## Acceptance checks

- Tray menu contains `Settings` and reuses one control-center window instance.
- Companion settings expose always-on-top, startup behavior placeholder, scale, and reset-position controls.
- Interactions view lists supported Interactions and binds each to an available Action or none.
- Action Packs view lists discovered packs, enabled state, publisher/version, declared capabilities, and validation errors.
- Users can revoke a pack's granted capabilities.
- Saving updates live companion behavior without requiring a restart where practical.
- Long names, missing actions, malformed manifests, empty pack directories, and revoked capabilities have explicit states.
- Core tests cover the added pack-state behavior; Release build and publish remain green.

## Decisions

- The control center is a separate normal WPF window, not an overlay inside the transparent renderer.
- Editing raw manifest parameters remains an authoring concern; the first control center manages packs, bindings, and host settings rather than becoming a generic JSON editor.
