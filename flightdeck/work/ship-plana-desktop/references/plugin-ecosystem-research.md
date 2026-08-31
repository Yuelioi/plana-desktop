# Desktop companion extension-system research

_Primary-source review, 2026-08-31_

## Conclusion

Plana should keep two extension classes deliberately separate:

| Extension class | What it contains | Who executes behavior | Security meaning |
| --- | --- | --- | --- |
| **Action Pack** | Data, localized labels, assets, and typed Action declarations | Plana's built-in adapters | Grants such as `url.open` or `file.open` are enforceable because third-party code never runs. |
| **Executable Plugin** | A manifest plus an executable that can discover or contribute behavior dynamically | A dedicated plugin process, communicating through IPC | A separate process contains crashes, but is **not** a permission boundary unless Windows also restricts that process. |

This preserves the existing ADR: never load third-party assemblies into `Plana.Desktop`. The best near-term result is to add file, folder, and script operations to Action Packs first, then introduce an out-of-process plugin protocol. A public third-party plugin system should either run plugins in AppContainer/LPAC or truthfully label unsandboxed plugins as full-trust code.

## Ecosystem evidence

### VPet: rich packages, useful lifecycle, unsafe trust boundary

VPet's mod is a directory rooted by `info.lps`. Its loader reads name, introduction, compatible game version, mod version, author, Workshop identity, and inline language records. Subdirectories select content types such as pets, food, images, text, languages, and plugins; `plugin/load.lps` can alter DLL loading and CPU selection. This is a useful precedent for a single discoverable package with explicit metadata and convention-based content. [VPet `CoreMOD` metadata and inline languages](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/Function/CoreMOD.cs#L90-L131), [language and plugin directories](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/Function/CoreMOD.cs#L288-L335)

VPet exposes a clear plugin lifecycle: construction, `LoadPlugin`, `GameLoaded`, `Save`, `Setting`, `LoadDIY`, and `EndGame`. That lifecycle is worth adapting to IPC, but the plugin also receives the broad `IMainWindow` surface. [VPet `MainPlugin`](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows.Interface/MainPlugin.cs#L6-L62)

Its security model should not be copied. DLLs are loaded into the desktop process with `Assembly.LoadFrom`, reflected, and instantiated. For untrusted/unsigned code, the UI asks for one grant and explicitly warns that the plugin can access all system and external-system data. This is informed consent, not isolation or granular authorization. [VPet DLL loading](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/Function/CoreMOD.cs#L340-L410), [VPet full-access warning](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/WinDesign/winGameSetting.xaml.cs#L1045-L1055)

VPet also demonstrates the demand for launching desktop resources: its DIY runner treats one string as a Windows path, URL, or keyboard input. The feature is useful, but the overloaded-string heuristic blurs opening a document with executing code. Plana should keep these as distinct typed operations. [VPet `RunDIY`](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/MainWindow.cs#L440-L493)

Localization is package-aware: translations can appear inline in `info.lps` and under culture-named `lang/` files, including before a mod is enabled. The useful lesson is to let extensions ship their own localized metadata while retaining a deterministic fallback language. [VPet localization loading](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/Function/CoreMOD.cs#L122-L130), [culture-directory loading](https://github.com/LorisYounger/VPet/blob/4fe8faf73eca1358592f3f68f52918a427dddc3e/VPet-Simulator.Windows/Function/CoreMOD.cs#L288-L307)

### Shimeji-ee: declarative character behavior is the scalable path

The historical Shimeji-ee source distribution treats a character as an image directory plus optional per-character `actions.xml` and `behaviors.xml`, falling back to global definitions. Actions describe what can happen; behaviors describe selection and transitions; required behaviors provide a stable minimum contract. This is much closer to Plana's Action Pack than to an executable plugin. [Shimeji-ee configuration README](https://github.com/gil/shimeji-ee/blob/0a24ef484fc9fc8b6fc1931dd574c57a51b1bb63/readme.txt#L61-L89)

The XML supports named built-in action types and can reference Java classes already available to the application. The source resolves an `Embedded` class with `Class.forName` and instantiates it in-process; there is no package permission broker. Therefore Shimeji-ee is evidence for data-driven animation/state-machine extensibility, not for safe file, browser, folder, process, or script access. [Shimeji-ee `ActionBuilder`](https://github.com/gil/shimeji-ee/blob/0a24ef484fc9fc8b6fc1931dd574c57a51b1bb63/src/com/group_finity/mascot/config/ActionBuilder.java#L76-L119)

The application itself uses culture-specific `language_<culture>.properties` files, but the documented character directory format has no localized display-name/description manifest. [Shimeji-ee English resource bundle](https://github.com/gil/shimeji-ee/blob/0a24ef484fc9fc8b6fc1931dd574c57a51b1bb63/conf/language_en.properties)

### Rainmeter: mature adjacent ecosystem, but still full-trust execution

Rainmeter is a useful adjacent Windows desktop ecosystem. A skin is a self-contained `.ini` module with a recommended metadata section; `.rmskin` packages can contain skins, layouts, and architecture-specific plugin DLLs, and the installer shows the included component types. This supports using a dedicated Plana package extension and an install review screen. [Rainmeter skin structure](https://github.com/rainmeter/rainmeter-docs/blob/master/source/manual/skins/index.html), [Rainmeter package installation](https://github.com/rainmeter/rainmeter-docs/blob/master/source/manual/installing-skins.html)

Rainmeter's RunCommand component shows valuable execution semantics: separate program and parameter fields, captured output, completion/error reporting, timeouts, and close/kill controls. Plana should copy those lifecycle semantics, not Rainmeter's default use of `cmd.exe` or free-form command strings. [Rainmeter RunCommand documentation](https://github.com/rainmeter/rainmeter-docs/blob/master/source/manual/plugins/runcommand.html)

Rainmeter also loads native plugin DLLs into the host with `LoadLibrary` and resolves lifecycle entry points such as `Initialize`, `Reload`, `Update`, and `Finalize`. That is a mature API but not a security model for Plana. [Rainmeter plugin loader](https://github.com/rainmeter/rainmeter/blob/master/Library/MeasurePlugin.cpp)

### Desktop Mate: distribution precedent, not an open plugin precedent

Desktop Mate's official site describes a platform for additional officially licensed characters distributed as DLC. In the official materials reviewed, no public SDK, plugin manifest, permission contract, or supported third-party code-loading API is documented. Community avatar loaders are therefore not an official architectural precedent and were excluded. [Desktop Mate official site](https://www.infiniteloop.co.jp/desktopmate/), [official Steam product/DLC surface](https://store.steampowered.com/app/3301060/Desktop_Mate/)

## Windows action contracts

Keep the current Plana capability names where they already exist, and extend them rather than silently changing their meaning:

| Capability | Contract and validation | Default consent |
| --- | --- | --- |
| `url.open` | Accept only an absolute, well-formed `http` or `https` URI. Never accept an arbitrary registered URI scheme under this capability. Windows documents that `http`/`https` open the default browser, while custom schemes can activate other applications. [Windows URI launching](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-default-app) | Persistable per pack/plugin, but prompt on non-user-initiated triggers such as idle events. |
| `file.open` | Resolve to an existing canonical file and open it with the user's default handler. Refuse executable/script types; Windows' `LaunchFileAsync` likewise refuses automatically executed types such as `.exe`, `.msi`, and `.js`. [Windows file launching](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-the-default-app-for-a-file) | Persistable for a declared file or user-selected scope; otherwise show the resolved path each run. |
| `folder.open` | Resolve to an existing canonical directory and shell-open that directory only. `.NET` documents that `UseShellExecute=true` delegates documents and registered file types to the graphical shell. [`.NET UseShellExecute`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute) | Same policy as `file.open`. |
| `process.launch` | Exact executable plus an argument array; no shell command. Show executable, arguments, and working directory before approval. | High risk; confirmation each run by default. |
| `command.run` | Preserve the existing bounded-output adapter: exact executable, argument array, working directory, stdout/stderr limits, timeout, and non-zero exit reporting. | High risk; confirmation each run by default. |
| `script.run` | Explicit interpreter identifier/path plus a script file and argument array. Prefer pack-local, hash-covered scripts; do not infer an interpreter from a filename and do not accept a single command string. | Highest risk; confirmation each run. |

Use `ProcessStartInfo.ArgumentList`, not a hand-built `Arguments` string: .NET escapes each supplied argument. Microsoft still warns that `ProcessStartInfo` with untrusted data is dangerous, so typed arguments do not replace validation and consent. [`.NET ArgumentList`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.argumentlist), [command-injection guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca3006)

`process.launch`, `command.run`, and `script.run` can ultimately execute arbitrary user-level code. Granting one of them is therefore effectively a full-code-execution decision unless the executable, script, arguments, and accessible resources are strongly constrained. Do not present their grants as a complete sandbox.

For timeouts, terminate the owned process tree and report truncation/timeout distinctly. A Windows Job Object is stronger lifecycle control than tracking one PID: child processes normally join the job, and `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` terminates associated processes when the owner closes the job. Job Objects also support process-count, CPU, and memory limits, but are not by themselves a filesystem/network security boundary. [Windows Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)

## Recommended phased v1 architecture

### Phase 1 — Action Packs and English-first localization

1. Add `file.open`, `folder.open`, and `script.run` as typed host adapters; retain `url.open`, `process.launch`, and `command.run` for compatibility.
2. Apply the validation and confirmation policy above. In particular, do not permit URL/file/process launches caused by idle automation without a visible per-run confirmation.
3. Move every built-in UI string behind stable resource keys now, with English as the neutral/default resource. .NET's resource model uses a neutral fallback culture and culture-specific satellite assemblies, so shipping English only initially does not require hard-coded English. [Resources in .NET apps](https://learn.microsoft.com/en-us/dotnet/core/extensions/resources)
4. Add Action Pack localization without changing action IDs: for example, `defaultLocale: "en"`, `locales/en.json`, and keys such as `pack.name`, `action.open-project.label`. Fallback should be requested culture -> parent culture -> `en` -> stable ID. Culture filenames should use normal .NET/BCP 47 tags such as `en`, `en-US`, and `zh-Hans`.

### Phase 2 — Executable Plugin protocol, initially developer/trusted preview

Use an installable `.planaplugin` archive (a validated ZIP) that expands to one versioned directory. A minimal `plugin.json` should contain:

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

Validate archive traversal, duplicate IDs, schema/API compatibility, entry-point containment, architecture, and file hashes before installation. Install disabled, display publisher/hash/capabilities, and revoke grants whenever an update adds or broadens capabilities.

Run one process per plugin. Use a local named pipe with an ACL limited to the current user and the expected plugin identity; named pipes support duplex local IPC and explicit access control. [`.NET NamedPipeServerStream`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream)

Use a small versioned request/response protocol rather than sharing .NET types. Recommended lifecycle:

`discover -> validate -> permission review -> spawn -> hello/API negotiation -> initialize -> contributeActions/contributeSettings -> ready -> invoke/cancel -> stop`

Every request needs an ID, protocol version, deadline/cancellation, bounded payload size, and structured error. Start and handshake must time out; crashes disable the plugin for the session after a bounded restart policy. Permission or version changes stop and restart the process. Plugin settings UI should be host-rendered from a declarative schema; do not embed arbitrary plugin WPF controls in the desktop process.

Until OS sandboxing is implemented, label this mode **full-trust executable plugin**, require a prominent one-time trust decision, and keep it out of any general third-party marketplace. Out-of-process execution at this stage protects Plana from plugin crashes and hangs, not the user's files or credentials.

### Phase 3 — Sandboxed public plugins

Launch each plugin in AppContainer or LPAC, give it only its private data directory and the IPC endpoint, and broker privileged operations through the typed host capabilities. Microsoft documents AppContainer as an isolation boundary for files, registry, credentials, network, processes, and windows; access is capability-based. [AppContainer isolation](https://learn.microsoft.com/en-us/windows/win32/secauthz/appcontainer-isolation), [launching an AppContainer](https://learn.microsoft.com/en-us/windows/win32/secauthz/implementing-an-appcontainer)

Combine AppContainer with a per-plugin Job Object for resource/lifetime control. Do not grant general filesystem or process access to the plugin process merely because its manifest declares `file.open` or `process.launch`; those declarations authorize specific broker calls. If compatibility requires an unsandboxed mode, keep it visually and semantically distinct as full trust.

## Decision summary for Plana

- Keep Action Packs as the normal authoring path; they already cover most requested automation without third-party code.
- Add the three missing typed operations now: `file.open`, `folder.open`, and `script.run`.
- Adopt VPet's package/lifecycle/localization strengths, Shimeji-ee's declarative behavior separation, and Rainmeter's install review plus bounded command lifecycle.
- Reject in-process DLL loading, overloaded action strings, and claims that process separation alone enforces permissions.
- Make English the only shipped language in the first release, but make hard-coded UI strings a release blocker so later localization is additive rather than a rewrite.
