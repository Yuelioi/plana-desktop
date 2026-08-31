# Ship Plana Desktop

**Status:** Open

## Goal

Deliver a usable Windows desktop companion that renders Plana with Spine, stays out of the user's way, supports configurable interactions, and safely invokes extensible Actions supplied by declarative Action Packs.

## Current

The production repository is self-contained and the technical foundation is working. The C#/WPF host renders the bundled Spine model through WebView2CompositionControl in a transparent always-on-top window; native hit testing passes clicks through empty corners. The Core module loads, validates, authorizes, lists, and dispatches Actions. The desktop host includes animation, URL, process, and command adapters, tray controls, capability prompts, JSON settings, interaction bindings, and window placement persistence. A framework-dependent win-x64 Release package builds and runs.

The application does not yet provide a user-facing control center. Action Packs and interaction bindings currently require manual JSON editing, scale is not persisted, hit testing uses an approximate polygon rather than an animation-aware mask, and startup/packaging recovery paths are not hardened.

## Next

Implement the first control-center window described in [the current Slice](slices/control-center.md). Start by extending settings and Action Pack state so the UI can list packs, enable or disable them, review capabilities, and edit Interaction bindings without manual JSON changes. Use [context.md](context.md) for product constraints and [plan.md](plan.md) for stage order.

## Current execution

Stage 4 — Control center and user configuration.

## Progress

- Validated WPF + WebView2CompositionControl with the existing Spine renderer in a throwaway prototype.
- Created the production solution with `Plana.Core`, `Plana.Desktop`, and Core interface tests.
- Recorded the WPF/WebView2 and declarative Action Pack architecture decisions.
- Implemented Action Pack loading, validation, capability authorization, and dispatch.
- Implemented host adapters for animations, HTTP(S) URLs, applications, and bounded command execution.
- Implemented the transparent companion window, approximate native click-through, drag movement, tray menu, and placement persistence.
- Migrated model and Spine runtime assets into this repository; builds have no cross-project references.
- Verified 4/4 Release tests, framework-dependent win-x64 publish, actual Spine rendering, and native `HTTRANSPARENT`/`HTCLIENT` behavior.
- Initialized the repository on the `main` Git branch and captured the production baseline in Git; no remote is configured yet.

## References

- [Product and delivery context](context.md)
- [Delivery plan](plan.md)
- [Domain language](../../../CONTEXT.md)
- [Action Pack format](../../../docs/action-packs.md)
- [WPF/WebView2 ADR](../../../docs/adr/0001-use-wpf-shell-with-web-renderer.md)
- [Action Pack ADR](../../../docs/adr/0002-declarative-action-packs-before-code-plugins.md)
