# Companion host rearchitecture

## Deliverable

A measured replacement architecture for the rejected Native/WebView Companion: WinUI remains the normal UI and supervisor, while a replaceable renderer process owns the transparent character window and executes semantic Character Performance commands.

## Current

Godot has passed Proof A and is now the default published Renderer. The stable system architecture is split-process WinUI/.NET + replaceable Renderer; the old Native/WebView implementation remains the automatic fallback when published Godot files are absent.

The AI-facing semantic seam is now established in Core. `CharacterPerformanceIntent` expresses Emotion, Gesture, and speaking state; `PlanaPerformancePlanner` hides numbered Spine states and gesture sequencing. Forty-two Core tests and the complete seven-project Release build pass with zero warnings/errors.

The production module is `src/Plana.Companion.Godot.Renderer`, using pinned Godot `4.6.1.stable` and the public `spine-godot` GDExtension built from the Spine `4.2` branch. The disposable Proof remains separate and is not published.

The official extension successfully imports and renders the real Spine `4.2.33` `NP0035_spr` asset. Automated captures verified `Idle_01`, `S_Pat_01_M_all`, and expression `17`; PNG sampling verified background alpha 0 and character alpha 255. Rendering used Godot Compatibility/OpenGL on the NVIDIA RTX 2060.

Godot's `WINDOW_FLAG_MOUSE_PASSTHROUGH` is not sufficient for the required Windows-wide pass-through behavior. A live HWND probe with the flag enabled returned extended style `0x40018`: neither `WS_EX_TRANSPARENT` nor `WS_EX_LAYERED` was present.

A disposable .NET Win32 supervisor now owns the Godot child process and HWND mode. Its automated state smoke passed: Interactive `0x40018` → PassThrough `0x40038` with `WS_EX_TRANSPARENT` → Interactive `0x40018`; renderer restart replaced both PID and HWND; stop ended the child.

The Host waits for a real `RENDERER_READY` handshake, then owns styles and placement. Character Performance plans cross a loopback TCP channel with acknowledgements; WinUI uses a host-owned named pipe to request semantic Emotion/Gesture changes. Godot emits click, double-click, and context events. Automated movement proved +30/+20 exact placement and restoration; left-button drag is implemented in the Renderer.

Published runtime checks passed with the real package: semantic `Happy + HeadPat` and `Affectionate` commands acknowledged; forced Renderer termination recovered within about one second; a kill-on-close Job Object prevents orphans when the Host is terminated. Low-processor mode plus a 30 FPS cap reduced the one-second idle CPU sample from about 172 ms to the measurement floor; cold start is about 1.1 seconds and working set about 150 MiB on the target machine.

Post-cutover input/rendering defects are fixed. Godot now ignores content aspect when the Host resizes the window, eliminating opaque top/bottom letterbox bars. Single click is delayed 250 ms so a second press can cancel it and emit exactly one double-click; the Host respects configured Interaction Bindings and the built-in interaction produces varied semantic performances rather than a hard-coded head-pat. Right-click raises the same native Context Menu used by the tray instead of activating a possibly obscured Control Center window.

Ordinary interactive mode now applies a normalized character-shaped `mouse_passthrough_polygon` and recomputes it on every window-size change. A real `WindowFromPoint` probe confirms the transparent point above Plana resolves to the underlying desktop while a body point still resolves to the Godot HWND. The separate full-window mode uses a Renderer-owned out-of-window polygon; its menu label states that it affects the whole window and a tray balloon explains that it must be disabled from the tray because the character intentionally stops receiving mouse input.

The initial full-window implementations were invalid after adding the character polygon. `WS_EX_TRANSPARENT` alone did not bypass a non-layered Godot composition HWND, while moving the polygon outside the window made the model invisible because the polygon also shaped visible content. The final mode uses a coordinated handshake: Godot first clears its polygon without clipping the model, then the Host applies `WS_EX_LAYERED | WS_EX_TRANSPARENT`; disabling reverses the order before restoring the character polygon. `tools/check-full-pass-through.ps1` proves command acknowledgement, body-point pass-through, interactive restoration, character visibility, and exact position/size stability in both directions.

The agent-runnable regression command is `tools/check-companion-regressions.ps1`. On the final published Host it reports zero top/bottom black ratio and passes all eight rendering/input checks, including transparent-top pass-through, interactive body hit, and full-window-mode explanation. Synthetic live input additionally produced six clicks with six distinct random performances, one double-click without an extra click, and a visible native context-menu HWND. Legacy persisted `doubleClick` binding keys resolve alongside canonical `double-click`, so existing user bindings continue to work. Core has 44 passing tests including deterministic random-interaction and legacy-binding regressions.

WinUI now has a dedicated bilingual Chat page with normal text focus/IME. It uses the configured Codex CLI subscription or OpenAI-compatible API and drives speaking/happy/worried character intent through the Host. The tray exposes Show, Hide, Head pat, Affection, Mouse pass-through, Actions, Settings, and Exit.

The current machine exposes only one 3440×1440 display at 96 DPI. Real 100%→150% cross-monitor behavior cannot be verified on this hardware without changing the user's system display configuration, so that acceptance check remains open rather than simulated.

## Next

Harden packaging: sign the Control Center MSIX or document/install through a trusted development certificate. Run the open 100%→150% cross-monitor check on suitable hardware. Continue Stage 7 interaction polish and project discovery on the selected Renderer architecture.

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
