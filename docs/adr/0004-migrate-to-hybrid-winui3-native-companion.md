# Migrate to a hybrid WinUI 3 and native Companion architecture

Plana Desktop will move to .NET 10. WinUI 3 owns ordinary application surfaces such as Settings, Action search, Tool Group editing, Action Pack and Plugin management, localization, and dialogs. The transparent Companion remains a separate native Win32/Composition window hosting the existing Spine renderer through a composition-capable WebView2 adapter.

An ordinary WinUI 3 Window with WinUI WebView2 is rejected for the Companion because that hosting path does not support the required transparent background. Keeping WPF indefinitely is also rejected as the target architecture because repeated control-center work requires third-party theming and custom templates to approximate the intended native Fluent product.

Migration is side by side. `Plana.Core`, Action Engine, Plugin Host/protocol, settings schema, and renderer assets remain authoritative. The WPF executable stays runnable as the behavioral baseline until the WinUI control center and native Companion independently pass renderer, click-through, settings, Action execution, and publish smoke checks.

JSON remains the settings store. SQLite is deferred until the product owns durable history, a large queryable catalog, or concurrent writers.
