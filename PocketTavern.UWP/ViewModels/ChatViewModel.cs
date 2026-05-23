using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Data;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;
using Windows.Storage;
using Windows.UI.Core;

namespace PocketTavern.UWP.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private readonly LlmService _llm = new LlmService();
        private readonly QuickReplyStorage _qrStorage = new QuickReplyStorage();
        private readonly RegexStorage _regexStorage = new RegexStorage();
        private readonly BackgroundStorage _backgroundStorage = new BackgroundStorage();
        private readonly TtsManager _ttsManager = new TtsManager();

        // ── State ─────────────────────────────────────────────────────────────

        private string _characterAvatar;
        private Character _character;
        private ObservableCollection<ChatMessage> _messages = new ObservableCollection<ChatMessage>();
        private string _inputText = "";
        private bool _isGenerating = false;
        private string _currentStreamingText = "";
        private string _chatFileName;
        private CancellationTokenSource _generationCts;
        private string _currentApiName = "";
        private string _currentModelName = "";
        private string _backgroundPath;
        private int _tokenCount;
        private bool _showTokenCount;
        private bool _showDeleteDialog;
        private bool _showGreetingPicker;
        private ObservableCollection<QuickReplyButton> _quickReplyButtons = new ObservableCollection<QuickReplyButton>();
        private ObservableCollection<string> _availableGreetings = new ObservableCollection<string>();
        private bool _isTtsSpeaking;
        private bool _isTtsEnabled;

        // Search
        private List<int> _searchResults = new List<int>();
        private int _searchIndex = -1;

        // Auto-continue
        private bool _autoContinueEnabled;
        private int _autoContinueMinLength = 200;
        private int _autoContinueCount;

        // Last-known persona name for multi-turn trimming
        private string _currentUserName = "User";

        // Serialize concurrent save operations to prevent file-in-use errors
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

        // UI thread dispatcher — captured in InitializeAsync; used to marshal progress callbacks safely
        private CoreDispatcher _dispatcher;

        // Long-term memory
        private string _memoryBlock = "";
        private int _summarizedTurnCount = 0;

        // Expression sprite
        private string _currentSpriteName;
        // Matches both ST-style <img src=(name)> and RisuRealm-style <img="name">
        private static readonly Regex _spriteTagRegex =
            new Regex(@"<img\s+src=\(?([^)>\s""]+)\)?>|<img=""([^""]+)"">",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ── Properties ───────────────────────────────────────────────────────

        public string CurrentApiName   { get => _currentApiName;   set => Set(ref _currentApiName,   value); }
        public string CurrentModelName { get => _currentModelName; set => Set(ref _currentModelName, value); }
        public string BackgroundPath   { get => _backgroundPath;   set => Set(ref _backgroundPath,   value); }
        public int    TokenCount       { get => _tokenCount;       set => Set(ref _tokenCount,       value); }
        public bool   ShowTokenCount   { get => _showTokenCount;   set => Set(ref _showTokenCount,   value); }
        public bool   ShowDeleteDialog { get => _showDeleteDialog; set => Set(ref _showDeleteDialog, value); }
        public bool   ShowGreetingPicker { get => _showGreetingPicker; set => Set(ref _showGreetingPicker, value); }
        public bool   IsTtsSpeaking    { get => _isTtsSpeaking;   set => Set(ref _isTtsSpeaking,   value); }
        public bool   IsTtsEnabled     { get => _isTtsEnabled;    set => Set(ref _isTtsEnabled,    value); }

        public string CurrentSpriteName
        {
            get => _currentSpriteName;
            set => Set(ref _currentSpriteName, value);
        }

        public Character Character
        {
            get => _character;
            set => Set(ref _character, value);
        }

        public ObservableCollection<ChatMessage> Messages
        {
            get => _messages;
            set => Set(ref _messages, value);
        }

        public string InputText
        {
            get => _inputText;
            set => Set(ref _inputText, value);
        }

        public bool IsGenerating
        {
            get => _isGenerating;
            set => Set(ref _isGenerating, value);
        }

        public string CurrentStreamingText
        {
            get => _currentStreamingText;
            set => Set(ref _currentStreamingText, value);
        }

        public ObservableCollection<QuickReplyButton> QuickReplyButtons
        {
            get => _quickReplyButtons;
            set => Set(ref _quickReplyButtons, value);
        }

        public ObservableCollection<string> AvailableGreetings
        {
            get => _availableGreetings;
            set => Set(ref _availableGreetings, value);
        }

        // ── Initialization ───────────────────────────────────────────────────

        public async Task InitializeAsync(string characterAvatar)
        {
            _dispatcher = Windows.UI.Xaml.Window.Current?.Dispatcher;
            _characterAvatar = characterAvatar;
            Character = await App.Characters.GetCharacterAsync(characterAvatar);
            if (Character == null) return;

            // Wire extension events
            App.Extensions.MessageSendRequested    += OnExtensionMessageSend;
            App.Extensions.ButtonSetsChanged       += OnButtonSetsChanged;
            App.Extensions.HiddenGenerateRequested += OnHiddenGenerateRequested;
            App.Extensions.ImageGenerateRequested  += OnImageGenerateRequested;
            App.Extensions.InsertMessageRequested  += OnInsertMessageRequested;

            // Load settings
            _autoContinueEnabled    = App.Settings.GetAutoContinueEnabled();
            _autoContinueMinLength  = App.Settings.GetAutoContinueMinLength();
            _currentUserName        = App.Settings.GetUserPersonaName().Trim();
            if (string.IsNullOrEmpty(_currentUserName)) _currentUserName = "User";

            RefreshQuickReplyButtons();
            RefreshApiIndicator();

            var ttsConfig = App.Settings.GetTtsConfig();
            IsTtsEnabled = ttsConfig.Enabled;

            BackgroundPath = _backgroundStorage.GetBackgroundPathOrNull(_characterAvatar);

            var chats = await App.Chats.GetChatInfosAsync(Character.Name);
            if (chats.Count > 0)
            {
                _chatFileName = chats[0].FileName;
                var chat = await App.Chats.LoadChatAsync(Character.Name, _chatFileName);
                NormalizeLoadedMessages(chat?.Messages);
                Messages.Clear();
                if (chat?.Messages != null)
                    foreach (var m in chat.Messages) Messages.Add(m);
                _memoryBlock = chat?.MemoryBlock ?? "";
                _summarizedTurnCount = chat?.SummarizedTurnCount ?? 0;
                await PushExtensionContextAsync();
                await App.Extensions.DispatchEventAsync("CHAT_CHANGED", JsonConvert.SerializeObject(_chatFileName));
            }
            else
            {
                await StartNewChatInternalAsync();
            }
        }

        public void Cleanup()
        {
            _generationCts?.Cancel(); // V36: cancel in-flight generation before releasing VM
            _generationCts = null;

            _ttsManager?.Stop(); // V37: stop TTS playback on navigation away

            App.Extensions.MessageSendRequested    -= OnExtensionMessageSend;
            App.Extensions.ButtonSetsChanged       -= OnButtonSetsChanged;
            App.Extensions.HiddenGenerateRequested -= OnHiddenGenerateRequested;
            App.Extensions.ImageGenerateRequested  -= OnImageGenerateRequested;
            App.Extensions.InsertMessageRequested  -= OnInsertMessageRequested;
        }

        // ── API / Character refresh ──────────────────────────────────────────

        public void RefreshApiIndicator()
        {
            var cfg = App.Settings.GetLlmConfig();
            CurrentApiName   = cfg.DisplayName;
            CurrentModelName = cfg.CurrentModel ?? "";
        }

        public async Task ReloadCharacterAsync()
        {
            if (string.IsNullOrEmpty(_characterAvatar)) return;
            var ch = await App.Characters.GetCharacterAsync(_characterAvatar);
            if (ch != null) Character = ch;
        }

        // ── Input handling ───────────────────────────────────────────────────

        public void UpdateInput(string text)
        {
            InputText = text;
            TokenCount = EstimateTokens(text);
        }

        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsGenerating) return;

            var rawText = InputText.Trim();
            InputText = "";
            TokenCount = 0;

            // /sys prefix → insert narrator message, skip LLM
            if (rawText.StartsWith("/sys "))
            {
                var narratorText = rawText.Substring(5).Trim();
                if (!string.IsNullOrEmpty(narratorText))
                    await InsertNarratorMessageAsync(narratorText);
                return;
            }

            await SendTextAsync(rawText);
        }

        private async Task SendTextAsync(string rawText)
        {
            if (IsGenerating) return;
            _autoContinueCount = 0;
            _currentUserName = App.Settings.GetUserPersonaName().Trim();
            if (string.IsNullOrEmpty(_currentUserName)) _currentUserName = "User";

            // Apply native input regex rules then substitute {{char}}/{{user}} in display text (V22)
            var message = ApplyNativeRegexRules(rawText, applyToInput: true);
            message = Regex.Replace(message, @"\{\{char\}\}", Character?.Name ?? "", RegexOptions.IgnoreCase);
            message = Regex.Replace(message, @"\{\{user\}\}", _currentUserName, RegexOptions.IgnoreCase);

            var userMsg = new ChatMessage { Content = message, IsUser = true };
            Messages.Add(userMsg);

            await PushExtensionContextAsync();
            await App.Extensions.DispatchEventAsync("MESSAGE_SENT",
                JsonConvert.SerializeObject(message));

            if (_chatFileName == null)
                _chatFileName = App.Chats.CreateChatFileName(Character?.Name ?? "chat");

            await GenerateResponseAsync(message, new List<ChatMessage>(Messages).Take(Messages.Count - 1).ToList());
        }

        public async Task SendQuickReplyAsync(QuickReplyButton button)
        {
            if (IsGenerating) return;

            if (!string.IsNullOrEmpty(button.Action))
            {
                var safeAction = EscapeJson(button.Action);
                var safeLabel  = EscapeJson(button.Label);
                await App.Extensions.DispatchEventAsync("BUTTON_CLICKED",
                    $"{{\"action\":\"{safeAction}\",\"label\":\"{safeLabel}\"}}");
                return;
            }

            var text = ApplyMacros(button.Message).Trim();
            if (string.IsNullOrEmpty(text)) return;
            await SendTextAsync(text);
        }

        public async Task InsertNarratorMessageAsync(string text)
        {
            var msg = new ChatMessage { Content = text, IsUser = false, IsNarrator = true };
            Messages.Add(msg);
            await SaveChatAsync();
        }

        public void StopGeneration()
        {
            _generationCts?.Cancel();
            _generationCts = null;

            if (!string.IsNullOrEmpty(CurrentStreamingText))
            {
                Messages.Add(new ChatMessage
                {
                    Content = CurrentStreamingText,
                    IsUser = false,
                    SenderName = Character?.Name ?? ""
                });
                var _ = SaveChatAsync();
            }

            IsGenerating = false;
            CurrentStreamingText = "";
            var __ = App.Extensions.DispatchEventAsync("GENERATION_STOPPED");
        }

        // ── Quick reply refresh ──────────────────────────────────────────────

        public void RefreshQuickReplyButtons()
        {
            var enabled = App.Settings.GetExtQuickReplyEnabled();
            _quickReplyButtons.Clear();

            if (!enabled) return;

            var presets = _qrStorage.Load();
            foreach (var preset in presets.Where(p => p.Enabled))
                foreach (var btn in preset.Buttons)
                    _quickReplyButtons.Add(btn);

            foreach (var set in App.Extensions.GetButtonSets().Values)
                foreach (var obj in set)
                {
                    if (obj is JObject jo)
                        _quickReplyButtons.Add(new QuickReplyButton
                        {
                            Label   = jo.Value<string>("label")   ?? "",
                            Message = jo.Value<string>("message") ?? "",
                            Action  = jo.Value<string>("action")  ?? ""
                        });
                }

            OnPropertyChanged(nameof(QuickReplyButtons));
        }

        // ── Chat management ──────────────────────────────────────────────────

        public async Task<List<ChatInfo>> GetChatInfosAsync()
        {
            if (Character == null) return new List<ChatInfo>();
            return await App.Chats.GetChatInfosAsync(Character.Name);
        }

        public async Task SelectChatAsync(string fileName)
        {
            if (Character == null) return;
            _chatFileName = fileName;
            var chat = await App.Chats.LoadChatAsync(Character.Name, fileName);
            NormalizeLoadedMessages(chat?.Messages);
            Messages.Clear();
            if (chat?.Messages != null)
                foreach (var m in chat.Messages) Messages.Add(m);
            _memoryBlock = chat?.MemoryBlock ?? "";
            _summarizedTurnCount = chat?.SummarizedTurnCount ?? 0;
            // CurrentSpriteName set by NormalizeLoadedMessages (null if no sprite tags in history)
            await PushExtensionContextAsync();
            await App.Extensions.DispatchEventAsync("CHAT_CHANGED",
                JsonConvert.SerializeObject(fileName));
        }

        public async Task NewChatAsync(string greeting = null)
        {
            // Show greeting picker if character has alternate greetings and no override provided
            if (greeting == null && Character != null)
            {
                var greetings = new List<string>();
                if (!string.IsNullOrEmpty(Character.FirstMessage))
                    greetings.Add(Character.FirstMessage);
                if (Character.AlternateGreetings != null)
                    greetings.AddRange(Character.AlternateGreetings.Where(g => !string.IsNullOrEmpty(g)));

                if (greetings.Count > 1)
                {
                    AvailableGreetings.Clear();
                    foreach (var g in greetings) AvailableGreetings.Add(g);
                    ShowGreetingPicker = true;
                    return;
                }

                greeting = greetings.FirstOrDefault();
            }

            await StartNewChatWithGreetingAsync(greeting);
        }

        public void DismissGreetingPicker()
        {
            ShowGreetingPicker = false;
            AvailableGreetings.Clear();
        }

        public async Task SelectGreetingAsync(string greeting)
        {
            ShowGreetingPicker = false;
            AvailableGreetings.Clear();
            await StartNewChatWithGreetingAsync(greeting);
        }

        private async Task StartNewChatInternalAsync()
        {
            var greetings = new List<string>();
            if (!string.IsNullOrEmpty(Character?.FirstMessage))
                greetings.Add(Character.FirstMessage);
            if (Character?.AlternateGreetings != null)
                greetings.AddRange(Character.AlternateGreetings.Where(g => !string.IsNullOrEmpty(g)));

            if (greetings.Count > 1)
            {
                _chatFileName = App.Chats.CreateChatFileName(Character?.Name ?? "chat");
                AvailableGreetings.Clear();
                foreach (var g in greetings) AvailableGreetings.Add(g);
                ShowGreetingPicker = true;
            }
            else
            {
                await StartNewChatWithGreetingAsync(greetings.FirstOrDefault());
            }
        }

        private async Task StartNewChatWithGreetingAsync(string greeting)
        {
            _chatFileName = App.Chats.CreateChatFileName(Character?.Name ?? "chat");
            Messages.Clear();

            if (!string.IsNullOrEmpty(greeting))
            {
                Messages.Add(new ChatMessage
                {
                    Content = ApplyMacros(greeting),
                    IsUser = false,
                    SenderName = Character?.Name ?? ""
                });
                await SaveChatAsync();
            }

            await PushExtensionContextAsync();
            await App.Extensions.DispatchEventAsync("CHAT_CHANGED",
                JsonConvert.SerializeObject(_chatFileName));
        }

        public async Task<bool> DeleteCurrentChatAsync()
        {
            if (Character == null) return false;
            await App.Chats.DeleteChatAsync(Character.Name, _chatFileName);
            var remaining = await App.Chats.GetChatInfosAsync(Character.Name);
            if (remaining.Count > 0)
            {
                await SelectChatAsync(remaining[0].FileName);
                return true;
            }
            await NewChatAsync();
            return false;
        }

        // Delete dialog state
        public void ShowDeleteDialogMethod() => ShowDeleteDialog = true;
        public void DismissDeleteDialogMethod() => ShowDeleteDialog = false;

        public async Task ClearChatAsync()
        {
            if (Character == null) return;
            Messages.Clear();
            _chatFileName = App.Chats.CreateChatFileName(Character.Name);
            if (!string.IsNullOrEmpty(Character.FirstMessage))
            {
                Messages.Add(new ChatMessage
                {
                    Content = ApplyMacros(Character.FirstMessage),
                    IsUser = false
                });
            }
            await SaveChatAsync();
            await PushExtensionContextAsync();
        }

        // ── Generation ───────────────────────────────────────────────────────

        public async Task RegenerateAsync()
        {
            if (IsGenerating || Messages.Count == 0) return;

            var lastAssistantIdx = -1;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (!Messages[i].IsUser) { lastAssistantIdx = i; break; }
            }
            if (lastAssistantIdx == -1) return;

            var userMsgIdx = -1;
            for (int i = lastAssistantIdx - 1; i >= 0; i--)
            {
                if (Messages[i].IsUser) { userMsgIdx = i; break; }
            }
            if (userMsgIdx == -1) return;

            var userMessage = Messages[userMsgIdx].Content;
            var history = new List<ChatMessage>();
            for (int i = 0; i < userMsgIdx; i++) history.Add(Messages[i]);

            // Remove last assistant message
            while (Messages.Count > lastAssistantIdx)
                Messages.RemoveAt(Messages.Count - 1);

            await GenerateResponseAsync(userMessage, history);
        }

        public async Task RegenerateWithSwipeAsync()
        {
            if (IsGenerating || Messages.Count == 0) return;

            var lastAssistantIdx = -1;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (!Messages[i].IsUser) { lastAssistantIdx = i; break; }
            }
            if (lastAssistantIdx == -1) return;

            var current = Messages[lastAssistantIdx];
            // Store current content as an alternate before regenerating
            current.StoreCurrentAsAlternate();

            var userMsgIdx = -1;
            for (int i = lastAssistantIdx - 1; i >= 0; i--)
            {
                if (Messages[i].IsUser) { userMsgIdx = i; break; }
            }
            if (userMsgIdx == -1) return;

            var userMessage = Messages[userMsgIdx].Content;
            var history = new List<ChatMessage>();
            for (int i = 0; i < userMsgIdx; i++) history.Add(Messages[i]);

            while (Messages.Count > lastAssistantIdx)
                Messages.RemoveAt(Messages.Count - 1);

            await GenerateResponseAsync(userMessage, history);
        }

        public async Task ContinueAsync()
        {
            if (IsGenerating || Messages.Count == 0) return;

            var lastAssistantIdx = -1;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (!Messages[i].IsUser) { lastAssistantIdx = i; break; }
            }
            if (lastAssistantIdx == -1) return;

            var lastMsg = Messages[lastAssistantIdx];
            var prefix = lastMsg.Content;

            var userMsgIdx = -1;
            for (int i = lastAssistantIdx - 1; i >= 0; i--)
            {
                if (Messages[i].IsUser) { userMsgIdx = i; break; }
            }

            var userMessage = userMsgIdx >= 0 ? Messages[userMsgIdx].Content : "";
            var history = new List<ChatMessage>();
            for (int i = 0; i < (userMsgIdx >= 0 ? userMsgIdx : 0); i++)
                history.Add(Messages[i]);

            // Remove last assistant message — generation will re-add with continuation appended
            Messages.RemoveAt(lastAssistantIdx);

            // Track the prefix so GenerateResponseAsync can prepend it
            _continuationPrefix = prefix;
            await GenerateResponseAsync(userMessage, history);
        }

        private string _continuationPrefix = null;

        /// <summary>
        /// Strips sprite tags from all loaded AI messages (normalizes content for display),
        /// preserves original in RawContent, and resolves CurrentSpriteName from the last tag found.
        /// Covers charx import, chat restore, and extension injection paths in one pass.
        /// </summary>
        private void NormalizeLoadedMessages(System.Collections.Generic.List<ChatMessage> messages)
        {
            if (messages == null) return;
            string lastSpriteName = null;
            foreach (var msg in messages)
            {
                if (msg.IsUser) continue;
                var match = _spriteTagRegex.Match(msg.Content ?? "");
                if (!match.Success) continue;
                if (msg.RawContent == null) msg.RawContent = msg.Content;
                msg.Content = _spriteTagRegex.Replace(msg.Content, "").Trim();
                lastSpriteName = match.Groups[1].Success && match.Groups[1].Length > 0
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;
            }
            CurrentSpriteName = lastSpriteName;
        }

        private async Task AutoContinueAfterDelayAsync(string capturedFileName)
        {
            await Task.Delay(500);
            if (_chatFileName != capturedFileName) return; // V35: abort if chat changed during delay
            await ContinueAsync();
        }

        private async Task GenerateResponseAsync(string userMessage, List<ChatMessage> history)
        {
            IsGenerating = true;
            _generationCts = new CancellationTokenSource();

            await PushExtensionContextAsync();
            await App.Extensions.DispatchEventAsync("GENERATION_STARTED");

            var aiMsg = new ChatMessage { Content = "", IsUser = false, SenderName = Character?.Name ?? "" };
            Messages.Add(aiMsg);
            var aiIdx = Messages.Count - 1;

            // Capture prefix for continue mode and reset it immediately
            var continuationPrefix = _continuationPrefix ?? "";
            _continuationPrefix = null;

            var config = App.Settings.GetLlmConfig();

            // Build stop sequences from instruct template (text-gen backends only)
            var stopSequences = new List<string>();
            if (!config.UsesChatCompletions)
            {
                var instructName = App.Settings.GetSelectedInstructPreset();
                if (!string.IsNullOrEmpty(instructName))
                {
                    try
                    {
                        var template = await App.Presets.GetInstructTemplateAsync(instructName);
                        if (template != null)
                        {
                            if (!string.IsNullOrEmpty(template.InputSequence))
                                stopSequences.Add(template.InputSequence);
                            if (!string.IsNullOrEmpty(template.StopSequence))
                                stopSequences.Add(template.StopSequence);
                        }
                    }
                    catch { }
                }
            }

            var dispatcher = _dispatcher;
            var progress = new Progress<StreamEvent>(evt =>
            {
                void Apply()
                {
                    if (evt is StreamEvent.Token t)
                    {
                        var displayed = continuationPrefix + t.Accumulated;
                        CurrentStreamingText = displayed;
                        aiMsg.Content = displayed;  // INPC fires; no collection replace needed
                    }
                    else if (evt is StreamEvent.Complete c)
                    {
                        CurrentStreamingText = "";
                        var rawText = continuationPrefix + c.FullText;

                        // Apply native output regex + trim multi-turn
                        var processed = TrimMultiTurn(ApplyNativeRegexRules(rawText, applyToInput: false));

                        // Scan for sprite tags — clear first so a tagless message hides previous sprite
                        // Group 1: <img src=name>, Group 2: <img="name"> (RisuRealm format)
                        CurrentSpriteName = null;
                        var spriteMatch = _spriteTagRegex.Match(processed);
                        if (spriteMatch.Success)
                            CurrentSpriteName = spriteMatch.Groups[1].Success && spriteMatch.Groups[1].Length > 0
                                ? spriteMatch.Groups[1].Value
                                : spriteMatch.Groups[2].Value;

                        // Strip sprite tags before storing/displaying
                        var stripped = _spriteTagRegex.Replace(processed, "").Trim();

                        // Apply JS extension output filters (strips metadata tags)
                        var displayText = App.Extensions.ApplyOutputFilters(stripped);

                        aiMsg.Content = displayText;
                        aiMsg.RawContent = processed != displayText ? processed : null;

                        var safeText = EscapeJson(displayText);
                        var _ = App.Extensions.DispatchEventAsync("MESSAGE_RECEIVED",
                            $"{{\"text\":\"{safeText}\",\"index\":{aiIdx},\"isUser\":false}}");
                        var __ = PushExtensionContextAsync();
                        var ___ = App.Extensions.DispatchEventAsync("GENERATION_STOPPED");

                        // Auto-play TTS
                        var ttsConfig = App.Settings.GetTtsConfig();
                        if (ttsConfig.Enabled && ttsConfig.AutoPlay && !string.IsNullOrWhiteSpace(displayText))
                        {
                            var charFile = _characterAvatar ?? $"{Character?.Name ?? "unknown"}.png";
                            var ____ = _ttsManager.SpeakAsync(displayText, charFile);
                        }

                        // Auto-continue
                        var estimatedTokens = EstimateTokens(processed);
                        if (_autoContinueEnabled && _autoContinueCount < 3 && estimatedTokens < _autoContinueMinLength)
                        {
                            _autoContinueCount++;
                            var capturedFileName = _chatFileName; // V35: capture before delay
                            var _____ = AutoContinueAfterDelayAsync(capturedFileName);
                        }

                        // Long-term memory: check threshold and fire background summarization (T13)
                        var memoryEnabled = App.Settings.GetMemoryEnabled();
                        if (memoryEnabled)
                        {
                            var charCfg = App.Settings.GetLlmConfig();
                            if (!string.IsNullOrWhiteSpace(charCfg.ApiServer) || charCfg.UsesChatCompletions)
                                TryTriggerMemorySummarization();
                        }
                    }
                    else if (evt is StreamEvent.Error e)
                    {
                        aiMsg.Content = "[Error: " + e.Message + "]";
                        if (aiIdx < Messages.Count)
                            Messages[aiIdx] = aiMsg;
                        var _ = App.Extensions.DispatchEventAsync("GENERATION_STOPPED");
                    }
                }

                if (dispatcher != null && !dispatcher.HasThreadAccess)
                    _ = dispatcher.RunAsync(CoreDispatcherPriority.Normal, Apply);
                else
                    Apply();
            });

            ShowTokenCount = true;
            try
            {
                if (config.UsesChatCompletions)
                {
                    var presetName = App.Settings.GetSelectedOaiPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetOaiPresetAsync(presetName) ?? new OaiPreset()
                        : new OaiPreset();
                    var memBlock = App.Settings.GetMemoryEnabled() && !string.IsNullOrWhiteSpace(_memoryBlock)
                        ? _memoryBlock : null;
                    var builtMessages = BuildChatCompletionMessages(history, preset, userMessage, memBlock);
                    TokenCount = EstimateTokens(string.Join(" ", builtMessages.Select(m => m.Value<string>("content") ?? "")));
                    await _llm.GenerateChatCompletionAsync(config, preset, builtMessages, progress, _generationCts.Token);
                }
                else
                {
                    var presetName = App.Settings.GetSelectedTextGenPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetTextGenPresetAsync(presetName) ?? new TextGenPreset()
                        : new TextGenPreset();
                    var systemPrompt = BuildTextGenSystemPrompt();
                    var prompt = BuildTextGenPrompt(history, systemPrompt, userMessage);
                    TokenCount = EstimateTokens(prompt);
                    await _llm.GenerateTextGenAsync(config, preset, prompt, progress,
                        _generationCts.Token, stopSequences.Count > 0 ? stopSequences : null);
                }
            }
            catch (Exception ex)
            {
                aiMsg.Content = "[Error: " + ex.Message + "]";
                if (aiIdx < Messages.Count)
                    Messages[aiIdx] = aiMsg;
            }
            finally
            {
                IsGenerating = false;
                await SaveChatAsync();
                OnPropertyChanged(nameof(QuickReplyButtons));
            }
        }

        // ── Message operations ───────────────────────────────────────────────

        public async Task SwipeLeftAsync(ChatMessage msg)
        {
            if (msg.IsUser || !msg.HasPrevSwipe) return;
            msg.SwipeLeft();
            OnPropertyChanged(nameof(Messages));
            await SaveChatAsync();
        }

        public async Task SwipeRightAsync(ChatMessage msg)
        {
            if (msg.IsUser || !msg.HasNextSwipe) return;
            msg.SwipeRight();
            OnPropertyChanged(nameof(Messages));
            await SaveChatAsync();
        }

        public async Task EditMessageAsync(ChatMessage msg, string newContent)
        {
            var idx = IndexOf(msg);
            if (idx < 0) return;
            Messages[idx] = new ChatMessage
            {
                Id = msg.Id,
                Content = newContent,
                IsUser = msg.IsUser,
                IsNarrator = msg.IsNarrator,
                Timestamp = msg.Timestamp,
                SenderName = msg.SenderName
            };
            await PushExtensionContextAsync();
            await SaveChatAsync();
        }

        public async Task DeleteMessageAsync(ChatMessage msg)
        {
            var idx = IndexOf(msg);
            if (idx < 0) return;
            Messages.RemoveAt(idx);
            await App.Extensions.DispatchEventAsync("MESSAGE_DELETED",
                idx.ToString());
            await SaveChatAsync();
        }

        public async Task DeleteFromHereAsync(ChatMessage msg)
        {
            var idx = IndexOf(msg);
            if (idx < 0) return;
            while (Messages.Count > idx)
                Messages.RemoveAt(idx);
            await SaveChatAsync();
        }

        public void ShowMessageActions(int messageIndex)
        {
            var safeIdx = messageIndex.ToString();
            var _ = App.Extensions.DispatchEventAsync("MESSAGE_LONG_PRESSED",
                $"{{\"messageIndex\":{safeIdx}}}");
        }

        public int GetMessageIndex(ChatMessage msg) => IndexOf(msg);
        public string EscapeJsonString(string s) => EscapeJson(s);

        private int IndexOf(ChatMessage msg)
        {
            for (int i = 0; i < Messages.Count; i++)
                if (Messages[i].Id == msg.Id) return i;
            return -1;
        }

        // ── Author's Note ─────────────────────────────────────────────────────

        public void UpdateAuthorsNote(string content, int depth = 4, int interval = 1,
            int position = 0, int role = 0)
        {
            if (Messages.Count == 0) return;
            var first = Messages[0];
            Messages[0] = new ChatMessage
            {
                Id = first.Id,
                Content = first.Content,
                IsUser = first.IsUser,
                IsNarrator = first.IsNarrator,
                Timestamp = first.Timestamp,
                SenderName = first.SenderName,
                RawContent = first.RawContent,
                ExtensionHeaders = first.ExtensionHeaders,
                ImagePath = first.ImagePath,
                Alternates = first.Alternates,
                CurrentSwipeIndex = first.CurrentSwipeIndex,
                ChatMetadata = new ChatMessageMetadata
                {
                    NotePrompt   = string.IsNullOrWhiteSpace(content) ? null : content,
                    NoteInterval = interval,
                    NoteDepth    = depth,
                    NotePosition = position,
                    NoteRole     = role
                }
            };
            var _ = SaveChatAsync();
        }

        public ChatMessageMetadata GetAuthorsNote()
            => Messages.Count > 0 ? Messages[0].ChatMetadata : null;

        // ── Background ──────────────────────────────────────────────────────

        public async Task UploadBackgroundAsync(StorageFile file)
        {
            if (string.IsNullOrEmpty(_characterAvatar)) return;
            var success = await _backgroundStorage.SaveFromStorageFileAsync(_characterAvatar, file);
            if (success)
                BackgroundPath = _backgroundStorage.GetBackgroundPathOrNull(_characterAvatar);
        }

        public void ClearBackground()
        {
            if (string.IsNullOrEmpty(_characterAvatar)) return;
            _backgroundStorage.DeleteBackground(_characterAvatar);
            BackgroundPath = null;
        }

        // ── Image Gallery ───────────────────────────────────────────────────

        public async Task<List<GalleryImage>> GetGalleryImagesAsync()
        {
            if (Character == null) return new List<GalleryImage>();
            var images = new List<GalleryImage>();
            var chats = await App.Chats.GetChatInfosAsync(Character.Name);
            foreach (var chatInfo in chats)
            {
                var chat = await App.Chats.LoadChatAsync(Character.Name, chatInfo.FileName);
                if (chat?.Messages == null) continue;
                for (int i = 0; i < chat.Messages.Count; i++)
                {
                    var path = chat.Messages[i].ImagePath;
                    if (string.IsNullOrEmpty(path)) continue;
                    var fullPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, path);
                    if (!File.Exists(fullPath)) continue;
                    var ts = new FileInfo(fullPath).LastWriteTime.Ticks;
                    images.Add(new GalleryImage
                    {
                        ImagePath    = fullPath,
                        ChatFileName = chatInfo.FileName,
                        Timestamp    = ts,
                        MessageIndex = i
                    });
                }
            }
            images.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            return images;
        }

        public async Task SaveImageToGalleryAsync(int messageIndex)
        {
            var msg = messageIndex < Messages.Count ? Messages[messageIndex] : null;
            if (msg?.ImagePath == null) return;
            var fullPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, msg.ImagePath);
            if (!File.Exists(fullPath)) return;
            await SaveImageFileToGalleryAsync(fullPath);
        }

        public async Task SaveGalleryImageAsync(GalleryImage image)
        {
            if (!File.Exists(image.ImagePath)) return;
            await SaveImageFileToGalleryAsync(image.ImagePath);
        }

        private async Task SaveImageFileToGalleryAsync(string sourcePath)
        {
            try
            {
                var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
                var folder = picturesLibrary.SaveFolder;
                var pocketTavernFolder = await folder.CreateFolderAsync("PocketTavern",
                    CreationCollisionOption.OpenIfExists);
                var fileName = $"{Character?.Name ?? "image"}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.png";
                var bytes = File.ReadAllBytes(sourcePath);
                var destFile = await pocketTavernFolder.CreateFileAsync(fileName,
                    CreationCollisionOption.GenerateUniqueName);
                await FileIO.WriteBytesAsync(destFile, bytes);
            }
            catch { }
        }

        public async Task DeleteGalleryImageAsync(GalleryImage image)
        {
            try
            {
                if (File.Exists(image.ImagePath))
                    File.Delete(image.ImagePath);
            }
            catch { }
        }

        // ── Delete Character ─────────────────────────────────────────────────

        public async Task DeleteCharacterAsync()
        {
            if (Character == null || string.IsNullOrEmpty(_characterAvatar)) return;
            var chatDir = App.Chats.GetChatDir(Character.Name);
            if (Directory.Exists(chatDir))
                Directory.Delete(chatDir, true);
            await App.Characters.DeleteCharacterAsync(_characterAvatar);
            _backgroundStorage.DeleteBackground(_characterAvatar);
        }

        // ── TTS ───────────────────────────────────────────────────────────────

        public async Task SpeakMessageAsync(ChatMessage msg)
        {
            var charFile = _characterAvatar ?? $"{Character?.Name ?? "unknown"}.png";
            await _ttsManager.SpeakAsync(msg.Content, charFile);
        }

        public void StopTts() => _ttsManager.Stop();

        // ── Extension: hidden generation ──────────────────────────────────────

        private async Task DoHiddenGenerateAsync(string prompt, string cbId)
        {
            try
            {
                var config = App.Settings.GetLlmConfig();
                var presetName = App.Settings.GetSelectedTextGenPreset();
                TextGenPreset preset = null;
                if (!string.IsNullOrEmpty(presetName))
                    preset = await App.Presets.GetTextGenPresetAsync(presetName);
                preset = preset ?? new TextGenPreset();

                // Build context-aware prompt including character info and recent messages
                var sb = new StringBuilder();
                if (Character != null)
                {
                    sb.Append("Character: ").AppendLine(Character.Name);
                    if (!string.IsNullOrEmpty(Character.Description))
                        sb.Append("Description: ").AppendLine(Character.Description.Length > 1000
                            ? Character.Description.Substring(0, 1000) : Character.Description);
                    if (!string.IsNullOrEmpty(Character.Personality))
                        sb.Append("Personality: ").AppendLine(Character.Personality.Length > 500
                            ? Character.Personality.Substring(0, 500) : Character.Personality);
                    if (!string.IsNullOrEmpty(Character.Scenario))
                        sb.Append("Scenario: ").AppendLine(Character.Scenario.Length > 500
                            ? Character.Scenario.Substring(0, 500) : Character.Scenario);
                    sb.AppendLine();
                }
                var recent = Messages.Count > 20
                    ? new List<ChatMessage>(Messages).GetRange(Messages.Count - 20, 20)
                    : new List<ChatMessage>(Messages);
                if (recent.Count > 0)
                {
                    sb.AppendLine("Recent conversation:");
                    foreach (var msg in recent)
                    {
                        var role = msg.IsUser ? _currentUserName : (Character?.Name ?? "Assistant");
                        var text = msg.RawContent ?? msg.Content;
                        sb.Append(role).Append(": ")
                          .AppendLine(text.Length > 2000 ? text.Substring(0, 2000) : text);
                    }
                    sb.AppendLine();
                }
                sb.Append(prompt);

                var result = await _llm.GenerateTextGenSingleAsync(config, preset, sb.ToString());
                await App.Extensions.ResolveHiddenGenerateAsync(cbId, result);
            }
            catch
            {
                await App.Extensions.ResolveHiddenGenerateAsync(cbId, "");
            }
        }

        // ── Extension: image generation ───────────────────────────────────────

        private async Task DoExtensionImageGenerateAsync(string prompt, string optionsJson, string cbId)
        {
            try
            {
                var imageConfig = App.Settings.GetImageGenConfig();
                if (!imageConfig.Enabled)
                {
                    await App.Extensions.ResolveImageGenerateAsync(cbId, "");
                    return;
                }
                JObject options;
                try { options = JObject.Parse(optionsJson); }
                catch { options = new JObject(); }

                var width  = options.Value<int?>("width")  ?? imageConfig.Width;
                var height = options.Value<int?>("height") ?? imageConfig.Height;
                var negPr  = options.Value<string>("negativePrompt") ?? imageConfig.NegativePrompt;
                var seed   = options.Value<int?>("seed") ?? imageConfig.Seed;

                var imgSvc = new ImageGenService(App.Settings);
                var @params = new ForgeGenerationParams
                {
                    Prompt         = prompt,
                    NegativePrompt = negPr,
                    Width          = width,
                    Height         = height,
                    Steps          = imageConfig.Steps,
                    CfgScale       = imageConfig.CfgScale,
                    Sampler        = imageConfig.Sampler,
                    Seed           = seed
                };

                string resultBase64 = "";
                var progress = new SyncProgress<GenerationState>(s =>
                {
                    if (s is GenerationState.Complete c) resultBase64 = c.ImageBase64;
                });
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                await imgSvc.GenerateAsync(@params, progress, cts.Token);

                await App.Extensions.ResolveImageGenerateAsync(cbId, resultBase64);
            }
            catch
            {
                await App.Extensions.ResolveImageGenerateAsync(cbId, "");
            }
        }

        // ── Extension: insert message ─────────────────────────────────────────

        private async Task DoExtensionInsertMessageAsync(string content, string optionsJson)
        {
            JObject options;
            try { options = JObject.Parse(optionsJson); }
            catch { options = new JObject(); }

            var type        = options.Value<string>("type") ?? "narrator";
            var imageBase64 = options.Value<string>("imageBase64") ?? "";

            if (type == "image" && !string.IsNullOrEmpty(imageBase64))
            {
                var imagePath = await SaveExtensionImageAsync(imageBase64);
                if (imagePath != null)
                {
                    var imgMsg = new ChatMessage
                    {
                        Content   = content,
                        IsUser    = false,
                        IsNarrator = true,
                        ImagePath = imagePath
                    };
                    Messages.Add(imgMsg);
                    await SaveChatAsync();
                }
            }
            else if (!string.IsNullOrEmpty(content))
            {
                await InsertNarratorMessageAsync(content);
            }
        }

        private async Task<string> SaveExtensionImageAsync(string base64)
        {
            try
            {
                var imageBytes = Convert.FromBase64String(base64);
                var chatImages = Path.Combine(ApplicationData.Current.LocalFolder.Path,
                    "chat_images", _chatFileName ?? "default");
                Directory.CreateDirectory(chatImages);
                var filename = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.png";
                var filePath = Path.Combine(chatImages, filename);
                File.WriteAllBytes(filePath, imageBytes);
                // Return path relative to LocalFolder
                return Path.Combine("chat_images", _chatFileName ?? "default", filename);
            }
            catch { return null; }
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnButtonSetsChanged(object sender, EventArgs e) => RefreshQuickReplyButtons();

        private async void OnExtensionMessageSend(object sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || IsGenerating) return;
            await SendTextAsync(text);
        }

        private async void OnHiddenGenerateRequested(object sender, HiddenGenerateRequest req)
        {
            await DoHiddenGenerateAsync(req.Prompt, req.CbId);
        }

        private async void OnImageGenerateRequested(object sender, ImageGenerateRequest req)
        {
            await DoExtensionImageGenerateAsync(req.Prompt, req.OptionsJson, req.CbId);
        }

        public bool IsImageGenEnabled => App.Settings.GetImageGenConfig().Enabled;

        public async Task GenerateImageDirectAsync()
        {
            if (IsGenerating || _character == null) return;
            var imageConfig = App.Settings.GetImageGenConfig();
            if (!imageConfig.Enabled) return;

            // Build prompt from character description + last assistant message for context
            var promptParts = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(_character.Description))
                promptParts.Append(_character.Description.Length > 200
                    ? _character.Description.Substring(0, 200)
                    : _character.Description);

            var lastAssistant = Messages.LastOrDefault(m => !m.IsUser);
            if (lastAssistant != null && !string.IsNullOrWhiteSpace(lastAssistant.Content))
            {
                var snippet = lastAssistant.Content.Length > 150
                    ? lastAssistant.Content.Substring(0, 150)
                    : lastAssistant.Content;
                if (promptParts.Length > 0) promptParts.Append(", ");
                promptParts.Append(snippet);
            }

            var prompt = promptParts.Length > 0 ? promptParts.ToString() : _character.Name;

            IsGenerating = true;
            try
            {
                var imgSvc = new ImageGenService(App.Settings);
                var @params = new ForgeGenerationParams
                {
                    Prompt         = prompt,
                    NegativePrompt = imageConfig.NegativePrompt,
                    Width          = imageConfig.Width,
                    Height         = imageConfig.Height,
                    Steps          = imageConfig.Steps,
                    CfgScale       = imageConfig.CfgScale,
                    Sampler        = imageConfig.Sampler,
                    Seed           = imageConfig.Seed
                };

                string resultBase64 = null;
                var progress = new Progress<GenerationState>(s =>
                {
                    if (s is GenerationState.Complete c) resultBase64 = c.ImageBase64;
                });
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                await imgSvc.GenerateAsync(@params, progress, cts.Token);

                if (!string.IsNullOrEmpty(resultBase64))
                {
                    var imagePath = await SaveExtensionImageAsync(resultBase64);
                    if (imagePath != null)
                    {
                        var msg = new ChatMessage
                        {
                            IsUser    = false,
                            Content   = "",
                            ImagePath = imagePath,
                            Timestamp = DateTimeOffset.Now
                        };
                        Messages.Add(msg);
                        await SaveChatAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Direct image gen failed: {ex.Message}");
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private async void OnInsertMessageRequested(object sender, InsertMessageRequest req)
        {
            await DoExtensionInsertMessageAsync(req.Content, req.OptionsJson);
        }

        // ── Prompt building ───────────────────────────────────────────────────

        private List<JObject> BuildChatCompletionMessages(List<ChatMessage> history, OaiPreset preset, string userMessage = null, string memoryBlock = null)
        {
            var messages = new List<JObject>();
            var order = preset?.PromptOrder ?? OaiPromptOrderItem.DefaultOrder();
            var personaName = App.Settings.GetUserPersonaName();
            var personaDesc = App.Settings.GetUserPersonaDesc();
            var injections = App.Extensions.GetPromptInjections().ToList();

            foreach (var item in order)
            {
                if (!item.Enabled) continue;
                switch (item.Id)
                {
                    case "main_prompt":
                        foreach (var inj in injections.Where(i => i.Position == 0))
                            if (!string.IsNullOrEmpty(inj.Text))
                                messages.Add(Msg("system", inj.Text));

                        var mainContent = !string.IsNullOrEmpty(Character?.SystemPrompt)
                            ? ApplyMacros(Character.SystemPrompt)
                            : ApplyMacros(item.Content ?? "");
                        if (!string.IsNullOrEmpty(mainContent))
                            messages.Add(Msg("system", mainContent));

                        foreach (var inj in injections.Where(i => i.Position == 1))
                            if (!string.IsNullOrEmpty(inj.Text))
                                messages.Add(Msg("system", inj.Text));
                        break;

                    case "persona_description":
                        if (!string.IsNullOrEmpty(personaDesc))
                            messages.Add(Msg("system",
                                $"[{personaName}'s persona: {ApplyMacros(personaDesc)}]"));
                        break;

                    case "char_description":
                        // Inject long-term memory as system block before char_description (V11)
                        if (!string.IsNullOrWhiteSpace(memoryBlock))
                        {
                            messages.Add(Msg("system", $"[Memory]\n{memoryBlock.Trim()}"));
                            memoryBlock = null;
                        }
                        if (!string.IsNullOrEmpty(Character?.Description))
                            messages.Add(Msg("system", ApplyMacros(Character.Description)));
                        break;

                    case "char_personality":
                        if (!string.IsNullOrEmpty(Character?.Personality))
                            messages.Add(Msg("system",
                                "Personality: " + ApplyMacros(Character.Personality)));
                        break;

                    case "scenario":
                        if (!string.IsNullOrEmpty(Character?.Scenario))
                            messages.Add(Msg("system",
                                "Scenario: " + ApplyMacros(Character.Scenario)));
                        break;

                    case "auxiliary_prompt":
                        if (!string.IsNullOrEmpty(item.Content))
                            messages.Add(Msg("system", ApplyMacros(item.Content)));
                        break;

                    case "chat_examples":
                        ParseMesExample(messages);
                        break;

                    case "chat_history":
                        foreach (var m in history)
                        {
                            if (m.IsNarrator) continue;
                            messages.Add(Msg(m.IsUser ? "user" : "assistant", m.Content));
                        }
                        break;

                    case "post_history_instructions":
                        if (!string.IsNullOrEmpty(Character?.PostHistoryInstructions))
                            messages.Add(Msg("system",
                                ApplyMacros(Character.PostHistoryInstructions)));
                        if (!string.IsNullOrEmpty(item.Content))
                            messages.Add(Msg("system", ApplyMacros(item.Content)));
                        break;
                }
            }
            if (!string.IsNullOrWhiteSpace(userMessage))
                messages.Add(Msg("user", ApplyMacros(userMessage)));
            return messages;
        }

        private void ParseMesExample(List<JObject> messages)
        {
            var example = Character?.MessageExample;
            if (string.IsNullOrWhiteSpace(example)) return;

            var personaName = App.Settings.GetUserPersonaName();
            var charName = Character?.Name ?? "";
            var applied = ApplyMacros(example);

            foreach (var block in applied.Split(new[] { "<START>" }, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var line in block.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (trimmed.StartsWith(personaName + ":"))
                        messages.Add(Msg("user", trimmed.Substring(personaName.Length + 1).Trim()));
                    else if (trimmed.StartsWith(charName + ":"))
                        messages.Add(Msg("assistant", trimmed.Substring(charName.Length + 1).Trim()));
                }
            }
        }

        private static JObject Msg(string role, string content)
            => new JObject { ["role"] = role, ["content"] = content };

        private string BuildTextGenSystemPrompt()
        {
            if (Character == null) return "";
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Character.SystemPrompt))
                sb.AppendLine(ApplyMacros(Character.SystemPrompt));
            if (!string.IsNullOrEmpty(Character.Description))
                sb.AppendLine(ApplyMacros(Character.Description));
            if (!string.IsNullOrEmpty(Character.Personality))
                sb.AppendLine("Personality: " + ApplyMacros(Character.Personality));
            if (!string.IsNullOrEmpty(Character.Scenario))
                sb.AppendLine("Scenario: " + ApplyMacros(Character.Scenario));
            return sb.ToString().Trim();
        }

        private string BuildTextGenPrompt(List<ChatMessage> history, string systemPrompt, string userMessage)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(systemPrompt))
                sb.AppendLine(systemPrompt).AppendLine();
            var personaName = App.Settings.GetUserPersonaName();
            foreach (var m in history)
            {
                var speaker = m.IsUser ? personaName : (Character?.Name ?? "Character");
                sb.AppendLine($"{speaker}: {m.Content}");
            }
            sb.AppendLine($"{personaName}: {userMessage}");
            sb.Append(Character?.Name ?? "Character" + ": ");
            return sb.ToString();
        }

        // ── Extension context ─────────────────────────────────────────────────

        private async Task PushExtensionContextAsync()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            if (Character != null)
            {
                sb.Append("\"character\":{");
                sb.Append("\"name\":").Append(JsonStr(Character.Name)).Append(",");
                sb.Append("\"description\":").Append(JsonStr(Character.Description ?? "")).Append(",");
                sb.Append("\"personality\":").Append(JsonStr(Character.Personality ?? "")).Append(",");
                sb.Append("\"scenario\":").Append(JsonStr(Character.Scenario ?? ""));
                sb.Append("},");
            }

            sb.Append("\"recentMessages\":[");
            var msgList = new List<ChatMessage>(Messages);
            for (int i = 0; i < msgList.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var msg = msgList[i];
                var text = msg.RawContent ?? msg.Content;
                sb.Append("{\"index\":").Append(i)
                  .Append(",\"text\":").Append(JsonStr(text))
                  .Append(",\"isUser\":").Append(msg.IsUser ? "true" : "false")
                  .Append("}");
            }
            sb.Append("],");

            sb.Append("\"personaName\":").Append(JsonStr(_currentUserName)).Append(",");
            var apiConfig = App.Settings.GetLlmConfig();
            sb.Append("\"apiType\":").Append(JsonStr(apiConfig.DisplayName));
            sb.Append("}");

            await App.Extensions.UpdateContextAsync(sb.ToString());

            var headers = App.Extensions.GetMessageHeaders()
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            await App.Extensions.PushMessageHeadersAsync(headers);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string ApplyMacros(string text)
        {
            if (text == null) return "";
            var personaName = App.Settings.GetUserPersonaName();
            return text
                .Replace("{{char}}", Character?.Name ?? "")
                .Replace("{{user}}", personaName)
                .Replace("{{Char}}", Character?.Name ?? "")
                .Replace("{{User}}", personaName);
        }

        private string ApplyNativeRegexRules(string text, bool applyToInput)
        {
            try
            {
                var rules = _regexStorage.Load();
                foreach (var rule in rules.Where(r => r.Enabled &&
                    (applyToInput ? r.ApplyToInput : r.ApplyToOutput)))
                {
                    try
                    {
                        var opts = rule.CaseInsensitive
                            ? RegexOptions.IgnoreCase
                            : RegexOptions.None;
                        text = rule.IsRegex
                            ? Regex.Replace(text, rule.Pattern, rule.Replacement ?? "", opts)
                            : text.Replace(rule.Pattern, rule.Replacement ?? "");
                    }
                    catch { }
                }
            }
            catch { }
            return text;
        }

        /// <summary>
        /// Strips any multi-turn continuation the model generated past its own response.
        /// Cuts at the first occurrence of a user-role marker on its own line.
        /// </summary>
        private string TrimMultiTurn(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var personaName = _currentUserName.Trim();
            var extras = !string.IsNullOrEmpty(personaName) && personaName != "User"
                ? "|" + Regex.Escape(personaName)
                : "";
            var pattern = $@"\n\s*(User|You|Human{extras})\s*:";
            var match = Regex.Match(text, pattern);
            if (!match.Success) return text;
            return text.Substring(0, match.Index).TrimEnd();
        }

        private static int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return (int)(text.Length / 3.5);
        }

        // ── Message search ────────────────────────────────────────────────────

        public string SearchResultText { get; private set; } = "";

        public void SearchMessages(string query)
        {
            _searchResults.Clear();
            _searchIndex = -1;
            if (!string.IsNullOrWhiteSpace(query))
            {
                for (int i = 0; i < _messages.Count; i++)
                    if (_messages[i].Content?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        _searchResults.Add(i);
                if (_searchResults.Count > 0) _searchIndex = 0;
            }
            UpdateSearchResultText();
        }

        public int SearchNext()
        {
            if (_searchResults.Count == 0) return -1;
            _searchIndex = (_searchIndex + 1) % _searchResults.Count;
            UpdateSearchResultText();
            return _searchResults[_searchIndex];
        }

        public int SearchPrev()
        {
            if (_searchResults.Count == 0) return -1;
            _searchIndex = (_searchIndex - 1 + _searchResults.Count) % _searchResults.Count;
            UpdateSearchResultText();
            return _searchResults[_searchIndex];
        }

        public int CurrentSearchMessageIndex()
        {
            if (_searchIndex < 0 || _searchIndex >= _searchResults.Count) return -1;
            return _searchResults[_searchIndex];
        }

        private void UpdateSearchResultText()
        {
            SearchResultText = _searchResults.Count == 0
                ? ""
                : $"{_searchIndex + 1}/{_searchResults.Count}";
            OnPropertyChanged(nameof(SearchResultText));
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        private static string JsonStr(string s) => "\"" + EscapeJson(s) + "\"";

        // ── Long-term memory ──────────────────────────────────────────────────

        private void TryTriggerMemorySummarization()
        {
            if (Character == null || string.IsNullOrEmpty(_chatFileName)) return;
            var msgs = new List<ChatMessage>(Messages);
            var unsummarized = msgs.Skip(_summarizedTurnCount).ToList();

            int charCount = unsummarized.Sum(m => (m.Content ?? "").Length);
            if (charCount < 12000) return;

            var config = App.Settings.GetLlmConfig();
            if (string.IsNullOrWhiteSpace(config.ApiServer) && !config.UsesChatCompletions) return;

            var charName = Character.Name;
            var chatFile = _chatFileName;
            var batch = unsummarized;
            var newCount = msgs.Count;

            Task.Run(async () =>
            {
                try
                {
                    var summary = await SummarizeHistoryService.SummarizeAsync(batch, config);
                    if (string.IsNullOrWhiteSpace(summary)) return;
                    _memoryBlock = summary;
                    _summarizedTurnCount = newCount;
                    await App.Chats.SaveChatMemoryAsync(charName, chatFile, summary, newCount);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatViewModel] Memory summarization failed: {ex.Message}");
                    // _memoryBlock and _summarizedTurnCount intentionally unchanged (V30)
                }
            });
        }

        private async Task SaveChatAsync()
        {
            if (Character == null) return;
            var list = new List<ChatMessage>(Messages);
            await _saveLock.WaitAsync();
            try
            {
                await App.Chats.SaveChatAsync(Character.Name, _chatFileName, list);
            }
            finally
            {
                _saveLock.Release();
            }
        }
    }

    public class GalleryImage
    {
        public string ImagePath    { get; set; }
        public string ChatFileName { get; set; }
        public long   Timestamp    { get; set; }
        public int    MessageIndex { get; set; }
    }

    internal sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;
        public SyncProgress(Action<T> callback) => _callback = callback;
        void IProgress<T>.Report(T value) => _callback(value);
    }
}
