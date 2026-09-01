# Companion host rearchitecture

## Deliverable

A measured replacement architecture for the rejected Native/WebView Companion: WinUI remains the normal UI and supervisor, while a replaceable renderer process owns the transparent character window and executes semantic Character Performance commands.

## Current

Architecture research is complete. The stable system decision is a split process boundary; Godot + official `spine-godot` 4.2 and native `spine-cpp` + Win32/DComp will compete through disposable proofs rather than architectural preference.

The AI-facing semantic seam is now established in Core. `CharacterPerformanceIntent` expresses Emotion, Gesture, and speaking state; `PlanaPerformancePlanner` hides numbered Spine states and gesture sequencing. Forty-two Core tests and the complete seven-project Release build pass with zero warnings/errors.

Proof A is running with the pinned official pair: Godot `4.6.1.stable` and the public `spine-godot` GDExtension built from the Spine `4.2` branch for Godot `4.6.1`. The ignored toolchain lives under `artifacts/proof-toolchain/`; the disposable source project is `src/Plana.Companion.Godot.Proof` and is not referenced by production projects.

The official extension successfully imports and renders the real Spine `4.2.33` `NP0035_spr` asset. Automated captures verified `Idle_01`, `S_Pat_01_M_all`, and expression `17`; PNG sampling verified background alpha 0 and character alpha 255. Rendering used Godot Compatibility/OpenGL on the NVIDIA RTX 2060.

Godot's `WINDOW_FLAG_MOUSE_PASSTHROUGH` is not sufficient for the required Windows-wide pass-through behavior. A live HWND probe with the flag enabled returned extended style `0x40018`: neither `WS_EX_TRANSPARENT` nor `WS_EX_LAYERED` was present.

A disposable .NET Win32 supervisor now owns the Godot child process and HWND mode. Its automated state smoke passed: Interactive `0x40018` → PassThrough `0x40038` with `WS_EX_TRANSPARENT` → Interactive `0x40018`; renderer restart replaced both PID and HWND; stop ended the child. The first attempt also proved that changing styles before Godot finishes window initialization is racy, so a production protocol must expose `renderer_ready` before the supervisor applies host-owned window state.

## Next

Extend Proof A with native drag and explicit per-monitor DPI observation. Prove interactive drag, 100%→150% monitor movement, and window restoration while the supervisor retains ownership of pass-through. Replace the bounded readiness delay with the first narrow `renderer_ready` handshake only after those window semantics pass.

## Proof A — Godot

- Load the real Spine 4.2.33 binary skeleton, atlas, and texture.
- Transparent, borderless, topmost renderer window with no toolbar or text input.
- Loop `Idle_01`; trigger head pat and an expression through a minimal local control.
- Prove draggable interactive mode and reliable whole-window click-through mode.
- Check 100%→150% DPI movement and transparency against desktop, browser, and taskbar.
- Record cold start, idle CPU/GPU/working set, and artifact size.
- Kill the renderer and prove a tiny controller can observe exit and restart it.

## Proof B — native baseline

- Use official `spine-cpp` 4.2 and the same model/cues.
- First prove HWND alpha, drag, full click-through, and `WM_DPICHANGED`; do not build a general Spine renderer.
- Cover only attachment, mesh, blend, and clipping features the real Plana model uses.
- Record the same measurements as Proof A.

## Acceptance decision

- Select Godot if model fidelity, transparent Windows behavior, DPI, recovery, and resource usage are acceptable on the target machine.
- Select native only if it reaches visual/input parity within the proof budget and its remaining renderer scope is bounded.
- Regardless of renderer, keep WinUI responsible for settings, tray, AI, plugins, IME, supervision, and durable state.
- Renderer IPC accepts versioned semantic performance/window commands, not WebView scripts or arbitrary animation strings from AI.

## References

- [Host architecture research](../references/companion-host-architecture-research.md)
- [Plana Spine inventory](../references/plana-spine-inventory.md)
- [Plana model asset research](../references/plana-model-assets-research.md)
