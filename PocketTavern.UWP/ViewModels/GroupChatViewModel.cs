using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PocketTavern.UWP.Models;
using PocketTavern.UWP.Services;

namespace PocketTavern.UWP.ViewModels
{
    public class GroupChatViewModel : ViewModelBase
    {
        private readonly LlmService _llm = new LlmService();
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);
        private readonly Random _rng = new Random();

        private Group _group;
        private Dictionary<string, Character> _members = new Dictionary<string, Character>();
        private string _chatKey;
        private string _chatFileName;
        private CancellationTokenSource _cts;
        private string _inputText = "";
        private bool _isGenerating;
        private string _currentStreamingText = "";
        private string _currentSpeaker = "";
        private string _lastSpeakerAvatar = null;
        private const int MaxFollowUps = 3;
        private const int MaxHistoryMessages = 24;

        public Group Group { get => _group; private set => Set(ref _group, value); }
        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
        public IReadOnlyList<Character> Members => _members.Values.ToList();
        public string InputText { get => _inputText; set => Set(ref _inputText, value); }
        public bool IsGenerating { get => _isGenerating; private set => Set(ref _isGenerating, value); }
        public string CurrentStreamingText { get => _currentStreamingText; private set => Set(ref _currentStreamingText, value); }
        public string CurrentSpeaker { get => _currentSpeaker; private set => Set(ref _currentSpeaker, value); }

        private string _error;
        public string Error { get => _error; private set => Set(ref _error, value); }

        // ── Load ─────────────────────────────────────────────────────────────

        public async Task LoadGroupAsync(string groupId)
        {
            var group = App.Groups.GetGroup(groupId);
            if (group == null) { Error = "Group not found."; return; }
            Group = group;
            _chatKey = App.Groups.ChatFolderName(groupId);

            _members = new Dictionary<string, Character>();
            foreach (var avatar in group.Members)
            {
                var ch = await App.Characters.GetCharacterAsync(avatar);
                if (ch != null) _members[avatar] = ch;
            }

            var chats = await App.Chats.GetChatInfosAsync(_chatKey);
            if (chats.Count > 0)
            {
                _chatFileName = chats[0].FileName;
                var chat = await App.Chats.LoadChatAsync(_chatKey, _chatFileName);
                if (chat?.Messages != null)
                    foreach (var m in chat.Messages) Messages.Add(m);
            }
            else
            {
                _chatFileName = App.Chats.CreateChatFileName(_chatKey);
            }
        }

        // ── Send ─────────────────────────────────────────────────────────────

        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText) || IsGenerating) return;
            var text = InputText.Trim();
            InputText = "";

            var userName = App.Settings.GetUserPersonaName();
            if (string.IsNullOrWhiteSpace(userName)) userName = "User";

            Messages.Add(new ChatMessage { Content = text, IsUser = true, SenderName = userName });
            await SaveChatAsync();
            await RunTurnsAsync(text);
        }

        // ── Stop ─────────────────────────────────────────────────────────────

        public void StopGeneration()
        {
            _cts?.Cancel();
            // Don't null _cts here — RunTurnsAsync holds a local token reference and
            // its finally block will clear IsGenerating/CurrentSpeaker cleanly.
        }

        public async Task NewChatAsync()
        {
            Messages.Clear();
            _chatFileName = App.Chats.CreateChatFileName(_chatKey);
            _lastSpeakerAvatar = null;
            await SaveChatAsync();
        }

        public async Task DeleteCurrentChatAsync()
        {
            if (_chatKey == null || _chatFileName == null) return;
            await App.Chats.DeleteChatAsync(_chatKey, _chatFileName);
            Messages.Clear();
            _chatFileName = App.Chats.CreateChatFileName(_chatKey);
            _lastSpeakerAvatar = null;
        }

        public Character GetMemberByAvatar(string avatar)
            => _members.TryGetValue(avatar, out var ch) ? ch : null;

        public async Task SaveGroupSettingsAsync(
            string systemPrompt, int chatStyle,
            int activationStrategy, int generationMode,
            bool allowSelfResponses, List<string> disabledMembers)
        {
            if (_group == null) return;
            _group.SystemPrompt           = systemPrompt ?? "";
            _group.ChatStyleValue         = chatStyle;
            _group.ActivationStrategyValue = activationStrategy;
            _group.GenerationModeValue    = generationMode;
            _group.AllowSelfResponses     = allowSelfResponses;
            _group.DisabledMembers        = disabledMembers ?? new List<string>();
            await App.Groups.SaveGroupAsync(_group);
        }

        // ── First message (narrator + character intros) ───────────────────────

        public async Task GenerateFirstMessageAsync()
        {
            if (IsGenerating || _group == null) return;

            IsGenerating = true;
            _cts = new CancellationTokenSource();
            var firstCt = _cts.Token;
            try
            {
                await GenerateNarratorMessageAsync(firstCt);
                if (firstCt.IsCancellationRequested) return;

                var enabled = _group.EnabledMembers;
                var openers = _group.ActivationStrategyValue == ActivationStrategy.List
                    ? enabled
                    : new List<string> { PickByTalkativeness(enabled) };

                foreach (var avatar in openers)
                {
                    if (firstCt.IsCancellationRequested) break;
                    if (!_members.TryGetValue(avatar, out var ch)) continue;
                    await GenerateFirstMessageForAsync(ch, avatar, firstCt);
                    _lastSpeakerAvatar = avatar;
                }
            }
            finally
            {
                IsGenerating = false;
                CurrentStreamingText = "";
                CurrentSpeaker = "";
            }
        }

        // ── Turn orchestration ───────────────────────────────────────────────

        private async Task RunTurnsAsync(string lastUserText)
        {
            if (_members.Count == 0) return;
            var enabled = _group?.EnabledMembers ?? new List<string>();
            if (enabled.Count == 0) return;

            IsGenerating = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                if (_group.ActivationStrategyValue == ActivationStrategy.List)
                {
                    // All enabled members respond in sequence
                    foreach (var avatar in enabled)
                    {
                        if (ct.IsCancellationRequested) break;
                        if (!_members.TryGetValue(avatar, out var ch)) continue;
                        await GenerateForMemberAsync(ch, avatar, ct);
                        _lastSpeakerAvatar = avatar;
                    }
                }
                else
                {
                    // Natural/Pooled/Manual: exclude last speaker, detect mentions, weight by talkativeness
                    var pool = enabled
                        .Where(a => a != _lastSpeakerAvatar)
                        .ToList();
                    if (pool.Count == 0) pool = enabled.ToList();

                    var mentioned = DetectMentionedCharacter(lastUserText, pool);
                    string firstAvatar;
                    if (mentioned != null)
                        firstAvatar = mentioned;
                    else if (_group.ActivationStrategyValue == ActivationStrategy.Pooled)
                        firstAvatar = pool[_rng.Next(pool.Count)];
                    else
                        firstAvatar = PickByTalkativeness(pool);

                    if (!_members.TryGetValue(firstAvatar, out var firstChar)) return;
                    await GenerateForMemberAsync(firstChar, firstAvatar, ct);
                    _lastSpeakerAvatar = firstAvatar;

                    // Follow-ups: up to MaxFollowUps additional responses
                    for (int i = 0; i < MaxFollowUps; i++)
                    {
                        if (ct.IsCancellationRequested) break;
                        var others = enabled.Where(a => a != _lastSpeakerAvatar).ToList();
                        if (others.Count == 0) break;
                        var nextAvatar = PickFollowUp(others, guaranteed: i == 0);
                        if (nextAvatar == null) break;
                        if (!_members.TryGetValue(nextAvatar, out var nextChar)) break;
                        await GenerateForMemberAsync(nextChar, nextAvatar, ct);
                        _lastSpeakerAvatar = nextAvatar;
                    }
                }
            }
            finally
            {
                IsGenerating = false;
                CurrentStreamingText = "";
                CurrentSpeaker = "";
            }
        }

        private string DetectMentionedCharacter(string text, List<string> candidates)
        {
            foreach (var avatar in candidates)
            {
                if (!_members.TryGetValue(avatar, out var ch)) continue;
                var pattern = new System.Text.RegularExpressions.Regex(
                    @"\b" + System.Text.RegularExpressions.Regex.Escape(ch.Name) + @"\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (pattern.IsMatch(text)) return avatar;
            }
            return null;
        }

        private string PickByTalkativeness(List<string> candidates)
        {
            if (candidates.Count == 1) return candidates[0];
            var weights = candidates.Select(a =>
                _members.TryGetValue(a, out var ch)
                    ? Math.Max(0.01f, Math.Min(1f, ch.Talkativeness))
                    : 0.5f).ToList();
            var total = weights.Sum();
            var r = (float)(_rng.NextDouble() * total);
            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }

        private string PickFollowUp(List<string> candidates, bool guaranteed)
        {
            if (candidates.Count == 0) return null;
            var picked = PickByTalkativeness(candidates);
            if (guaranteed) return picked;
            var talk = _members.TryGetValue(picked, out var ch)
                ? Math.Max(0f, Math.Min(1f, ch.Talkativeness)) : 0.5f;
            return ((float)_rng.NextDouble() < 0.3f + talk * 0.6f) ? picked : null;
        }

        // ── Per-character generation ─────────────────────────────────────────

        private async Task GenerateForMemberAsync(Character member, string avatar, CancellationToken ct)
        {
            CurrentSpeaker = member.Name;
            var aiMsg = new ChatMessage { Content = "", IsUser = false, SenderName = member.Name };
            Messages.Add(aiMsg);
            var aiIdx = Messages.Count - 1;

            var config = App.Settings.GetLlmConfig();
            var personaName = App.Settings.GetUserPersonaName();
            if (string.IsNullOrWhiteSpace(personaName)) personaName = "User";

            var progress = new Progress<StreamEvent>(evt =>
            {
                if (evt is StreamEvent.Token t)
                {
                    CurrentStreamingText = t.Accumulated;
                    aiMsg.Content = t.Accumulated;
                    if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
                }
                else if (evt is StreamEvent.Complete c)
                {
                    var cleaned = CleanResponse(c.FullText.Trim(), member.Name, personaName);
                    CurrentStreamingText = "";
                    aiMsg.Content = cleaned;
                    if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
                    var _ = SaveChatAsync();
                }
                else if (evt is StreamEvent.Error e)
                {
                    aiMsg.Content = $"[Error: {e.Message}]";
                    if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
                }
            });

            await RunGenerationAsync(member, avatar, personaName, config, progress, ct);
        }

        private async Task GenerateNarratorMessageAsync(CancellationToken ct)
        {
            CurrentSpeaker = "Narrator";
            var narMsg = new ChatMessage { Content = "", IsUser = false, IsNarrator = true, SenderName = "Narrator" };
            Messages.Add(narMsg);
            var narIdx = Messages.Count - 1;

            var config = App.Settings.GetLlmConfig();
            var personaName = App.Settings.GetUserPersonaName();
            if (string.IsNullOrWhiteSpace(personaName)) personaName = "User";

            var chars = _group.EnabledMembers.Where(a => _members.ContainsKey(a))
                .Select(a => _members[a]).ToList();
            var prompt = BuildNarratorPrompt(chars, personaName);
            var stopSeqs = new List<string> { "```", "\njson\n{", "\n{\"" };

            var progress = new Progress<StreamEvent>(evt =>
            {
                if (evt is StreamEvent.Token t)
                {
                    CurrentStreamingText = t.Accumulated;
                    narMsg.Content = t.Accumulated;
                    if (narIdx < Messages.Count) Messages[narIdx] = narMsg;
                }
                else if (evt is StreamEvent.Complete c)
                {
                    CurrentStreamingText = "";
                    narMsg.Content = c.FullText.Trim();
                    if (narIdx < Messages.Count) Messages[narIdx] = narMsg;
                    var _ = SaveChatAsync();
                }
                else if (evt is StreamEvent.Error e)
                {
                    narMsg.Content = $"[Error: {e.Message}]";
                    if (narIdx < Messages.Count) Messages[narIdx] = narMsg;
                }
            });

            try
            {
                if (config.UsesChatCompletions)
                {
                    var presetName = App.Settings.GetSelectedOaiPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetOaiPresetAsync(presetName) ?? new OaiPreset()
                        : new OaiPreset();
                    var msgs = new List<JObject>
                    {
                        new JObject { ["role"] = "system", ["content"] = prompt }
                    };
                    await _llm.GenerateChatCompletionAsync(config, preset, msgs, progress, ct);
                }
                else
                {
                    var presetName = App.Settings.GetSelectedTextGenPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetTextGenPresetAsync(presetName) ?? new TextGenPreset()
                        : new TextGenPreset();
                    await _llm.GenerateTextGenAsync(config, preset, prompt, progress, ct, stopSeqs);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                narMsg.Content = $"[Error: {ex.Message}]";
                if (narIdx < Messages.Count) Messages[narIdx] = narMsg;
            }
        }

        private async Task GenerateFirstMessageForAsync(Character member, string avatar, CancellationToken ct)
        {
            CurrentSpeaker = member.Name;
            var aiMsg = new ChatMessage { Content = "", IsUser = false, SenderName = member.Name };
            Messages.Add(aiMsg);
            var aiIdx = Messages.Count - 1;

            var config = App.Settings.GetLlmConfig();
            var personaName = App.Settings.GetUserPersonaName();
            if (string.IsNullOrWhiteSpace(personaName)) personaName = "User";

            var others = _group.EnabledMembers
                .Where(a => a != avatar && _members.ContainsKey(a))
                .Select(a => _members[a]).ToList();

            var progress = new Progress<StreamEvent>(evt =>
            {
                if (evt is StreamEvent.Token t)
                {
                    CurrentStreamingText = t.Accumulated;
                    aiMsg.Content = t.Accumulated;
                    if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
                }
                else if (evt is StreamEvent.Complete c)
                {
                    var cleaned = CleanResponse(c.FullText.Trim(), member.Name, personaName);
                    CurrentStreamingText = "";
                    aiMsg.Content = cleaned;
                    if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
                    var _ = SaveChatAsync();
                }
                else if (evt is StreamEvent.Error e)
                {
                    aiMsg.Content = $"[Error: {e.Message}]";
                    if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
                }
            });

            var stopSeqs = BuildStopSequences(member.Name, personaName);
            try
            {
                if (config.UsesChatCompletions)
                {
                    var presetName = App.Settings.GetSelectedOaiPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetOaiPresetAsync(presetName) ?? new OaiPreset()
                        : new OaiPreset();
                    var msgs = BuildFirstMessageChatMessages(member, avatar, others, personaName);
                    await _llm.GenerateChatCompletionAsync(config, preset, msgs, progress, ct);
                }
                else
                {
                    var presetName = App.Settings.GetSelectedTextGenPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetTextGenPresetAsync(presetName) ?? new TextGenPreset()
                        : new TextGenPreset();
                    var prompt = BuildFirstMessagePrompt(member, avatar, others, personaName);
                    await _llm.GenerateTextGenAsync(config, preset, prompt, progress, ct, stopSeqs);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                aiMsg.Content = $"[Error: {ex.Message}]";
                if (aiIdx < Messages.Count) Messages[aiIdx] = aiMsg;
            }
        }

        private async Task RunGenerationAsync(
            Character member, string avatar, string personaName,
            ApiConfiguration config, IProgress<StreamEvent> progress, CancellationToken ct)
        {
            var stopSeqs = BuildStopSequences(member.Name, personaName);
            try
            {
                if (config.UsesChatCompletions)
                {
                    var presetName = App.Settings.GetSelectedOaiPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetOaiPresetAsync(presetName) ?? new OaiPreset()
                        : new OaiPreset();
                    var msgs = BuildGroupChatMessages(member, avatar, personaName);
                    await _llm.GenerateChatCompletionAsync(config, preset, msgs, progress, ct);
                }
                else
                {
                    var presetName = App.Settings.GetSelectedTextGenPreset();
                    var preset = presetName != null
                        ? await App.Presets.GetTextGenPresetAsync(presetName) ?? new TextGenPreset()
                        : new TextGenPreset();
                    var prompt = BuildGroupTextGenPrompt(member, avatar, personaName);
                    await _llm.GenerateTextGenAsync(config, preset, prompt, progress, ct, stopSeqs);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                progress?.Report(new StreamEvent.Error { Message = ex.Message });
            }
        }

        // ── Stop sequences ───────────────────────────────────────────────────

        private List<string> BuildStopSequences(string characterName, string personaName)
        {
            var stops = new List<string>
            {
                $"\n{personaName}:",
                $"\n{personaName}: ",
                "```",
                "\njson\n{",
                "\n{\""
            };
            foreach (var avatar in _group.EnabledMembers)
            {
                if (!_members.TryGetValue(avatar, out var ch)) continue;
                if (ch.Name == characterName) continue;
                stops.Add($"\n{ch.Name}:");
                stops.Add($"\n{ch.Name}: ");
            }
            return stops;
        }

        // ── Prompt builders (text-gen) ───────────────────────────────────────

        private string BuildGroupTextGenPrompt(Character speaker, string speakerAvatar, string personaName)
        {
            var others = _group.EnabledMembers
                .Where(a => a != speakerAvatar && _members.ContainsKey(a))
                .Select(a => _members[a]).ToList();
            var otherNames = others.Count > 0
                ? string.Join("/", others.Select(c => c.Name))
                : "other characters";
            var isRP = _group.ChatStyleValue == ChatStyle.RP;

            var sb = new StringBuilder();
            sb.Append($"### RULE: You are {speaker.Name}. You ONLY write {speaker.Name}'s words and actions. ");
            sb.Append($"{personaName} is the human user — NEVER write their dialogue, thoughts, feelings, or reactions. ");
            sb.AppendLine($"Stop writing the moment {speaker.Name}'s turn ends. ###\n");

            sb.AppendLine($"[character(\"{speaker.Name}\")");
            if (!string.IsNullOrEmpty(speaker.Description))
                sb.AppendLine($"description: {Truncate(speaker.Description, 600)}");
            if (!string.IsNullOrEmpty(speaker.Personality))
                sb.AppendLine($"personality: {Truncate(speaker.Personality, 300)}");
            if (!string.IsNullOrEmpty(speaker.Scenario))
                sb.AppendLine($"scenario: {Truncate(speaker.Scenario, 300)}");
            sb.AppendLine("]\n");

            if (!string.IsNullOrEmpty(_group.SystemPrompt))
                sb.AppendLine($"[scenario]\n{_group.SystemPrompt}\n");

            if (others.Count > 0)
            {
                sb.AppendLine("[other characters in this conversation]");
                foreach (var other in others)
                {
                    var snippet = !string.IsNullOrEmpty(other.Personality)
                        ? Truncate(other.Personality, 120)
                        : Truncate(other.Description, 120);
                    sb.Append(other.Name);
                    if (!string.IsNullOrEmpty(snippet)) sb.Append($": {snippet}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            if (isRP)
            {
                sb.AppendLine($"You are {speaker.Name}. The scene is \"{_group.Name}\".");
                sb.AppendLine($"{personaName} is who you are talking to. Always acknowledge or respond to them.");
                sb.AppendLine("Write in FIRST PERSON using proper Markdown: *asterisks* for actions, \"double quotes\" for all spoken words.");
                sb.AppendLine($"Do NOT refer to yourself in third person. Do NOT describe or control what {otherNames} does. Keep it to 1-3 short paragraphs.");
            }
            else
            {
                sb.AppendLine($"You are {speaker.Name}. The setting is \"{_group.Name}\".");
                sb.AppendLine($"{personaName} is who you are talking to. Always acknowledge or respond to them.");
                sb.AppendLine("Write spoken dialogue in \"double quotes\". No action descriptions. Keep it concise.");
            }
            sb.AppendLine($"Be responsive to what {personaName} wants — engage with their direction rather than repeatedly pushing your own agenda. Stay in character, but follow their lead.");
            sb.AppendLine($"Do NOT write topic outlines, numbered lists, analysis plans, or meta-commentary. Respond naturally as {speaker.Name} would in actual conversation.");
            sb.AppendLine($"IMPORTANT: Write ONLY {speaker.Name}'s response. Do NOT prefix lines with your name. Do NOT write for {personaName} or any other character. Do NOT refer to {speaker.Name} in third person — you ARE {speaker.Name}.\n");

            // Narrator scene context
            var narratorMsg = Messages.FirstOrDefault(m => m.IsNarrator && m.SenderName == "Narrator");
            if (narratorMsg != null)
                sb.AppendLine($"[Scene: {narratorMsg.Content}]\n");

            // History (last N non-narrator messages)
            var recent = TakeLast(Messages.Where(m => !m.IsNarrator), MaxHistoryMessages);
            foreach (var m in recent)
            {
                var role = m.IsUser ? personaName : (m.SenderName ?? "Unknown");
                sb.AppendLine($"{role}: {m.Content}");
            }
            sb.Append($"{speaker.Name}:");
            return sb.ToString();
        }

        private string BuildFirstMessagePrompt(
            Character speaker, string speakerAvatar,
            List<Character> others, string personaName)
        {
            var isRP = _group.ChatStyleValue == ChatStyle.RP;
            var sb = new StringBuilder();

            sb.AppendLine($"[character(\"{speaker.Name}\")");
            if (!string.IsNullOrEmpty(speaker.Description))
                sb.AppendLine($"description: {Truncate(speaker.Description, 600)}");
            if (!string.IsNullOrEmpty(speaker.Personality))
                sb.AppendLine($"personality: {Truncate(speaker.Personality, 300)}");
            if (!string.IsNullOrEmpty(speaker.Scenario))
                sb.AppendLine($"scenario: {Truncate(speaker.Scenario, 300)}");
            sb.AppendLine("]\n");

            if (!string.IsNullOrEmpty(_group.SystemPrompt))
                sb.AppendLine($"[scenario]\n{_group.SystemPrompt}\n");

            if (others.Count > 0)
            {
                sb.AppendLine("[other characters present]");
                foreach (var other in others)
                {
                    var snippet = !string.IsNullOrEmpty(other.Personality)
                        ? Truncate(other.Personality, 100)
                        : Truncate(other.Description, 100);
                    sb.Append(other.Name);
                    if (!string.IsNullOrEmpty(snippet)) sb.Append($": {snippet}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            if (isRP)
            {
                sb.AppendLine($"You are {speaker.Name}. The scene is \"{_group.Name}\".");
                sb.AppendLine("Write in FIRST PERSON using proper Markdown: *asterisks* for actions, \"double quotes\" for all spoken words. Do NOT refer to yourself in third person. Do NOT control other characters' actions. Keep it to 1-3 short paragraphs.");
            }
            else
            {
                sb.AppendLine($"You are {speaker.Name}. The setting is \"{_group.Name}\".");
                sb.AppendLine("Write spoken dialogue in \"double quotes\". No action descriptions. Keep it concise.");
            }

            var narratorMsg = Messages.FirstOrDefault(m => m.IsNarrator && m.SenderName == "Narrator");
            if (narratorMsg != null)
                sb.AppendLine($"\n[Scene: {narratorMsg.Content}]\n");

            var nonNarrator = Messages.Where(m => !m.IsNarrator).ToList();
            if (nonNarrator.Count == 0)
            {
                sb.AppendLine($"You MUST speak directly to {personaName} in your response — address them, react to their presence, or engage with them in whatever way fits the scene. Stay in character. Be creative and concise.");
            }
            else
            {
                sb.AppendLine($"The scene is underway. Speak directly to {personaName} or react to what they said. Join naturally, in character.\n");
                var recent = TakeLast(nonNarrator, 10);
                foreach (var m in recent)
                {
                    var role = m.IsUser ? personaName : (m.SenderName ?? "Unknown");
                    sb.AppendLine($"{role}: {m.Content}");
                }
            }
            sb.AppendLine($"IMPORTANT: Write ONLY {speaker.Name}'s response. Do NOT prefix lines with your name. Do NOT write for anyone else.");
            sb.Append($"{speaker.Name}:");
            return sb.ToString();
        }

        private string BuildNarratorPrompt(List<Character> chars, string personaName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a neutral narrator introducing a scene.\n");
            sb.AppendLine("Characters present:");
            foreach (var ch in chars)
            {
                var snippet = !string.IsNullOrEmpty(ch.Personality)
                    ? Truncate(ch.Personality, 200)
                    : Truncate(ch.Description, 200);
                sb.Append($"- {ch.Name}");
                if (!string.IsNullOrEmpty(snippet)) sb.Append($": {snippet}");
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Write a vivid scene-setting narration (3-5 sentences) that:");
            sb.AppendLine("- Describes a specific, random location");
            sb.AppendLine("- Describes each character's demeanor and what they are doing");
            sb.AppendLine($"- Naturally establishes {personaName}'s presence in the scene in whatever way fits the narrative — they might already be there, just arrived, or about to be noticed");
            sb.AppendLine($"Do NOT describe {personaName}'s appearance or personality. Write in third person. Be immersive and creative.\n");
            sb.Append("Narration:");
            return sb.ToString();
        }

        // ── Prompt builders (chat completions) ──────────────────────────────

        private List<JObject> BuildGroupChatMessages(Character speaker, string speakerAvatar, string personaName)
        {
            var others = _group.EnabledMembers
                .Where(a => a != speakerAvatar && _members.ContainsKey(a))
                .Select(a => _members[a]).ToList();
            var otherNames = others.Count > 0
                ? string.Join("/", others.Select(c => c.Name))
                : "other characters";
            var isRP = _group.ChatStyleValue == ChatStyle.RP;

            // Build system message matching text-gen prompt structure
            var sys = new StringBuilder();
            sys.Append($"You are {speaker.Name}. You ONLY write {speaker.Name}'s words and actions. ");
            sys.Append($"{personaName} is the human user — NEVER write their dialogue, thoughts, feelings, or reactions. ");
            sys.AppendLine($"Stop writing the moment {speaker.Name}'s turn ends.\n");

            sys.AppendLine($"[character(\"{speaker.Name}\")");
            if (!string.IsNullOrEmpty(speaker.Description))
                sys.AppendLine($"description: {Truncate(speaker.Description, 600)}");
            if (!string.IsNullOrEmpty(speaker.Personality))
                sys.AppendLine($"personality: {Truncate(speaker.Personality, 300)}");
            if (!string.IsNullOrEmpty(speaker.Scenario))
                sys.AppendLine($"scenario: {Truncate(speaker.Scenario, 300)}");
            sys.AppendLine("]\n");

            if (!string.IsNullOrEmpty(_group.SystemPrompt))
                sys.AppendLine($"[scenario]\n{_group.SystemPrompt}\n");

            if (others.Count > 0)
            {
                sys.AppendLine("[other characters in this conversation]");
                foreach (var other in others)
                {
                    var snippet = !string.IsNullOrEmpty(other.Personality)
                        ? Truncate(other.Personality, 120)
                        : Truncate(other.Description, 120);
                    sys.Append(other.Name);
                    if (!string.IsNullOrEmpty(snippet)) sys.Append($": {snippet}");
                    sys.AppendLine();
                }
                sys.AppendLine();
            }

            if (isRP)
            {
                sys.AppendLine($"You are {speaker.Name}. The scene is \"{_group.Name}\".");
                sys.AppendLine($"{personaName} is who you are talking to. Always acknowledge or respond to them.");
                sys.AppendLine("Write in FIRST PERSON using proper Markdown: *asterisks* for actions, \"double quotes\" for all spoken words.");
                sys.AppendLine($"Do NOT refer to yourself in third person. Do NOT describe or control what {otherNames} does. Keep it to 1-3 short paragraphs.");
            }
            else
            {
                sys.AppendLine($"You are {speaker.Name}. The setting is \"{_group.Name}\".");
                sys.AppendLine($"{personaName} is who you are talking to. Always acknowledge or respond to them.");
                sys.AppendLine("Write spoken dialogue in \"double quotes\". No action descriptions. Keep it concise.");
            }
            sys.AppendLine($"Be responsive to what {personaName} wants. Stay in character, but follow their lead.");
            sys.AppendLine($"IMPORTANT: Write ONLY {speaker.Name}'s response. Do NOT prefix with your name. Do NOT write for {personaName} or any other character.");

            var narratorMsg = Messages.FirstOrDefault(m => m.IsNarrator && m.SenderName == "Narrator");
            if (narratorMsg != null)
                sys.AppendLine($"\n[Scene: {narratorMsg.Content}]");

            var msgs = new List<JObject>
            {
                new JObject { ["role"] = "system", ["content"] = sys.ToString().Trim() }
            };

            // History as user/assistant turns; non-speaker AI messages get name prefix
            var recent = TakeLast(Messages.Where(m => !m.IsNarrator), MaxHistoryMessages);
            foreach (var m in recent)
            {
                if (m.IsUser)
                {
                    msgs.Add(new JObject { ["role"] = "user", ["content"] = m.Content });
                }
                else
                {
                    var prefix = !string.IsNullOrEmpty(m.SenderName) && m.SenderName != speaker.Name
                        ? $"[{m.SenderName}]: " : "";
                    msgs.Add(new JObject { ["role"] = "assistant", ["content"] = prefix + m.Content });
                }
            }

            return msgs;
        }

        private List<JObject> BuildFirstMessageChatMessages(
            Character speaker, string speakerAvatar,
            List<Character> others, string personaName)
        {
            var isRP = _group.ChatStyleValue == ChatStyle.RP;
            var sys = new StringBuilder();

            sys.AppendLine($"[character(\"{speaker.Name}\")");
            if (!string.IsNullOrEmpty(speaker.Description))
                sys.AppendLine($"description: {Truncate(speaker.Description, 600)}");
            if (!string.IsNullOrEmpty(speaker.Personality))
                sys.AppendLine($"personality: {Truncate(speaker.Personality, 300)}");
            if (!string.IsNullOrEmpty(speaker.Scenario))
                sys.AppendLine($"scenario: {Truncate(speaker.Scenario, 300)}");
            sys.AppendLine("]\n");

            if (!string.IsNullOrEmpty(_group.SystemPrompt))
                sys.AppendLine($"[scenario]\n{_group.SystemPrompt}\n");

            if (others.Count > 0)
            {
                sys.AppendLine("[other characters present]");
                foreach (var other in others)
                {
                    var snippet = !string.IsNullOrEmpty(other.Personality)
                        ? Truncate(other.Personality, 100)
                        : Truncate(other.Description, 100);
                    sys.Append(other.Name);
                    if (!string.IsNullOrEmpty(snippet)) sys.Append($": {snippet}");
                    sys.AppendLine();
                }
                sys.AppendLine();
            }

            if (isRP)
            {
                sys.AppendLine($"You are {speaker.Name}. The scene is \"{_group.Name}\".");
                sys.AppendLine("Write in FIRST PERSON using proper Markdown: *asterisks* for actions, \"double quotes\" for all spoken words.");
            }
            else
            {
                sys.AppendLine($"You are {speaker.Name}. The setting is \"{_group.Name}\".");
                sys.AppendLine("Write spoken dialogue in \"double quotes\". No action descriptions. Keep it concise.");
            }

            var narratorMsg = Messages.FirstOrDefault(m => m.IsNarrator && m.SenderName == "Narrator");
            if (narratorMsg != null)
                sys.AppendLine($"\n[Scene: {narratorMsg.Content}]");

            sys.AppendLine($"IMPORTANT: Write ONLY {speaker.Name}'s response. Do NOT prefix with your name. Do NOT write for anyone else.");

            var msgs = new List<JObject>
            {
                new JObject { ["role"] = "system", ["content"] = sys.ToString().Trim() }
            };

            var nonNarrator = TakeLast(Messages.Where(m => !m.IsNarrator), 10);
            if (nonNarrator.Count == 0)
            {
                msgs.Add(new JObject
                {
                    ["role"] = "user",
                    ["content"] = $"Start the scene. Speak directly to {personaName}."
                });
            }
            else
            {
                foreach (var m in nonNarrator)
                {
                    if (m.IsUser)
                        msgs.Add(new JObject { ["role"] = "user", ["content"] = m.Content });
                    else
                        msgs.Add(new JObject { ["role"] = "assistant",
                            ["content"] = (!string.IsNullOrEmpty(m.SenderName) && m.SenderName != speaker.Name
                                ? $"[{m.SenderName}]: " : "") + m.Content });
                }
            }
            return msgs;
        }

        // ── Clean response ───────────────────────────────────────────────────

        private static readonly System.Text.RegularExpressions.Regex _numberedListRe =
            new System.Text.RegularExpressions.Regex(
                @"^\s*\d+[\.\)]\s",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex _metaLineRe =
            new System.Text.RegularExpressions.Regex(
                @"^(Discuss|Address|Write|Following|Put as|Subtopic|Topics?:|Format:|Note:|-->)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private string CleanResponse(string text, string characterName, string personaName)
        {
            var p1 = characterName + ": ";
            var p2 = characterName + ":";

            // Strip character name prefix from each line
            var lines = text.Split('\n').Select(line =>
            {
                if (line.StartsWith(p1)) return line.Substring(p1.Length);
                if (line.StartsWith(p2)) return line.Substring(p2.Length).TrimStart();
                return line;
            }).ToList();

            // Truncate at first numbered-list or meta-commentary line
            var cutAt = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i].TrimStart();
                if (_numberedListRe.IsMatch(l) || _metaLineRe.IsMatch(l))
                {
                    // Only cut if there's already some real content before this
                    if (i > 0 && lines.Take(i).Any(x => !string.IsNullOrWhiteSpace(x)))
                        cutAt = i;
                    break;
                }
            }
            if (cutAt > 0) lines = lines.Take(cutAt).ToList();

            var stripped = string.Join("\n", lines).Trim();

            // Discard JSON/code dumps
            var t = stripped.TrimStart();
            if (t.StartsWith("json\n{") || t.StartsWith("```") ||
                (t.StartsWith("{") && t.Contains("\"name\"") && t.TrimEnd().EndsWith("}")))
            {
                var afterBlock = stripped.Substring(stripped.LastIndexOf('}') + 1).Trim();
                return afterBlock.Length > 0 ? afterBlock : "";
            }

            if (string.IsNullOrWhiteSpace(personaName)) return stripped;

            // Truncate at any paragraph where model starts narrating the user persona
            var paragraphs = stripped.Split(new[] { "\n\n" }, StringSplitOptions.None);
            var result = new List<string>();
            foreach (var para in paragraphs)
            {
                var pt = para.TrimStart();
                var isUserNarration = pt.StartsWith(personaName, StringComparison.OrdinalIgnoreCase)
                    && !pt.StartsWith("\"") && !pt.StartsWith("'")
                    && !pt.StartsWith("*") && !pt.StartsWith("(");
                var isUserSpeaker =
                    pt.StartsWith(personaName + ":", StringComparison.OrdinalIgnoreCase) ||
                    pt.StartsWith(personaName + " :", StringComparison.OrdinalIgnoreCase);
                if (isUserNarration || isUserSpeaker) break;
                result.Add(para);
            }
            return string.Join("\n\n", result).Trim();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string Truncate(string s, int max)
            => s != null && s.Length > max ? s.Substring(0, max) : s ?? "";

        private static List<T> TakeLast<T>(IEnumerable<T> source, int count)
        {
            var list = source.ToList();
            return list.Count <= count ? list : list.Skip(list.Count - count).ToList();
        }

        private async Task SaveChatAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                if (_chatFileName == null || _chatKey == null) return;
                await App.Chats.SaveChatAsync(_chatKey, _chatFileName, new List<ChatMessage>(Messages));
            }
            finally { _saveLock.Release(); }
        }
    }
}
