# Companion chat UI research

Research date: 2026-09-01. Target: the always-on-desktop Plana conversation surface. User job: send one short prompt with normal Windows IME and read a transient response without opening the Control Center. Trigger: the initial Godot bubble had scaled/jagged text and a Renderer acknowledgement timeout could crash the Host.

## Source matrix

- [Microsoft TextBox guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/text-box): use a normal single-line text control for short plaintext entry, provide a clear placeholder, keep localization width in mind, and preserve the familiar edit/context-menu behavior.
- [Microsoft TeachingTip guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/teaching-tip): contextual floating information should be succinct and transient; the pattern may be used without a tail. It should not become a permanent history surface.
- [Microsoft Windows 11 rounded-corner guidance](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners): prefer platform window rounding; custom framing can lose automatic rounding, so the Host uses DWM corners on Windows 11 and a physical-pixel region fallback on Windows 10.
- [Windows App SDK samples](https://github.com/microsoft/WindowsAppSDK-Samples): Windows windowing/composition remains the reference for separately managed desktop surfaces and non-activating overlays.
- Production references reviewed: [DumbPet](https://dumbpet.com/) uses a conventional chat application window; [Vcot review](https://forest.watch.impress.co.jp/docs/review/1551986.html) shows a desktop mascot with a nearby transient text bubble. The latter fits Plana's always-present interaction better; the former is appropriate only when full chat history becomes a separate explicit task.

## Patterns

1. **Host-owned input, Renderer-owned character.** Text focus, IME, clipboard, selection, and accessibility stay in a native Host window. Never scale text with the character viewport.
2. **One-line composer.** The attached field handles short prompts, Enter-to-send, visible disabled/busy state, placeholder copy, and a single send affordance. It follows the model but is not part of the model hit shape.
3. **Transient response card.** Thinking, success, and error use a concise non-activating native card near the character. It auto-dismisses and remains mouse-transparent. It is not chat history.
4. **Physical-pixel typography.** Use Segoe UI and native text rendering. DWM/system geometry wins over hand-scaled bitmap or game-engine text.
5. **Failure remains local.** Bubble presentation never participates in AI request success. A missing/closed visual surface cannot throw an unhandled async UI exception.

## Local application

- Removed the Control Center Chat page; Settings keeps provider/model/API configuration only.
- Added `CompanionChatInput`, a Host-owned native single-line composer below the model.
- Added `CompanionSpeechBubble`, a Host-owned non-activating/mouse-transparent response window positioned near the model.
- Godot no longer renders text or acknowledges bubble commands; it renders only Plana and semantic performances.
- The existing AI provider path remains unchanged. Thinking/success/error still drive semantic Character Performance intents.

## Next step

If conversation history becomes necessary, expose it as an explicit full Control Center history surface opened from the bubble/input context—not as the default prompt flow and not inside the transparent Renderer.

## Latency finding

The configured provider is the local Codex subscription path. Replaying the app's minimal `codex exec --ephemeral` request took more than 30 seconds; forcing low reasoning took about 43 seconds, and `codex-mini-latest` was unavailable for the current subscription login. The latency is therefore dominated by CLI/model request startup rather than bubble/input rendering. Official OpenAI documentation describes [`codex-mini-latest`](https://developers.openai.com/api/docs/models/codex-mini-latest) as a fast Codex CLI model, but availability cannot be assumed for this user's subscription. The UI shows a persistent elapsed thinking state; true lower latency requires a provider/model available to the user, such as a configured OpenAI-compatible API model.
## Codex latency follow-up

The original subscription path launched `codex exec --ephemeral` for every message and measured
30–43 seconds even with low reasoning. Codex CLI 0.151.0 exposes a persistent stdio app-server. Its
locally generated v2 protocol schema defines `initialize`, `thread/start`, `turn/start`,
`item/completed`, and `turn/completed`.

A two-turn probe against one ephemeral, read-only, low-effort app-server thread measured 9.27
seconds for cold initialization plus the first answer and 3.75 seconds for the second answer. The
production Host now owns one app-server client for its lifetime and keeps the Plana conversation in
a single thread.

The first production attempt silently fell back because redirected app-server streams were not
explicitly UTF-8; Chinese JSON notifications became invalid after system-codepage decoding. A
published-binary probe reproduced the exception and verified the UTF-8 correction. On the same
probe, the default coding model took 22.95 seconds cold / 3.87 seconds warm, while
`gpt-5.6-luna` took 5.42 seconds cold / 2.71 seconds warm. Blank Codex model settings therefore
resolve to Luna for short Companion chat; explicit user model choices remain unchanged. The
pre-release application has no legacy CLI fallback: app-server failures surface directly in the
speech bubble so they cannot masquerade as slow responses.
