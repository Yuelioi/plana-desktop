# Quick Launch UI research

## Decision

Quick Launch is an independent, transient WinUI window rather than a Control Center page. It opens
centered near the upper fifth of the active display, focuses one search field, and closes after a
successful command or with Escape. `Ctrl+Alt+Space`, the tray command, and the Companion quick dock
all open the same `plana://commands` route.

The resting surface contains only a large search field and one horizontally scrollable row of pill
filters: All, My actions, Projects, Tool groups, System, and Extensions. Matching commands use the
native `AutoSuggestBox` suggestion flyout, so an idle launcher does not become a management page.

## Evidence

- Listary describes its launcher as lightweight, keyboard-first, and dismissed after selection:
  <https://www.listary.com/feature/minimal-launcher>
- Listary documents hotkey-driven invocation as a primary path: <https://www.listary.com/help-center>
- WinUI `AutoSuggestBox` provides the native text-change, suggestion-selection, and query-submission
  interaction model used here:
  <https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.autosuggestbox>

## Constraints

- Search and execution continue to use the shared Core command catalog and action service.
- The Control Center remains the place to manage settings, actions, groups, and extensions.
- No permanent result list, sidebar, page title, or duplicate navigation appears in this surface.
