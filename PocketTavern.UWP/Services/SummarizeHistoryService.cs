using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Summarizes a batch of chat turns into a compact memory block.
    /// Requires active ApiConfiguration — skips silently if none. (V12)
    /// </summary>
    public static class SummarizeHistoryService
    {
        private static readonly LlmService _llm = new LlmService();

        private const string SystemPrompt =
            "Summarize the following conversation excerpt into concise bullet-point facts only. " +
            "Rules:\n" +
            "- Use bullet points (- fact)\n" +
            "- Facts only — no roleplay, no prose, no filler\n" +
            "- Maximum 300 tokens\n" +
            "- Focus on character relationships, key events, and established facts\n" +
            "Output ONLY the bullet list, nothing else.";

        /// <summary>
        /// Summarizes a batch of turns.
        /// Inserts a placeholder user turn if batch starts with assistant (V23).
        /// Returns the summary string, or throws on failure (V14).
        /// </summary>
        public static async Task<string> SummarizeAsync(
            IList<ChatMessage> turns,
            ApiConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (turns == null || turns.Count == 0) return "";

            // Build conversation text for the user message
            var sb = new System.Text.StringBuilder();

            bool firstIsAssistant = !turns[0].IsUser;
            if (firstIsAssistant)
                sb.AppendLine("[conversation excerpt]");  // V23 guard

            foreach (var msg in turns)
            {
                var role = msg.IsUser ? "User" : "Assistant";
                sb.AppendLine($"{role}: {msg.Content}");
                sb.AppendLine();
            }

            var result = await _llm.GenerateSimpleAsync(config, SystemPrompt, sb.ToString().Trim());
            if (string.IsNullOrWhiteSpace(result))
                throw new Exception("Summarization returned empty result");

            return result.Trim();
        }
    }
}
