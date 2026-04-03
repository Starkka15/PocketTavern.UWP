using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    public static class PromptStringExtensions
    {
        public static string IfBlank(this string s, string fallback) =>
            string.IsNullOrWhiteSpace(s) ? fallback : s;
    }

    /// <summary>
    /// Builds prompts following SillyTavern's prompt construction pipeline.
    ///
    /// Prompt order (for text completion APIs):
    /// 1. Story string (system prompt + description + personality + scenario)
    /// 2. Message examples
    /// 3. World Info (position: before character)
    /// 4. Chat history with depth-based injections (Author's Note, World Info by depth)
    /// 5. World Info (position: after character)
    /// 6. New user message
    /// 7. Assistant response start
    /// </summary>
    public class PromptBuilder
    {
        private static readonly Random _rng = new Random();

        private readonly Character _character;
        private readonly ChatContext _chatContext;
        private readonly string _userName;
        private readonly string _mainPromptOverride;
        private readonly List<string> _extensionInjections;
        private readonly InstructTemplate _instructTemplate;
        private readonly string _systemPrompt;

        public PromptBuilder(
            Character character,
            ChatContext chatContext,
            string userName = "User",
            string mainPromptOverride = "",
            List<string> extensionInjections = null)
        {
            _character = character;
            _chatContext = chatContext;
            _userName = userName;
            _mainPromptOverride = mainPromptOverride ?? "";
            _extensionInjections = extensionInjections ?? new List<string>();
            _instructTemplate = chatContext.InstructTemplate;

            var strippedOverride = StripCommentMacros(_mainPromptOverride).Trim();
            var globalPrompt = !string.IsNullOrWhiteSpace(strippedOverride) ? strippedOverride
                : !string.IsNullOrWhiteSpace(chatContext.SystemPromptPreset) ? chatContext.SystemPromptPreset
                : _instructTemplate?.SystemPrompt ?? "";

            var characterPrompt = character.SystemPrompt;

            DebugLogger.LogSection("System Prompt Construction");
            DebugLogger.LogKeyValue("mainPromptOverride (stripped)", strippedOverride.Length > 0 ? strippedOverride.Substring(0, Math.Min(120, strippedOverride.Length)) : "(empty/comment-only)");
            DebugLogger.LogKeyValue("Global system prompt", globalPrompt.Length > 0 ? globalPrompt.Substring(0, Math.Min(120, globalPrompt.Length)) : "(empty)");
            DebugLogger.LogKeyValue("Character system prompt", characterPrompt.Length > 0 ? characterPrompt.Substring(0, Math.Min(120, characterPrompt.Length)) : "(empty)");

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(globalPrompt))
                sb.Append(globalPrompt);
            if (!string.IsNullOrWhiteSpace(characterPrompt))
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(characterPrompt);
            }
            _systemPrompt = sb.ToString();

            DebugLogger.LogKeyValue("Combined system prompt length", _systemPrompt.Length);
        }

        /// <summary>
        /// Build the complete prompt for text completion APIs.
        /// </summary>
        public string BuildPrompt(List<ChatMessage> chatHistory, string newMessage)
        {
            return (_instructTemplate != null && !string.IsNullOrWhiteSpace(_instructTemplate.InputSequence))
                ? BuildInstructPrompt(chatHistory, newMessage)
                : BuildSimplePrompt(chatHistory, newMessage);
        }

        /// <summary>
        /// Build structured messages for chat completion APIs (OpenAI, Claude, Mistral, etc.).
        /// Returns a list of role-tagged messages instead of a single flat string.
        ///
        /// Message order mirrors SillyTavern's OAI prompt ordering:
        ///  1. system  — combined system prompt + character card
        ///  2. system  — World Info (before_char, position=0)
        ///  3. user/assistant — message examples
        ///  4. user/assistant/system — chat history with depth injections
        ///  5. system  — World Info (after_char, position=1)
        ///  6. system  — post-history instructions
        ///  7. user    — new user message
        /// </summary>
        public List<PromptMessage> BuildChatCompletionMessages(
            List<ChatMessage> chatHistory,
            string newMessage,
            List<OaiPromptOrderItem> promptOrder = null)
        {
            if (promptOrder == null)
                promptOrder = OaiPromptOrderItem.DefaultOrder();

            var messages = new List<PromptMessage>();

            var historyIdx = promptOrder.FindIndex(i => i.Id == "chat_history");
            var beforeHistory = historyIdx >= 0 ? promptOrder.Take(historyIdx).ToList() : promptOrder.ToList();
            var afterHistory = historyIdx >= 0 ? promptOrder.Skip(historyIdx + 1).ToList() : new List<OaiPromptOrderItem>();

            string BlockContent(OaiPromptOrderItem item)
            {
                if (!item.IsMarker)
                {
                    var presetText = item.Content ?? "";
                    switch (item.Id)
                    {
                        case "main_prompt":
                            return !string.IsNullOrWhiteSpace(_systemPrompt) ? SubstituteMacros(_systemPrompt) : "";
                        case "post_history_instructions":
                        {
                            var text = !string.IsNullOrWhiteSpace(presetText) ? presetText : _character.PostHistoryInstructions;
                            return !string.IsNullOrWhiteSpace(text) ? SubstituteMacros(text).Trim() : "";
                        }
                        default:
                            return !string.IsNullOrWhiteSpace(presetText) ? SubstituteMacros(presetText).Trim() : "";
                    }
                }
                switch (item.Id)
                {
                    case "world_info_before":
                        return GetWorldInfoByPosition(0, chatHistory, newMessage);
                    case "persona_description":
                    {
                        var persona = _chatContext.UserPersona;
                        if (string.IsNullOrWhiteSpace(persona.Description)) return "";
                        var prefix = !string.IsNullOrWhiteSpace(persona.Name)
                            ? $"[{persona.Name}'s persona: "
                            : "[Persona: ";
                        return prefix + SubstituteMacros(persona.Description) + "]";
                    }
                    case "char_description":
                        return !string.IsNullOrWhiteSpace(_character.Description) ? SubstituteMacros(_character.Description) : "";
                    case "char_personality":
                        return !string.IsNullOrWhiteSpace(_character.Personality)
                            ? $"{_character.Name}'s personality: {SubstituteMacros(_character.Personality)}"
                            : "";
                    case "scenario":
                        return !string.IsNullOrWhiteSpace(_character.Scenario)
                            ? $"Scenario: {SubstituteMacros(_character.Scenario)}"
                            : "";
                    case "world_info_after":
                        return GetWorldInfoByPosition(1, chatHistory, newMessage);
                    default:
                        return "";
                }
            }

            var depthInjectionItems = promptOrder
                .Where(i => i.Enabled && i.InjectionPosition == 1 && !i.IsMarker)
                .ToList();

            DebugLogger.LogSection("Prompt Order Processing");
            DebugLogger.LogKeyValue("Total order items", promptOrder.Count);
            DebugLogger.LogKeyValue("History pivot index", historyIdx);
            DebugLogger.LogKeyValue("Depth-injection items", depthInjectionItems.Count);

            foreach (var item in beforeHistory)
            {
                if (!item.Enabled) { DebugLogger.Log($"  [SKIP disabled] {item.Id}"); continue; }
                if (item.InjectionPosition == 1) { DebugLogger.Log($"  [DEFER depth={item.InjectionDepth}] {item.Id}"); continue; }

                if (item.Id == "chat_examples")
                {
                    var examples = ParseMessageExamples(_character.MessageExample);
                    DebugLogger.Log($"  [chat_examples] injecting {examples.Count} example pairs");
                    foreach (var ex in examples)
                        messages.Add(new PromptMessage(ex.Item1 ? "user" : "assistant", SubstituteMacros(ex.Item2)));
                }
                else
                {
                    var content = BlockContent(item);
                    var role = item.IsMarker ? "system" : item.Role;
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        DebugLogger.Log($"  [OK role={role}] {item.Id} — {content.Length} chars");
                        messages.Add(new PromptMessage(role, content));
                    }
                    else
                    {
                        DebugLogger.Log($"  [SKIP blank] {item.Id}");
                    }
                }
            }

            foreach (var injection in _extensionInjections)
            {
                if (!string.IsNullOrWhiteSpace(injection))
                {
                    DebugLogger.Log($"  [extension] injecting {injection.Length} chars");
                    messages.Add(new PromptMessage("system", SubstituteMacros(injection)));
                }
            }

            var historyItem = promptOrder.FirstOrDefault(i => i.Id == "chat_history");
            if (historyItem == null || historyItem.Enabled)
            {
                DebugLogger.Log($"  [chat_history] injecting {chatHistory.Count} messages + {depthInjectionItems.Count} depth-injected blocks");
                foreach (var item in InjectDepthPrompts(chatHistory, depthInjectionItems))
                {
                    if (item is HistoryItem.Message msgItem)
                    {
                        var msg = msgItem.Msg;
                        if (msg.IsUser)
                        {
                            messages.Add(new PromptMessage("user", SubstituteMacros(CleanMessageContent(PromptContent(msg)))));
                        }
                        else
                        {
                            var clean = SubstituteMacros(CleanMessageContent(PromptContent(msg)));
                            var msgContent = (msg.SenderName != null && msg.SenderName != _character.Name)
                                ? $"{msg.SenderName}: {clean}"
                                : clean;
                            messages.Add(new PromptMessage("assistant", msgContent));
                        }
                    }
                    else if (item is HistoryItem.Injection injItem && !string.IsNullOrWhiteSpace(injItem.Content))
                    {
                        messages.Add(new PromptMessage(injItem.Role, injItem.Content));
                    }
                }
            }
            else
            {
                DebugLogger.Log("  [SKIP disabled] chat_history");
            }

            foreach (var item in afterHistory)
            {
                if (!item.Enabled) { DebugLogger.Log($"  [SKIP disabled] {item.Id}"); continue; }
                if (item.InjectionPosition == 1) { DebugLogger.Log($"  [DEFER depth={item.InjectionDepth}] {item.Id}"); continue; }

                var content = BlockContent(item);
                var role = item.IsMarker ? "system" : item.Role;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    DebugLogger.Log($"  [OK role={role}] {item.Id} — {content.Length} chars");
                    messages.Add(new PromptMessage(role, content));
                }
                else
                {
                    DebugLogger.Log($"  [SKIP blank] {item.Id}");
                }
            }

            messages.Add(new PromptMessage("user", SubstituteMacros(newMessage)));

            var normalized = NormalizeForChatApi(messages);

            DebugLogger.LogSection("Chat Completion Messages Summary");
            DebugLogger.LogKeyValue("Total messages (raw)", messages.Count);
            DebugLogger.LogKeyValue("Total messages (normalized)", normalized.Count);

            return normalized;
        }

        /// <summary>
        /// Normalize a chat completion message list to satisfy API requirements:
        /// 1. Merge adjacent user+user or assistant+assistant messages.
        ///    System messages stay as separate entries.
        /// 2. Ensure the first non-system message is user — if it's assistant, insert a
        ///    "[Start a new chat]" user placeholder.
        /// </summary>
        private List<PromptMessage> NormalizeForChatApi(List<PromptMessage> messages)
        {
            if (messages.Count == 0) return messages;

            var merged = new List<PromptMessage>();
            foreach (var msg in messages)
            {
                var last = merged.Count > 0 ? merged[merged.Count - 1] : null;
                if (last != null && last.Role == msg.Role && msg.Role != "system")
                    merged[merged.Count - 1] = new PromptMessage(last.Role, last.Content + "\n\n" + msg.Content);
                else
                    merged.Add(msg);
            }

            var firstNonSystem = merged.FindIndex(m => m.Role != "system");
            if (firstNonSystem >= 0 && merged[firstNonSystem].Role == "assistant")
                merged.Insert(firstNonSystem, new PromptMessage("user", "[Start a new chat]"));

            return merged;
        }

        /// <summary>Build prompt with instruct mode formatting.</summary>
        private string BuildInstructPrompt(List<ChatMessage> chatHistory, string newMessage)
        {
            var template = _instructTemplate;
            var sb = new StringBuilder();

            var storyString = BuildStoryString();
            if (!string.IsNullOrWhiteSpace(storyString) || !string.IsNullOrWhiteSpace(_systemPrompt))
            {
                if (!string.IsNullOrWhiteSpace(template.SystemSequence))
                    sb.Append(template.SystemSequence);
                if (!string.IsNullOrWhiteSpace(_systemPrompt))
                {
                    sb.Append(SubstituteMacros(_systemPrompt));
                    sb.Append("\n\n");
                }
                if (!string.IsNullOrWhiteSpace(storyString))
                    sb.Append(storyString);
                if (!string.IsNullOrWhiteSpace(template.SystemSuffix))
                    sb.Append(template.SystemSuffix);
                else if (!string.IsNullOrWhiteSpace(template.StopSequence))
                    sb.Append(template.StopSequence);
                sb.Append("\n");
            }

            foreach (var injection in _extensionInjections)
            {
                if (!string.IsNullOrWhiteSpace(injection))
                    sb.Append(WrapAsSystem(SubstituteMacros(injection), template));
            }

            var examples = BuildMessageExamples();
            if (!string.IsNullOrWhiteSpace(examples))
                sb.Append(examples);

            var worldInfoBefore = GetWorldInfoByPosition(0, chatHistory, newMessage);
            if (!string.IsNullOrWhiteSpace(worldInfoBefore))
                sb.Append(WrapAsSystem(worldInfoBefore, template));

            var historyWithInjections = InjectDepthPrompts(chatHistory);
            var isFirstAssistant = true;

            foreach (var item in historyWithInjections)
            {
                if (item is HistoryItem.Message msgItem)
                {
                    var msg = msgItem.Msg;
                    if (msg.IsUser)
                    {
                        sb.Append(template.InputSequence);
                        sb.Append(SubstituteMacros(PromptContent(msg)));
                        AppendSuffix(sb, template, isUser: true);
                        sb.Append("\n");
                    }
                    else
                    {
                        var outputSeq = (isFirstAssistant && !string.IsNullOrWhiteSpace(template.FirstOutputSequence))
                            ? template.FirstOutputSequence
                            : template.OutputSequence;
                        sb.Append(outputSeq);
                        if (msg.SenderName != null && msg.SenderName != _character.Name)
                            sb.Append($"{msg.SenderName}: ");
                        sb.Append(SubstituteMacros(CleanMessageContent(PromptContent(msg))));
                        AppendSuffix(sb, template, isUser: false);
                        sb.Append("\n");
                        isFirstAssistant = false;
                    }
                }
                else if (item is HistoryItem.Injection injItem)
                {
                    sb.Append(WrapAsSystem(injItem.Content, template));
                }
            }

            var worldInfoAfter = GetWorldInfoByPosition(1, chatHistory, newMessage);
            if (!string.IsNullOrWhiteSpace(worldInfoAfter))
                sb.Append(WrapAsSystem(worldInfoAfter, template));

            sb.Append(template.InputSequence);
            sb.Append(SubstituteMacros(newMessage));
            AppendSuffix(sb, template, isUser: true);
            sb.Append("\n");

            if (!string.IsNullOrWhiteSpace(_character.PostHistoryInstructions))
                sb.Append(WrapAsSystem(SubstituteMacros(_character.PostHistoryInstructions), template));

            var lastOutputSeq = !string.IsNullOrWhiteSpace(template.LastOutputSequence)
                ? template.LastOutputSequence
                : template.OutputSequence;
            sb.Append(lastOutputSeq);

            return sb.ToString();
        }

        /// <summary>Build a simple prompt without instruct formatting.</summary>
        private string BuildSimplePrompt(List<ChatMessage> chatHistory, string newMessage)
        {
            var sb = new StringBuilder();

            var storyString = BuildStoryString();
            if (!string.IsNullOrWhiteSpace(storyString))
            {
                sb.Append(storyString);
                sb.Append("\n\n");
            }

            foreach (var injection in _extensionInjections)
            {
                if (!string.IsNullOrWhiteSpace(injection))
                    sb.Append($"[{SubstituteMacros(injection)}]\n");
            }

            if (!string.IsNullOrWhiteSpace(_character.MessageExample))
            {
                sb.Append(SubstituteMacros(_character.MessageExample));
                sb.Append("\n\n");
            }

            foreach (var item in InjectDepthPrompts(chatHistory))
            {
                if (item is HistoryItem.Message msgItem)
                {
                    var msg = msgItem.Msg;
                    var name = msg.SenderName ?? (msg.IsUser ? _userName : _character.Name);
                    sb.Append($"{name}: {SubstituteMacros(CleanMessageContent(PromptContent(msg)))}\n");
                }
                else if (item is HistoryItem.Injection injItem)
                {
                    sb.Append($"[{injItem.Content}]\n");
                }
            }

            sb.Append($"{_userName}: {SubstituteMacros(newMessage)}\n");
            sb.Append($"{_character.Name}:");

            return sb.ToString();
        }

        /// <summary>Build the story string (character description, personality, scenario).</summary>
        private string BuildStoryString()
        {
            var parts = new List<string>();
            var persona = _chatContext.UserPersona;

            if (!string.IsNullOrWhiteSpace(_character.Description))
                parts.Add(SubstituteMacros(_character.Description));
            if (!string.IsNullOrWhiteSpace(_character.Personality))
                parts.Add($"{_character.Name}'s personality: {SubstituteMacros(_character.Personality)}");
            if (!string.IsNullOrWhiteSpace(_character.Scenario))
                parts.Add($"Scenario: {SubstituteMacros(_character.Scenario)}");
            if (persona.Position == 0 && !string.IsNullOrWhiteSpace(persona.Description))
                parts.Add($"[{persona.Name}'s persona: {SubstituteMacros(persona.Description)}]");

            return string.Join("\n\n", parts);
        }

        /// <summary>Build formatted message examples.</summary>
        private string BuildMessageExamples()
        {
            var examples = _character.MessageExample;
            if (string.IsNullOrWhiteSpace(examples)) return "";

            var template = _instructTemplate;
            if (template == null)
                return SubstituteMacros(examples) + "\n";

            var parsedExamples = ParseMessageExamples(examples);
            if (parsedExamples.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var ex in parsedExamples)
            {
                var isUser = ex.Item1; var content = ex.Item2;
                if (isUser)
                {
                    sb.Append(template.InputSequence);
                    sb.Append(SubstituteMacros(content));
                    AppendSuffix(sb, template, isUser: true);
                    sb.Append("\n");
                }
                else
                {
                    sb.Append(template.OutputSequence);
                    sb.Append(SubstituteMacros(content));
                    AppendSuffix(sb, template, isUser: false);
                    sb.Append("\n");
                }
            }
            return sb.ToString();
        }

        /// <summary>Parse message examples from ST format: &lt;START&gt;\n{{user}}: msg\n{{char}}: msg</summary>
        private List<Tuple<bool, string>> ParseMessageExamples(string examples)
        {
            var result = new List<Tuple<bool, string>>();
            if (string.IsNullOrWhiteSpace(examples)) return result;

            var lines = examples.Split('\n');
            bool? currentIsUser = null;
            var currentContent = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.Equals(trimmed, "<START>", StringComparison.OrdinalIgnoreCase)) continue;

                var userMatch = trimmed.StartsWith("{{user}}:", StringComparison.OrdinalIgnoreCase)
                             || trimmed.StartsWith(_userName + ":", StringComparison.OrdinalIgnoreCase);
                var charMatch = trimmed.StartsWith("{{char}}:", StringComparison.OrdinalIgnoreCase)
                             || trimmed.StartsWith(_character.Name + ":", StringComparison.OrdinalIgnoreCase);

                if (userMatch)
                {
                    if (currentIsUser.HasValue && currentContent.Length > 0)
                        result.Add(Tuple.Create(currentIsUser.Value, currentContent.ToString().Trim()));
                    currentIsUser = true;
                    currentContent = new StringBuilder(trimmed.Substring(trimmed.IndexOf(':') + 1).Trim());
                }
                else if (charMatch)
                {
                    if (currentIsUser.HasValue && currentContent.Length > 0)
                        result.Add(Tuple.Create(currentIsUser.Value, currentContent.ToString().Trim()));
                    currentIsUser = false;
                    currentContent = new StringBuilder(trimmed.Substring(trimmed.IndexOf(':') + 1).Trim());
                }
                else if (currentIsUser.HasValue)
                {
                    currentContent.Append("\n").Append(trimmed);
                }
            }

            if (currentIsUser.HasValue && currentContent.Length > 0)
                result.Add(Tuple.Create(currentIsUser.Value, currentContent.ToString().Trim()));

            return result;
        }

        /// <summary>
        /// Inject Author's Note, User Persona, World Info, and OAI preset depth-injection blocks
        /// at correct depths in chat history.
        /// </summary>
        private List<HistoryItem> InjectDepthPrompts(
            List<ChatMessage> chatHistory,
            List<OaiPromptOrderItem> extraDepthInjections = null)
        {
            if (extraDepthInjections == null) extraDepthInjections = new List<OaiPromptOrderItem>();

            var result = new List<HistoryItem>();
            var reversedHistory = Enumerable.Reverse(chatHistory).ToList();
            var historySize = chatHistory.Count;

            var authorsNote = _chatContext.AuthorsNote;
            var depthPrompt = !string.IsNullOrWhiteSpace(_character.DepthPrompt)
                ? _character.DepthPrompt
                : authorsNote.Content;
            var depthPromptDepth = !string.IsNullOrWhiteSpace(_character.DepthPrompt)
                ? _character.DepthPromptDepth
                : authorsNote.Depth;

            DebugLogger.LogSection("Author's Note / Depth Prompt");
            DebugLogger.LogKeyValue("Using depthPrompt", string.IsNullOrWhiteSpace(depthPrompt) ? "(empty)" : depthPrompt.Substring(0, Math.Min(100, depthPrompt.Length)));
            DebugLogger.LogKeyValue("Using depthPromptDepth", depthPromptDepth);
            DebugLogger.LogKeyValue("History size", historySize);

            if (depthPromptDepth == 0 && !string.IsNullOrWhiteSpace(depthPrompt))
            {
                DebugLogger.Log("Injecting Author's Note at depth 0 (end of history)");
                var persona = _chatContext.UserPersona;
                switch (persona.Position)
                {
                    case 2: // TOP_OF_AN
                        if (!string.IsNullOrWhiteSpace(persona.Description))
                            result.Add(new HistoryItem.Injection($"[{persona.Name}'s persona: {SubstituteMacros(persona.Description)}]"));
                        result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));
                        break;
                    case 3: // BOTTOM_OF_AN
                        result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));
                        if (!string.IsNullOrWhiteSpace(persona.Description))
                            result.Add(new HistoryItem.Injection($"[{persona.Name}'s persona: {SubstituteMacros(persona.Description)}]"));
                        break;
                    default:
                        result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));
                        break;
                }
            }

            foreach (var item in Enumerable.Reverse(extraDepthInjections.Where(i => i.InjectionDepth == 0)))
            {
                var content = SubstituteMacros(item.Content ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(content))
                    result.Add(new HistoryItem.Injection(content, item.Role));
            }

            var persona2 = _chatContext.UserPersona;
            var personaContent = !string.IsNullOrWhiteSpace(persona2.Description)
                ? $"[{persona2.Name}'s persona: {SubstituteMacros(persona2.Description)}]"
                : "";

            var worldInfoByDepth = GetWorldInfoByDepth(chatHistory);
            var overflowWorldInfo = string.Join("\n",
                worldInfoByDepth.Where(kv => kv.Key > historySize).Select(kv => kv.Value));

            for (int index = 0; index < reversedHistory.Count; index++)
            {
                var message = reversedHistory[index];
                var depth = index + 1;

                if (worldInfoByDepth.TryGetValue(depth, out var wiContent))
                    result.Add(new HistoryItem.Injection(SubstituteMacros(wiContent)));

                foreach (var item in Enumerable.Reverse(extraDepthInjections.Where(i => i.InjectionDepth == depth)))
                {
                    var content = SubstituteMacros(item.Content ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(content))
                        result.Add(new HistoryItem.Injection(content, item.Role));
                }

                if (depth == depthPromptDepth && !string.IsNullOrWhiteSpace(depthPrompt))
                {
                    switch (persona2.Position)
                    {
                        case 2:
                            if (!string.IsNullOrWhiteSpace(personaContent))
                                result.Add(new HistoryItem.Injection(personaContent));
                            result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));
                            break;
                        case 3:
                            result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));
                            if (!string.IsNullOrWhiteSpace(personaContent))
                                result.Add(new HistoryItem.Injection(personaContent));
                            break;
                        default:
                            result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));
                            break;
                    }
                }

                if (persona2.Position == 1 && depth == persona2.Depth && !string.IsNullOrWhiteSpace(personaContent))
                    result.Add(new HistoryItem.Injection(personaContent));

                result.Add(new HistoryItem.Message(message));
            }

            if (!string.IsNullOrWhiteSpace(overflowWorldInfo))
                result.Add(new HistoryItem.Injection(SubstituteMacros(overflowWorldInfo)));

            if (depthPromptDepth > historySize && !string.IsNullOrWhiteSpace(depthPrompt))
                result.Add(new HistoryItem.Injection(SubstituteMacros(depthPrompt)));

            foreach (var item in Enumerable.Reverse(extraDepthInjections.Where(i => i.InjectionDepth > historySize)))
            {
                var content = SubstituteMacros(item.Content ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(content))
                    result.Add(new HistoryItem.Injection(content, item.Role));
            }

            result.Reverse();
            return result;
        }

        private Dictionary<int, string> GetWorldInfoByDepth(List<ChatMessage> chatHistory)
        {
            var result = new Dictionary<int, string>();
            var triggered = ScanWorldInfo(chatHistory);
            var depthEntries = triggered.Where(e => e.Depth > 0).ToList();

            DebugLogger.LogSection("World Info By Depth");
            DebugLogger.LogKeyValue("Entries with depth > 0", depthEntries.Count);

            foreach (var group in depthEntries.GroupBy(e => e.Depth))
            {
                result[group.Key] = string.Join("\n", group.Select(e => e.Content));
            }
            return result;
        }

        private string GetWorldInfoByPosition(int position, List<ChatMessage> chatHistory, string newMessage)
        {
            var triggered = ScanWorldInfo(chatHistory, newMessage);
            var positionEntries = triggered.Where(e => e.Position == position && e.Depth == 0).ToList();

            DebugLogger.LogSection($"World Info By Position {position}");
            DebugLogger.LogKeyValue("Total triggered", triggered.Count);
            DebugLogger.LogKeyValue($"With position={position} and depth=0", positionEntries.Count);

            return string.Join("\n", positionEntries.OrderBy(e => e.Order).Select(e => SubstituteMacros(e.Content)));
        }

        /// <summary>Scan chat history for World Info keyword triggers.</summary>
        private List<WorldInfoEntry> ScanWorldInfo(List<ChatMessage> chatHistory, string newMessage = "")
        {
            var allEntries = _chatContext.WorldInfoEntries.Where(e => e.Enabled).ToList();
            if (allEntries.Count == 0)
            {
                DebugLogger.Log("PromptBuilder: No enabled World Info entries to scan");
                return new List<WorldInfoEntry>();
            }

            var settings = _chatContext.WorldInfoSettings;
            var scanDepth = settings.Depth;

            var baseText = new StringBuilder();
            baseText.Append(newMessage);
            baseText.Append(" ");
            foreach (var msg in chatHistory.Skip(Math.Max(0, chatHistory.Count - scanDepth)))
            {
                baseText.Append(PromptContent(msg));
                baseText.Append(" ");
            }
            baseText.Append(_character.Description);
            baseText.Append(" ");
            baseText.Append(_character.Scenario);

            DebugLogger.LogSection("PromptBuilder - World Info Scan");
            DebugLogger.LogKeyValue("Scan depth", scanDepth);
            DebugLogger.LogKeyValue("Recursive", settings.Recursive);

            var triggered = new List<WorldInfoEntry>();
            var remainingEntries = new List<WorldInfoEntry>(allEntries);
            var scanText = baseText.ToString();
            var passes = 0;

            do
            {
                var passTriggered = new List<WorldInfoEntry>();
                var stillRemaining = new List<WorldInfoEntry>();
                var scanLower = scanText.ToLowerInvariant();

                foreach (var entry in remainingEntries)
                {
                    if (MatchesWorldInfoEntry(entry, scanText, scanLower))
                        passTriggered.Add(entry);
                    else
                        stillRemaining.Add(entry);
                }

                triggered.AddRange(passTriggered);
                remainingEntries.Clear();
                remainingEntries.AddRange(stillRemaining);
                passes++;

                if (settings.Recursive && passTriggered.Count > 0)
                {
                    var newContent = string.Join(" ", passTriggered.Select(e => e.Content));
                    scanText = scanText + " " + newContent;
                    DebugLogger.Log($"  Recursive pass {passes} triggered {passTriggered.Count} entries");
                }
            }
            while (settings.Recursive && remainingEntries.Count > 0 && triggered.Count > 0);

            return ApplyTokenBudget(triggered, settings);
        }

        private bool MatchesWorldInfoEntry(WorldInfoEntry entry, string scanText, string scanLower)
        {
            if (entry.Constant) return true;

            if (entry.Probability < 100)
            {
                if (_rng.Next(100) >= entry.Probability) return false;
            }

            var primaryMatch = entry.Keys.Any(key =>
            {
                if (string.IsNullOrWhiteSpace(key)) return false;
                return KeyMatchesText(key, scanText, scanLower, entry.CaseSensitive, entry.MatchWholeWords);
            });

            if (!primaryMatch) return false;

            if (entry.Selective && entry.SecondaryKeys.Count > 0)
            {
                return entry.SecondaryKeys.Any(key =>
                {
                    if (string.IsNullOrWhiteSpace(key)) return false;
                    return KeyMatchesText(key, scanText, scanLower, entry.CaseSensitive, false);
                });
            }

            return true;
        }

        private bool KeyMatchesText(string key, string scanText, string scanLower, bool caseSensitive, bool wholeWords)
        {
            if (key.StartsWith("/") && key.Length > 2)
            {
                var lastSlash = key.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    var pattern = key.Substring(1, lastSlash - 1);
                    var flags = key.Substring(lastSlash + 1);
                    try
                    {
                        var options = flags.Contains("i") ? RegexOptions.IgnoreCase : RegexOptions.None;
                        return Regex.IsMatch(scanText, pattern, options);
                    }
                    catch { return false; }
                }
            }

            var escapedKey = Regex.Escape(key);
            var regexPattern = wholeWords ? $@"\b{escapedKey}\b" : escapedKey;
            try
            {
                var options = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                return Regex.IsMatch(caseSensitive ? scanText : scanLower, regexPattern, options);
            }
            catch { return false; }
        }

        private List<WorldInfoEntry> ApplyTokenBudget(List<WorldInfoEntry> entries, WorldInfoSettings settings)
        {
            if (settings.BudgetCap <= 0) return entries;

            var sorted = entries.OrderBy(e => e.Order).ToList();
            var result = new List<WorldInfoEntry>();
            var tokenCount = 0;

            foreach (var entry in sorted)
            {
                var entryTokens = Math.Max(1, (int)(entry.Content.Length * 0.75));
                if (tokenCount + entryTokens > settings.BudgetCap)
                {
                    DebugLogger.Log($"  WI budget cap ({settings.BudgetCap}) reached at '{entry.Comment}' — skipping");
                    continue;
                }
                result.Add(entry);
                tokenCount += entryTokens;
            }

            return result;
        }

        private string WrapAsSystem(string content, InstructTemplate template)
        {
            if (string.IsNullOrWhiteSpace(content)) return "";
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(template.SystemSequence))
                sb.Append(template.SystemSequence);
            sb.Append(content);
            if (!string.IsNullOrWhiteSpace(template.SystemSuffix))
                sb.Append(template.SystemSuffix);
            else if (!string.IsNullOrWhiteSpace(template.StopSequence))
                sb.Append(template.StopSequence);
            sb.Append("\n");
            return sb.ToString();
        }

        private void AppendSuffix(StringBuilder sb, InstructTemplate template, bool isUser)
        {
            var suffix = isUser ? template.InputSuffix : template.OutputSuffix;
            if (!string.IsNullOrWhiteSpace(suffix))
                sb.Append(suffix);
            else if (!string.IsNullOrWhiteSpace(template.StopSequence))
                sb.Append(template.StopSequence);
        }

        private string PromptContent(ChatMessage msg) =>
            msg.RawContent ?? msg.Content;

        private string CleanMessageContent(string text)
        {
            return Regex.Replace(text, @"\[\]\(#['""][^'""]*['""]\)", "")
                .Replace("\r\n", "\n")
                .Trim();
        }

        private string StripCommentMacros(string text) =>
            Regex.Replace(text, @"\{\{//.*?\}\}", "", RegexOptions.Singleline);

        private string SubstituteMacros(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            var result = StripCommentMacros(text);

            result = Regex.Replace(result, @"\{\{random:(.*?)\}\}", match =>
            {
                var options = match.Groups[1].Value.Split(',')
                    .Select(o => o.Trim())
                    .Where(o => o.Length > 0)
                    .ToArray();
                return options.Length == 0 ? "" : options[_rng.Next(options.Length)];
            }, RegexOptions.IgnoreCase);

            var now = DateTime.Now;
            result = ReplaceMacro(result, "{{char}}", _character.Name);
            result = ReplaceMacro(result, "{{user}}", _userName);
            result = ReplaceMacro(result, "{{charname}}", _character.Name);
            result = ReplaceMacro(result, "{{username}}", _userName);
            result = ReplaceMacro(result, "{{description}}", _character.Description);
            result = ReplaceMacro(result, "{{personality}}", _character.Personality);
            result = ReplaceMacro(result, "{{scenario}}", _character.Scenario);
            result = ReplaceMacro(result, "{{persona}}", _chatContext.UserPersona.Description);
            result = ReplaceMacro(result, "{{mesexample}}", _character.MessageExample);
            result = ReplaceMacro(result, "{{mes_example}}", _character.MessageExample);
            result = ReplaceMacro(result, "{{time}}", now.ToString("HH:mm"));
            result = ReplaceMacro(result, "{{date}}", now.ToString("yyyy-MM-dd"));
            result = ReplaceMacro(result, "{{weekday}}", now.DayOfWeek.ToString());
            result = ReplaceMacro(result, "{{trim}}", "");
            result = ReplaceMacro(result, "{{original}}", "");
            return result;
        }

        /// <summary>Case-insensitive string replacement safe for UWP .NET Native.</summary>
        private static string ReplaceMacro(string input, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue)) return input;
            var sb = new StringBuilder();
            int startIndex = 0;
            int index;
            while ((index = input.IndexOf(oldValue, startIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                sb.Append(input, startIndex, index - startIndex);
                sb.Append(newValue);
                startIndex = index + oldValue.Length;
            }
            sb.Append(input, startIndex, input.Length - startIndex);
            return sb.ToString();
        }

        private abstract class HistoryItem
        {
            public sealed class Message : HistoryItem
            {
                public ChatMessage Msg { get; }
                public Message(ChatMessage msg) { Msg = msg; }
            }

            public sealed class Injection : HistoryItem
            {
                public string Content { get; }
                public string Role { get; }
                public Injection(string content, string role = "system") { Content = content; Role = role; }
            }
        }
    }
}
