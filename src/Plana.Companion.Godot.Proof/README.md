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

The controller owns the renderer process and its whole-window `WS_EX_TRANSPARENT` mode. Press `T` to switch between interactive and pass-through, `R` to kill/restart the renderer, and `Q` to exit. Use `-Smoke` for an automated transition check.
