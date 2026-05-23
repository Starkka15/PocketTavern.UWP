# SPEC — PocketTavern UWP: Architecture Hardening

> Prior spec (parity T1–T21) fully complete. §V1–V24 remain binding — do not regress them.

---

## §G Goal

Harden PocketTavern.UWP runtime stability: fix threading safety (INPC on background thread), credential storage races (PasswordVault re-instantiation, vault/remove ordering), async deadlocks (.AsTask().Wait() on UI thread), background exception swallowing, JS sandbox injection and missing error handler, regex DoS, ZIP import overflow, stale-navigation in auto-continue, and lifecycle leaks (orphaned streaming tasks, TTS MediaElement).

---

## §C Constraints

- UWP C#/XAML, Windows 10+. No new NuGet packages.
- Solo developer — no new layers of abstraction unless the fix genuinely requires it.
- PasswordVault already integrated in SettingsStorage (Session 2). This spec hardens it further.
- All public API shapes (CharacterStorage, ChatStorage, LlmService, PromptBuilder) must remain stable.
- Must not regress any feature in T1–T21 or §V1–V24.
- Fixes in SettingsStorage must also apply to ConnectionProfileStorage where the same pattern exists.

---

## §I Interfaces

| id | surface | notes |
|----|---------|-------|
| I.chat | `ViewModels/ChatViewModel.cs` | streaming progress, auto-continue, Cleanup(), CTS lifecycle |
| I.settings | `Data/SettingsStorage.cs` | PasswordVault instance caching, atomic vault write |
| I.connprofile | `Data/ConnectionProfileStorage.cs` | same vault patterns — audit and apply same fixes |
| I.tts | `Services/OpenAiTtsProvider.cs` | `.AsTask().Wait()` / `.GetAwaiter().GetResult()` replacement |
| I.prompt | `Services/PromptBuilder.cs` | Regex.Replace timeout |
| I.charx | `Services/CharxParser.cs` | ZIP entry size guard before ToArray() |
| I.jshost | `Services/JsExtensionHost.cs` | NavigationFailed handler, JSON-serialized event dispatch |
| I.sum | `ViewModels/ChatViewModel.cs` + `SummarizeHistoryService.cs` | Task.Run exception handling |

---

## §V Invariants

| id | invariant |
|----|----------|
| V25 | INPC and ObservableCollection mutations raised from a background thread MUST be dispatched to the UI thread via CoreDispatcher or DispatcherQueue before firing |
| V26 | PasswordVault MUST be instantiated once per storage class (field or lazy property) — never inside per-call methods where concurrent calls race on separate instances |
| V27 | Removing a plaintext fallback key MUST only occur after the PasswordVault.Add() call returns without throwing — credential loss on vault failure is a bug |
| V28 | `async void` is permitted only on event handlers; every `async void` handler MUST wrap its body in try/catch and not let exceptions escape to the dispatcher |
| V29 | No `.AsTask().Wait()`, `.Result`, or `.GetAwaiter().GetResult()` on the UI thread — all callers in the async call chain MUST await |
| V30 | Every `Task.Run()` background body MUST catch `Exception` internally — unobserved task exceptions are silently swallowed and leave state corrupt |
| V31 | Every `Regex.Replace` / `Regex.Match` call on user-supplied text MUST supply a timeout ≤ 2 000 ms; `RegexMatchTimeoutException` MUST be caught and the input returned unchanged |
| V32 | CharxParser MUST check `ZipArchiveEntry.Length` before calling `ToArray()` — reject any entry whose uncompressed size exceeds 64 MB |
| V33 | JsExtensionHost internal WebView MUST handle `NavigationFailed` — reset script-loaded state and log to Debug; silence is indistinguishable from success |
| V34 | JS extension event payloads MUST be serialized with `JsonConvert.SerializeObject` — never string-interpolated; untrusted character names and message text must not escape the JSON string boundary |
| V35 | Auto-continue MUST capture the active chat ID before its delay and abort if that ID changed when the callback fires — stale navigation causes messages to land in the wrong chat |
| V36 | ChatViewModel.Cleanup() MUST cancel and await (or fire-and-forget with null-check) the in-flight generation CancellationTokenSource before the VM is released |
| V37 | TTS MediaElement MUST be stopped and disposed in ChatViewModel.Cleanup() — orphaned playback after page navigation is a resource leak and audible regression |

---

## §T Tasks

| id | status | description | cites |
|----|--------|-------------|-------|
| T22 | x | `ChatViewModel` streaming progress callback — wrap `IProgress<StreamEvent>` implementation so all INPC/collection mutations are posted to captured `CoreDispatcher` (capture in ctor or `OnNavigatedTo`) | V25,I.chat |
| T23 | x | `SettingsStorage` — replace per-call `new PasswordVault()` with a single `private readonly PasswordVault _vault = new PasswordVault()` field; audit `ConnectionProfileStorage` for same pattern and apply | V26,I.settings,I.connprofile |
| T24 | x | `SettingsStorage` — make vault writes atomic: wrap `_vault.Add()` in try/catch; only call `Remove(plaintext key)` inside the try after Add returns; rethrow or log on failure | V27,I.settings |
| T25 | x | `OpenAiTtsProvider` — replace all `.AsTask().Wait()` / `.GetAwaiter().GetResult()` with `await`; propagate async up the call chain (update callers to `async Task` as needed) | V29,I.tts |
| T26 | x | `ChatViewModel` background summarization — wrap the `Task.Run(() => { ... })` body in try/catch; on exception log to `System.Diagnostics.Debug` and leave `MemoryBlock` / `SummarizedTurnCount` unchanged | V28,V30,I.sum |
| T27 | x | `PromptBuilder` — add `TimeSpan.FromMilliseconds(2000)` timeout arg to every `Regex.Replace` / `Regex.Match` on user-supplied strings; catch `RegexMatchTimeoutException`, return input text unchanged | V31,I.prompt |
| T28 | x | `CharxParser` — before `entry.Open()` + `ms.ToArray()`, check `entry.Length > 64 * 1024 * 1024` and throw `InvalidDataException("entry too large")` | V32,I.charx |
| T29 | x | `JsExtensionHost` — subscribe to the internal WebView's `NavigationFailed` event in `Initialize()`; handler sets `_scriptLoaded = false` and writes to `System.Diagnostics.Debug` | V33,I.jshost |
| T30 | x | `JsExtensionHost` `DispatchEventAsync` — replace string-interpolated payload with `JsonConvert.SerializeObject(new { type, payload })` and inject the resulting JSON string via `InvokeScriptAsync` | V34,I.jshost |
| T31 | x | `ChatViewModel` auto-continue — capture `_currentChatId` (or equivalent key) into a local variable before the `Task.Delay`; after the delay, compare against current value and return early if changed | V35,I.chat |
| T32 | x | `ChatViewModel.Cleanup()` — add `_generationCts?.Cancel()` followed by null-assignment before releasing other state; ensure no streaming callback fires after Cleanup returns | V36,I.chat |
| T33 | x | `ChatViewModel.Cleanup()` — stop TTS: call `_ttsPlayer?.Stop()` (or equivalent `MediaElement.Stop()`) and dispose/null the player reference | V37,I.chat |
| T34 | . | Charx avatar not loading — `CharxParser` falls back to 1×1 white PNG when icon not found; need to audit actual ZIP entry paths (debug log added), and fix avatar display to treat 1×1 fallback as "no avatar" | I.charx |
| T35 | . | Charx sprites not loading — `SpriteStorage.GetFile` lookup uses `charAvatar` key but sprite dir may be keyed differently; verify sprites written to disk after charx import and confirm `GetFile` path matches | I.charx |
| T36 | . | In-chat image generation not working — `ImageUri` in `ChatMessage` builds a `file:///` URI from a relative `ImagePath` joined to `LocalFolder`; if path separator or sandboxing blocks `file:///` in `BitmapImage`, images never render | I.chat |
| T37 | . | CharaVault Card Search — "Login Failed: Automated access not permitted" — server rejects `User-Agent: PocketTavern/1.0` as a bot; fix requires allowlisting this UA on CharaVault server and/or sending a more realistic UA from the UWP client | I.chat |
| T38 | . | Image gen — add a built-in base negative prompt that is always prepended to the user-configured negative prompt, covering universal quality/anatomy defects (e.g. "worst quality, low quality, bad anatomy…") so users don't need to configure it from scratch | I.chat |

---

## §B Bugs

| id | date | cause | fix |
|----|------|-------|-----|
| B1 | 2026-05-22 | Android JSONL uses `mes`/`send_date`/`is_system` fields; UWP ChatStorage only read `content`/`timestamp`/`is_narrator` — all imported messages showed empty | Added field fallbacks in `ParseMessageJObject`; header line skip via `user_name` check |
| B2 | 2026-05-22 | Sprite regex only matched `<img src=name>` — RisuRealm cards emit `<img="name">`; tags showed raw in chat and sprite never updated | Extended `_spriteTagRegex` to match both formats; added tag-strip before storing to `Content` |
