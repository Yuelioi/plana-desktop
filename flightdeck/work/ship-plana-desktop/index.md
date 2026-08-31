# Ship Plana Desktop

**Status:** Open

## Goal

Deliver a usable, localization-ready Windows desktop companion that renders Plana with Spine, stays out of the user's way, supports configurable interactions, safely invokes extensible Actions from declarative Action Packs, and isolates executable Plugins behind a narrow out-of-process protocol.

## Current

Work is paused by user request. The repository builds and the WinUI control center direction is acceptable, but the native Companion architecture is not accepted as a production baseline. Combining a transparent Win32 window, WebView2 Composition rendering, HTML toolbar/input, native hit testing, WinForms tray, and cross-boundary keyboard/mouse forwarding has produced recurring cursor, drag, focus, menu-lifetime, and settings-refresh failures. Do not resume by patching this stack further.

The host is now localization-ready. Action Packs can open HTTP(S) URLs, files, folders, and applications or run bounded commands and explicitly interpreted scripts; executable/script files cannot hide behind `file.open`. A visible Starter Pack ships with every build. The Plugins page discovers, imports, starts, disables, and diagnoses executable extensions through the out-of-process Plugin Host. No Plugin trust-review or hash-approval workflow remains.

The side-by-side migration is complete. `Plana.ControlCenter` is the packaged single-instance WinUI 3 application; `Plana.Companion.Native` is the production Companion; reusable modules and Plugin Host target .NET 10; Core also preserves the WPF net8 rollback reference. The solution passes 28 tests and builds with zero warnings or errors.

The Actions page now searches the complete Action catalog and runs Actions directly. Users can create persisted Windows Terminal project launchers with a project folder and ordered custom arguments, including `{folder}` substitution and direct `codex` startup. Automatic bounded discovery across project roots remains next.

Users can also add personal typed Actions directly from the UI without creating a pack. Website, file, folder, application, command, and script Actions are persisted, searchable, runnable, bindable, and available from the Companion toolbar.

The domain model reserves right-click for the Context Menu and limits configurable Interactions to click/double-click. Idle is future Automation and cannot launch external Actions. Pack/Plugin pages provide folder import, managed-folder access, reload, source visibility, and a bundled sample; valid Plugins start automatically unless disabled.

Valid enabled Plugins launch one-per-Host, complete a bounded identity/API handshake, contribute declared Actions, and execute them through serialized requests. Typed broker requests for URL, file, folder, process, command, and script capabilities reuse existing adapters. Failure/cancellation tears down the session and removes contributions. The published sample passes lifecycle, contribution, invocation, and broker flow end to end.

## Next

Before implementation resumes, research mature Windows desktop-pet architectures and compare at least: direct native Spine rendering, a proven game/rendering host with native overlay controls, and a split-process Companion plus WinUI tool palette. Build disposable proofs for transparent rendering, alpha hit testing, drag, text input, IME, tray, and DPI before selecting a replacement. Treat the existing native Companion as evidence and anti-reference, not as the production path.

## Current execution

Paused — architecture re-evaluation required before further product work.

## Progress

- Validated WPF + WebView2CompositionControl with the existing Spine renderer in a throwaway prototype.
- Created the production solution with `Plana.Core`, `Plana.Desktop`, and Core interface tests.
- Recorded the WPF/WebView2 and declarative Action Pack architecture decisions.
- Implemented Action Pack loading, validation, capability authorization, and dispatch.
- Implemented host adapters for animations, HTTP(S) URLs, applications, and bounded command execution.
- Implemented the transparent companion window, approximate native click-through, drag movement, tray menu, and placement persistence.
- Migrated model and Spine runtime assets into this repository; builds have no cross-project references.
- Verified 4/4 Release tests, framework-dependent win-x64 publish, actual Spine rendering, and native `HTTRANSPARENT`/`HTCLIENT` behavior.
- Initialized the repository on the `main` Git branch and captured the production baseline in Git; no remote is configured yet.
- Implemented the single-instance control center, persisted pack enablement and scale, non-fatal pack diagnostics, missing-Action states, and live settings application; verified 5/5 Core tests, a warning-free Release build, standard win-x64 publish, and launch from the standard published path.
- Added English-first localization resources; typed file, folder, and script Actions; executable-file separation; Plugin manifest validation; and the supervised Plugin Host protocol skeleton. Verified 16/16 tests, warning-free Release build, combined publish, and launch of the latest Desktop package.
- Fixed the invisible-extension release by bundling a Starter Action Pack, rescanning packs whenever Settings opens, and adding recursive Plugin discovery plus a visible Plugins diagnostics tab. Verified the exact repro green, 17/17 tests, combined publish, and launch from the standard path.
- Added searchable Action execution and persisted custom Windows Terminal project launchers with safe ordered arguments and `{folder}` substitution; verified the official `wt -d` contract, Release build/publish, and launch of the 19:16 standard package.
- Replaced the launcher-only limitation with a generic `Add custom Action` editor covering six host capabilities; verified the exact red/green feature check, clean UI detector, 17/17 tests, combined publish, and launch of the 19:25 standard package.
- Rebuilt the control center on WPF UI Fluent resources and fixed screenshot-confirmed Actions overlap by separating both editors from the full-height searchable list; verified Release build/runtime, zero layout-detector findings, combined publish, and launch of the 19:43 package.
- Added live persisted English/Simplified-Chinese switching across Fluent UI, editors, status/validation text, tray, built-in Actions, and capability prompts; verified 110/110 resource parity, 17/17 tests, clean UI detector, combined publish, and launch of the 20:06 package.
- Corrected the Interaction/Automation model, reserved right-click, restricted future Automation to ambient animation, and added validated import/open-folder/reload workflows plus honest sourcing guidance for Action Packs and Plugins. Verified 19/19 tests, 128/128 bilingual resources, clean UI detector, publish, and launch of the 20:21 package.
- Connected approved Plugins to per-Plugin Hosts with bounded identity/API handshake, live lifecycle diagnostics, reconciliation and process-tree cleanup; added protocol tests and a published zero-capability sample verified end to end. Verified 25/25 tests, 152/152 bilingual resources, publish, and launch of the 21:05 package.
- Added capability-checked Plugin Action contribution, ordinary Action Engine integration, serialized invoke/result transport, deadline/cancellation teardown, dynamic catalog removal, and a published sample verified end to end through contribution and execution. Verified 28/28 tests, publish, and launch of the 21:33 package.
- Completed typed Plugin host brokering for URL/file/folder/process/command/script adapters with dual manifest+Action capability checks and no host-object exposure; upgraded the sample and verified the real hostRequest/hostResponse/result chain. Verified 29/29 tests, publish, and launch of the 22:02 package. Stage 5 complete.
- Reworked the Fluent control center into a left-navigation, single-content-panel shell with theme-token typography/surfaces and a proper primary save action while preserving page behavior and scroll ownership. Verified 29/29 tests, runtime startup, zero layout-detector findings, publish, and launch of the 23:10 package.
- Removed the screenshot-confirmed duplicate inner title/description and applied explicit Fluent button hierarchy across create/import/run/reload/review/remove/disable/save actions. Verified 29/29 tests, runtime startup, zero layout findings, publish, and launch of the 23:37 package.
- Fixed screenshot-confirmed CTA hierarchy and invisible Save styling: `Add Action` now precedes the secondary `Project launcher`, while Add/Save explicitly use Fluent accent and on-accent tokens. Verified the exact red/green source check, 29/29 tests, 154/154 bilingual resources, publish, and launch of the 00:15 package.
- Replaced selectable Pack/Plugin lists with non-selectable themed card collections, exposed bundled/installed Pack origins with real-location access, and added one-click sample Plugin import. Verified the exact red/green source check, 29/29 tests, 158/158 bilingual resources, publish, and launch of the 00:28 package.
- Removed Plugin trust review, package hashing, approval snapshots, expiry logic, review UI, and related tests. Valid discovered Plugins now start automatically unless present in the ordinary disabled list. Verified the exact removal/auto-run check, 25/25 tests, 142/142 bilingual resources, publish, and launch of the 00:59 package.
- Added a bilingual Companion Quick Rail for Settings, focused Action search, and Hide with a dedicated native hit-test rectangle that preserves click-through elsewhere. Verified 25/25 tests, runtime/source assertions, UI detector, publish, and launch of the 01:08 package.
- Replaced the left Quick Rail with a two-row top Quick Toolbar, preserved model size/placement, added query handoff, configurable persisted Tool Groups, group Action selection/run, and a full management window. Updated pet hit testing to WebView coordinates. Verified 25/25 tests, 161/161 resources, runtime/source assertions, layout detector, publish, and launch of the 01:21 package.
- Approved the hybrid migration: .NET 10 reusable modules, WinUI 3 application surfaces, and a native Win32/Composition Companion. Recorded ADR 0004 and the migration Slice; toolchain audit found .NET 10 and WinUI templates missing, while the WPF baseline remains runnable.
- Installed .NET SDK 10.0.400 and official WinUI templates; retargeted reusable projects, moved settings into Core, added the WinUI 3 control center with real Action search and persisted companion/language settings, and launched the packaged app. Verified 25/25 tests and a six-project, zero-warning Release build.
- Replaced the WinUI Action placeholders with a persisted six-kind Action editor and real execution for URL/file/folder/process/command/script Actions, including custom Terminal arguments. Renamed the installed package to Plana Desktop, rebuilt warning-free, and launched PID 10860.
- Registered `plana://settings` and `plana://actions?query=...`, routed the existing Companion toolbar/tray entry through it with WPF fallback, verified real protocol activation, published the latest host, and launched one current Control Center plus the updated Companion.
- Added the UI-neutral `ICompanionSurface` contract and a separate .NET 10 raw Win32 native Companion project. Verified layered transparency, topmost placement, native drag/hit-through logic, DPI/scale application, five-second runtime survival, and the seven-project zero-warning build; the temporary GDI placeholder was closed after verification.
- Completed the native production cutover: WebView2 Composition renders the real Spine model; the compact toolbar supports search/settings/groups/direct run; tray, interactions, startup, live settings, Packs, and out-of-process Plugins work on the .NET 10 host. Added WinUI Extensions and Tool Group management, single-instance activation, clean native/legacy publish separation, runtime hit-test and Plugin Host verification, and final screenshots. Stage 6 complete.
- Corrected post-cutover UX and persistence defects: moved Settings to the first normal navigation tab, removed framework/footer copy, made the toolbar light translucent, hid ungrouped Pack Actions, and made atomic settings writes resilient to the native file watcher. Added a deterministic lock-contention regression test; 26/26 tests pass.
- Removed the remaining runtime deadlock: Core settings I/O no longer captures the WinForms UI context, polling reliably detects atomic replacements, WebView refresh never blocks the message loop, ContentDialogs bind an explicit XamlRoot, and exit synchronously terminates Plugin Hosts before ending the process. Verified live 110%→140% resize, responsive state, complete exit, and 27/27 tests.
- Completed the bilingual editors and Tool Group state model: every Action field/type/placeholder is localized; existing groups load their saved name and selections; + starts a new group; Save updates or creates; Delete removes. Normalized legacy raw Action IDs so already-created groups populate and run without recreation. Verified 28/28 tests.
- Fixed the native context-menu lifetime crash: the menu is reused for the Companion lifetime and is no longer disposed from its own `Closed` event while WinForms is still unwinding the click. Republished and smoke-checked right-click without a .NET error dialog; 28/28 tests and the full build remain green.
- Replaced toolbar Action search with AI conversation, Enter-to-send, Settings, and a clear Fluent collapse control for the Tool Group row. Added local Codex CLI subscription and OpenAI-compatible API providers, model/base-URL/API-key-environment settings, response/error speech panel, and explicit cursor ownership. Verified the installed Codex CLI subscription path returns `OK` in ephemeral read-only mode.
- Separated Actions and Tool Groups into dedicated searchable WinUI pages. Actions now use a Name/Description/Type table with edit/delete/run controls and a bilingual create/edit form. Tool Groups have their own search, create/edit/delete flow and allow empty groups. Replaced renderer-dependent drag with native mouse capture and `SetWindowPos`; runtime probe verified +30/+20 movement and exact restoration. Cursor probe verified the model uses the system arrow rather than the busy cursor.

## References

- [Product and delivery context](context.md)
- [Delivery plan](plan.md)
- [Domain language](../../../CONTEXT.md)
- [Action Pack format](../../../docs/action-packs.md)
- [WPF/WebView2 ADR](../../../docs/adr/0001-use-wpf-shell-with-web-renderer.md)
- [Action Pack ADR](../../../docs/adr/0002-declarative-action-packs-before-code-plugins.md)
- [Plugin system preview](../../../docs/plugin-system.md)
- [Plugin Host ADR](../../../docs/adr/0003-out-of-process-plugin-host.md)
- [Plugin ecosystem research](references/plugin-ecosystem-research.md)
- [WinUI 3 migration evaluation](references/winui3-migration-evaluation.md)
- [WinUI 3 migration Slice](slices/winui3-migration.md)
- [Hybrid migration ADR](../../../docs/adr/0004-migrate-to-hybrid-winui3-native-companion.md)
