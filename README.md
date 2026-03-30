# PocketTavern UWP

> **⚠️ VERY EARLY ALPHA — Expect bugs, missing features, and rough edges. Not ready for general use.**

A Windows 10 UWP port of [PocketTavern](https://github.com/Starkka15/PocketTavern) — an AI character chat app originally built for Android.

Targets Windows 10 SDK 10.0.16299.0 (Fall Creators Update) with ARM support for Windows 10 Mobile.

---

## What works

- Character list, create/edit characters, avatar picker, PNG card import
- Full chat with LLM streaming (KoboldAI / text-generation-webui / OpenAI-compatible)
- Character settings (system prompt, persona, message examples, depth prompt)
- Theme system: Fire & Ice, Ember, Midnight Plum, Sand & Sea (animated background + music)
- Preset system: TextGen presets, OAI presets, Context/Instruct/Sysprompt templates
- CharaVault character search (chub.ai or self-hosted)
- All TextGen sampling parameters
- OAI preset with full prompt order editor
- Author's Note, Persona, Context Settings, Formatting, World Info (read-only)
- Chat history switcher, New Chat, Delete Chat
- Markdown rendering in messages (bold, italic, code, quoted dialogue)
- Recent chats with avatars

---

## Differences from Android

### Missing screens

| Feature | Status |
|---|---|
| Groups / Group Chat | Not started |
| Quick Reply settings | Not started |
| Regex settings | Not started |
| Image Generation settings | Not started |
| TTS settings | Not started |
| Debug Log | Not started |
| Connection Profile editing | Not started (list exists, edit/delete missing) |
| ST Import (SillyTavern batch import) | Not started |
| Extension panel UI | Not started |
| Profile / authentication screen | Not started |
| Setup / onboarding guide | Not started |

### Incomplete features

| Feature | What's missing |
|---|---|
| Chat | Streaming typing indicator, quick reply row, swipe gestures, image attachments, per-chat background |
| Chat menu | Upload Background, Image Gallery, Delete Character options |
| Characters screen | Groups tab, filter/sort options |
| Persona page | Multiple personas (create/select/delete) — currently single persona only |
| Extensions page | Per-extension settings UI (only enable/disable toggle exists) |
| Main screen | Theme audio, extension panels, update notifications |
| Settings hub | Flat list vs Android's grouped sections |

### Platform limitations

- `RadialGradientBrush` not available below Windows 10 1903 — icon glows are simulated with stacked ellipses

---

## Building

Requires Visual Studio 2017+ with the Universal Windows Platform workload and Windows 10 SDK 10.0.16299.0.

**x64 debug (local):**
1. Open `PocketTavern.UWP.sln`
2. Set configuration to `Debug | x64`
3. Build and deploy

**ARM release (Windows 10 Mobile):**
1. Set configuration to `Release | ARM` and build in Visual Studio
2. Run `PackAndInstallARM.ps1` (elevated PowerShell) — produces `PocketTavern_ARM.appx`
3. Deploy via Windows Device Portal or `Add-AppxPackage`

---

## License

Same license as the Android app.
