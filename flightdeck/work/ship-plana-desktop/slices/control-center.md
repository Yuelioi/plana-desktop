# Control center

## Deliverable

A normal taskbar-visible settings window, opened from the tray, that removes the need to edit JSON manually for everyday configuration.

## Current

The first control center is implemented as a normal taskbar-visible WPF window opened through a new tray `Settings` item and reused as a single instance. Its Companion, Interactions, and Action Packs tabs edit topmost behavior, persisted scale, bindings, pack enablement, and capability revocation; startup is shown as an explicit disabled placeholder. Saving applies the live settings and rebuilds the Action catalog.

Pack discovery now preserves per-manifest parse and validation diagnostics, so one malformed pack no longer suppresses valid packs. Disabled and invalid packs remain visible, and bindings to missing Actions have an explicit option. Five Core tests pass and the Release solution builds without warnings. After the user exited the old process, the framework-dependent win-x64 package published successfully to the standard `artifacts/win-x64` directory and that executable launched successfully from the published path.

The source-level finish review passed after invalid packs were made non-toggleable and the retained window was refreshed after live pack reloads. Screenshot-dependent layout, contrast, and focus checks remain unassessed. Dirty-state feedback for staged edits and refresh-on-reopen after external pack-directory changes are non-blocking follow-ups.

The earlier release looked unchanged because `%LOCALAPPDATA%\PlanaDesktop\packs` contained no manifests and the package supplied no starter content. The standard package now includes a valid `Plana Starter Pack` with URL, file, folder, and script examples. Opening Settings re-scans both bundled and user pack directories, and the control center includes an explicit Plugins tab. The deterministic visibility check changed from red to green after publish.

A user screenshot exposed a real Actions-page layout failure: the fixed launcher editor consumed the lower region and covered the Action list at the delivered window size. The editor was moved into its own scrollable focused window, alongside the existing custom Action editor; the Actions page now contains only search, two add buttons, and a list that owns all remaining height. The control center minimum/default size increased to 720×560/840×680. WPF UI 4.2 Fluent theme/control resources now replace raw stock WPF styling and provide coherent focus, light/dark/high-contrast primitives. Release build, runtime startup, layout detector, publish, and dependency presence checks pass.

The Companion page now exposes a persisted Language setting with `English` and `简体中文`. Saving replaces the culture overlay immediately, rebuilds code-generated rows and the Action catalog, and reconstructs the WinForms tray menu in the selected language. English remains the base resource fallback; the Chinese overlay currently covers all 110 English resource keys, including editor validation and capability-confirmation prompts. Release build, resource parity, UI detector, combined publish, and runtime startup are green.

The Interaction model was corrected after user review: only click and double-click are configurable. Right-click remains the host-owned Context Menu, and idle is removed from Interaction configuration because it is Automation and must never launch external desktop Actions. `InteractionPolicy` enforces these rules and admits only capability-free Companion animations as future Automation candidates.

Action Packs and Plugins are no longer read-only directory reports. Both pages explain their role and offer folder import, managed-folder access, and reload. Imports validate required structure and reject duplicate IDs. Personal automation belongs in `Your Actions`; Plugins have no official catalog and valid Plugins start automatically unless disabled.

The Fluent shell received a structural visual pass. Five equal-weight top tabs were replaced by a fixed 184px left navigation rail and one elastic right content panel; hidden TabControl chrome preserves the existing page implementation and keyboard content order without duplicating location state. Navigation/content/background/text/save controls now use WPF UI theme tokens and the primary appearance. Existing list scrolling and separate editor windows remain intact. Release runtime startup and the bounded layout detector pass; user-visible screenshot review remains the visual authority because automated capture is unavailable.

Screenshot review found the OS title bar already carried the product/settings identity, making the inner `Plana Desktop` heading and description redundant. That whole header row was removed so navigation and page content begin at the top of the client area. High-value buttons now use explicit WPF UI Primary/Secondary appearances, reload uses Transparent, Plugin review uses Caution, and remove/revoke/disable use Danger. Release build/runtime, 29/29 tests, layout detector, publish, and launch are green.

Another screenshot exposed two polish regressions: the Windows Terminal template preceded the generic Action CTA, and the Save Primary appearance rendered without a visible accent fill in the delivered theme combination. The Actions toolbar now leads with `Add Action` as an explicitly accented Primary button and renames the secondary template entry to `Project launcher`; Save now explicitly binds WPF UI accent/on-accent/border tokens. The source-level red/green check verifies CTA order and contrast tokens; 29/29 tests, 154/154 bilingual resources, UI detector, publish, and launch are green.

Action Pack screenshot review exposed a selectable-ListBox mismatch: clicking an informational card painted the entire row accent blue. Pack and Plugin collections now use non-selectable ItemsControls inside ScrollViewers, and cards use Fluent background/stroke tokens. Pack rows distinguish bundled versus managed-folder sources and open their actual manifest location, explaining why bundled packs are absent from `%LOCALAPPDATA%`. Plugins now offer one-click import of the published sample package. The exact source red/green check, 29/29 tests, 158/158 bilingual resources, UI detector, publish, and launch are green.

The Companion gained an always-visible compact Fluent Quick Rail at its left edge with Settings, Search, and Hide. Settings opens the Companion section; Search opens Actions, focuses, and selects the search field; Hide removes the Companion immediately. Native hit testing treats only the measured rail rectangle as interactive and preserves click-through elsewhere. Release runtime, source assertions for rail/navigation/focus/hit testing, 25/25 tests, UI detector, publish, and launch are green.

User review replaced that low-value left Quick Rail with a two-row Quick Toolbar above the character. The window adds a fixed 82px toolbar row while preserving the WebView/model height and translating persisted model-top placement, so the character does not shrink or move. Row one has Action search, search, and a compact Settings glyph; row two has Tool Group and Action dropdowns plus Run. Tool Groups are persisted user-named Action-reference collections managed from Actions via a bilingual create/rename/select/delete window. Missing Actions are ignored. Native pet hit testing now normalizes against WebView coordinates, and only the toolbar rectangle captures the new space. Runtime/source assertions, 25/25 tests, 161/161 resources, layout detector, publish, and launch are green.

User testing of a full-path application Action exposed that launches inherited Plana's working directory. Launch Actions now default to the executable's own directory when given a fully qualified path, while explicit working directories and script behavior remain unchanged; Control Center and Companion execution share the same tested factory. The Action Group editor now has live name/description search and preserves selections hidden by filtering. Core tests pass 64/64 and the full Release solution builds with zero warnings or errors.

## Next

Inspect the Fluent control center in the currently running standard package, especially Actions list height, both editor windows, narrow-window behavior, keyboard focus, and long arguments. Confirm or fix remaining material defects, then close this Slice. The automated Windows capture helper still fails, so user screenshots remain the visual truth source.

## Acceptance checks

- Tray menu contains `Settings` and reuses one control-center window instance.
- Companion settings expose always-on-top, startup behavior placeholder, scale, and reset-position controls.
- Interactions view lists supported Interactions and binds each to an available Action or none.
- Action Packs view lists discovered packs, enabled state, publisher/version, declared capabilities, and validation errors.
- Users can revoke a pack's granted capabilities.
- Saving updates live companion behavior without requiring a restart where practical.
- Long names, missing actions, malformed manifests, empty pack directories, and revoked capabilities have explicit states.
- Core tests cover the added pack-state behavior; Release build and publish remain green.

## Decisions

- The control center is a separate normal WPF window, not an overlay inside the transparent renderer.
- Editing raw manifest parameters remains an authoring concern; the first control center manages packs, bindings, and host settings rather than becoming a generic JSON editor.
- Pack discovery is diagnostic and non-fatal: every manifest produces a visible discovery result, while only enabled and valid packs enter the Action catalog.
- Scale is persisted as a multiplier over the companion's stored base dimensions and constrained to 75–150% in this first UI.
- WPF UI 4.2 supplies the Fluent operational-control system; editing forms are separate focused windows, never fixed panels competing with searchable list space.
