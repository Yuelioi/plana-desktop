# Product

<!-- impeccable:product-schema 1 -->

## Platform

adaptive

## Users

Plana Desktop serves ordinary Windows users who want a lightweight desktop companion and should not need to understand or edit JSON to configure everyday behavior.

## Product Purpose

Plana Desktop makes an expressive Spine character useful without letting it obstruct normal desktop work. Success means users can safely configure the companion, its interaction bindings, and declarative Action Packs through a clear graphical interface.

## Positioning

Plana combines a present, playful desktop character with user-controlled Actions and capability-scoped declarative extension packs, while keeping executable extensions outside the desktop process.

## Operating Context

The companion lives continuously on the Windows desktop, stays above ordinary windows when requested, passes clicks through empty areas, and is managed from its tray icon and a taskbar-visible control center.

## Capabilities and Constraints

- Windows is the primary and currently exclusive platform.
- The production host uses C# and .NET 10: WinUI 3 owns the control center, the .NET Host owns Windows integration, and the supervised Godot process owns transparent Spine rendering. Historical WPF/WebView implementations are archived and never selected at runtime.
- Users configure companion settings, Interaction bindings, Action Packs, and Plugins without editing JSON.
- Declarative Action Packs do not load executable code into the desktop process.
- Executable Plugins run outside the desktop process through a versioned, capability-mediated protocol.
- The host UI supports persisted live culture switching; the first release ships English and Simplified Chinese.
- Broken packs and failed Actions must remain recoverable and must not terminate the companion.

## Brand Commitments

The product name is Plana Desktop. The companion should feel present and playful while its control surfaces remain quiet, native, and task-oriented.

## Evidence on Hand

- The bundled Spine model and renderer assets are the product's primary character assets.
- Existing WPF companion, tray, Action Engine, Action Pack format, tests, and architecture records are implemented in this repository.
- No commercial claims, customer evidence, or performance benchmarks are established.

## Product Principles

- Keep routine configuration understandable without exposing storage formats.
- Preserve normal Windows expectations for settings, focus, taskbar, and tray behavior.
- Make permissions and failures explicit, local, and recoverable.
- Let the character feel playful while operational controls stay calm and precise.
- Apply safe configuration changes live whenever practical.

## Accessibility & Inclusion

Use native keyboard navigation, visible focus, readable text, explicit state labels, and controls that do not rely on color alone.
