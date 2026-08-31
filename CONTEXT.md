# Plana Desktop

Plana Desktop is a Windows companion whose character interactions can invoke useful, user-configured actions without coupling those actions to the renderer.

## Language

**Companion**:
The visible character instance that lives on the desktop and receives user interactions.
_Avoid_: Pet, widget, avatar

**Interaction**:
A deliberate user gesture involving the Companion, such as click, double-click, or drag. Right-click is reserved for the Context Menu; idle and schedules are Automation, not Interactions.
_Avoid_: Event, trigger, idle

**Context Menu**:
The host-owned menu opened by right-click for discovering and running Actions and managing the Companion. It is navigation, not a configurable Interaction.
_Avoid_: Right-click Action Binding

**Automation**:
A host-initiated condition such as idle timeout or schedule. Automation may invoke only an Ambient Action unless the product later introduces a separate, explicit background-execution grant.
_Avoid_: Interaction, background click

**Ambient Action**:
An Action with no external desktop side effect, such as playing a Companion animation. It may be eligible for Automation.
_Avoid_: Safe Action

**Action**:
A named operation that can be invoked by an Interaction or menu, such as playing an animation, opening a URL, or launching an application.
_Avoid_: Command, function, tool

**Action Binding**:
A user-configurable association from an Interaction to an Action.
_Avoid_: Shortcut, event handler

**Tool Group**:
A user-named ordered collection of Action references shown in the Companion's Quick Toolbar. Tool Groups organize existing Actions; they do not copy or redefine them.
_Avoid_: Action Pack, Plugin, toolbar command

**Quick Toolbar**:
The two-row control surface above the Companion: Action search and Settings on the first row, then Tool Group, Action selection, and Run on the second. It is direct navigation and invocation, not an Action Pack.
_Avoid_: Quick Rail, menu bar

**Action Pack**:
A portable declarative bundle that contributes Actions using capabilities implemented by the host. It is the import/export and distribution form; personal automation created in the UI lives in the local Your Actions Pack.
_Avoid_: Plugin, script pack

**Your Actions Pack**:
The host-managed local Action Pack containing personal Actions and project launchers created through the control center. Users do not edit its storage format or import it to create ordinary personal automation.
_Avoid_: Plugin, custom command list

**Capability**:
A sensitive host operation an Action Pack must declare before one of its Actions may use it, such as opening URLs or launching processes.
_Avoid_: Permission, scope

**Plugin**:
Executable extension code hosted outside the desktop process. A valid discovered Plugin runs by default unless the user disables it; Plugins are reserved for behavior that cannot be expressed as an Action Pack.
_Avoid_: Action Pack, mod, trusted Plugin, approved Plugin

**Renderer**:
The embedded surface that displays the Companion and reports animation and pointer information to the desktop host.
_Avoid_: Frontend, WebView
