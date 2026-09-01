# PROTOTYPE — Godot Companion renderer

Disposable Proof A for the replacement Companion host. It answers whether Godot 4.6.1 plus the official Spine 4.2 GDExtension can render the real Plana model in a transparent Windows window and support the required animation state transitions.

Run from the repository root:

```powershell
.\src\Plana.Companion.Godot.Proof\run-proof.ps1
```

Controls:

- `1`: idle
- `2`: head-pat sequence, then idle
- `3`: affection expression, then idle
- `T`: toggle Godot's whole-window mouse-passthrough flag
- `Escape`: exit

This is not a production renderer and must not be referenced by production projects.

Run the Win32 supervisor state prototype:

```powershell
.\src\Plana.Companion.Godot.Proof\run-controller.ps1
```

The controller owns the renderer process and its whole-window `WS_EX_TRANSPARENT` mode. It waits for the renderer's `PROOF_READY` handshake before taking HWND ownership, connects a loopback command channel, and displays live DPI/bounds/startup/working-set data. Press `H` for happy, `P` for happy head-pat, `L` for affection, `T` to switch between interactive and pass-through, `R` to kill/restart the renderer, arrow keys to move it, and `Q` to exit. The character window itself also supports left-button drag. Use `-Smoke` for automated style, movement/restoration, semantic command acknowledgement, resource, restart, and shutdown checks.
