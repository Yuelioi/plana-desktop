# Plugin system

Plana Desktop has two extension levels:

- **Action Packs** are declarative and preferred. They use host adapters for typed operations and never execute extension code in the desktop process.
- **Executable Plugins** are a developer-preview feature for behavior that cannot be represented by an Action Pack. They run as separate processes behind `Plana.PluginHost`.

## Runtime model

The current Plugin Host provides crash and hang containment, protocol validation, bounded messages, and current-user local IPC. Valid discovered Plugins start automatically unless disabled by the user. This product stage has no trust-review, package-hash approval, or approval-expiry workflow.

Host capabilities such as `url.open`, `file.open`, `folder.open`, `process.launch`, `command.run`, and `script.run` remain typed and separately authorized. Process separation does not make an arbitrary executable obey those grants; sandboxed public Plugins will need all sensitive operations brokered by the desktop host.

## Manifest preview

Each unpacked Plugin occupies one versioned directory containing `plugin.json`:

```json
{
  "schemaVersion": 1,
  "id": "example.productivity",
  "version": "1.0.0",
  "publisher": "Example",
  "hostApi": "1",
  "entryPoint": "plugin/Example.Plugin.exe",
  "defaultLocale": "en",
  "locales": { "en": "locales/en.json" },
  "capabilities": ["url.open", "folder.open"]
}
```

The entry point must remain inside the package directory. Stable IDs, protocol fields, and capability names are never localized. The host ships English and Simplified Chinese; locale files use .NET/BCP 47 culture names with fallback to English and then the stable ID.

## Protocol preview

`Plana.PluginHost` is launched with a unique pipe name and a manifest path. It opens a duplex named pipe restricted to the current user, starts one Plugin process, and relays newline-delimited JSON envelopes:

```json
{
  "protocolVersion": 1,
  "requestId": "01J...",
  "type": "hello",
  "payload": {}
}
```

Messages larger than 1 MiB, unsupported versions, and envelopes without a request ID or type are rejected. The implemented startup lifecycle is:

1. The Plugin writes `hello` with its exact Plugin ID and host API version.
2. Desktop verifies identity/API and writes `initialize` with the selected culture and approved capability set.
3. The Plugin answers `ready` using the initialize request ID.
4. The Plugin sends one bounded `contributeActions` message. Every Action ID must be unique and every requested capability must already exist in the Plugin manifest.
5. Desktop answers `actionsAccepted`; accepted contributions enter the normal searchable Action catalog.

Connection, handshake, and contribution share a five-second startup deadline. The control center reports Starting, Ready, Failed, or Exited. Disabling a Plugin or quitting Plana closes the session and stops the Plugin Host process tree.

Running a contributed Action sends `invoke` with a unique request ID and the Plugin-local Action ID. The Plugin returns a structured `result` with success and a message. Invocation is serialized per Plugin and bounded to 30 seconds. Cancellation, timeout, malformed response, or request-ID mismatch fails the session, removes its contributed Actions, and terminates its process tree so a late response cannot corrupt the next request.

During invoke, a Plugin may send typed `hostRequest` messages for `url.open`, `file.open`, `folder.open`, `process.launch`, `command.run`, or `script.run`. The requested kind must map to a capability declared by both the Plugin manifest and the currently invoked contributed Action. Desktop then constructs a temporary typed Action, runs the existing adapter's validation and execution, and returns a structured `hostResponse`. Unknown kinds and undeclared requests fail without reaching an adapter. Plugins never receive WinUI, Win32, renderer, or arbitrary host objects.

## Example Plugin

The standard package includes an example under `SamplePlugins/hello`. It contributes `Open Plugin package folder`, declares only `folder.open`, and requests that operation through the broker. Install it from the Plugins page to exercise the full Plugin Host and broker flow. Its source is under `examples/Plana.ExamplePlugin`.

## Remaining before public use

- Per-request audit history and richer Plugin execution diagnostics.
- Signed/archive package format, ZIP traversal validation, and explicit update workflow.
- Job Object CPU, memory, process-count, and kill-on-close limits.
- AppContainer or LPAC isolation and brokered capability requests.
