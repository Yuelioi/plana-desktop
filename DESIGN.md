# Plana Desktop design system

## Direction

Plana Desktop pairs an expressive transparent character with quiet Windows-native controls. The character is the playful surface; operational UI stays compact, familiar, and task-oriented. Do not recreate the former dense WPF settings form or place large controls beside the character.

## Surfaces

- The Companion is a transparent always-on-top Win32/Composition window. The Spine character owns most of the canvas.
- The quick toolbar sits directly above the character in two compact rows: AI conversation/settings/collapse first, Tool Group/Action/run second. Collapsing hides the complete second row.
- The control center is a conventional WinUI 3 window with Mica, a left NavigationView, one content column, and native Fluent controls.
- Dialogs are reserved for short creation tasks such as a new Action or Tool Group. Persistent management belongs on a page.

## Color and material

- Control center colors come from WinUI semantic theme resources so light, dark, contrast, hover, focus, and disabled states remain native.
- Interactive accent: Plana blue (`#4E6FD8`) or the active Windows accent where the native control supplies it.
- Companion toolbar: near-black blue (`rgba(20,24,34,.88)`), white text, one blue primary button role, and a single subtle light border.
- Transparency is functional around the character, not decorative glass inside ordinary pages.

## Type and spacing

- Use Segoe UI Variable/Segoe UI and WinUI type resources.
- Page titles are 28 px semibold; section titles are 18 px semibold; body text follows native defaults.
- Control-center page padding is 32 px with 24–28 px between major sections and 6–12 px inside related groups.
- Toolbar controls are 24–26 px tall with 3–5 px gaps; labels stay concise enough for the narrow companion width.

## Interaction rules

- Single and double click are configurable interactions. Right click always opens the context menu.
- Transparent companion regions return `HTTRANSPARENT`; the character and toolbar return `HTCLIENT`.
- Character drag begins only after the renderer's movement threshold. Toolbar fields remain directly interactive.
- Settings persist immediately. Scale, topmost, interaction bindings, personal Actions, and Tool Groups refresh the running Companion without opening the storage format.
- Enter submits the AI prompt; the response appears in a compact speech panel. The toolbar run button executes the selected Action directly.

## Content and localization

- UI ships English and Simplified Chinese. Stable Action, Pack, Plugin, capability, and protocol IDs are never localized.
- Controls name the action they perform. Empty states explain the recovery: search again, create an Action, or open the managed extension folder.
- Diagnostics state the failing Pack/Plugin and its error; they do not terminate the Companion.

## Accessibility and quality floor

- Preserve native keyboard navigation, visible focus, semantic labels, and theme contrast.
- Never encode state by color alone; toggles, labels, and diagnostics carry explicit state.
- Avoid nested cards, decorative gradients, oversized headers, and duplicate page/window titles.
- Every release must verify the WinUI window, the transparent Companion, toolbar clipping, hit-through corners, model input, single-instance behavior, and both languages.
