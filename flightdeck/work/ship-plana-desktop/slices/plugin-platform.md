# Plugin platform

## Deliverable

A versioned, out-of-process executable Plugin system that cannot load third-party code into the WPF host and that integrates Plugin-contributed Actions through the existing Action Engine.

## Current

The existing `IActionHandler` seam now covers declarative Action Pack capabilities for HTTP(S) URLs, files, folders, applications, bounded commands, and explicitly interpreted scripts. Pack-relative paths resolve from the manifest directory. The host UI has persisted live English/Simplified-Chinese switching.

Primary-source research covering VPet, Shimeji-ee, Rainmeter, Desktop Mate, and Windows isolation is complete. It confirms that Plana should borrow package/lifecycle/localization ideas while rejecting in-process DLL loading and overloaded action strings.

`Plana.Core` now validates versioned `plugin.json` manifests and prevents entry points from escaping their package directories. A new `Plana.PluginHost` executable opens a current-user-only named pipe, starts one Plugin process, validates bounded versioned JSON-line envelopes, relays them over redirected standard streams, and terminates the Plugin process tree when the relay ends. This is a protocol and supervision skeleton, not yet a usable Desktop-integrated Plugin system or a security sandbox.

The control center discovers `plugin.json` manifests recursively under `%LOCALAPPDATA%\PlanaDesktop\plugins`, displays identity/publisher/version/API/capabilities/diagnostics, and lets users disable or re-enable them. Valid Plugins start automatically. The standard win-x64 package includes `PluginHost/Plana.PluginHost.exe`.

The Plugins page explains that no official catalog exists, opens the managed plugins directory, reloads discovery, imports a selected folder after structural validation, and can install the bundled sample. Imported valid Plugins start immediately unless disabled.

Enabled Plugins launch through one `Plana.PluginHost` per Plugin. `PluginProtocolSession` owns a five-second bounded `hello`/`initialize`/`ready` handshake, verifies Plugin ID and host API, matches the ready request ID, and limits messages to 1 MiB. `PluginRuntimeManager` reports Starting/Ready/Failed/Exited, stops sessions on disable, and terminates process trees on shutdown.

The published zero-capability `SamplePlugins/hello` package implements the lifecycle. A real end-to-end smoke check launched the published Host and sample process, observed the correct hello identity, sent initialize, and received a matching ready response before cleanup.

Plugins now send one bounded `contributeActions` message after ready. Core rejects malformed/duplicate IDs and any Action capability absent from the reviewed manifest. Accepted declarations become a normal Action Pack using the `plugin.invoke` adapter, so they appear in search, bindings, and tray menus and pass through the existing capability policy. Invocation uses a unique request ID and a structured result, is serialized per Plugin, and has a 30-second deadline. Cancellation, timeout, malformed response, or response mismatch fails and tears down the session, removes contributions, and prevents late-message desynchronization.

Plugin invocations now accept typed `hostRequest` messages for URL, file, folder, process, command, and script adapters. Protocol policy maps each kind to one capability and refuses requests absent from the invoked Action's already authorized capability set; contribution policy already proves that set is contained by the reviewed manifest. `PluginHostRequestBroker` constructs a temporary typed Action, runs the existing adapter validation/execution, and returns `hostResponse`. Unknown kinds and escalation never reach an adapter. The sample now contributes `Open Plugin package folder` with only `folder.open`; a real Host/broker smoke check completed hostRequest/hostResponse/result successfully.

The control center also has a searchable Actions page. Users can run any current Action directly and create/remove persisted project launchers with a name, folder, executable, and one argument per line. The default `wt.exe` preset uses `-d` and `{folder}`; custom Windows Terminal window/profile/tab parameters and a final `codex` command are supported without constructing a shell string. User-authored launchers become built-in-authority Actions in a synthetic `Project launchers` pack.

The earlier launcher-only editor did not satisfy manual Action creation. The Actions page now exposes `Add custom Action`, opening a focused editor with six typed choices: website, file, folder, application, bounded command, and script. It validates URLs and absolute existing paths, separates every argument, forces per-run confirmation for commands/scripts, persists to `UserActions`, and materializes entries in the synthetic capability-reviewed `Your Actions` pack. The deterministic feature check changed from red (`UserActions` and kind selector absent) to green.

## Next

The Stage 5 deliverable is complete. Route later package signing/AppContainer, request audit history, restart policy, richer diagnostics, and catalog work into Stage 7 hardening. Keep the published sample as the end-to-end compatibility fixture.

## Acceptance checks

- Third-party executable code never loads into `Plana.Desktop`.
- Plugin manifests declare identity, protocol version, entry point, Actions, and requested capabilities.
- Host and Plugin Host communicate through a narrow, versioned, local-only protocol.
- Plugin processes are started, monitored, timed out, and terminated independently.
- File, URL, folder, process, and script requests pass through the same capability policy as Action Packs.
- Invalid, incompatible, crashed, or unresponsive Plugins remain visible as recoverable diagnostics.
- Plugin and host UI strings have an English-first localization path without localizing stable IDs or protocol fields.

## Decisions

- V1 wire messages are newline-delimited JSON envelopes with `protocolVersion`, `requestId`, `type`, and `payload`; payloads are capped at 1 MiB.
- Each Plugin gets its own Plugin Host and child process for lifecycle isolation.
- `file.open` rejects executable and script extensions; those require the separately reviewed process or script capability.
- Startup is a five-second `hello → initialize → ready` handshake; ready must match the initialize request ID and declared identity/API before the Plugin is considered running.
- Each Plugin contributes Actions once after ready. Contributions request only manifest-approved capabilities, enter the ordinary Action Engine, and invoke over serialized request/response with a 30-second deadline.
- Host requests are typed data and must be covered by both reviewed manifest capabilities and the invoked Action declaration; they reuse existing host adapters and never expose host objects.
