# PocketTavern.UWP Feature Parity Plan

## Constraints
- **Min SDK**: 10.0.15063 (Windows 10 Mobile - CANNOT increase)
- **Target SDK**: 10.0.16299
- **Language**: C# 8.0, XAML
- **NuGet**: MNTK 6.1.9, sqlite-net-pcl 1.6.292, SQLitePCLRaw 1.1.13, Newtonsoft.Json 13.0.3

## Feature Gap Analysis

### Phase 1: Core Chat Enhancements (HIGH priority)

#### 1.1 Message Swipes/Alternatives
- **Status**: UWP has no swipe support. Android has full swipe navigation for alternative AI responses.
- **Files to modify**:
  - `Models/ChatMessage.cs` - Add `List<string> Alternates` and `int CurrentAlternateIndex`
  - `ViewModels/ChatViewModel.cs` - Add swipe navigation methods, store alternates during generation
  - `Views/ChatPage.xaml` - Add swipe left/right indicators and gesture handling
- **Reference**: Android `ChatViewModel.kt` swipe logic, `ChatBubble.kt` swipe UI

#### 1.2 Author's Note
- **Status**: UWP has `ChatMessageMetadata` with NotePrompt/Interval/Depth/Position/Role fields but no injection logic. Android has full implementation.
- **Files to modify**:
  - `Models/ChatContext.cs` - Ensure AuthorsNote model is complete
  - `Services/PromptBuilder.cs` - Implement Author's Note injection at depth/position/interval
  - `ViewModels/ChatViewModel.cs` - Wire AuthorsNote into prompt building
  - `Views/ContextSettingsPage.xaml` - Add AuthorsNote editor UI
- **Reference**: Android `PromptBuilder.kt` authorsNote injection, `ContextSettingsScreen.kt`

#### 1.3 Advanced PromptBuilder
- **Status**: UWP PromptBuilder.cs is a stub (only `IfBlank` extension). Android has full engine with WI scanning, token budget, recursive scanning.
- **Files to modify**:
  - `Services/PromptBuilder.cs` - Complete rewrite: context template assembly, WI keyword scanning, secondary keys, probability, recursive scanning, token budget enforcement, macro substitution
  - `ViewModels/ChatViewModel.cs` - Refactor to use PromptBuilder instead of inline prompt logic
- **Reference**: Android `PromptBuilder.kt` (full implementation)

### Phase 2: Group Chat System (HIGH priority)

#### 2.1 Group Storage
- **Status**: Group model exists. No storage class.
- **Files to create**:
  - `Data/GroupStorage.cs` - CRUD for groups (JSON files in LocalFolder)
- **Reference**: Android `CharacterStorage.kt` pattern, UWP `ChatStorage.cs` pattern

#### 2.2 Group Pages
- **Status**: No group pages exist.
- **Files to create**:
  - `Views/GroupsPage.xaml/.xaml.cs` - Group list, create group dialog
  - `Views/GroupChatPage.xaml/.xaml.cs` - Group chat interface
  - `ViewModels/GroupsViewModel.cs` - Group list management
  - `ViewModels/GroupChatViewModel.cs` - Group chat logic with multi-character generation
- **Reference**: Android `GroupsScreen.kt`, `GroupChatScreen.kt`

#### 2.3 Group Navigation
- **Files to modify**:
  - `Views/MainPage.xaml` - Add Groups entry point
  - `Services/NavigationService.cs` - Add group routes

### Phase 3: Image Generation (HIGH priority)

#### 3.1 Image Generation Service
- **Status**: `ImageGenModels.cs` has backend enum + config + capabilities models. No service implementation.
- **Files to create**:
  - `Services/ImageGenService.cs` - Abstract backend + 6 implementations:
    - `SdWebuiBackend` (SD WebUI / Forge)
    - `ComfyUiBackend` (auto-build workflow)
    - `DalleBackend` (OpenAI DALL-E 2/3)
    - `StabilityBackend` (Stability AI)
    - `PollinationsBackend` (Pollinations)
    - `HuggingFaceBackend` (HuggingFace Inference API)
- **Reference**: Android `ImageGenBackend.kt`, `SdWebuiBackend.kt`, `ComfyUiBackend.kt`, etc.

#### 3.2 Image Gen UI Integration
- **Files to modify**:
  - `Views/ImageGenSettingsPage.xaml` - Ensure all backend configs are present
  - `ViewModels/ImageGenSettingsViewModel.cs` - Wire to ImageGenService
  - `ViewModels/ChatViewModel.cs` - Add image gen trigger methods
  - `Views/ChatPage.xaml` - Add image gen trigger button
- **Reference**: Android `ImageGenSettingsScreen.kt`, `ChatScreen.kt`

### Phase 4: SillyTavern Migration Wizard (MEDIUM priority)

#### 4.1 ST Import
- **Status**: Nothing exists in UWP.
- **Files to create**:
  - `Services/StImportService.cs` - Import from ST server (HTTP) or local folder (SAF-like)
  - `Views/StImportPage.xaml/.xaml.cs` - Wizard UI
  - `ViewModels/StImportViewModel.cs` - Import logic
- **Reference**: Android `StImportScreen.kt`, `StImportViewModel.kt`

#### 4.2 ST Import Navigation
- **Files to modify**:
  - `Views/SettingsPage.xaml` - Add "Import from SillyTavern" entry
  - `Services/NavigationService.cs` - Add ST import route

### Phase 5: Native Extensions Enhancement (MEDIUM priority)

#### 5.1 Built-in Extensions
- **Status**: QuickReply and Regex exist as settings pages. Not wired as native extensions.
- **Files to create**:
  - `Services/NativeExtensions/QuickReplyExtension.cs` - Native quick reply provider
  - `Services/NativeExtensions/RegexExtension.cs` - Native regex output filter
  - `Services/NativeExtensions/TokenCounterExtension.cs` - Live token estimation
- **Reference**: Android `QuickReplyExtension.kt`, `RegexExtension.kt`, `TokenCounterExtension.kt`

#### 5.2 Extension Manager
- **Files to create**:
  - `Services/ExtensionManager.cs` - Coordinate native + JS extensions (unified dispatcher)
- **Files to modify**:
  - `Services/JsExtensionHost.cs` - Wire into ExtensionManager

### Phase 6: Platform & Quality (MEDIUM priority)

#### 6.1 Generation Keep-Alive
- **Status**: UWP has no keep-alive. Android uses foreground service with wake lock.
- **Approach**: Use `MaintenanceTrigger` background task or `Application.Current.Suspending` event handler to request deferral during generation.
- **Files to modify**:
  - `ViewModels/ChatViewModel.cs` - Add suspension deferral during generation
  - `App.xaml.cs` - Register background task if needed

#### 6.2 Chat Completion Refactoring
- **Status**: `LlmService.cs` is monolithic (431 lines, all backends in one file).
- **Files to refactor**:
  - `Services/LlmService.cs` - Extract per-backend methods into partial class or helper classes
- **Note**: LOW priority, functional but messy

## Implementation Order

1. **Message Swipes** (~30 min) - Small model change, big UX win
2. **Author's Note Injection** (~45 min) - Model exists, needs wiring
3. **PromptBuilder Rewrite** (~2 hr) - Core functionality, biggest impact
4. **Group Storage + Pages** (~3 hr) - New feature, models exist
5. **Image Generation Service** (~3 hr) - Models exist, needs backends
6. **ST Migration Wizard** (~2 hr) - New feature
7. **Native Extensions** (~1.5 hr) - Enhancement
8. **Keep-Alive** (~30 min) - Platform fix
9. **LlmService Refactor** (~1 hr) - Code quality

## Files Summary

### New Files (estimated)
```
PocketTavern.UWP/
  Data/GroupStorage.cs
  Services/ImageGenService.cs
  Services/StImportService.cs
  Services/ExtensionManager.cs
  Services/NativeExtensions/QuickReplyExtension.cs
  Services/NativeExtensions/RegexExtension.cs
  Services/NativeExtensions/TokenCounterExtension.cs
  Views/GroupsPage.xaml + .xaml.cs
  Views/GroupChatPage.xaml + .xaml.cs
  Views/StImportPage.xaml + .xaml.cs
  ViewModels/GroupsViewModel.cs
  ViewModels/GroupChatViewModel.cs
  ViewModels/StImportViewModel.cs
```

### Modified Files (estimated)
```
PocketTavern.UWP/
  Models/ChatMessage.cs (swipes)
  Models/ChatContext.cs (authors note)
  Services/PromptBuilder.cs (full rewrite)
  Services/LlmService.cs (image gen methods)
  ViewModels/ChatViewModel.cs (swipes, authors note, prompt builder, image gen)
  Views/ChatPage.xaml (swipe UI, image gen button)
  Views/ContextSettingsPage.xaml (authors note editor)
  Views/MainPage.xaml (groups entry)
  Views/SettingsPage.xaml (ST import entry)
  Views/ImageGenSettingsPage.xaml (backend configs)
  ViewModels/ImageGenSettingsViewModel.cs (wire to service)
  Services/NavigationService.cs (new routes)
  App.xaml.cs (group storage singleton, keep-alive)
```
