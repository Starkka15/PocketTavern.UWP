using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private readonly LlmService _llm = new LlmService();

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

        public string CurrentApiName  { get => _currentApiName;  set => Set(ref _currentApiName,  value); }
        public string CurrentModelName { get => _currentModelName; set => Set(ref _currentModelName, value); }

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

        public async Task InitializeAsync(string characterAvatar)
        {
            _characterAvatar = characterAvatar;
            Character = await App.Characters.GetCharacterAsync(characterAvatar);
            if (Character == null) return;

            // Subscribe to extension-triggered sends
            App.Extensions.MessageSendRequested += OnExtensionMessageSend;

            // Load API indicator
            RefreshApiIndicator();

            // Load most recent chat or create new
            var chats = await App.Chats.GetChatInfosAsync(Character.Name);
            if (chats.Count > 0)
            {
                _chatFileName = chats[0].FileName;
                var chat = await App.Chats.LoadChatAsync(Character.Name, _chatFileName);
                Messages.Clear();
                if (chat?.Messages != null)
                    foreach (var m in chat.Messages) Messages.Add(m);
            }
            else
            {
                _chatFileName = App.Chats.CreateChatFileName(Character.Name);
                if (!string.IsNullOrEmpty(Character.FirstMessage))
                {
                    Messages.Add(new ChatMessage
                    {
                        Content = ApplyMacros(Character.FirstMessage),
                        IsUser = false
                    });
                    await SaveChatAsync();
                }
            }
        }

        public void RefreshApiIndicator()
        {
            var cfg = App.Settings.GetLlmConfig();
            CurrentApiName   = cfg.DisplayName;
            CurrentModelName = cfg.CurrentModel ?? "";
        }

        /// <summary>Returns all chat infos for the current character, sorted newest first.</summary>
        public async Task<List<ChatInfo>> GetChatInfosAsync()
        {
            if (Character == null) return new List<ChatInfo>();
            return await App.Chats.GetChatInfosAsync(Character.Name);
        }

        /// <summary>Switch to an existing chat by filename.</summary>
        public async Task SelectChatAsync(string fileName)
        {
            if (Character == null) return;
            _chatFileName = fileName;
            var chat = await App.Chats.LoadChatAsync(Character.Name, fileName);
            Messages.Clear();
            if (chat?.Messages != null)
                foreach (var m in chat.Messages) Messages.Add(m);
        }

        /// <summary>Start a new chat. greeting = the opening message to use (null = no greeting).</summary>
        public async Task NewChatAsync(string greeting = null)
        {
            _chatFileName = App.Chats.CreateChatFileName(Character?.Name ?? "chat");
            Messages.Clear();
            var text = greeting ?? Character?.FirstMessage;
            if (!string.IsNullOrEmpty(text))
            {
                Messages.Add(new ChatMessage
                {
                    Content = ApplyMacros(text),
                    IsUser = false,
                    SenderName = Character?.Name ?? ""
                });
                await SaveChatAsync();
            }
        }

        /// <summary>Delete the current chat. Returns true if another chat was loaded, false if none remain.</summary>
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
            // No chats left — start fresh
            await NewChatAsync();
            return false;
        }

        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsGenerating) return;

            var userText = InputText.Trim();
            InputText = "";

            var userMsg = new ChatMessage { Content = userText, IsUser = true };
            Messages.Add(userMsg);

            // Notify extensions: message sent
            var sentData = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                text  = userText,
                index = Messages.Count - 1,
                isUser = true
            });
            await App.Extensions.DispatchEventAsync("MESSAGE_SENT", sentData);

            await GenerateResponseAsync();
        }

        public void StopGeneration() => _generationCts?.Cancel();

        public void Cleanup()
        {
            App.Extensions.MessageSendRequested -= OnExtensionMessageSend;
        }

        private async void OnExtensionMessageSend(object sender, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || IsGenerating) return;
            InputText = text;
            await SendMessageAsync();
        }

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
        }

        public async Task RegenerateAsync()
        {
            if (IsGenerating || Messages.Count == 0) return;
            // Remove last AI message if present, then regenerate
            var last = Messages[Messages.Count - 1];
            if (!last.IsUser)
                Messages.RemoveAt(Messages.Count - 1);
            await GenerateResponseAsync();
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
            await SaveChatAsync();
        }

        public async Task DeleteMessageAsync(ChatMessage msg)
        {
            var idx = IndexOf(msg);
            if (idx < 0) return;
            Messages.RemoveAt(idx);
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

        private int IndexOf(ChatMessage msg)
        {
            for (int i = 0; i < Messages.Count; i++)
                if (Messages[i].Id == msg.Id) return i;
            return -1;
        }

        public async Task ContinueAsync()
        {
            if (IsGenerating || Messages.Count == 0) return;
            // Append to the last AI message
            var last = Messages[Messages.Count - 1];
            if (!last.IsUser)
            {
                var continued = new ChatMessage
                {
                    Id = last.Id,
                    Content = last.Content + " [continued] ",
                    IsUser = false,
                    Timestamp = last.Timestamp
                };
                Messages[Messages.Count - 1] = continued;
            }
            await GenerateResponseAsync();
        }

        private async Task GenerateResponseAsync()
        {
            IsGenerating = true;
            _generationCts = new CancellationTokenSource();

            // Push context to JS sandbox before generation
            var contextJson = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                characterName = Character?.Name ?? "",
                userName      = App.Settings.GetUserPersonaName(),
                messageCount  = Messages.Count
            });
            await App.Extensions.UpdateContextAsync(contextJson);
            await App.Extensions.PushMessageHeadersAsync(
                App.Extensions.GetMessageHeaders()
                    .ToDictionary(kv => kv.Key, kv => kv.Value));

            // Add placeholder AI message
            var aiMsg = new ChatMessage { Content = "", IsUser = false, SenderName = Character?.Name ?? "" };
            Messages.Add(aiMsg);
            var aiIdx = Messages.Count - 1;

            var config = App.Settings.GetLlmConfig();

            var progress = new Progress<StreamEvent>(evt =>
            {
                if (evt is StreamEvent.Token t)
                {
                    CurrentStreamingText = t.Accumulated;
                    var updated = new ChatMessage
                    {
                        Id = aiMsg.Id,
                        Content = t.Accumulated,
                        IsUser = false,
                        Timestamp = aiMsg.Timestamp
                    };
                    if (aiIdx < Messages.Count)
                        Messages[aiIdx] = updated;
                    aiMsg = updated;
                }
                else if (evt is StreamEvent.Complete c)
                {
                    CurrentStreamingText = "";
                    var filteredText = App.Extensions.ApplyOutputFilters(c.FullText);
                    var final = new ChatMessage
                    {
                        Id = aiMsg.Id,
                        Content = filteredText,
                        IsUser = false,
                        Timestamp = aiMsg.Timestamp
                    };
                    if (aiIdx < Messages.Count)
                        Messages[aiIdx] = final;
                    aiMsg = final;

                    // Notify extensions: message received
                    var receivedData = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        text   = filteredText,
                        index  = aiIdx,
                        isUser = false
                    });
                    // Fire-and-forget — runs on UI thread via Dispatcher
                    var _ = App.Extensions.DispatchEventAsync("MESSAGE_RECEIVED", receivedData);
                }
                else if (evt is StreamEvent.Error e)
                {
                    if (aiIdx < Messages.Count)
                        Messages[aiIdx] = new ChatMessage
                        {
                            Id = aiMsg.Id,
                            Content = "[Error: " + e.Message + "]",
                            IsUser = false
                        };
                }
            });

            try
            {
                var historyList = new System.Collections.Generic.List<ChatMessage>(Messages);
                historyList.RemoveAt(historyList.Count - 1); // Remove placeholder

                if (config.UsesChatCompletions)
                {
                    var presetName = App.Settings.GetSelectedOaiPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetOaiPresetAsync(presetName) ?? new OaiPreset()
                        : new OaiPreset();
                    var builtMessages = BuildChatCompletionMessages(historyList, preset);
                    await _llm.GenerateChatCompletionAsync(config, preset, builtMessages, progress, _generationCts.Token);
                }
                else
                {
                    var presetName = App.Settings.GetSelectedTextGenPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetTextGenPresetAsync(presetName) ?? new TextGenPreset()
                        : new TextGenPreset();
                    var systemPrompt = BuildTextGenSystemPrompt();
                    var prompt = BuildTextGenPrompt(historyList, systemPrompt);
                    await _llm.GenerateTextGenAsync(config, preset, prompt, progress, _generationCts.Token);
                }
            }
            catch (Exception ex)
            {
                // Surface any unhandled exception in the AI message bubble
                if (aiIdx < Messages.Count)
                    Messages[aiIdx] = new ChatMessage
                    {
                        Id = aiMsg.Id,
                        Content = "[Error: " + ex.Message + "]",
                        IsUser = false,
                        SenderName = aiMsg.SenderName
                    };
            }
            finally
            {
                IsGenerating = false;
                await SaveChatAsync();
            }
        }

        private async Task SaveChatAsync()
        {
            if (Character == null) return;
            var list = new System.Collections.Generic.List<ChatMessage>(Messages);
            await App.Chats.SaveChatAsync(Character.Name, _chatFileName, list);
        }

        // ── Chat completion prompt builder (mirrors Android PromptBuilder) ────────

        private List<JObject> BuildChatCompletionMessages(List<ChatMessage> history, OaiPreset preset)
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
                        // Extension injections at BEFORE_CHAR_DEFS (pos=0)
                        foreach (var inj in injections.Where(i => i.Position == 0))
                            if (!string.IsNullOrEmpty(inj.Text))
                                messages.Add(Msg("system", inj.Text));

                        // Character system prompt takes priority; fall back to preset content
                        var mainContent = !string.IsNullOrEmpty(Character?.SystemPrompt)
                            ? ApplyMacros(Character.SystemPrompt)
                            : ApplyMacros(item.Content ?? "");
                        if (!string.IsNullOrEmpty(mainContent))
                            messages.Add(Msg("system", mainContent));

                        // Extension injections at AFTER_CHAR_DEFS (pos=1)
                        foreach (var inj in injections.Where(i => i.Position == 1))
                            if (!string.IsNullOrEmpty(inj.Text))
                                messages.Add(Msg("system", inj.Text));
                        break;

                    case "persona_description":
                        if (!string.IsNullOrEmpty(personaDesc))
                            messages.Add(Msg("system", $"[{personaName}'s persona: {ApplyMacros(personaDesc)}]"));
                        break;

                    case "char_description":
                        if (!string.IsNullOrEmpty(Character?.Description))
                            messages.Add(Msg("system", ApplyMacros(Character.Description)));
                        break;

                    case "char_personality":
                        if (!string.IsNullOrEmpty(Character?.Personality))
                            messages.Add(Msg("system", "Personality: " + ApplyMacros(Character.Personality)));
                        break;

                    case "scenario":
                        if (!string.IsNullOrEmpty(Character?.Scenario))
                            messages.Add(Msg("system", "Scenario: " + ApplyMacros(Character.Scenario)));
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
                            messages.Add(Msg("system", ApplyMacros(Character.PostHistoryInstructions)));
                        if (!string.IsNullOrEmpty(item.Content))
                            messages.Add(Msg("system", ApplyMacros(item.Content)));
                        break;
                }
            }
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

        // ── Text gen prompt builder ───────────────────────────────────────────────

        private string BuildTextGenSystemPrompt()
        {
            if (Character == null) return "";
            var sb = new System.Text.StringBuilder();
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

        private string BuildTextGenPrompt(List<ChatMessage> history, string systemPrompt)
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(systemPrompt))
                sb.AppendLine(systemPrompt).AppendLine();
            var personaName = App.Settings.GetUserPersonaName();
            foreach (var m in history)
            {
                var speaker = m.IsUser ? personaName : (Character?.Name ?? "Character");
                sb.AppendLine($"{speaker}: {m.Content}");
            }
            sb.Append(Character?.Name ?? "Character" + ": ");
            return sb.ToString();
        }

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
    }
}
