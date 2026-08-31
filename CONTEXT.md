# Plana Desktop

Plana Desktop is a Windows companion whose character interactions can invoke useful, user-configured actions without coupling those actions to the renderer.

## Language

**Companion**:
The visible character instance that lives on the desktop and receives user interactions.
_Avoid_: Pet, widget, avatar

**Interaction**:
A gesture or event involving the Companion, such as click, double-click, drag, idle timeout, or schedule.
_Avoid_: Event, trigger

**Action**:
A named operation that can be invoked by an Interaction or menu, such as playing an animation, opening a URL, or launching an application.
_Avoid_: Command, function, tool

**Action Binding**:
A user-configurable association from an Interaction to an Action.
_Avoid_: Shortcut, event handler

**Action Pack**:
A declarative manifest that contributes Actions using capabilities implemented by the host. It contains data, not executable plugin code.
_Avoid_: Plugin, script pack

**Capability**:
A sensitive host operation an Action Pack must declare before one of its Actions may use it, such as opening URLs or launching processes.
_Avoid_: Permission, scope

**Plugin**:
Executable extension code hosted outside the desktop process. Plugins are reserved for behavior that cannot be expressed as an Action Pack.
_Avoid_: Action Pack, mod

**Renderer**:
The embedded surface that displays the Companion and reports animation and pointer information to the desktop host.
_Avoid_: Frontend, WebView
