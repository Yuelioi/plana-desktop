# Archive

This directory contains historical implementations and completed proofs retained only for reference.

- `legacy-wpf-webview/` contains the retired WPF control center and Native/WebView companion implementation.
- `proofs/` contains completed renderer experiments.
- `generated-legacy-outputs/` contains local, ignored build outputs moved out of active artifact paths; it may be deleted manually when no rollback inspection is needed.

Nothing under `archive/` participates in the solution, build, publish, packaging, or runtime fallback path. Production development targets WinUI Control Center + .NET Companion Host + Godot Renderer.
