# WinUI 3 migration evaluation

_Primary-source review, 2026-09-01_

## Verdict

Move the product shell to .NET 10 and WinUI 3 only if Plana commits to a hybrid window architecture:

- **WinUI 3** owns Settings, Action search, Tool Group editing, Pack/Plugin management, dialogs, tray-facing management, localization, and other ordinary application UI.
- A separate **native Win32/Composition Companion window** owns transparency, always-on-top behavior, drag, per-pixel hit testing/click-through, and the Spine WebView renderer.

Do not rewrite the Companion as an ordinary WinUI 3 `Window` containing WinUI `WebView2`. Microsoft explicitly documents that WinUI 3 does not support transparent backgrounds in that hosting path. [Microsoft Edge WebView2: WinUI 3 transparency](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/winui3-windows-app-sdk)

## Platform facts

- .NET 10 is LTS through November 2028; .NET 8 support ends November 2026. Moving the reusable projects to `net10.0` is reasonable. [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- Microsoft recommends WinUI 3 for new native Windows desktop UI. It ships in the Windows App SDK and supports Windows 10 1809+ and Windows 11. [WinUI 3 overview](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- The current WinUI toolchain supports .NET 10 project templates and packaged, packaged-with-external-location, and unpackaged distribution. [Get started with WinUI](https://learn.microsoft.com/en-us/windows/apps/get-started/winui-get-started-overview)
- WinUI windows remain HWND-backed. C# can obtain their HWND through `WindowNative.GetWindowHandle` and use Win32 APIs. [WinUI 3 Win32 interop walkthrough](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/desktop-winui3-app-with-basic-interop)
- Win32 layered windows provide alpha-blended/per-pixel rendering and transparent hit testing; `UpdateLayeredWindow` is the native mechanism when the app supplies per-pixel alpha. [Win32 layered-window behavior](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)
- Windows App SDK can also be adopted incrementally by existing WPF/Win32 apps; a full rewrite is not required merely to use newer Windows APIs. [Windows App SDK overview](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- WinUI deployment is materially more involved than the current WPF folder publish. Unpackaged apps must arrange Windows App SDK runtime deployment/bootstrap, or ship self-contained output; self-contained output grows and native dependencies remain. [Unpackaged deployment](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps), [self-contained deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps)

## Architecture comparison

### Full WinUI 3

Good for ordinary windows, poor fit for the transparent Companion. The built-in WinUI WebView2 path does not supply the required transparent background. Forcing layered-window behavior onto the same XAML window would combine two rendering models at the most fragile seam. Reject.

### Hybrid WinUI 3 + native Companion

Best long-term fit. WinUI 3 provides modern controls and navigation without third-party WPF themes. A native top-level HWND uses Win32/Composition plus a WebView2 composition controller for the Companion. The windows can live in one process initially, but their interfaces stay independent. Recommend.

### Keep WPF

Lowest migration cost and already proves transparent rendering/click-through. It remains a valid fallback, but repeated visual work shows that the control center is paying an ongoing design-system tax through WPF UI and custom templates. Keep only if shipping speed is more important than establishing the intended Windows visual platform.

## What survives migration

- `Plana.Core`, its tests, Action Engine, Action Pack loader, interaction policy, file policy, and Plugin protocol are UI-independent and should move largely unchanged to `net10.0`.
- `Plana.PluginHost` and the example Plugin remain console/process modules; retarget to .NET 10.
- JSON settings models and existing user settings should be retained with additive migration. Keep JSON: current data is small, local, document-shaped, and atomically replaced. SQLite becomes worthwhile only for durable event/history data, large searchable catalogs, or concurrent writers.
- Process/URL/file/folder/command/script adapters are mostly portable. Replace only WPF dialogs and localization lookups.
- Spine HTML/runtime/model assets survive. The host interface changes from WPF `WebView2CompositionControl` to a native WebView2 composition adapter.

## What must be rewritten

- All WPF XAML windows and code-behind: Control Center, Action editor, Launcher editor, Tool Group manager, localization resource plumbing, capability dialogs, and the current Quick Toolbar.
- Companion HWND creation, composition/WebView attachment, hit testing, drag, placement, DPI/multi-monitor behavior, and toolbar anchoring.
- Tray/control-center lifecycle and deployment/packaging scripts.

## Recommended phases

1. Retarget `Plana.Core`, tests, Plugin Host, and example Plugin to .NET 10 without changing behavior.
2. Create a new WinUI 3 control-center shell and port one vertical slice: Settings + language + Actions search. Keep the current WPF executable runnable as the reference implementation.
3. Define a small `ICompanionSurface` interface and build a native Win32/Composition prototype proving transparent Spine rendering, per-pixel hit testing, drag, topmost, DPI, and the two-row Quick Toolbar.
4. Port remaining editors, Packs/Plugins, tray, and placement; migrate existing JSON settings.
5. Replace the WPF startup project only after renderer, click-through, settings, Action execution, and publish smoke tests pass side by side.

Do not introduce AI chat, Live2D, SQLite, or another plugin redesign during the migration. They are separate product decisions and would destroy the value of a controlled platform replacement.
