# uTools plugin and quick-tool system: lessons for Plana Desktop

Research date: 2026-09-01. Sources are limited to uTools' official help/developer documentation and the official `uTools-Labs/utools-docs` repository. The comparison with Plana is based on this repository's current code and Flightdeck records.

## Executive conclusion

The valuable part of uTools is not its Electron plugin runtime. It is the user-facing command model: one summonable search field indexes small, named features; a feature may also match the user's current text, image, files, or active window; users can pin frequent commands or give them global shortcuts; and plugins can create personalized commands dynamically.

Plana should adopt that interaction model over its existing Action catalog, while retaining its own safer execution architecture:

- Keep **Action Packs** for declarative operations and the **out-of-process Plugin Host** for executable extensions.
- Replace the fragmented “Actions / Tool Groups / Chat” launch experience with a single **Command Palette** whose results may be Actions, plugin-contributed commands, Tool Groups, settings destinations, and Chat intents.
- Add typed input matching and command aliases, but keep all execution behind Plana's existing capability broker.
- Do not copy uTools' preload model, which intentionally releases web sandbox restrictions and allows Node.js filesystem/network/native access. Plana's per-plugin process and narrow capability-mediated protocol are a better security boundary.

## How uTools works

### 1. Search and command entry model

uTools is summoned with `Alt+Space`; the main input searches installed plugin features and built-in items. Chinese commands automatically support pinyin and pinyin-initial lookup, so a plugin declares human terms rather than duplicating transliterations. Users may pin commands to the search panel or “super panel,” group pinned icons into folders, and assign a global hotkey to a command. [Official setup guide](https://www.u-tools.cn/docs/guide/uTools-setup.html) [Official `plugin.json` reference](https://www.u-tools.cn/docs/developer/information/plugin-json.html)

The important unit is a **feature**, not an application. A plugin may contribute many independently searchable features, each with a stable `code`, explanation, optional icon, and one or more commands (`cmds`). On entry, uTools passes the selected feature code to the plugin so one package can dispatch to the correct behavior. [Official `plugin.json` reference](https://www.u-tools.cn/docs/developer/information/plugin-json.html)

### 2. Manifest and feature commands

Every ordinary UI plugin declares `plugin.json` with an HTML `main`, required logo, optional `preload`, and `features`. Each feature has a unique `code`, optional `explain` and icon, and a non-empty command list. `mainHide` supports immediate/background-like commands without reopening the search UI; `mainPush` allows a plugin to contribute result candidates back into the main search list. Plugin UI is HTML/CSS/JavaScript; uTools also offers no-UI and standardized list/document templates for smaller tools. [Manifest reference](https://www.u-tools.cn/docs/developer/information/plugin-json.html) [Template plugin reference](https://www.u-tools.cn/docs/developer/information/window-exports.html) [Plugin structure reference](https://www.u-tools.cn/docs/developer/information/file-structure.html)

The current manifest also supports `tools`: JSON-Schema-described functions registered at runtime with `utools.registerTool`, including UI-less plugins intended only for AI agents. This is directly relevant to Plana's Chat: the same underlying capability can be made discoverable to a human command palette and, when explicitly eligible, to AI. [Manifest tools section](https://www.u-tools.cn/docs/developer/information/plugin-json.html) [Official AI tool API](https://www.u-tools.cn/docs/developer/utools-api/tools.html)

### 3. Keyword and input matching behavior

uTools separates literal feature commands from **matching commands**. A feature can match:

- text by regular expression (`regex`) or broadly (`over`), with length and exclusion bounds;
- pasted images (`img`);
- files/directories (`files`), constrained by file kind, extension, name pattern, and count;
- the current foreground window (`window`), constrained by application, title, or Windows window class.

When invoked, `onPluginEnter` receives the feature code, input type, typed payload, and source (`main`, panel, hotkey, or redirect). With `mainPush`, a plugin can return multiple title/text/icon candidates and either enter its UI or execute silently when one is selected. This is more capable than Plana's current text-only Action search and fixed zero-input Action invocation. [Manifest matching-command reference](https://www.u-tools.cn/docs/developer/information/plugin-json.html) [Official lifecycle/event API](https://www.u-tools.cn/docs/developer/api-reference/utools/events.html)

uTools also allows dynamic features through `getFeatures`, `setFeature`, and removal APIs. Its own documentation uses user-configured web shortcuts as the motivating example: personalized commands need not be known in the static manifest. [Official dynamic-feature API](https://www.u-tools.cn/docs/developer/api-reference/utools/features.html)

### 4. Permissions and security

The official preload design deliberately lifts ordinary web sandbox limits: preload code can use Node.js APIs and Electron renderer APIs to access files, cross-origin network resources, and local storage. Marketplace policy mitigates review risk by requiring preload and bundled third-party modules to remain readable—not minified, obfuscated, or bundled. Newer UPXS packages add encryption and developer-information signing to help identify the publisher and detect package tampering. [Official preload documentation](https://www.u-tools.cn/docs/developer/information/preload-js/preload-js.html) [Official changelog](https://www.u-tools.cn/docs/guide/changelog.html)

This is review/signing policy, not least-privilege runtime isolation. The official docs do not describe a per-capability runtime permission broker comparable to Plana's `url.open`, `file.open`, `process.launch`, `command.run`, and `script.run` grants. Therefore Plana should not infer safety from uTools' popularity or copy unrestricted Node access.

### 5. Installation, updates, and discovery

Users discover plugins through the official marketplace, which exposes description, ratings/comments, versions, installed-plugin settings, commands, and runtime status. Publishing goes through the developer tool, version metadata and screenshots, source-policy checks, and official review. Offline `.upx` packages can also be installed by dropping/copying them into the main input according to uTools' official docs repository. Current uTools versions use signed UPXS packages; background automatic plugin update is a membership feature as of v7.6.0. [Marketplace guide](https://www.u-tools.cn/docs/guide/plugin-store.html) [Publishing guide](https://www.u-tools.cn/docs/developer/basic/publish-plugin.html) [Official docs repository quick start](https://github.com/uTools-Labs/utools-docs/blob/master/docs/developer/welcome.md) [Official changelog](https://www.u-tools.cn/docs/guide/changelog.html)

### 6. Lifecycle and runtime behavior

Plugin entry and exit are explicit lifecycle events. `onPluginOut` distinguishes hiding to the background from killing the process; by default, pressing Escape hides a plugin and leaves it running for quick re-entry, while the installed-app view exposes an “end running” control. Plugins may be single-instance by default, may opt out, and can detach into independent windows. [Official event API](https://www.u-tools.cn/docs/developer/api-reference/utools/events.html) [Marketplace lifecycle guide](https://www.u-tools.cn/docs/guide/plugin-store.html) [Manifest settings](https://www.u-tools.cn/docs/developer/information/plugin-json.html)

The public documentation establishes lifecycle callbacks and Electron/preload execution, but does not establish strong process or privilege isolation between individual plugins. By contrast, Plana currently starts one `Plana.PluginHost` and one child process per executable plugin, performs a bounded handshake, caps messages, times out calls, and kills the process tree on protocol failure or shutdown.

### 7. Data and storage

uTools offers a local NoSQL document API (documents up to 1 MB), promise and synchronous variants, a localStorage-like `dbStorage`, and encrypted `dbCryptoStorage`. Users may enable cloud synchronization; the docs warn about cross-device document conflicts and recommend splitting state across documents. `onDbPull` reports remote changes while a plugin is running. [Official database API](https://www.u-tools.cn/docs/developer/utools-api/db.html) [Official event API](https://www.u-tools.cn/docs/developer/api-reference/utools/events.html)

Plana currently has host-owned `DesktopSettings` and no plugin-scoped storage contract. Giving plugins an unrestricted package/app-data path would be easy but would weaken portability, quotas, cleanup, and future sync. A brokered namespaced store is the better adaptation.

## Comparison with Plana today

| Concern | uTools | Plana today | Practical gap |
|---|---|---|---|
| Search unit | Feature command (`code`, aliases, icon, explanation) | Action descriptor; separate Actions, Tool Groups, Chat pages | No unified, summonable result model or aliases |
| Contextual input | Text/regex/image/files/window typed payloads | Actions are generally invoked without a typed query payload | Cannot say “open this URL with…” or route selected files |
| Personal quick commands | Dynamic features, pinned commands, global hotkeys, super-panel folders | User Actions, project launchers, Tool Groups | Good underlying data, but configuration and launch surfaces are fragmented |
| Declarative extensions | Manifest features and templates | Action Packs with typed adapters/capabilities | Plana is safer; metadata/discovery UX is thinner |
| Executable extensions | Electron web plugin + Node-capable preload | Per-plugin executable behind `PluginHost` and capability broker | Plana isolation is stronger; plugin UI and input contract are immature |
| Lifecycle | enter/out/detach, background retention, single/multiple instance | start/ready/fail/exit; disable tears down host | Add activation/deactivation and optional keep-warm policy, not arbitrary window ownership first |
| Storage | plugin DB, key/value, encrypted key/value, optional sync | Host settings only; plugin protocol has no storage API | Add namespaced brokered storage |
| AI integration | Manifest-declared JSON-Schema tools registered at runtime | Chat exists; plugin contributions become ordinary zero-input Actions | Need one typed command/tool descriptor with explicit AI eligibility |
| Distribution | Marketplace, review, versions, ratings, signed packages, updates | Folder import and managed directory; no catalog/update channel | Start with package metadata, signature/hash, update URL—marketplace later |

## Recommendation matrix

| uTools idea | Decision | Plana implementation |
|---|---|---|
| One global search entry | **Adopt** | Add a WinUI Command Palette reachable from the pet, tray, and configurable global hotkey. Search Actions, plugin commands, Tool Groups, settings, and Chat together. |
| Feature as the searchable unit | **Adopt** | Introduce a host-owned `CommandDescriptor` with stable ID, title, subtitle, icon key, aliases, source, input contract, capabilities, and execution target. Adapt current Actions into it rather than replacing Action Engine. |
| Pinyin/initial matching | **Adopt** | Index Chinese display names plus generated pinyin/initial tokens; never require pack authors to duplicate aliases. Preserve exact-ID and ordinary substring/fuzzy ranking. |
| Typed contextual matching | **Adapt** | First support `text`, `url`, `file`, `files`, and `folder`; later add image and foreground-window context. Compile/validate regexes in the host, bound input length/count, and pass a typed payload through Action/Plugin protocols. |
| Dynamic/user-created features | **Adopt** | Treat existing User Actions and project launchers as first-class dynamic commands. Let plugins contribute/remove commands through a versioned reconciliation message, not arbitrary mutation of global settings. |
| Pinning, folders, global hotkeys | **Adapt** | Rename/reframe Tool Groups as user-facing collections/favorites; allow pin/reorder and per-command hotkeys. Keep a compact pet launcher, but make it a view of the same catalog. |
| No-UI/list templates | **Adapt** | Provide host-rendered result/list/form surfaces for common plugins so extensions need not ship UI. Delay arbitrary embedded plugin UI until a clear use case and containment model exist. |
| `mainPush` provider results | **Adapt cautiously** | Allow bounded asynchronous result providers with cancellation, debounce, item limit, latency budget, and source label. Do not start every executable plugin on every keystroke. |
| Full Node/native preload access | **Reject** | Keep executable plugins out of process. All OS effects continue through typed, capability-checked host requests. |
| Readable-source marketplace policy | **Adapt** | Useful for a future reviewed catalog, but do not substitute it for runtime enforcement. Add package hashes/signatures and publisher identity before automatic updates. |
| Background-retained plugins | **Adapt** | Default to lazy start and idle shutdown; permit an explicit `background` capability only for real event-driven extensions. Show state and provide Stop/Restart. |
| Plugin-scoped DB/crypto store | **Adopt via broker** | Add versioned `storage.get/set/delete/list` requests, namespace by plugin ID, quotas, atomic writes, and OS-protected secret storage. Never expose Chat API secrets through ordinary plugin storage. |
| AI-callable tools | **Adopt with explicit opt-in** | Extend command metadata with JSON input/output schemas and `aiCallable`. The Chat agent sees only enabled tools whose capabilities the user approved; execution uses the same broker and confirmation policy as manual invocation. |
| Marketplace first | **Reject for now** | Improve local package import, metadata, diagnostics, signing/hash, and update manifests before building discovery/reviews/accounts. |

## Staged roadmap

### Stage 1 — make existing capabilities feel like one product

1. Build the Command Palette and a unified `CommandDescriptor` adapter over built-ins, User Actions, project launchers, Action Packs, plugin contributions, Tool Groups, settings destinations, and Chat.
2. Add icon fallback rules (source icon, kind icon, then a guaranteed complete Fluent glyph) so missing/cropped glyphs cannot produce blank command affordances.
3. Add aliases, pinyin/initial indexing, recent/frequency ranking with time decay, pinning, and keyboard-first execution.
4. Reframe Tool Groups as collections/favorites in the UI while preserving stored IDs and compatibility.

### Stage 2 — contextual quick tools

1. Version Action Pack and plugin contribution schemas for typed input contracts.
2. Support text/URL/file(s)/folder matching and pass `CommandInvocation { commandId, source, input }` to the existing Action Engine or Plugin Host.
3. Add bounded provider results with cancellation; ship a few host-owned examples such as URL open/search, file reveal, project open, and “ask Plana about this text.”
4. Add configurable global hotkeys and a “pin to pet launcher” action.

### Stage 3 — complete the extension platform

1. Add plugin-scoped storage and protected secrets through the broker.
2. Add lazy activation, idle shutdown, health/restart controls, and an explicit background capability.
3. Add package integrity/publisher metadata and opt-in update manifests. Only then consider a curated catalog.
4. Expose selected schema-described commands to Chat/AI, reusing the exact manual capability and confirmation path.

## Design guardrails

- One catalog, many surfaces: palette, pet, tray, collections, settings, and Chat should not maintain divergent command lists.
- Human and AI invocation share descriptors and policy, but `aiCallable` is never implied by “manually runnable.”
- Matching discovers candidates; it never grants capability or bypasses confirmation.
- Plugin UI is optional. Prefer host-rendered small-tool surfaces before embedding arbitrary web content.
- Performance budgets are part of the protocol: input providers must be cancellable, bounded, and observable.
- Preserve compatibility with current `user.action.*`, `user.launcher.*`, Action Pack IDs, Tool Group membership, and `plugin.invoke` contributions during migration.
