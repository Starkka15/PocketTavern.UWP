using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PocketTavern.UWP.Models;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// Translates character card fields using the active LLM.
    /// Preserves {{macros}}, &lt;img&gt; sprite tags, and markdown markers. (V3,V4,V5)
    /// Throws on any failure so caller can leave the card untouched. (V9)
    /// </summary>
    public static class TranslateCardService
    {
        private static readonly LlmService _llm = new LlmService();

        private const string SystemPrompt =
            "You are a translation assistant. Translate the following text to English. " +
            "RULES (must follow exactly):\n" +
            "1. Preserve all {{macro}} placeholders verbatim (e.g. {{char}}, {{user}}, {{original}}).\n" +
            "2. Preserve all <img src=(...)> tags verbatim.\n" +
            "3. Preserve all markdown markers (*, **, _) exactly as written.\n" +
            "4. Output ONLY the translated text — no explanations, no meta-commentary.\n" +
            "5. If the text is already in English, output it unchanged.";

        public static async Task<Character> TranslateAsync(
            Character character,
            IList<string> fields,
            ApiConfiguration config)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (fields == null || fields.Count == 0) return character;

            // Work on a copy; only commit if everything succeeds (V9)
            var copy = ShallowCopy(character);

            foreach (var field in fields)
            {
                if (field.ToLowerInvariant() == "alternate_greetings")
                {
                    // Translate each non-empty greeting individually (Android parity)
                    if (copy.AlternateGreetings == null || copy.AlternateGreetings.Count == 0) continue;
                    var translated = new List<string>();
                    foreach (var greeting in copy.AlternateGreetings)
                    {
                        if (string.IsNullOrWhiteSpace(greeting))
                        {
                            translated.Add(greeting);
                            continue;
                        }
                        var result = await _llm.GenerateSimpleAsync(config, SystemPrompt, greeting);
                        if (string.IsNullOrWhiteSpace(result))
                            throw new Exception("Translation returned empty result for an alternate greeting");
                        translated.Add(result.Trim());
                    }
                    copy.AlternateGreetings = translated;
                    continue;
                }

                var original = GetField(copy, field);
                if (string.IsNullOrWhiteSpace(original)) continue;

                var translatedField = await _llm.GenerateSimpleAsync(config, SystemPrompt, original);
                if (string.IsNullOrWhiteSpace(translatedField))
                    throw new Exception($"Translation returned empty result for field '{field}'");

                SetField(copy, field, translatedField.Trim());
            }

            return copy;
        }

        private static string GetField(Character c, string field)
        {
            switch (field.ToLowerInvariant())
            {
                case "description":           return c.Description;
                case "personality":           return c.Personality;
                case "scenario":              return c.Scenario;
                case "first_mes":
                case "firstmessage":          return c.FirstMessage;
                case "mes_example":
                case "messageexample":        return c.MessageExample;
                case "system_prompt":
                case "systemprompt":          return c.SystemPrompt;
                case "creator_notes":
                case "creatornotes":          return c.CreatorNotes;
                default:                      return null;
            }
        }

        private static void SetField(Character c, string field, string value)
        {
            switch (field.ToLowerInvariant())
            {
                case "description":           c.Description     = value; break;
                case "personality":           c.Personality     = value; break;
                case "scenario":              c.Scenario        = value; break;
                case "first_mes":
                case "firstmessage":          c.FirstMessage    = value; break;
                case "mes_example":
                case "messageexample":        c.MessageExample  = value; break;
                case "system_prompt":
                case "systemprompt":          c.SystemPrompt    = value; break;
                case "creator_notes":
                case "creatornotes":          c.CreatorNotes    = value; break;
            }
        }

        private static Character ShallowCopy(Character c) => new Character
        {
            Name                   = c.Name,
            Avatar                 = c.Avatar,
            Description            = c.Description,
            Personality            = c.Personality,
            Scenario               = c.Scenario,
            FirstMessage           = c.FirstMessage,
            MessageExample         = c.MessageExample,
            CreatorNotes           = c.CreatorNotes,
            SystemPrompt           = c.SystemPrompt,
            PostHistoryInstructions= c.PostHistoryInstructions,
            DepthPrompt            = c.DepthPrompt,
            DepthPromptDepth       = c.DepthPromptDepth,
            DepthPromptRole        = c.DepthPromptRole,
            Talkativeness          = c.Talkativeness,
            IsFavorite             = c.IsFavorite,
            UseAvatarForImageGen   = c.UseAvatarForImageGen,
            HasCharacterBook       = c.HasCharacterBook,
            Tags                   = c.Tags,
            AlternateGreetings     = c.AlternateGreetings
        };
    }
}
